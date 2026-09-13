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
        Check(catalog.Select(x=>x.Id).OrderBy(x=>x).SequenceEqual(Enumerable.Range(1,54)),"Complete feature coverage");
        Check(catalog.All(x=>!x.Enabled),"No gameplay changes enabled by default");
        Check(catalog.Select(x=>x.Id).Distinct().Count()==54,"Unique feature IDs");
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
        Check(response.Ok&&response.Features.Count==54&&response.Protocol==3,"Runtime response round trip");
        Check(!new Request().Confirmed,"Achievement unlock never confirmed by default");
        var profile=Wire.Decode<Request>(Wire.Encode(new Request{Command="profile",Settings=catalog.Where(x=>!x.Action).ToList()}));
        Check(profile.Settings.Count==catalog.Count(x=>!x.Action),"Profile round trip");
        bool oversized=false;try{Wire.Decode<Request>(new string('x',8000001));}catch(System.IO.InvalidDataException){oversized=true;}Check(oversized,"Oversized payload rejected");
        var known=new System.Collections.Generic.HashSet<string>{"Wood"};
        var pickups=new System.Collections.Generic.Dictionary<string,float>{{"Wood",8},{"Stone",0}};
        var other=new System.Collections.Generic.Dictionary<string,float>{{"Other",99}};
        int changed=DiscoveryRecords.Apply(new[]{"Wood","Stone","Copper","Copper",""},known,new[]{pickups,other});
        Check(changed==3,"Discovery updates only distinct valid item types");
        Check(known.SetEquals(new[]{"Wood","Stone","Copper"}),"Discovery marks supplied types known");
        Check(pickups["Wood"]==8&&pickups["Stone"]==1&&pickups["Copper"]==1,"Existing pickup counts preserved and missing records filled");
        Check(other["Other"]==99&&other["Copper"]==1,"Unrelated pickup records preserved");
        Check(DiscoveryRecords.Apply(new[]{"Wood","Stone","Copper"},known,new[]{pickups,other})==0,"Repeated discovery action is idempotent");
        Console.WriteLine(checks+" checks passed.");return 0;
    }
}
