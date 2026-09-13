using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SteamManagerProtocol;

class BlueprintFileTests
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Reject(Action action,string label){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception(label);}
    static BlueprintDocument Parse(string line)=>BlueprintFile.Parse(line,".blueprint","Test");
    const string Piece="wood_floor;Building;1;2;3;0;0;0;1;null;1;1;1";
    static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=new CultureInfo("de-DE");
        var plan=Parse("#Name:Cabin\n#Terrain\n1;2;3\n#SnapPoints\n0;0;0\n#Pieces\n"+Piece);
        Check(plan.Name=="Cabin"&&plan.Pieces.Count==1,"Sections and metadata parsed");
        Check(plan.Warnings.Count==2,"Unsupported terrain and snap data reported");
        Check(Parse("#Description\nLegacy multiline description\n#Pieces\n"+Piece).Pieces.Count==1,"Legacy multiline description is not treated as geometry");
        var exported=BlueprintFile.Export(plan);var round=Parse(exported);
        Check(round.Pieces[0].X==1&&round.Pieces[0].Y==2&&round.Pieces[0].Qw==1,"PlanBuild export round trip");
        var vbuild=BlueprintFile.Parse("wood_beam_45  -.55557  -.83147 -20.08298 1.177017 31.44012",".vbuild","Legacy");
        Check(Math.Abs(vbuild.Pieces[0].X+20.08298)<0.0001&&vbuild.Pieces[0].Qx==0&&vbuild.Pieces[0].Qz==0,"Legacy empty vbuild fields retain zero positions");
        Check(Math.Abs(BlueprintFile.Parse("wood_floor 0 0 0 1 1,5 2 3",".VBUILD","Decimal").Pieces[0].X-1.5)<0.001,"Legacy decimal comma accepted independent of culture");
        Check(Parse(Piece.Replace(";null;",";\"saved sign text\";")).Warnings.Count==1,"Ignored per-piece data reported");
        var scaled=Parse(Piece.Replace(";1;1;1",";2;1;1"));Check(scaled.Pieces[0].Sx==2&&scaled.PreviewOnly,"Scale preserved in preview-only imports");
        Reject(()=>BlueprintFile.ValidatePlacement(scaled),"Scaled preview cannot be placed");
        scaled.PreviewOnly=false;Reject(()=>BlueprintFile.ValidatePlacement(scaled),"Scaled placement blocked even if preview flag is removed");
        Reject(()=>Parse(Piece.Replace(";1;2;3",";NaN;2;3")),"NaN rejected");
        Reject(()=>Parse(Piece.Replace(";1;2;3",";Infinity;2;3")),"Infinity rejected");
        Reject(()=>Parse(Piece.Replace(";1;2;3",";201;2;3")),"Oversized extent rejected");
        Reject(()=>Parse(Piece.Replace(";0;0;0;1;",";0;0;0;0;")),"Zero quaternion rejected");
        Reject(()=>Parse("wood_floor;Building;0"),"Incomplete record rejected");
        Reject(()=>Parse(string.Join("\n",Enumerable.Repeat(Piece,BlueprintFile.MaxPieces+1))),"Piece cap enforced");
        Reject(()=>Parse(new string('x',BlueprintFile.MaxFileBytes+1)),"Input size cap enforced");
        Reject(()=>Parse("#Name:Empty"),"Empty blueprint rejected");
        Reject(()=>Parse(Piece.Replace("wood_floor","../wood_floor")),"Malformed prefab rejected");
        var request=Wire.Decode<Request>(Wire.Encode(new Request{Command="blueprint-prepare",Blueprint=round,X=1,Y=2,Z=3,Yaw=90}));
        Check(request.Blueprint.Pieces.Count==1&&request.Yaw==90,"Blueprint wire request round trip");
        var response=Wire.Decode<Response>(Wire.Encode(new Response{Blueprint=round}));
        Check(response.Blueprint.Name=="Cabin","Capture response round trip");
        var invalid=Parse(Piece);invalid.Pieces[0].X=float.NaN;Reject(()=>BlueprintFile.Validate(invalid),"Runtime validates untrusted wire transforms");
        var large=Parse(string.Join("\n",Enumerable.Repeat(Piece,501)));Check(large.PreviewOnly,"Large imports automatically lock placement");
        large.PreviewOnly=false;Reject(()=>BlueprintFile.ValidatePlacement(large),"Runtime size limit cannot be bypassed with flag");
        var locked=Parse(Piece);locked.PreviewOnly=true;Reject(()=>BlueprintFile.ValidatePlacement(locked),"Explicit preview-only flag blocks small blueprints too");
        Reject(()=>Parse(Piece.Replace(";1;1;1",";0;1;1")),"Zero scale rejected");
        Check(Parse(BlueprintFile.Export(scaled)).Pieces[0].Sx==2,"Scaled export round trip");
        if(args.Length>0){var castle=BlueprintFile.Parse(File.ReadAllText(args[0]),".blueprint",Path.GetFileNameWithoutExtension(args[0]));Check(castle.Pieces.Count==8557,"User castle contains 8557 records");Check(castle.Pieces.Count(BlueprintFile.Scaled)==9,"All nine scaled records preserved");Check(castle.PreviewOnly,"Castle is locked to preview");Reject(()=>BlueprintFile.ValidatePlacement(castle),"Castle cannot be placed");var json=Wire.Encode(new Request{Command="blueprint-prepare",Blueprint=castle});Check(Wire.Decode<Request>(json).Blueprint.Pieces.Count==8557,"Whole castle fits and round trips through IPC");Console.WriteLine("Castle wire characters: "+json.Length);}
        Console.WriteLine(checks+" blueprint format checks passed.");return 0;
    }
}
