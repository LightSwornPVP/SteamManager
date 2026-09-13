using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    public sealed partial class MainForm
    {
        void BuildMapTools()
        {
            var page=Browser("map-results","Map tools");
            page.Actions.Height=145;
            page.Search.Width=260;
            var filters=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList};
            filters.Items.AddRange(new object[]{"All results","Crypt","Cave","Hildir","Haldor","Goblin","Copper","Tin","Silver","Pickable"});filters.SelectedIndex=0;
            filters.SelectedIndexChanged+=(s,e)=>page.Search.Text=filters.SelectedIndex==0?"":filters.Text;
            page.Search.Parent.Controls.Add(filters);
            Action<string,Request> action=(label,req)=>{
                var button=Button(label,async()=>{
                    if(req.Command=="map-reveal"&&MessageBox.Show(this,"Reveal all map fog for this character in this world? Exploration is saved by Valheim and cannot be undone here. This does not reveal every server location.","Reveal map",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                    var request=new Request{Command=req.Command,Text=req.Text,Confirmed=req.Command=="map-reveal"};
                    if(req.Command=="map-pin"){if(page.Selected==null){message.Text="Select a scan result first.";return;}request.Text=page.Selected.Id;}
                    if(req.Command=="map-pin-filter")request.Text=page.Search.Text;
                    await Run(()=>Client.Send(request),false,false,r=>{page.Entries=r.Entries??new List<Entry>();page.Filter();});
                });page.Actions.Controls.Add(button);gameControls.Add(button);
            };
            action("Scan world locations",new Request{Command="map-scan",Text="locations"});
            action("Scan locations: 200 m",new Request{Command="map-scan",Text="nearby"});
            action("Scan resources: 200 m",new Request{Command="map-scan",Text="resources"});
            action("Pin selected",new Request{Command="map-pin"});
            action("Pin matching names",new Request{Command="map-pin-filter"});
            action("Remove my pins",new Request{Command="map-clear"});
            action("Reveal entire map",new Request{Command="map-reveal"});
            foreach(bool copy in new[]{false,true})
            {
                var button=Button(copy?"Copy seed":"Show seed",async()=>{
                    await Run(()=>Client.Send(new Request{Command="map-seed"}),false,false,r=>{
                        if(string.IsNullOrEmpty(r.WorldSeed))throw new InvalidOperationException("This helper does not provide the seed. Restart Valheim after updating SteamManager.");
                        if(copy){Clipboard.SetText(r.WorldSeed);message.Text="Current world seed copied to clipboard.";}
                        else MessageBox.Show(this,"World: "+r.WorldName+Environment.NewLine+Environment.NewLine+"Seed: "+r.WorldSeed,"Current world seed",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    });
                });page.Actions.Controls.Add(button);gameControls.Add(button);
            }
            page.Detail.Text="Scan, then search by prefab name (for example Crypt, Cave, or Copper). World scans use local world records or server-provided markers. Resource scans cover loaded objects within 200 m. Pins last for this game session.";
        }
    }
}
