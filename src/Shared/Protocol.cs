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
        [DataMember] public bool Confirmed;
        [DataMember] public List<Feature> Settings;
        [DataMember] public BlueprintDocument Blueprint;
        [DataMember] public float X,Y,Z,Yaw;
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
        [DataMember] public string Description;
        [DataMember] public bool Unlocked;
    }
    [DataContract] public class Response
    {
        [DataMember] public bool Ok;
        [DataMember] public string Message;
        [DataMember] public string Version;
        [DataMember] public string Player;
        [DataMember] public string WorldSeed;
        [DataMember] public string WorldName;
        [DataMember] public BlueprintDocument Blueprint;
        [DataMember] public bool Multiplayer;
        [DataMember] public int Protocol = 3;
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
            F(1,"God mode","Player","Uses the game's god-mode flag; restores its original state on disable."),
            F(2,"Unlimited health","Player","Blocks damage to your local character and restores health."),
            F(3,"Unlimited stamina","Player","Prevents stamina spending; requires a living character."),
            F(4,"Unlimited Eitr","Player","Prevents Eitr spending. Eat Eitr food for a visible Eitr bar."),
            F(5,"Stealth / ghost mode","Player","Stops normal enemy detection. Other players can still see you."),
            F(6,"Carry weight","Player","Overrides your carry limit while enabled.",1000,1,100000),
            F(7,"Movement multiplier","Player","Multiplies walking, running, and swimming speed.",1,0.1f,10),
            F(8,"Jump multiplier","Player","Multiplies your normal jump force.",1,0.1f,10),
            F(9,"No fall damage","Player","Blocks fall damage only."),
            F(10,"Health regeneration","Survival tuning","Multiplies health regeneration from food and effects.",1,0,20),
            F(11,"Stamina regeneration","Survival tuning","Multiplies stamina recovery.",1,0,20),
            F(12,"Eitr regeneration","Survival tuning","Multiplies Eitr recovery.",1,0,20),
            F(13,"Stamina cost","Survival tuning","0 means no cost; 0.5 means half cost.",1,0,5),
            F(14,"Eitr cost","Survival tuning","0 means no cost; 0.5 means half cost.",1,0,5),
            F(15,"Food duration","Food & rest","0 freezes remaining duration; 1 is normal; 2 lasts twice as long.",0,0,100),
            F(16,"Rested duration","Food & rest","0 freezes an existing rested effect; positive values multiply duration.",0,0,100),
            F(17,"Maximum comfort","Food & rest","Computes comfort from available building pieces. Still requires resting."),
            F(18,"Buff browser","Food & rest","Apply or remove an individual status effect.",action:true),
            F(19,"No death penalty","Survival tuning","Keeps inventory and skill levels on death. You still respawn."),
            F(20,"Protect skills on death","Survival tuning","Preserves skill levels while keeping other death mechanics."),
            F(21,"Guardian cooldown","Survival tuning","0 clears cooldown; 0.5 makes it recover twice as fast.",1,0,10),
            F(22,"Guardian duration","Survival tuning","Multiplies the duration of active guardian power effects.",1,0.1f,100),
            F(23,"Damage multiplier","Combat","Outgoing attacks against non-player, untamed creatures only.",1,0.25f,20),
            F(24,"Damage received","Combat","0 blocks damage; 0.5 halves it; 2 doubles it.",1,0,10),
            F(25,"One-hit kills","Combat","High damage against non-player, untamed creatures. Boss phases may resist."),
            F(26,"Chopping / mining multiplier","Gathering","Scales tool damage to trees and mineable objects.",1,0.1f,100),
            F(27,"One-hit object destruction","Gathering","Your hits destroy trees, rocks, and structures. Saved destruction cannot be undone."),
            F(28,"Protect buildings from my attacks","Gathering","Blocks your hits against structures; takes priority over one-hit destruction."),
            F(29,"Unlimited items","Inventory","Keeps consumables and crafting resources when used. Transfers and drops remain normal."),
            F(30,"Unlimited durability","Inventory","Keeps carried equipment repaired while active."),
            F(31,"Repair all equipment","Inventory","One-time repair of carried equipment; this persists in saves.",action:true),
            F(32,"Inventory editor","Inventory","Select a carried stack to change quantity or repair it.",action:true),
            F(33,"Item browser","Inventory","Search installed item definitions; spawned items persist in saves.",action:true),
            F(34,"Skill XP multiplier","Skills","Multiplies skill experience earned through gameplay.",2,0.1f,100),
            F(35,"Skill editor","Skills","Set an individual skill level; this persists in saves.",action:true),
            F(36,"Free crafting","Building & exploration","Uses the game's no-cost mode; also exposes recipes while active."),
            F(37,"All blueprints","Building & exploration","Temporarily exposes recipes and building pieces."),
            F(38,"Build restriction overrides","Building & exploration","Overrides placement and station requirements. Does not supply materials."),
            F(39,"Flight","Building & exploration","Uses game flight controls: movement, jump to ascend, crouch to descend."),
            F(40,"Noclip","Building & exploration","Enables flight and disables local body collisions. Exit outside solid geometry."),
            F(41,"Saved locations","Travel","Save and teleport to named locations, separated by world.",action:true),
            F(42,"Return to previous location","Travel","Return to the location before your last trainer teleport.",action:true),
            F(43,"Portal materials","Building & exploration","Allows restricted inventory through the local portal check."),
            F(44,"Freeze daytime","Time & camera","Freezes your displayed daylight; does not freeze remote-server simulation."),
            F(45,"Time passage speed","Time & camera","Single-player world clock multiplier. On remote servers, changes local daylight only.",1,0.1f,20),
            F(46,"Game speed","Time & camera","Local simulation speed. Multiplayer can desynchronize; the server remains authoritative.",1,0.25f,3),
            F(47,"Hide HUD","Time & camera","Hides the gameplay HUD while active."),
            F(48,"Photo camera","Time & camera","Free camera with field of view below. Scroll changes movement speed.",65,10,120),
            F(49,"Keep achievements enabled","Achievements","Enables the game achievement bypass and suppresses its blocked-item pickup warning. Existing cheat tags remain."),
            F(50,"Achievement browser","Achievements","Choose one Steam achievement to unlock. Unlocks persist on your Steam account.",action:true),
            F(51,"Sprint speed override","Player","Overrides the movement multiplier for sprinting only.",1,0.1f,10),
            F(52,"Swim speed override","Player","Overrides the movement multiplier for swimming only.",1,0.1f,10),
            F(53,"Mining speed override","Gathering","Overrides the gathering multiplier for pickaxe damage only.",1,0.1f,100),
            F(54,"Item discovery","Achievements","Mark crafting ingredients or all available item types discovered and collected. Changes character records.",action:true)
        }; }
        public static void Validate(Feature f, float value)
        {
            if (f == null || f.Action) throw new ArgumentException("Unknown or non-toggle feature.");
            if (float.IsNaN(value) || float.IsInfinity(value) || (f.Numeric && (value < f.Min || value > f.Max))) throw new ArgumentOutOfRangeException("value");
            if (!string.IsNullOrEmpty(f.Error)) throw new InvalidOperationException(f.Error);
        }
    }
}
