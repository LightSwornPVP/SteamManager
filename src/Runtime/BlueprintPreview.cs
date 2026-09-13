using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SteamManagerProtocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteamManagerRuntime
{
    // Only meshes and materials are created. No prefab is instantiated and no ZDO is created.
    sealed class BlueprintPreview:IDisposable
    {
        sealed class Part { public Mesh Mesh; public Material Material; public int Sub; public Matrix4x4 Local; }
        sealed class Group { public Mesh Mesh; public Material Source,Instanced; public int Sub; public List<Matrix4x4> Pending=new List<Matrix4x4>();public List<Matrix4x4[]> Batches=new List<Matrix4x4[]>();public bool Failed; }
        readonly BlueprintDocument doc;
        readonly GameObject[] prefabs;
        readonly Matrix4x4 root;
        readonly Dictionary<int,List<Part>> templates=new Dictionary<int,List<Part>>();
        readonly Dictionary<string,Group> groups=new Dictionary<string,Group>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly HashSet<string> omitted=new HashSet<string>();
        readonly Material ghost;
        int next,draws;
        bool ready;
        public bool Real=true;
        public bool Ready=>ready;
        public string Description=>!ready?"Preparing preview: "+next+" / "+doc.Pieces.Count+" objects.":"Preview only: "+doc.Pieces.Count+" records, "+groups.Count+" mesh/material groups."+(omitted.Count>0?" No renderable model for: "+string.Join(", ",omitted.Take(12))+(omitted.Count>12?" …":""):"");
        public BlueprintPreview(BlueprintDocument document,GameObject[] models,Matrix4x4 transform,bool real)
        {
            doc=document;prefabs=models;root=transform;Real=real;
            var shader=Shader.Find("Sprites/Default")??Shader.Find("UI/Default");if(shader!=null)ghost=new Material(shader){color=new Color(.04f,.3f,.23f,.3f)};
        }
        List<Part> Template(GameObject prefab)
        {
            int key=prefab.GetInstanceID();List<Part> result;if(templates.TryGetValue(key,out result))return result;
            result=new List<Part>();templates.Add(key,result);
            var lower=new HashSet<Renderer>();var first=new HashSet<Renderer>();
            foreach(var lod in prefab.GetComponentsInChildren<LODGroup>(true)){var levels=lod.GetLODs();for(int i=0;i<levels.Length;i++)foreach(var renderer in levels[i].renderers){if(i==0)first.Add(renderer);else lower.Add(renderer);}}lower.ExceptWith(first);
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if(lower.Contains(renderer)||!renderer.enabled||!Visible(renderer.transform,prefab.transform))continue;
                Mesh mesh=null;var filter=renderer.GetComponent<MeshFilter>();var skin=renderer as SkinnedMeshRenderer;
                if(skin!=null&&skin.sharedMesh!=null){mesh=new Mesh();ownedMeshes.Add(mesh);try{skin.BakeMesh(mesh);}catch(Exception){omitted.Add(prefab.name+" (skinned model)");continue;}}
                else if(filter!=null)mesh=filter.sharedMesh;
                if(mesh==null||mesh.vertexCount==0)continue;
                var mats=renderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)if(sub<mats.Length&&mats[sub]!=null)
                    result.Add(new Part{Mesh=mesh,Material=mats[sub],Sub=sub,Local=prefab.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
            }
            return result;
        }
        static bool Visible(Transform child,Transform root){for(var n=child;n!=null&&n!=root;n=n.parent)if(!n.gameObject.activeSelf)return false;return true;}
        public void Tick()
        {
            if(!ready)
            {
                var watch=Stopwatch.StartNew();int processed=0;
                while(next<doc.Pieces.Count&&processed++<100&&watch.ElapsedMilliseconds<4)
                {
                    int i=next++;var entry=doc.Pieces[i];var prefab=prefabs[i];if(prefab==null){omitted.Add(entry.Prefab);continue;}
                    var parts=Template(prefab);if(parts.Count==0){omitted.Add(entry.Prefab);continue;}
                    var transform=root*Matrix4x4.TRS(new Vector3(entry.X,entry.Y,entry.Z),new Quaternion(entry.Qx,entry.Qy,entry.Qz,entry.Qw),Vector3.Scale(prefab.transform.localScale,new Vector3(entry.Sx,entry.Sy,entry.Sz)));
                    foreach(var part in parts)
                    {
                        if(++draws>150000)throw new InvalidOperationException("Preview geometry exceeds the rendering budget.");
                        string key=part.Mesh.GetInstanceID()+":"+part.Material.GetInstanceID()+":"+part.Sub;Group group;
                        if(!groups.TryGetValue(key,out group))
                        {group=new Group{Mesh=part.Mesh,Source=part.Material,Sub=part.Sub};if(SystemInfo.supportsInstancing)group.Instanced=new Material(part.Material){enableInstancing=true};groups.Add(key,group);}
                        group.Pending.Add(transform*part.Local);
                        if(group.Pending.Count==1023){group.Batches.Add(group.Pending.ToArray());group.Pending.Clear();}
                    }
                }
                if(next==doc.Pieces.Count)
                {foreach(var group in groups.Values){if(group.Pending.Count>0)group.Batches.Add(group.Pending.ToArray());group.Pending.Clear();}if(groups.Count==0)throw new InvalidOperationException("No models could be rendered.");ready=true;}
                return;
            }
            int fallback=0;
            foreach(var group in groups.Values)foreach(var batch in group.Batches)
            {
                if(Real&&group.Instanced!=null&&!group.Failed)
                {
                    try {Graphics.DrawMeshInstanced(group.Mesh,group.Sub,group.Instanced,batch,batch.Length,null,ShadowCastingMode.Off,true,0);continue;}
                    catch(InvalidOperationException){group.Failed=true;}
                }
                fallback+=batch.Length;if(fallback>3000)throw new InvalidOperationException("This preview requires GPU-instanced real materials. Select real materials or use a smaller blueprint.");
                foreach(var matrix in batch)Graphics.DrawMesh(group.Mesh,matrix,Real||ghost==null?group.Source:ghost,0,null,group.Sub);
            }
        }
        public void Dispose()
        {
            foreach(var group in groups.Values)if(group.Instanced!=null)UnityEngine.Object.Destroy(group.Instanced);
            foreach(var mesh in ownedMeshes)UnityEngine.Object.Destroy(mesh);
            if(ghost!=null)UnityEngine.Object.Destroy(ghost);
            groups.Clear();templates.Clear();ownedMeshes.Clear();
        }
    }
}
