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
    }
    [DataContract] public class BlueprintDocument
    {
        [DataMember] public string Name;
        [DataMember] public List<BlueprintPiece> Pieces=new List<BlueprintPiece>();
        [DataMember] public List<string> Warnings=new List<string>();
    }
    public static class BlueprintFile
    {
        public const int MaxPieces=500;
        static float Number(string text)
        {
            float value;
            if(string.IsNullOrEmpty(text))return 0;
            if(!float.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Invalid number in blueprint.");
            return value;
        }
        public static BlueprintDocument Parse(string text,string extension,string name)
        {
            if(text==null||text.Length>512000)throw new InvalidDataException("Blueprint exceeds 512 KB.");
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
                        if(p.Length>10&&(p.Length<13||Math.Abs(Number(p[10])-1)>0.001||Math.Abs(Number(p[11])-1)>0.001||Math.Abs(Number(p[12])-1)>0.001))throw new InvalidDataException("Scaled pieces are not supported; export at normal scale.");
                    }
                    doc.Pieces.Add(piece);
                    if(doc.Pieces.Count>MaxPieces)throw new InvalidDataException("Maximum 500 pieces per blueprint.");
                }
                catch(Exception e){throw new InvalidDataException("Line "+lineNumber+": "+e.Message);}
            }
            Validate(doc);return doc;
        }
        static void Warn(BlueprintDocument doc,string value){if(!doc.Warnings.Contains(value))doc.Warnings.Add(value);}
        public static void Validate(BlueprintDocument doc)
        {
            if(doc==null||doc.Pieces==null||doc.Pieces.Count==0||doc.Pieces.Count>MaxPieces)throw new InvalidDataException("Blueprint must contain 1–500 pieces.");
            if(string.IsNullOrWhiteSpace(doc.Name)||doc.Name.Length>160||doc.Name.Any(char.IsControl))throw new InvalidDataException("Blueprint name must be 1–160 printable characters.");
            foreach(var p in doc.Pieces)
            {
                if(p==null||string.IsNullOrWhiteSpace(p.Prefab)||p.Prefab.Length>120||p.Prefab.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'&&c!='.'))throw new InvalidDataException("Invalid prefab name.");
                foreach(float f in new[]{p.X,p.Y,p.Z,p.Qx,p.Qy,p.Qz,p.Qw})if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Non-finite blueprint transform.");
                if(Math.Abs(p.X)>200||Math.Abs(p.Y)>200||Math.Abs(p.Z)>200)throw new InvalidDataException("Pieces must be within 200 m of the blueprint origin.");
                double norm=Math.Sqrt((double)p.Qx*p.Qx+(double)p.Qy*p.Qy+(double)p.Qz*p.Qz+(double)p.Qw*p.Qw);
                if(norm<0.000001)throw new InvalidDataException("Piece has an invalid zero rotation.");
                p.Qx=(float)(p.Qx/norm);p.Qy=(float)(p.Qy/norm);p.Qz=(float)(p.Qz/norm);p.Qw=(float)(p.Qw/norm);
            }
        }
        public static string Export(BlueprintDocument doc)
        {
            Validate(doc);var text=new StringBuilder("#Name:"+doc.Name+"\n#Creator:SteamManager\n#Description:\"Vanilla pieces only\"\n#Category:Building\n#Pieces\n");
            foreach(var p in doc.Pieces){text.Append(p.Prefab).Append(";Building;");text.Append(string.Join(";",new[]{p.X,p.Y,p.Z,p.Qx,p.Qy,p.Qz,p.Qw}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture))));text.Append(";null;1;1;1\n");}return text.ToString();
        }
    }
}
