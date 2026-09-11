using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using SteamManagerProtocol;

namespace SteamManagerRuntime
{
    public static class Discovery
    {
        [DataContract] public class Backup
        {
            [DataMember] public string Character;
            [DataMember] public long CharacterId;
            [DataMember] public List<string> Known;
            [DataMember] public List<string> Recipes;
            [DataMember] public List<Dictionary<string,float>> Pickups;
        }
        static bool Available(ItemDrop.ItemData item)
        {return item!=null&&item.m_shared!=null&&!string.IsNullOrEmpty(item.m_shared.m_name)&&(string.IsNullOrEmpty(item.m_shared.m_dlc)||DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc));}
        static Dictionary<string,ItemDrop.ItemData> Items()
        {
            return ObjectDB.instance.m_items.Where(x=>x!=null).Select(x=>x.GetComponent<ItemDrop>()).Where(x=>x!=null&&Available(x.m_itemData)).Select(x=>x.m_itemData).GroupBy(x=>x.m_shared.m_name).ToDictionary(x=>x.Key,x=>x.First());
        }
        static HashSet<string> Ingredients()
        {
            var names=new HashSet<string>();
            foreach(var recipe in ObjectDB.instance.m_recipes.Where(x=>x!=null&&x.m_enabled&&x.m_item!=null&&Available(x.m_item.m_itemData)))
                foreach(var resource in recipe.m_resources)if(resource.m_resItem!=null&&Available(resource.m_resItem.m_itemData))names.Add(resource.m_resItem.m_itemData.m_shared.m_name);
            foreach(var prefab in ZNetScene.instance.m_prefabs){if(prefab==null)continue;var piece=prefab.GetComponent<Piece>();if(piece==null||!piece.m_enabled||(!string.IsNullOrEmpty(piece.m_dlc)&&!DLCMan.instance.IsDLCInstalled(piece.m_dlc)))continue;foreach(var resource in piece.m_resources)if(resource.m_resItem!=null&&Available(resource.m_resItem.m_itemData))names.Add(resource.m_resItem.m_itemData.m_shared.m_name);}
            return names;
        }
        public static Response Handle(Request request,Player player)
        {
            var known=(HashSet<string>)AccessTools.Field(typeof(Player),"m_knownMaterial").GetValue(player);
            var recipes=(HashSet<string>)AccessTools.Field(typeof(Player),"m_knownRecipes").GetValue(player);
            var profile=Game.instance.GetPlayerProfile();int difficulty=Achievements.GetCurrentAchievementDifficultyIndex();
            if(difficulty<1||difficulty>=profile.m_playerStats.Length)throw new InvalidOperationException("Achievement difficulty is not ready.");
            var items=Items();var ingredients=Ingredients();string message="Known materials and pickup records for this character. Crafted-item counts are separate.";
            if(request.Command!="discoveries")
            {
                if(!request.Confirmed)throw new InvalidOperationException("Choose a discovery action and confirm the character changes.");
                if(request.Command!="discover-ingredients"&&request.Command!="discover-all")throw new ArgumentException("Unknown discovery action.");
                var backup=new Backup{Character=player.GetPlayerName(),CharacterId=player.GetPlayerID(),Known=known.ToList(),Recipes=recipes.ToList(),Pickups=profile.m_playerStats.Select(x=>new Dictionary<string,float>(x.m_itemPickupStats)).ToList()};
                string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamManager","discovery-backups");Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,player.GetPlayerID()+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,Wire.Encode(backup));
                try
                {
                    var selected=request.Command=="discover-all"?items.Keys.Where(x=>true):ingredients.Where(items.ContainsKey);
                    int count=DiscoveryRecords.Apply(selected,known,new[]{0,1,difficulty}.Distinct().Select(x=>profile.m_playerStats[x].m_itemPickupStats));
                    AccessTools.Method(typeof(Player),"UpdateKnownRecipesList").Invoke(player,null);
                    AccessTools.Method(typeof(Player),"UpdateAvailablePiecesList").Invoke(player,null);
                    message=count+" item records updated. Saved with your character on the next normal game save. Craft items again for crafting progress. Backup: "+Path.GetFileName(path);
                }
                catch
                {
                    known.Clear();known.UnionWith(backup.Known);recipes.Clear();recipes.UnionWith(backup.Recipes);
                    for(int i=0;i<backup.Pickups.Count;i++){var target=profile.m_playerStats[i].m_itemPickupStats;target.Clear();foreach(var entry in backup.Pickups[i])target[entry.Key]=entry.Value;}
                    throw;
                }
            }
            var response=Bridge.Snapshot(message);
            response.Entries=items.Select(x=>{float count;profile.m_playerStats[difficulty].m_itemPickupStats.TryGetValue(x.Key,out count);return new Entry{Id=x.Key,Name=Localization.instance.Localize(x.Key),Value=count,Unlocked=known.Contains(x.Key)&&count>=1,Description=(ingredients.Contains(x.Key)?"Crafting/building ingredient. ":"")+"Discovered: "+(known.Contains(x.Key)?"yes":"no")+". Eligible pickups at current difficulty: "+count+". This does not record crafting or add inventory items."};}).OrderBy(x=>x.Name).ToList();
            return response;
        }
    }
}
