using System;
using System.Collections.Generic;
using System.Linq;
using SteamManagerProtocol;
using UnityEngine;

namespace SteamManagerRuntime
{
    public static class BlueprintTools
    {
        sealed class Visual { public Mesh Mesh; public Matrix4x4 Local; public int Index; public Material[] Materials; }
        static BlueprintDocument document;
        static Piece[] prefabs;
        static readonly List<Visual> visuals=new List<Visual>();
        static readonly List<string> missing=new List<string>();
        static Material material;
        static Player owner;
        static long world;
        static Vector3 origin,offset;
        static float yaw;
        static bool preview,building,free;
        static bool realMaterials=true;
        static int placed;
        static float nextPlacement;
        static string status="Import a blueprint or capture nearby player-built pieces.";
        public static bool Active=>preview||building;
        static Dictionary<string,Piece> Available()
        {
            var result=new Dictionary<string,Piece>(StringComparer.Ordinal);
            foreach(var item in ObjectDB.instance.m_items)
            {
                var drop=item.GetComponent<ItemDrop>();var table=drop==null?null:drop.m_itemData.m_shared.m_buildPieces;if(table==null)continue;
                foreach(var prefab in table.m_pieces)
                {
                    if(prefab==null)continue;var piece=prefab.GetComponent<Piece>();
                    if(piece==null||!piece.m_enabled||prefab.GetComponent<ZNetView>()==null||piece.m_repairPiece||piece.m_removePiece)continue;
                    if(!string.IsNullOrEmpty(piece.m_dlc)&&!DLCMan.instance.IsDLCInstalled(piece.m_dlc))continue;
                    result[prefab.name]=piece;
                }
            }
            return result;
        }
        static void CheckContext(Player p)
        {
            if(owner!=p||ZNet.instance.GetWorldUID()!=world){Clear();owner=p;world=ZNet.instance.GetWorldUID();document=null;prefabs=null;missing.Clear();status="Import a blueprint or capture nearby player-built pieces.";}
        }
        public static void Clear()
        {
            if(building)status="Stopped after "+placed+" pieces. Already placed pieces remain in the world.";
            building=false;preview=false;visuals.Clear();
            if(material!=null)UnityEngine.Object.Destroy(material);material=null;
        }
        static Vector3 Position(BlueprintPiece p)=>origin+offset+Quaternion.Euler(0,yaw,0)*new Vector3(p.X,p.Y,p.Z);
        static Quaternion Rotation(BlueprintPiece p)=>Quaternion.Euler(0,yaw,0)*new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);
        static Dictionary<string,int> Costs()
        {
            var costs=new Dictionary<string,int>();
            if(prefabs==null)return costs;
            foreach(var p in prefabs.Where(x=>x!=null))foreach(var r in p.m_resources)
            {if(r.m_resItem==null||r.GetAmount(0)<=0)continue;string name=r.m_resItem.m_itemData.m_shared.m_name;costs[name]=(costs.ContainsKey(name)?costs[name]:0)+r.GetAmount(0);}
            return costs;
        }
        static Response Snapshot(Player player)
        {
            var r=Bridge.Snapshot(building?"Building: "+placed+" / "+document.Pieces.Count+". Cancel stops further placement.":status);
            r.Entries=missing.Select(x=>new Entry{Id=x,Name="Unavailable: "+x,Description="Missing, disabled, or not an available building-tool piece. Placement is blocked."}).ToList();
            foreach(var x in Costs())r.Entries.Add(new Entry{Id=x.Key,Name=Localization.instance.Localize(x.Key)+" × "+x.Value,Value=x.Value,Description="Base materials: "+x.Value+" • carried: "+player.GetInventory().CountItems(x.Key)+". Free crafting (#36) skips costs."});
            if(document!=null)foreach(var warning in document.Warnings??new List<string>())r.Entries.Add(new Entry{Id=warning,Name=warning,Description=warning});
            return r;
        }
        public static Response Handle(Request req,Player p)
        {
            if(ObjectDB.instance==null||ZNet.instance==null)throw new InvalidOperationException("Enter a world first.");
            CheckContext(p);
            if(req.Command=="blueprint-status")return Snapshot(p);
            if(req.Command=="blueprint-style")
            {
                SetStyle(req.Text);
                if(preview)status="Local preview: "+(realMaterials?"real textures and materials (solid appearance).":"translucent teal ghost.")+" Visual only; pieces have not been placed.";
                return Snapshot(p);
            }
            if(req.Command=="blueprint-cancel"){Clear();if(placed==0)status="Preview cleared.";return Snapshot(p);}
            if(building)throw new InvalidOperationException("Wait for placement to finish or cancel it first.");
            if(req.Command=="blueprint-capture")
            {
                if(float.IsNaN(req.Value)||req.Value<1||req.Value>50)throw new ArgumentException("Capture radius must be 1–50 m.");
                var available=Available();var all=new List<Piece>();Piece.GetAllPiecesInRadius(p.transform.position,req.Value,all);
                var doc=new BlueprintDocument{Name=req.Text};
                foreach(var piece in all)
                {
                    if(!piece.IsPlacedByPlayer())continue;string name=piece.gameObject.name.Split('(')[0];Piece prefab;
                    if(!available.TryGetValue(name,out prefab))continue;
                    if((piece.transform.lossyScale-prefab.transform.localScale).sqrMagnitude>0.001f)throw new InvalidOperationException("Selection contains scaled pieces. Capture normal-scale pieces only.");
                    Vector3 pos=piece.transform.position-p.transform.position;var rot=piece.transform.rotation;
                    doc.Pieces.Add(new BlueprintPiece{Prefab=name,X=pos.x,Y=pos.y,Z=pos.z,Qx=rot.x,Qy=rot.y,Qz=rot.z,Qw=rot.w});
                }
                BlueprintFile.Validate(doc);var result=Bridge.Snapshot("Captured "+doc.Pieces.Count+" player-built pieces within "+req.Value+" m. Geometry only; container contents, signs and terrain are excluded.");result.Blueprint=doc;return result;
            }
            if(req.Command=="blueprint-prepare")
            {
                BlueprintFile.Validate(req.Blueprint);Clear();document=req.Blueprint;placed=0;
                var available=Available();prefabs=new Piece[document.Pieces.Count];missing.Clear();
                for(int i=0;i<prefabs.Length;i++){Piece prefab;if(available.TryGetValue(document.Pieces[i].Prefab,out prefab))prefabs[i]=prefab;else if(!missing.Contains(document.Pieces[i].Prefab))missing.Add(document.Pieces[i].Prefab);}
                origin=p.transform.position+p.transform.forward*6;offset=Vector3.zero;yaw=0;
                status=document.Name+": "+document.Pieces.Count+" pieces. "+(missing.Count==0?"Ready to preview. Anchor starts 6 m in front of you.":missing.Count+" unavailable piece types; placement blocked.");
                return Snapshot(p);
            }
            if(document==null)throw new InvalidOperationException("Load a blueprint first.");
            if(req.Command=="blueprint-preview")
            {
                SetStyle(req.Text);
                if(missing.Count>0)throw new InvalidOperationException("Resolve the unavailable pieces before previewing or placing.");
                foreach(float f in new[]{req.X,req.Y,req.Z,req.Yaw})if(float.IsNaN(f)||float.IsInfinity(f)||Math.Abs(f)>360)throw new ArgumentException("Invalid preview offset or rotation.");
                if(req.Enabled)origin=p.transform.position+p.transform.forward*6;
                offset=new Vector3(req.X,req.Y,req.Z);yaw=req.Yaw;
                if(material==null)
                {
                    var shader=Shader.Find("Sprites/Default")??Shader.Find("UI/Default");if(shader==null)throw new InvalidOperationException("Preview shader unavailable.");
                    material=new Material(shader){color=new Color(0.04f,0.3f,0.23f,0.3f)};
                    visuals.Clear();int draws=0;
                    for(int i=0;i<prefabs.Length;i++)
                    {
                        var lowerDetail=new HashSet<Renderer>();var highestDetail=new HashSet<Renderer>();
                        foreach(var group in prefabs[i].GetComponentsInChildren<LODGroup>(true))
                        {var levels=group.GetLODs();for(int level=0;level<levels.Length;level++)foreach(var renderer in levels[level].renderers){if(level==0)highestDetail.Add(renderer);else lowerDetail.Add(renderer);}}
                        lowerDetail.ExceptWith(highestDetail);
                        foreach(var mesh in prefabs[i].GetComponentsInChildren<MeshFilter>(true))
                        {
                            var renderer=mesh.GetComponent<MeshRenderer>();
                            if(mesh.sharedMesh==null||renderer==null||!renderer.enabled||lowerDetail.Contains(renderer)||!VisiblePart(mesh.transform,prefabs[i].transform))continue;
                            draws+=mesh.sharedMesh.subMeshCount;if(draws>3000){Clear();throw new InvalidOperationException("Blueprint has too many meshes for a responsive preview. Use a smaller selection.");}
                            visuals.Add(new Visual{Index=i,Mesh=mesh.sharedMesh,Local=prefabs[i].transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix,Materials=renderer.sharedMaterials});
                        }
                    }
                    if(visuals.Count==0){Clear();throw new InvalidOperationException("No supported meshes found for preview.");}
                }
                preview=true;status="Local preview: "+document.Pieces.Count+" pieces, "+(realMaterials?"real materials":"teal ghost")+". Adjust offsets/rotation, then Place blueprint. No world objects created.";
                return Snapshot(p);
            }
            if(req.Command=="blueprint-place")
            {
                if(!req.Confirmed||!preview||missing.Count>0)throw new InvalidOperationException("Preview the blueprint and confirm placement first.");
                free=Bridge.On(36)||p.NoCostCheat();
                for(int i=0;i<prefabs.Length;i++)ValidateSite(p,i);
                if(!free)
                {
                    foreach(var cost in Costs())if(p.GetInventory().CountItems(cost.Key)<cost.Value)throw new InvalidOperationException("Not enough total materials: "+Localization.instance.Localize(cost.Key)+". Enable Free crafting (#36) or gather the materials.");
                    foreach(var prefab in prefabs)if(!p.HaveRequirements(prefab,Player.RequirementMode.CanBuild))throw new InvalidOperationException("Missing materials or a required crafting station.");
                }
                placed=0;nextPlacement=Time.realtimeSinceStartup;building=true;preview=false;status="Placement started.";return Snapshot(p);
            }
            throw new ArgumentException("Unknown blueprint command.");
        }
        static void ValidateSite(Player player,int i)
        {
            var position=Position(document.Pieces[i]);
            if(Vector3.Distance(player.transform.position,position)>100)throw new InvalidOperationException("All pieces must be within 100 m of your character.");
            if(Location.IsInsideNoBuildLocation(position)||!PrivateArea.CheckAccess(position,0,false))throw new InvalidOperationException("A piece overlaps a protected or no-build area.");
        }
        static void SetStyle(string style)
        {
            if(style!=null&&style!="real"&&style!="ghost")throw new ArgumentException("Unknown preview style.");
            realMaterials=style!="ghost";
        }
        static bool VisiblePart(Transform part,Transform root)
        {
            for(var node=part;node!=null&&node!=root;node=node.parent)if(!node.gameObject.activeSelf)return false;
            return true;
        }
        public static void Tick(Player p)
        {
            if(!Active)return;
            if(p==null||p!=owner||p.IsDead()||ZNet.instance==null||ZNet.instance.GetWorldUID()!=world){Clear();return;}
            try
            {
                if(building)
                {
                    if(Time.realtimeSinceStartup<nextPlacement)return;
                    nextPlacement=Time.realtimeSinceStartup+0.1f;
                    int i=placed;var prefab=prefabs[i];ValidateSite(p,i);
                    if(!free&&!p.HaveRequirements(prefab,Player.RequirementMode.CanBuild))throw new InvalidOperationException("Materials or crafting station no longer available.");
                    bool cheated=(free||p.GetInventory().ItemCheated(prefab.m_resources))&&!PlayerProfile.s_bypassCheatChecks;
                    p.PlacePiece(prefab,Position(document.Pieces[i]),Rotation(document.Pieces[i]),false,cheated);
                    placed++; // Do not retry a placed piece if a later bookkeeping operation fails.
                    if(!free&&!ZoneSystem.instance.GetGlobalKey(prefab.FreeBuildKey()))p.ConsumeResources(prefab.m_resources,0);
                    if(placed==prefabs.Length){building=false;status="Placed "+placed+" pieces. The game saves them normally; structural support still applies.";Clear();}
                }
                else if(preview)
                {
                    foreach(var v in visuals)
                    {
                        var piece=document.Pieces[v.Index];var matrix=Matrix4x4.TRS(Position(piece),Rotation(piece),prefabs[v.Index].transform.localScale)*v.Local;
                        for(int sub=0;sub<v.Mesh.subMeshCount;sub++)
                        {
                            var drawMaterial=realMaterials&&sub<v.Materials.Length&&v.Materials[sub]!=null?v.Materials[sub]:material;
                            Graphics.DrawMesh(v.Mesh,matrix,drawMaterial,0,null,sub);
                        }
                    }
                }
            }
            catch(Exception e){Clear();status="Stopped after "+placed+" pieces: "+e.GetBaseException().Message+" Already placed pieces remain.";Bridge.Log("Blueprint operation stopped: "+e.GetType().Name);}
        }
    }
}
