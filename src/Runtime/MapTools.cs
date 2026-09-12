using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SteamManagerProtocol;
using UnityEngine;

namespace SteamManagerRuntime
{
    public static class MapTools
    {
        sealed class Place { public string Name; public Vector3 Position; }
        static readonly Dictionary<string,Place> results=new Dictionary<string,Place>();
        static readonly List<Minimap.PinData> ownedPins=new List<Minimap.PinData>();
        static Minimap currentMap;
        static Player currentPlayer;
        static long world;
        static bool revealing;
        static string scope="Scan locations or nearby resources.";
        static float Distance(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
        static void Context(Player player)
        {
            long id=ZNet.instance.GetWorldUID();
            if(currentMap==Minimap.instance&&currentPlayer==player&&world==id)return;
            if(currentMap!=null)foreach(var pin in ownedPins)currentMap.RemovePin(pin);
            ownedPins.Clear();results.Clear();revealing=false;
            currentMap=Minimap.instance;currentPlayer=player;world=id;
            scope="Scan locations or nearby resources.";
        }
        static void Add(string name,Vector3 position)
        {
            if(string.IsNullOrEmpty(name)||results.Count>=10000)return;
            name=name.Replace("(Clone)","").Trim();
            string key=name+"|"+position.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+position.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            results[key]=new Place{Name=name,Position=position};
        }
        public static Response Handle(Request req,Player player)
        {
            if(Minimap.instance==null||ZoneSystem.instance==null)throw new InvalidOperationException("The map is not ready yet.");
            Context(player);
            if(req.Command=="map-scan")
            {
                if(req.Text!="locations"&&req.Text!="nearby"&&req.Text!="resources")throw new ArgumentException("Choose a scan type.");
                results.Clear();
                bool local=ZNet.instance.IsServer();
                if(req.Text=="resources")
                {
                    foreach(var item in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))if(Distance(player.transform.position,item.transform.position)<=200)Add(item.gameObject.name,item.transform.position);
                    foreach(var item in UnityEngine.Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None))if(Distance(player.transform.position,item.transform.position)<=200)Add(item.gameObject.name,item.transform.position);
                    foreach(var item in UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None))if(Distance(player.transform.position,item.transform.position)<=200)Add(item.gameObject.name,item.transform.position);
                    scope="Loaded pickables and mineable rocks within 200 m; some may already be depleted. Refresh after moving.";
                }
                else
                {
                    if(local)foreach(var item in ZoneSystem.instance.GetLocationList())
                        if(req.Text!="nearby"||Distance(player.transform.position,item.m_position)<=200)Add(item.m_location.m_prefabName,item.m_position);
                    var icons=new Dictionary<Vector3,string>();ZoneSystem.instance.GetLocationIcons(icons);
                    foreach(var item in icons)if(req.Text!="nearby"||Distance(player.transform.position,item.Key)<=200)Add(item.Value,item.Key);
                    scope=local?"World location records (including unvisited sites).":"Server-provided markers only; the server's complete location list is unavailable to this client.";
                    if(req.Text=="nearby")scope+=" Within 200 m.";
                }
            }
            else if(req.Command=="map-pin")
            {
                Place place;if(!results.TryGetValue(req.Text??"",out place))throw new ArgumentException("Scan and select a location first.");
                Pin(place);scope="Selected result pinned. Pins are temporary and removed with Remove my pins or when the game closes.";
            }
            else if(req.Command=="map-pin-filter")
            {
                string filter=(req.Text??"").Trim();
                var selected=results.Values.Where(x=>x.Name.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0).ToList();
                if(selected.Count>250)throw new ArgumentException("More than 250 results match. Narrow the search before pinning.");
                foreach(var place in selected)Pin(place);
                scope=selected.Count+" matching results pinned. Pins are temporary; Remove my pins clears only SteamManager pins.";
            }
            else if(req.Command=="map-clear")
            {
                foreach(var pin in ownedPins)currentMap.RemovePin(pin);ownedPins.Clear();scope="SteamManager pins removed. Your own pins are preserved.";
            }
            else if(req.Command=="map-reveal")
            {
                if(!req.Confirmed)throw new ArgumentException("Confirm full map reveal first; exploration is saved with your character.");
                if(!revealing){revealing=true;currentMap.StartCoroutine(Reveal(currentMap,player,world));}
                scope="Revealing map gradually. Exploration is saved normally by the game.";
            }
            else if(req.Command!="map-results")throw new ArgumentException("Unknown map action.");
            var response=Bridge.Snapshot(scope+(revealing?" Reveal in progress.":""));
            response.Entries=results.OrderBy(x=>Distance(player.transform.position,x.Value.Position)).Select(x=>new Entry{Id=x.Key,Name=x.Value.Name,Description=Math.Round(Distance(player.transform.position,x.Value.Position))+" m away • X "+Math.Round(x.Value.Position.x)+", Z "+Math.Round(x.Value.Position.z)+" • "+scope}).ToList();
            return response;
        }
        static void Pin(Place place)
        {
            if(ownedPins.Any(x=>x.m_name=="[SM] "+place.Name&&Distance(x.m_pos,place.Position)<1))return;
            if(ownedPins.Count>=500)throw new InvalidOperationException("500 SteamManager pins already exist. Remove them before adding more.");
            ownedPins.Add(currentMap.AddPin(place.Position,Minimap.PinType.Icon3,"[SM] "+place.Name,false,false,0L));
        }
        static IEnumerator Reveal(Minimap map,Player player,long id)
        {
            try
            {
                var explore=(Func<int,int,bool>)Delegate.CreateDelegate(typeof(Func<int,int,bool>),map,AccessTools.Method(typeof(Minimap),"Explore",new[]{typeof(int),typeof(int)}));
                int size=(int)AccessTools.Field(typeof(Minimap),"m_textureSize").GetValue(map);
                var texture=(Texture2D)AccessTools.Field(typeof(Minimap),"m_fogTexture").GetValue(map);
                for(int y=0;y<size;y++)
                {
                    if(map==null||Player.m_localPlayer!=player||ZNet.instance==null||ZNet.instance.GetWorldUID()!=id)yield break;
                    for(int x=0;x<size;x++)explore(x,y);
                    if(y%8==7)yield return null;
                }
                texture.Apply();scope="Entire map revealed. Location markers require a separate scan.";
            }
            finally {revealing=false;}
        }
    }
}
