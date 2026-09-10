using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SteamManagerProtocol
{
    [DataContract] public class Request
    {
        [DataMember] public string Command;
        [DataMember] public string Auth;
        [DataMember] public int Id;
        [DataMember] public bool Enabled;
        [DataMember] public float Value;
        [DataMember] public string Text;
        [DataMember] public int Quantity;
        [DataMember] public int Quality;
    }
    [DataContract] public class Feature
    {
        [DataMember] public int Id;
        [DataMember] public string Name;
        [DataMember] public string Group;
        [DataMember] public string Hint;
        [DataMember] public bool Enabled;
        [DataMember] public float Value;
        [DataMember] public float Min;
        [DataMember] public float Max;
        [DataMember] public bool Numeric;
        [DataMember] public bool Action;
        [DataMember] public string Error;
    }
    [DataContract] public class Entry
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public float Value;
        [DataMember] public int Max;
    }
    [DataContract] public class Response
    {
        [DataMember] public bool Ok;
        [DataMember] public string Message;
        [DataMember] public string Version;
        [DataMember] public string Player;
        [DataMember] public bool Multiplayer;
        [DataMember] public List<Feature> Features;
        [DataMember] public List<Entry> Entries;
    }
    public static class Wire
    {
        public static string Encode<T>(T value)
        {
            using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public static T Decode<T>(string value)
        {
            if (value == null || value.Length > 2000000) throw new InvalidDataException("Invalid message size.");
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(value))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }
    public static class Catalog
    {
        static Feature F(int id, string name, string group, string hint, float value = 0, float min = 0, float max = 0, bool action = false)
        { return new Feature { Id = id, Name = name, Group = group, Hint = hint, Value = value, Min = min, Max = max, Numeric = max > min, Action = action }; }
        public static List<Feature> Create() { return new List<Feature> {
            F(2,"Unlimited health","Player","Blocks damage to your local character and restores health."),
            F(3,"Unlimited stamina","Player","Prevents stamina spending; requires a living character."),
            F(4,"Unlimited Eitr","Player","Prevents Eitr spending. Eat Eitr food for a visible Eitr bar."),
            F(6,"Carry weight","Player","Overrides your carry limit while enabled.",1000,1,100000),
            F(7,"Movement multiplier","Player","Multiplies walking, running, and swimming speed.",1,0.1f,10),
            F(8,"Jump multiplier","Player","Multiplies your normal jump force.",1,0.1f,10),
            F(9,"No fall damage","Player","Blocks fall damage only."),
            F(15,"Food duration","Food & rest","0 freezes remaining duration; 1 is normal; 2 lasts twice as long.",0,0,100),
            F(16,"Rested duration","Food & rest","0 freezes an existing rested effect; positive values multiply duration.",0,0,100),
            F(17,"Maximum comfort","Food & rest","Computes comfort from available building pieces. Still requires resting."),
            F(23,"Damage multiplier","Combat","Outgoing attacks against non-player, untamed creatures only.",1,0.25f,20),
            F(25,"One-hit kills","Combat","High damage against non-player, untamed creatures. Boss phases may resist."),
            F(29,"Unlimited items","Inventory","Keeps consumables and crafting resources when used. Transfers and drops remain normal."),
            F(30,"Unlimited durability","Inventory","Keeps carried equipment repaired while active."),
            F(31,"Repair all equipment","Inventory","One-time repair of carried equipment; this persists in saves.",action:true),
            F(33,"Item browser","Inventory","Search installed item definitions; spawned items persist in saves.",action:true),
            F(34,"Skill XP multiplier","Skills","Multiplies skill experience earned through gameplay.",2,0.1f,100),
            F(35,"Skill editor","Skills","Set an individual skill level; this persists in saves.",action:true),
            F(36,"Free crafting","Building & exploration","Uses the game's no-cost mode; also exposes recipes while active."),
            F(37,"All blueprints","Building & exploration","Temporarily exposes recipes and building pieces."),
            F(39,"Flight","Building & exploration","Uses game flight controls: movement, jump to ascend, crouch to descend."),
            F(43,"Portal materials","Building & exploration","Allows restricted inventory through the local portal check."),
            F(49,"Keep achievements enabled","Achievements","Keeps the normal achievement eligibility gate open. Does not award achievements.")
        }; }
        public static void Validate(Feature f, float value)
        {
            if (f == null || f.Action) throw new ArgumentException("Unknown or non-toggle feature.");
            if (float.IsNaN(value) || float.IsInfinity(value) || (f.Numeric && (value < f.Min || value > f.Max))) throw new ArgumentOutOfRangeException("value");
            if (!string.IsNullOrEmpty(f.Error)) throw new InvalidOperationException(f.Error);
        }
    }
}
