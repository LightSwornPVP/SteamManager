using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using SteamManagerProtocol;
using Steamworks;
using UnityEngine;

namespace SteamManagerRuntime
{
    public static class Extended
    {
        static Player owner;
        static bool god,ghost,collisions,bodyCollisions,hudHidden,freeCamera,debugTime;
        static float timeScale,dayTime,fov,localClock;
        static bool globalCaptured,clockActive,noclipActive,speedActive,hudActive,cameraActive;
        static CapsuleCollider collider;
        static Rigidbody body;
        static readonly Dictionary<string,ItemDrop.ItemData> inventory=new Dictionary<string,ItemDrop.ItemData>();
        static readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamManager");
        [DataContract] public class Bookmark
        {
            [DataMember] public string Name;
            [DataMember] public float X;
            [DataMember] public float Y;
            [DataMember] public float Z;
            [DataMember] public float Yaw;
        }
        static Bookmark previous;
        static string world;
        static bool On(int id){return Bridge.On(id);}static float V(int id){return Bridge.Value(id);}
        static bool Local(Character c){return Bridge.Local(c);}
        public static void Install()
        {
            P(10,typeof(SEMan),"ModifyHealthRegen",null,"HealthRegen");
            P(11,typeof(SEMan),"ModifyStaminaRegen",null,"StaminaRegen");
            P(12,typeof(SEMan),"ModifyEitrRegen",null,"EitrRegen");
            P(13,typeof(Player),"HaveStamina","StaminaCost");P(14,typeof(Player),"HaveEitr","EitrCost");
            P(19,typeof(Player),"CreateTombStone","Tombstone");
            P(19,typeof(Skills),"OnDeath","SkillDeath");P(20,typeof(Skills),"Clear","SkillClear");
            P(21,typeof(Player),"UpdateGuardianPower","GuardianCooldown");
            P(22,typeof(StatusEffect),"UpdateStatusEffect","GuardianDuration");
            P(24,typeof(Character),"ApplyDamage","ReceivedDamage");
            foreach(var t in new[]{typeof(TreeBase),typeof(TreeLog),typeof(MineRock),typeof(MineRock5),typeof(Destructible),typeof(WearNTear)})P(26,t,"Damage","ObjectDamage");
            P(38,typeof(Player),"UpdatePlacementGhost",null,"Placement");
            P(38,typeof(Player),"RequiredCraftingStation","Station");
            P(38,typeof(CraftingStation),"CheckUsable","StationUsable");
            P(38,typeof(Player),"HaveRequirements","PieceRequirements",args:new[]{typeof(Piece),typeof(Player.RequirementMode)});
            P(45,typeof(ZNet),"UpdateNetTime","WorldTime");
        }
        static void P(int id,Type type,string method,string pre=null,string post=null,Type[] args=null)
        {
            try{var target=args==null?AccessTools.Method(type,method):AccessTools.Method(type,method,args);if(target==null)throw new MissingMethodException(type.Name,method);Bridge.Harmony.Patch(target,pre==null?null:new HarmonyMethod(typeof(Extended),pre),post==null?null:new HarmonyMethod(typeof(Extended),post));}
            catch(Exception e){Bridge.Features.Find(x=>x.Id==id).Error=e.GetBaseException().Message;Bridge.Log("Feature "+id+": "+e);}
        }
        public static void Capture(Player p)
        {
            owner=p;god=p.InGodMode();ghost=p.InGhostMode();collider=p.GetComponent<CapsuleCollider>();body=p.GetComponent<Rigidbody>();
            collisions=collider!=null&&collider.enabled;bodyCollisions=body!=null&&body.detectCollisions;
            inventory.Clear();previous=null;world=ZNet.instance.GetWorldUID().ToString(CultureInfo.InvariantCulture);
            timeScale=Time.timeScale;hudHidden=Hud.instance!=null&&Hud.instance.m_userHidden;
            freeCamera=GameCamera.InFreeFly();fov=GameCamera.instance!=null?GameCamera.instance.m_fov:65;
            debugTime=EnvMan.instance!=null&&EnvMan.instance.m_debugTimeOfDay;dayTime=EnvMan.instance!=null?EnvMan.instance.m_debugTime:0.5f;
            globalCaptured=true;clockActive=false;noclipActive=false;
        }
        public static void Apply(Player p)
        {
            if(p!=owner)return;
            p.SetGodMode(god||On(1));p.SetGhostMode(ghost||On(5));
            if(collider!=null)collider.enabled=On(40)?false:collisions;
            if(body!=null)body.detectCollisions=On(40)?false:bodyCollisions;
            noclipActive=On(40);
            if(On(46)){if(!speedActive)timeScale=Time.timeScale;Time.timeScale=V(46);speedActive=true;}else if(speedActive){Time.timeScale=timeScale;speedActive=false;}
            if(Hud.instance!=null){if(On(47)){if(!hudActive)hudHidden=Hud.instance.m_userHidden;Hud.instance.m_userHidden=true;hudActive=true;}else if(hudActive){Hud.instance.m_userHidden=hudHidden;hudActive=false;}}
            if(GameCamera.instance!=null&&(On(48)||cameraActive))
            {
                if(On(48)&&!cameraActive){freeCamera=GameCamera.InFreeFly();fov=GameCamera.instance.m_fov;}cameraActive=On(48);
                bool desired=freeCamera||On(48);if(GameCamera.InFreeFly()!=desired)GameCamera.instance.ToggleFreeFly();
                GameCamera.instance.m_fov=On(48)?V(48):fov;
                var cam=GameCamera.instance.GetComponent<Camera>();if(cam!=null)cam.fieldOfView=On(48)?V(48):fov;
            }
            UpdateClock(0);
        }
        public static void Tick(Player p)
        {
            if(On(21)&&V(21)==0)p.m_guardianPowerCooldown=0;
            if(noclipActive){if(collider!=null)collider.enabled=false;if(body!=null)body.detectCollisions=false;}
            UpdateClock(Time.unscaledDeltaTime);
        }
        static void UpdateClock(float dt)
        {
            var env=EnvMan.instance;if(env==null)return;
            bool visual=On(44)||(On(45)&&(!ZNet.instance.IsServer()||ZNet.IsOpenServer()));
            if(visual)
            {
                if(!clockActive){localClock=env.GetDayFraction();debugTime=env.m_debugTimeOfDay;dayTime=env.m_debugTime;}clockActive=true;
                if(!On(44)&&On(45))localClock=Mathf.Repeat(localClock+dt*V(45)/Math.Max(1,env.m_dayLengthSec),1);
                env.m_debugTimeOfDay=true;env.m_debugTime=localClock;
            }
            else if(clockActive){env.m_debugTimeOfDay=debugTime;env.m_debugTime=dayTime;clockActive=false;}
        }
        public static void RestoreGlobal()
        {
            if(!globalCaptured)return;
            if(speedActive)Time.timeScale=timeScale;speedActive=false;
            if(hudActive&&Hud.instance!=null)Hud.instance.m_userHidden=hudHidden;hudActive=false;
            if(cameraActive&&GameCamera.instance!=null){if(GameCamera.InFreeFly()!=freeCamera)GameCamera.instance.ToggleFreeFly();GameCamera.instance.m_fov=fov;}cameraActive=false;
            if(EnvMan.instance!=null&&clockActive){EnvMan.instance.m_debugTimeOfDay=debugTime;EnvMan.instance.m_debugTime=dayTime;}clockActive=false;
            if(collider!=null)collider.enabled=collisions;if(body!=null)body.detectCollisions=bodyCollisions;noclipActive=false;
        }
        public static void HealthRegen(Character ___m_character,ref float regenMultiplier){if(Local(___m_character)&&On(10))regenMultiplier*=V(10);}
        public static void StaminaRegen(Character ___m_character,ref float staminaMultiplier){if(Local(___m_character)&&On(11))staminaMultiplier*=V(11);}
        public static void EitrRegen(Character ___m_character,ref float eitrMultiplier){if(Local(___m_character)&&On(12))eitrMultiplier*=V(12);}
        public static void StaminaCost(Player __instance,ref float amount){if(Local(__instance)&&On(13))amount*=V(13);}
        public static void EitrCost(Player __instance,ref float amount){if(Local(__instance)&&On(14))amount*=V(14);}
        public static bool Tombstone(Player __instance){return !(Local(__instance)&&On(19));}
        public static bool SkillDeath(Skills __instance){return !(Player.m_localPlayer!=null&&__instance==Player.m_localPlayer.GetSkills()&&(On(19)||On(20)));}
        public static bool SkillClear(Skills __instance){return Player.m_localPlayer==null||!Player.m_localPlayer.IsDead()||SkillDeath(__instance);}
        public static void GuardianCooldown(Player __instance,ref float dt){if(!Local(__instance)||!On(21))return;if(V(21)==0)__instance.m_guardianPowerCooldown=0;else dt/=V(21);}
        public static void GuardianDuration(StatusEffect __instance,ref float dt){if(Local(__instance.m_character)&&On(22)&&__instance.name.StartsWith("GP_",StringComparison.Ordinal))dt/=V(22);}
        public static void ReceivedDamage(Character __instance,HitData hit){if(Local(__instance)&&On(24))hit.m_damage.Modify(V(24));}
        public static bool ObjectDamage(object __instance,HitData hit)
        {
            if(Player.m_localPlayer==null||hit.GetAttacker()!=Player.m_localPlayer)return true;
            if(__instance is WearNTear&&On(28))return false;
            if(On(26)){hit.m_damage.m_chop*=V(26);hit.m_damage.m_pickaxe*=On(53)?V(53):V(26);}
            else if(On(53))hit.m_damage.m_pickaxe*=V(53);
            if(On(27)){hit.m_damage.m_damage=10000000;hit.m_damage.m_chop=10000000;hit.m_damage.m_pickaxe=10000000;hit.m_toolTier=short.MaxValue;}
            return true;
        }
        public static void Placement(Player __instance,GameObject ___m_placementGhost,ref Player.PlacementStatus ___m_placementStatus)
        {if(Local(__instance)&&On(38)&&___m_placementGhost!=null&&___m_placementGhost.activeSelf&&___m_placementStatus!=Player.PlacementStatus.NoRayHits)___m_placementStatus=Player.PlacementStatus.Valid;}
        public static bool Station(Player __instance,ref bool __result){if(!Local(__instance)||!On(38))return true;__result=true;return false;}
        public static bool StationUsable(Player player,ref bool __result){if(!Local(player)||!On(38))return true;__result=true;return false;}
        public static bool PieceRequirements(Player __instance,Piece piece,Player.RequirementMode mode,ref bool __result)
        {
            if(!Local(__instance)||!On(38))return true;
            __result=string.IsNullOrEmpty(piece.m_dlc)||DLCMan.instance.IsDLCInstalled(piece.m_dlc);
            if(__result&&!__instance.NoCostCheat())foreach(var requirement in piece.m_resources)
                if(requirement.m_resItem!=null&&requirement.m_amount>0&&__instance.GetInventory().CountItems(requirement.m_resItem.m_itemData.m_shared.m_name)<requirement.m_amount){__result=false;break;}
            return false;
        }
        public static void WorldTime(ZNet __instance,ref float dt){if(On(45)&&__instance.IsServer()&&!ZNet.IsOpenServer())dt*=V(45);}
        static Response Entries(string text,List<Entry> entries){var r=Bridge.Snapshot(text);r.Entries=entries;return r;}
        public static Response Command(Request req,Player p)
        {
            if(req.Command=="inventory")
            {
                foreach(var key in inventory.Where(x=>!p.GetInventory().ContainsItem(x.Value)).Select(x=>x.Key).ToArray())inventory.Remove(key);
                var result=new List<Entry>();foreach(var item in p.GetInventory().GetAllItems())
                {var pair=inventory.FirstOrDefault(x=>ReferenceEquals(x.Value,item));string id=pair.Key??Guid.NewGuid().ToString("N");inventory[id]=item;result.Add(new Entry{Id=id,Name=Localization.instance.Localize(item.m_shared.m_name),Value=item.m_stack,Max=Math.Max(1,item.m_shared.m_maxStackSize),Description="Quality "+item.m_quality+" • durability "+item.m_durability.ToString("0")});}
                return Entries("Select a carried item.",result);
            }
            if(req.Command=="inventory-edit"||req.Command=="inventory-repair")
            {
                ItemDrop.ItemData item;if(!inventory.TryGetValue(req.Text??"",out item)||!p.GetInventory().ContainsItem(item))throw new InvalidOperationException("Item moved or removed. Refresh inventory.");
                bool newCheatState=req.Command=="inventory-edit"&&!Achievements.IsCheatedAtAll();
                if(req.Command=="inventory-edit"){if(req.Quantity<1||req.Quantity>Math.Max(1,item.m_shared.m_maxStackSize))throw new ArgumentException("Quantity exceeds this item's stack limit.");item.m_stack=req.Quantity;item.m_cheated=true;}
                else item.m_durability=item.GetMaxDurability();
                AccessTools.Method(typeof(Inventory),"Changed").Invoke(p.GetInventory(),new object[]{true,newCheatState});return Bridge.Snapshot("Selected item updated.");
            }
            if(req.Command=="buffs")
            {
                var effects=ObjectDB.instance.m_StatusEffects.Concat(p.GetSEMan().GetStatusEffects()).Where(x=>x!=null).GroupBy(x=>x.NameHash()).Select(x=>x.First());
                return Entries("Choose a status effect to apply or remove.",effects.Select(x=>new Entry{Id=x.NameHash().ToString(CultureInfo.InvariantCulture),Name=Localization.instance.Localize(x.m_name)+" ["+x.name+"]",Unlocked=p.GetSEMan().GetStatusEffect(x.NameHash())!=null,Description=x.m_tooltip}).OrderBy(x=>x.Name).ToList());
            }
            if(req.Command=="buff-add"||req.Command=="buff-remove")
            {
                int hash;if(!int.TryParse(req.Text,out hash))throw new ArgumentException("Invalid effect.");
                var effect=ObjectDB.instance.m_StatusEffects.FirstOrDefault(x=>x!=null&&x.NameHash()==hash)??p.GetSEMan().GetStatusEffect(hash);
                if(effect==null)throw new ArgumentException("Unknown effect.");
                if(req.Command=="buff-remove")p.GetSEMan().RemoveStatusEffect(hash);
                else {p.GetSEMan().AddStatusEffect(effect,true);var active=p.GetSEMan().GetStatusEffect(hash);if(active==null)throw new InvalidOperationException("The game rejected this effect for this character.");if(req.Value>0&&req.Value<=86400)active.m_ttl=req.Value;}
                return Bridge.Snapshot("Status effect updated.");
            }
            if(req.Command=="locations"||req.Command=="save-location"||req.Command=="delete-location"||req.Command=="teleport"||req.Command=="return")return Locations(req,p);
            throw new ArgumentException("Unknown command.");
        }
        static Bookmark Here(Player p){return new Bookmark{Name="Previous",X=p.transform.position.x,Y=p.transform.position.y,Z=p.transform.position.z,Yaw=p.transform.eulerAngles.y};}
        static Response Locations(Request req,Player p)
        {
            Directory.CreateDirectory(dataDir);string path=Path.Combine(dataDir,"locations-"+world+".json");
            var list=File.Exists(path)?Wire.Decode<List<Bookmark>>(File.ReadAllText(path)):new List<Bookmark>();
            if(req.Command=="save-location")
            {string name=(req.Text??"").Trim();if(name.Length==0||name.Length>80)throw new ArgumentException("Location name must be 1–80 characters.");var location=Here(p);location.Name=name;list.RemoveAll(x=>x.Name==name);list.Add(location);File.WriteAllText(path,Wire.Encode(list));}
            if(req.Command=="delete-location"){list.RemoveAll(x=>x.Name==req.Text);File.WriteAllText(path,Wire.Encode(list));}
            if(req.Command=="teleport"||req.Command=="return")
            {
                var target=req.Command=="return"?previous:list.FirstOrDefault(x=>x.Name==req.Text);if(target==null)throw new InvalidOperationException("No matching saved location.");
                if(float.IsNaN(target.X)||float.IsNaN(target.Y)||float.IsNaN(target.Z)||float.IsNaN(target.Yaw)||float.IsInfinity(target.Yaw)||Math.Abs(target.X)>20000||Math.Abs(target.Z)>20000||Math.Abs(target.Y)>10000)throw new InvalidOperationException("Saved coordinates are invalid.");
                var from=Here(p);if(!p.TeleportTo(new Vector3(target.X,target.Y,target.Z),Quaternion.Euler(0,target.Yaw,0),true))throw new InvalidOperationException("Teleport is busy or cooling down. Try again shortly.");previous=from;
                return Bridge.Snapshot("Teleport started.");
            }
            return Entries("Locations for this world.",list.Select(x=>new Entry{Id=x.Name,Name=x.Name,Description=x.X.ToString("0")+", "+x.Y.ToString("0")+", "+x.Z.ToString("0")}).ToList());
        }
        public static Response AchievementCommand(Request req)
        {
            var entries=new List<Entry>();uint count=SteamUserStats.GetNumAchievements();if(count==0)throw new InvalidOperationException("Steam achievement data is not ready. Keep Steam online and try again.");
            for(uint i=0;i<count;i++){string id=SteamUserStats.GetAchievementName(i);bool unlocked;if(!SteamUserStats.GetAchievement(id,out unlocked))throw new InvalidOperationException("Steam stats are not ready yet.");entries.Add(new Entry{Id=id,Name=SteamUserStats.GetAchievementDisplayAttribute(id,"name"),Description=SteamUserStats.GetAchievementDisplayAttribute(id,"desc"),Unlocked=unlocked});}
            if(req.Command=="unlock-achievement")
            {
                if(!req.Confirmed)throw new InvalidOperationException("Select an achievement and confirm its unlock in the desktop menu.");
                var selected=entries.FirstOrDefault(x=>x.Id==req.Text);if(selected==null)throw new ArgumentException("Unknown achievement ID.");
                if(selected.Unlocked)return Entries("This achievement is already unlocked.",entries);
                if(!SteamUserStats.SetAchievement(selected.Id))throw new InvalidOperationException("Steam rejected this achievement unlock.");
                if(!SteamUserStats.StoreStats())throw new InvalidOperationException("Steam could not submit the updated stats. Check Steam before retrying.");
                selected.Unlocked=true;return Entries("Unlock submitted to Steam: "+selected.Name+". Steam synchronization may take a moment.",entries);
            }
            return Entries("Select an achievement. Unlocking changes your Steam account.",entries);
        }
    }
}
