using System;
using System.Linq;
using SteamManagerProtocol;

static class ProtocolTests
{
    static int checks;
    static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++; }
    static void Reject(Action action,string label) { bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,label); }
    static int Main()
    {
        var catalog=Catalog.Create();
        int[] priorities={2,3,4,6,7,8,9,15,16,17,23,25,29,30,31,33,34,35,36,37,39,43,49};
        Check(catalog.Select(x=>x.Id).SequenceEqual(priorities),"Priority coverage and order");
        Check(catalog.All(x=>!x.Enabled),"No gameplay changes enabled by default");
        Check(catalog.Select(x=>x.Id).Distinct().Count()==23,"Unique feature IDs");
        foreach(var f in catalog.Where(x=>x.Numeric))
        {
            Catalog.Validate(f,f.Value);checks++;
            Reject(()=>Catalog.Validate(f,float.NaN),f.Name+" rejects NaN");
            Reject(()=>Catalog.Validate(f,float.PositiveInfinity),f.Name+" rejects infinity");
            Reject(()=>Catalog.Validate(f,f.Min-1),f.Name+" enforces lower bound");
            Reject(()=>Catalog.Validate(f,f.Max+1),f.Name+" enforces upper bound");
        }
        foreach(var f in catalog.Where(x=>x.Action))Reject(()=>Catalog.Validate(f,0),"Action cannot become toggle");
        Reject(()=>Catalog.Validate(null,0),"Unknown feature rejected");
        var request=new Request{Command="spawn",Auth="test",Text="Sword\"\n雪",Quantity=20,Quality=3,Value=2.5f};
        var copy=Wire.Decode<Request>(Wire.Encode(request));
        Check(copy.Text==request.Text&&copy.Quantity==20&&copy.Quality==3&&copy.Auth=="test","Request escaping and Unicode round trip");
        var response=Wire.Decode<Response>(Wire.Encode(new Response{Ok=true,Features=catalog,Message="ready"}));
        Check(response.Ok&&response.Features.Count==23,"Runtime response round trip");
        bool oversized=false;try{Wire.Decode<Request>(new string('x',2000001));}catch(System.IO.InvalidDataException){oversized=true;}Check(oversized,"Oversized payload rejected");
        Console.WriteLine(checks+" checks passed.");return 0;
    }
}
