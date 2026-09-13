using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace SteamManagerProtocol
{
    [DataContract] public class BlueprintPiece
    {
        [DataMember] public string Prefab;
        [DataMember] public float X,Y,Z,Qx,Qy,Qz,Qw;
        [DataMember] public float Sx=1,Sy=1,Sz=1;
        [OnDeserializing] void Defaults(StreamingContext context){Sx=Sy=Sz=1;}
    }
    [DataContract] public class BlueprintDocument
    {
        [DataMember] public string Name;
        [DataMember] public bool PreviewOnly;
        [DataMember] public List<BlueprintPiece> Pieces=new List<BlueprintPiece>();
        [DataMember] public List<string> Warnings=new List<string>();
    }
    public static class BlueprintFile
    {
        public const int MaxPieces=12000,MaxPlacementPieces=500,MaxFileBytes=2000000;
        static float Number(string text)
        {
            float value;
            if(string.IsNullOrEmpty(text))return 0;
            if(!float.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Invalid number in blueprint.");
            return value;
        }
        public static BlueprintDocument Parse(string text,string extension,string name)
        {
            if(text==null||text.Length>MaxFileBytes)throw new InvalidDataException("Blueprint exceeds 2 MB.");
            bool vbuild=string.Equals(extension,".vbuild",StringComparison.OrdinalIgnoreCase);
            if(!vbuild&&!string.Equals(extension,".blueprint",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Choose a .blueprint or .vbuild file.");
            var doc=new BlueprintDocument{Name=name};string section="pieces";int lineNumber=0;
            foreach(var raw in text.Split('\n'))
            {
                lineNumber++;string line=raw.TrimEnd('\r').TrimStart('\uFEFF');
                if(string.IsNullOrWhiteSpace(line))continue;
                if(line.StartsWith("#Name:")){doc.Name=line.Substring(6);continue;}
                if(line=="#Pieces"){section="pieces";continue;}
                if(line=="#Terrain"){section="terrain";continue;}
                if(line=="#SnapPoints"){section="snaps";continue;}
                if(line=="#Description"){section="description";continue;}
                if(line.StartsWith("#"))continue;
                if(section=="description")continue;
                if(section!="pieces"){Warn(doc,section=="terrain"?"Terrain modifications are not imported.":"Custom snap markers are not imported.");continue;}
                try
                {
                    string[] p=vbuild?line.Split(' '):line.Split(';');
                    // Empty vbuild numeric fields are zero in the original format.
                    if(p.Length<(vbuild?8:10))throw new InvalidDataException("Incomplete piece record.");
                    int pos=vbuild?5:2,rot=vbuild?1:5;
                    var piece=new BlueprintPiece{Prefab=p[0].Split('(')[0],X=Number(p[pos]),Y=Number(p[pos+1]),Z=Number(p[pos+2]),Qx=Number(p[rot]),Qy=Number(p[rot+1]),Qz=Number(p[rot+2]),Qw=Number(p[rot+3])};
                    if(!vbuild)
                    {
                        if(p[9]!=""&&p[9]!="null"&&p[9]!="\"\"")Warn(doc,"Extra piece data (signs, containers, item stands, etc.) is not imported.");
                        if(p.Length>10){if(p.Length<13)throw new InvalidDataException("Incomplete scale.");piece.Sx=Number(p[10]);piece.Sy=Number(p[11]);piece.Sz=Number(p[12]);}
                    }
                    doc.Pieces.Add(piece);
                    if(doc.Pieces.Count>MaxPieces)throw new InvalidDataException("Maximum 12,000 objects per preview.");
                }
                catch(Exception e){throw new InvalidDataException("Line "+lineNumber+": "+e.Message);}
            }
            Validate(doc);if(doc.Pieces.Count>MaxPlacementPieces||doc.Pieces.Any(Scaled)){doc.PreviewOnly=true;Warn(doc,"Preview only: large or scaled blueprints cannot be placed.");}return doc;
        }
        static void Warn(BlueprintDocument doc,string value){if(!doc.Warnings.Contains(value))doc.Warnings.Add(value);}
        public static void Validate(BlueprintDocument doc)
        {
            if(doc==null||doc.Pieces==null||doc.Pieces.Count==0||doc.Pieces.Count>MaxPieces)throw new InvalidDataException("Blueprint must contain 1–12,000 objects.");
            if(string.IsNullOrWhiteSpace(doc.Name)||doc.Name.Length>160||doc.Name.Any(char.IsControl))throw new InvalidDataException("Blueprint name must be 1–160 printable characters.");
            foreach(var p in doc.Pieces)
            {
                if(p==null||string.IsNullOrWhiteSpace(p.Prefab)||p.Prefab.Length>120||p.Prefab.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'&&c!='.'))throw new InvalidDataException("Invalid prefab name.");
                foreach(float f in new[]{p.X,p.Y,p.Z,p.Qx,p.Qy,p.Qz,p.Qw,p.Sx,p.Sy,p.Sz})if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Non-finite blueprint transform.");
                foreach(float f in new[]{p.Sx,p.Sy,p.Sz})if(f<=0||f>20)throw new InvalidDataException("Scale must be greater than zero and at most 20.");
                if(Math.Abs(p.X)>200||Math.Abs(p.Y)>200||Math.Abs(p.Z)>200)throw new InvalidDataException("Pieces must be within 200 m of the blueprint origin.");
                double norm=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);
                if(norm<0.000001)throw new InvalidDataException("Piece has an invalid zero rotation.");
                p.Qx=(float)(p.Qx/norm);p.Qy=(float)(p.Qy/norm);p.Qz=(float)(p.Qz/norm);p.Qw=(float)(p.Qw/norm);
            }
        }
        public static bool Scaled(BlueprintPiece p)=>Math.Abs(p.Sx-1)>0.001||Math.Abs(p.Sy-1)>0.001||Math.Abs(p.Sz-1)>0.001;
        public static void ValidatePlacement(BlueprintDocument doc)
        {
            Validate(doc);
            if(doc.PreviewOnly||doc.Pieces.Count>MaxPlacementPieces||doc.Pieces.Any(Scaled))throw new InvalidDataException("This blueprint is preview-only. Placement is blocked.");
        }
        public static string Export(BlueprintDocument doc)
        {
            Validate(doc);var text=new StringBuilder("#Name:"+doc.Name+"\n#Creator:SteamManager\n#Description:\"Vanilla pieces only\"\n#Category:Building\n#Pieces\n");
            foreach(var p in doc.Pieces){text.Append(p.Prefab).Append(";Building;");text.Append(string.Join(";",new[]{p.X,p.Y,p.Z,p.Qx,p.Qy,p.Qz,p.Qw}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));text.Append(";null;").Append(string.Join(";",new[]{p.Sx,p.Sy,p.Sz}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)))).Append('\n');}return text.ToString();
        }
    }
}
