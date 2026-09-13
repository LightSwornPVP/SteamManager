using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SteamManagerProtocol;
using SteamManagerRuntime;
using UnityEngine;

// In-memory game boundary: exercise command behavior without touching a game or save.
namespace UnityEngine {
    public class Object { public static T[] FindObjectsByType<T>(FindObjectsSortMode mode){return new T[0];} }
    public enum FindObjectsSortMode { None }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static float Distance(Vector3 a,Vector3 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z));} }
    public class Transform { public Vector3 position; }
    public class GameObject { public string name; }
    public class Texture2D { public void Apply(){} }
}
namespace HarmonyLib { public static class AccessTools { public static System.Reflection.MethodInfo Method(Type t,string n,Type[] p){return t.GetMethod(n,p);} public static System.Reflection.FieldInfo Field(Type t,string n){return t.GetField(n);} } }
public class Player { public static Player m_localPlayer; public Transform transform=new Transform(); }
public class World { public string m_seedName; public string m_name; }
public class ZNet { public static ZNet instance=new ZNet(); public bool Server; public long World=1; public World Data; public World GetWorld(){return Data;} public long GetWorldUID(){return World;} public bool IsServer(){return Server;} }
public class ZoneSystem {
    public static ZoneSystem instance=new ZoneSystem();
    public class Definition { public string m_prefabName; }
    public class Location { public Definition m_location; public Vector3 m_position; }
    public List<Location> Locations=new List<Location>(); public Dictionary<Vector3,string> Icons=new Dictionary<Vector3,string>();
    public IEnumerable<Location> GetLocationList(){return Locations;} public void GetLocationIcons(Dictionary<Vector3,string> icons){foreach(var x in Icons)icons.Add(x.Key,x.Value);}
}
public class Pickable { public Transform transform; public GameObject gameObject; }
public class MineRock:Pickable{} public class MineRock5:Pickable{}
public class Minimap {
    public static Minimap instance=new Minimap();
    public enum PinType { Icon3 }
    public class PinData { public string m_name; public Vector3 m_pos; public bool Saved; }
    public List<PinData> Pins=new List<PinData>(); public int Coroutines;
    public PinData AddPin(Vector3 p,PinType t,string n,bool save,bool check,long owner){var pin=new PinData{m_name=n,m_pos=p,Saved=save};Pins.Add(pin);return pin;}
    public void RemovePin(PinData pin){Pins.Remove(pin);} public void StartCoroutine(IEnumerator e){Coroutines++;}
}
namespace SteamManagerRuntime { public static class Bridge { public static Response Snapshot(string message){return new Response{Ok=true,Message=message};} } }
class MapToolsTests {
    static int count; static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
    static Response Run(string command,string text=null,bool confirmed=false){return MapTools.Handle(new Request{Command=command,Text=text,Confirmed=confirmed},Player.m_localPlayer);}
    static void Reject(Action action,string label){try{action();}catch(ArgumentException){count++;return;}throw new Exception(label);}
    static int Main(){
        Player.m_localPlayer=new Player();
        ZNet.instance.Data=new World{m_seedName="AbC012xYz9",m_name="Test world"};
        var seed=Run("map-seed");Check(seed.WorldSeed=="AbC012xYz9"&&seed.WorldName=="Test world","Remote seed is read exactly with case preserved");
        var seedCopy=Wire.Decode<Response>(Wire.Encode(seed));Check(seedCopy.WorldSeed==seed.WorldSeed&&seedCopy.WorldName==seed.WorldName,"Seed response survives serialization");
        ZNet.instance.Data=new World{m_seedName="ChangedSeed",m_name="Second world"};Check(Run("map-seed").WorldSeed=="ChangedSeed","Seed is read fresh after world changes");
        ZNet.instance.Data=null;bool unavailable=false;try{Run("map-seed");}catch(InvalidOperationException){unavailable=true;}Check(unavailable,"Missing world cannot return a stale seed");
        Check(Run("map-results").WorldSeed==null,"Regular map requests do not expose seed");
        ZoneSystem.instance.Locations.Add(new ZoneSystem.Location{m_location=new ZoneSystem.Definition{m_prefabName="SecretCrypt"},m_position=new Vector3(100,0,0)});
        ZoneSystem.instance.Icons.Add(new Vector3(400,0,0),"Haldor");
        var remote=Run("map-scan","locations");Check(remote.Entries.Count==1&&remote.Entries[0].Name=="Haldor","Remote clients cannot expose host records");
        Check(Run("map-scan","nearby").Entries.Count==0,"Nearby scan excludes distant icons");
        ZNet.instance.Server=true;var local=Run("map-scan","locations");Check(local.Entries.Count==2,"Host scan includes unvisited records");
        Check(Run("map-scan","nearby").Entries.Single().Name=="SecretCrypt","Nearby host scan applies radius");
        var own=Minimap.instance.AddPin(new Vector3(0,0,0),Minimap.PinType.Icon3,"My pin",true,false,0);
        string selected=Run("map-scan","locations").Entries[0].Id;
        Run("map-pin",selected);Run("map-pin",selected);Check(Minimap.instance.Pins.Count==2,"Repeated pin command is idempotent");
        Check(!Minimap.instance.Pins.Last().Saved,"Tool pins are not written to saves");
        Run("map-clear");Check(Minimap.instance.Pins.Single()==own,"Clear preserves user pins");
        Run("map-pin-filter","HALDOR");Check(Minimap.instance.Pins.Last().m_name=="[SM] Haldor","Name filters are case insensitive");
        ZNet.instance.World=2;Check(Run("map-results").Entries.Count==0&&Minimap.instance.Pins.Single()==own,"World switch invalidates results and tool pins");
        Reject(()=>Run("map-pin",selected),"Stale results rejected");
        Reject(()=>Run("map-reveal"),"Reveal requires explicit confirmation");Check(Minimap.instance.Coroutines==0,"Rejected reveal cannot start exploration");
        Run("map-reveal",confirmed:true);Run("map-reveal",confirmed:true);Check(Minimap.instance.Coroutines==1,"Reveal cannot run twice concurrently");
        Reject(()=>Run("map-scan","invalid"),"Unknown scan type rejected");
        Console.WriteLine(count+" map command checks passed.");return 0;
    }
}
