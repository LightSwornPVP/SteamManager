using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using HarmonyLib;
using SteamManagerProtocol;
using UnityEngine;

namespace SteamManagerRuntime
{
    public static class Bootstrap
    {
        static int started;
        public static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            try { Bridge.Initialize(); }
            catch (Exception e) { Bridge.Log(e.ToString()); throw; }
        }
    }
    public static class Bridge
    {
        public static readonly List<Feature> Features = Catalog.Create();
        public static Harmony Harmony;
        static readonly ConcurrentQueue<Job> queue = new ConcurrentQueue<Job>();
        static Player trackedPlayer;
        static float walk, run, swim, jump;
        static bool initialFly, initialFree;
        static long lastContact;
        static int lastFrame = -1;
        static bool resetting;
        static string secret;
        public static int Comfort = 30;
        sealed class Job
        {
            public Request Request; public Response Response;
            public DateTime Deadline = DateTime.UtcNow.AddSeconds(5);
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public readonly object Gate = new object();
            public bool Cancelled;
        }
        public static void Log(string text)
        {
            try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(typeof(Bridge).Assembly.Location), "runtime.log"), DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine); } catch { }
        }
        public static bool On(int id) { var f = Features.Find(x => x.Id == id); return f != null && f.Enabled && f.Error == null; }
        public static float Value(int id) { return Features.Find(x => x.Id == id).Value; }
        public static bool Local(Character c) { return c != null && c == Player.m_localPlayer; }
        public static void Initialize()
        {
            secret = File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Bridge).Assembly.Location), "runtime.token")).Trim();
            if (secret.Length < 32) throw new InvalidOperationException("Missing session authentication token.");
            Harmony = new Harmony("LightSwornPVP.SteamManager.Runtime");
            Patch(0, typeof(Game), "Update", null, "Pump");
            Patch(0, typeof(FejdStartup), "Update", null, "Pump");
            Hooks.Install();
            Extended.Install();
            lastContact = DateTime.UtcNow.Ticks;
            new Thread(Serve) { IsBackground = true, Name = "SteamManager IPC" }.Start();
            Log("Runtime loaded; all gameplay controls start disabled.");
        }
        public static void Patch(int id, Type type, string method, string prefix = null, string postfix = null, Type[] args = null)
        {
            try
            {
                var target = args == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, args);
                if (target == null) throw new MissingMethodException(type.Name, method);
                Harmony.Patch(target, prefix == null ? null : new HarmonyMethod(typeof(Hooks), prefix), postfix == null ? null : new HarmonyMethod(typeof(Hooks), postfix));
            }
            catch (Exception e)
            {
                if (id == 0) throw;
                Features.Find(x => x.Id == id).Error = "Hook unavailable: " + type.Name + "." + method + " — " + e.GetBaseException().Message;
                Log(Features.Find(x => x.Id == id).Error);
            }
        }
        static void Serve()
        {
            while (true)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream("SteamManager.Valheim.v3." + Process.GetCurrentProcess().Id, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        pipe.WaitForConnection();
                        using (var reader = new StreamReader(pipe))
                        using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                        {
                            var read = reader.ReadLineAsync();
                            if (!read.Wait(3000)) continue;
                            var request = Wire.Decode<Request>(read.Result);
                            if (request == null || request.Auth != secret) continue;
                            Interlocked.Exchange(ref lastContact, DateTime.UtcNow.Ticks);
                            var job = new Job { Request = request };
                            queue.Enqueue(job);
                            if (!job.Done.WaitOne(6000))
                            {
                                lock (job.Gate) { job.Cancelled = true; }
                                writer.WriteLine(Wire.Encode(new Response { Message = "Game main thread is busy. Request expired; reconnect when loading finishes." }));
                            }
                            else writer.WriteLine(Wire.Encode(job.Response));
                        }
                    }
                }
                catch (Exception e) { Log("IPC: " + e); Thread.Sleep(3000); }
            }
        }
        public static void Tick()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                var player = Player.m_localPlayer;
                if (player != trackedPlayer)
                {
                    Reset(); trackedPlayer = player;
                    if (player != null)
                    {
                        walk = player.m_speed; run = player.m_runSpeed; swim = player.m_swimSpeed; jump = player.m_jumpForce;
                        initialFly = player.IsDebugFlying(); initialFree = player.NoCostCheat();
                        Extended.Capture(player);
                        ComputeComfort();
                    }
                }
                if (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastContact) > TimeSpan.FromSeconds(15).Ticks && Features.Any(x => x.Enabled)) { Reset(); Log("Desktop heartbeat lost; runtime controls reset."); }
                Job job;
                for (int i = 0; i < 8 && queue.TryDequeue(out job); i++)
                {
                    lock (job.Gate)
                    {
                        if (job.Cancelled || DateTime.UtcNow > job.Deadline) job.Response = new Response { Message = "Request expired before execution." };
                        else
                        {
                            try { job.Response = Handle(job.Request); }
                            catch (Exception e) { job.Response = new Response { Message = e.GetBaseException().Message }; Log(e.ToString()); }
                        }
                        job.Done.Set();
                    }
                }
                if (player != null && !player.IsDead())
                {
                    if (On(2)) player.SetHealth(player.GetMaxHealth());
                    if (On(3)) AccessTools.Field(typeof(Player), "m_stamina").SetValue(player, player.GetMaxStamina());
                    if (On(4)) AccessTools.Field(typeof(Player), "m_eitr").SetValue(player, player.GetMaxEitr());
                    if (On(30)) Repair(player);
                    Extended.Tick(player);
                }
            }
            catch (Exception e) { Log("Tick: " + e); Reset(); }
        }
        static Response Handle(Request req)
        {
            if (req.Command == "status") return Snapshot("Connected. Controls reset when changing characters or losing connection.");
            if (req.Command == "reset") { Reset(); return Snapshot("All runtime controls disabled. Saved item and skill changes remain."); }
            if (req.Command == "achievements" || req.Command == "unlock-achievement") return Extended.AchievementCommand(req);
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) throw new InvalidOperationException("Enter a world with a living character first.");
            if(req.Command=="discoveries"||req.Command=="discover-ingredients"||req.Command=="discover-all")return Discovery.Handle(req,p);
            if(req.Command=="profile")
            {
                if(req.Settings==null||req.Settings.Count>100)throw new ArgumentException("Invalid profile.");
                foreach(var f in req.Settings) Catalog.Validate(Features.Find(x=>x.Id==f.Id),f.Value);
                var before=Features.Select(x=>new Feature{Id=x.Id,Enabled=x.Enabled,Value=x.Value}).ToList();
                try { foreach(var f in Features)f.Enabled=false;foreach(var f in req.Settings){var target=Features.Find(x=>x.Id==f.Id);target.Enabled=f.Enabled;target.Value=f.Value;}Apply(p); }
                catch { foreach(var f in before){var target=Features.Find(x=>x.Id==f.Id);target.Enabled=f.Enabled;target.Value=f.Value;}Apply(p);throw; }
                return Snapshot("Profile applied.");
            }
            if (req.Command == "diagnostics")
            {
                var r = Snapshot("Read-only runtime measurements.");
                r.Entries = new List<Entry> {
                    new Entry { Id="health",Value=p.GetHealth() },new Entry { Id="maxHealth",Value=p.GetMaxHealth() },
                    new Entry { Id="stamina",Value=p.GetStamina() },new Entry { Id="eitr",Value=p.GetEitr() },
                    new Entry { Id="carry",Value=p.GetMaxCarryWeight() },new Entry { Id="walk",Value=p.m_speed },
                    new Entry { Id="run",Value=p.m_runSpeed },new Entry { Id="swim",Value=p.m_swimSpeed },
                    new Entry { Id="jump",Value=p.m_jumpForce },new Entry { Id="flight",Value=p.IsDebugFlying()?1:0 },
                    new Entry { Id="freeCraft",Value=p.NoCostCheat()?1:0 },new Entry { Id="comfort",Value=p.GetComfortLevel() },
                    new Entry { Id="portal",Value=p.GetInventory().IsTeleportable(false)?1:0 },
                    new Entry { Id="achievementEligibility",Value=Achievements.CanGetAchievements()?1:0 },
                    new Entry { Id="inventoryEntries",Value=p.GetInventory().GetAllItems().Count }
                }; return r;
            }
            if (req.Command == "set")
            {
                var f = Features.Find(x => x.Id == req.Id);
                Catalog.Validate(f, req.Value);
                bool before = f.Enabled; float oldValue = f.Value;
                f.Enabled = req.Enabled; f.Value = req.Value;
                try { Apply(p); }
                catch { f.Enabled = before; f.Value = oldValue; Apply(p); throw; }
                return Snapshot(f.Name + (f.Enabled ? " enabled." : " disabled."));
            }
            if (req.Command == "repair") { Repair(p); return Snapshot("Carried equipment repaired."); }
            if (req.Command == "items")
            {
                string search = req.Text ?? "";
                var entries = new List<Entry>();
                foreach (var prefab in ObjectDB.instance.m_items)
                {
                    if (prefab == null) continue;
                    var item = prefab.GetComponent<ItemDrop>(); if (item == null) continue;
                    string name = Localization.instance.Localize(item.m_itemData.m_shared.m_name);
                    if (prefab.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    entries.Add(new Entry { Id = prefab.name, Name = name, Max = Math.Max(1, item.m_itemData.m_shared.m_maxQuality) });
                }
                var r = Snapshot("Item definitions from the running game."); r.Entries = entries.OrderBy(x => x.Name).ToList(); return r;
            }
            if (req.Command == "spawn")
            {
                if (req.Quantity < 1 || req.Quantity > 1000) throw new ArgumentException("Quantity must be 1–1000.");
                var prefab = ObjectDB.instance.GetItemPrefab(req.Text ?? "");
                var def = prefab == null ? null : prefab.GetComponent<ItemDrop>();
                if (def == null) throw new ArgumentException("Unknown item.");
                if (req.Quality < 1 || req.Quality > Math.Max(1, def.m_itemData.m_shared.m_maxQuality)) throw new ArgumentException("Quality is outside this item's range.");
                int remaining = req.Quantity;
                while (remaining > 0)
                {
                    var item = def.m_itemData.Clone(); item.m_dropPrefab = prefab; item.m_quality = req.Quality;
                    item.m_stack = Math.Min(remaining, Math.Max(1, item.m_shared.m_maxStackSize));
                    item.m_durability = item.GetMaxDurability(); item.m_worldLevel = (byte)Game.m_worldLevel;
                    item.m_cheated = true;
                    int requested = item.m_stack;
                    bool added = p.GetInventory().AddItem(item);
                    if (!added) return Snapshot("Inventory filled; added " + (req.Quantity - remaining + requested - item.m_stack) + " of " + req.Quantity + ". Nothing was dropped.");
                    remaining -= requested;
                }
                return Snapshot("Added " + req.Quantity + " × " + req.Text + ". This change persists in saves.");
            }
            if (req.Command == "skills")
            {
                var r = Snapshot("Current skill levels.");
                r.Entries = p.GetSkills().m_skills.Select(x => new Entry { Id = x.m_skill.ToString(), Name = x.m_skill.ToString(), Value = p.GetSkills().GetSkillLevel(x.m_skill), Max = 100 }).ToList(); return r;
            }
            if (req.Command == "skill")
            {
                Skills.SkillType type;
                if (!Enum.TryParse(req.Text, out type) || !p.GetSkills().m_skills.Any(x => x.m_skill == type)) throw new ArgumentException("Unknown skill.");
                if (float.IsNaN(req.Value) || float.IsInfinity(req.Value) || req.Value < 0 || req.Value > 100) throw new ArgumentException("Skill level must be 0–100.");
                var skill = (Skills.Skill)AccessTools.Method(typeof(Skills), "GetSkill").Invoke(p.GetSkills(), new object[] { type });
                skill.m_level = req.Value; skill.m_accumulator = 0;
                return Snapshot(type + " set to " + req.Value + ". This change persists in saves.");
            }
            return Extended.Command(req,p);
        }
        public static Response Snapshot(string message)
        {
            return new Response { Ok = true, Message = message, Version = global::Version.GetVersionString(), Player = Player.m_localPlayer == null ? null : Player.m_localPlayer.GetPlayerName(), Multiplayer = ZNet.instance != null && (!ZNet.instance.IsServer() || ZNet.IsOpenServer()), Features = Features };
        }
        static void Apply(Player p)
        {
            p.m_speed = walk * (On(7) ? Value(7) : 1); p.m_runSpeed = run * (On(51) ? Value(51) : On(7) ? Value(7) : 1); p.m_swimSpeed = swim * (On(52) ? Value(52) : On(7) ? Value(7) : 1);
            p.m_jumpForce = jump * (On(8) ? Value(8) : 1);
            p.SetNoPlacementCost(initialFree || On(36));
            bool fly = initialFly || On(39) || On(40);
            if (p.IsDebugFlying() != fly) p.ToggleDebugFly();
            AccessTools.Method(typeof(Player), "UpdateAvailablePiecesList").Invoke(p, null);
            Extended.Apply(p);
        }
        public static void Reset()
        {
            if (resetting) return;
            resetting = true;
            try
            {
                bool altered = Features.Any(x => x.Enabled);
                foreach (var f in Features) f.Enabled = false;
                if (altered && trackedPlayer != null) Apply(trackedPlayer);
                Extended.RestoreGlobal();
            }
            catch (Exception e) { Log("Reset: " + e.Message); }
            finally { resetting = false; }
        }
        static void Repair(Player p)
        {
            foreach (var item in p.GetInventory().GetAllItems()) if (item.m_shared.m_useDurability) item.m_durability = item.GetMaxDurability();
        }
        static void ComputeComfort()
        {
            if (ZNetScene.instance == null) return;
            var groups = new Dictionary<Piece.ComfortGroup, int>(); var names = new Dictionary<string, int>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue; var piece = prefab.GetComponent<Piece>();
                if (piece == null || !piece.m_enabled || piece.GetComfort() <= 0) continue;
                if (piece.m_comfortGroup == Piece.ComfortGroup.None) { int n; names.TryGetValue(piece.m_name, out n); names[piece.m_name] = Math.Max(n, piece.GetComfort()); }
                else { int n; groups.TryGetValue(piece.m_comfortGroup, out n); groups[piece.m_comfortGroup] = Math.Max(n, piece.GetComfort()); }
            }
            Comfort = Math.Max(2, 2 + groups.Values.Sum() + names.Values.Sum());
        }
    }
}
