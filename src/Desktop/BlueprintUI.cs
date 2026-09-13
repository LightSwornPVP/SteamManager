using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    public sealed partial class MainForm
    {
        void BuildBlueprintTools()
        {
            var page=Browser("blueprint-status","Blueprints");page.Actions.Height=230;page.Detail.Height=65;
            var status=new Label{Dock=DockStyle.Top,Height=65,ForeColor=Muted,Text="Import .blueprint / .vbuild, or capture nearby player-built pieces. Preview is local. Placement creates real pieces visible to other players. First version: up to 500 pieces; no terrain or extra piece data."};
            page.Page.Controls.Add(status);status.SendToBack();
            var name=new TextBox{Width=180,Text="My building",MaxLength=160};
            var radius=new NumericUpDown{Width=65,Minimum=1,Maximum=50,Value=10};
            page.Actions.Controls.Add(new Label{Text="Capture name",AutoSize=true});page.Actions.Controls.Add(name);
            page.Actions.Controls.Add(new Label{Text="Radius (m)",AutoSize=true});page.Actions.Controls.Add(radius);
            var import=Button("Import blueprint",async()=>{
                using(var dialog=new OpenFileDialog{Filter="Blueprints|*.blueprint;*.vbuild",CheckFileExists=true})
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    if(new FileInfo(dialog.FileName).Length>512000)throw new InvalidDataException("Blueprint exceeds 512 KB.");
                    var doc=BlueprintFile.Parse(File.ReadAllText(dialog.FileName),Path.GetExtension(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName));
                    await Run(()=>Client.Send(new Request{Command="blueprint-prepare",Blueprint=doc}),false,false,r=>{status.Text=r.Message;page.Entries=r.Entries??new List<Entry>();page.Filter();});
                }
            });page.Actions.Controls.Add(import);gameControls.Add(import);
            var capture=Button("Capture and save",async()=>{
                BlueprintDocument doc=null;
                var request=new Request{Command="blueprint-capture",Value=(float)radius.Value,Text=name.Text};
                await Run(()=>Client.Send(request),false,false,r=>{doc=r.Blueprint;status.Text=r.Message;});
                if(doc==null)return;
                using(var dialog=new SaveFileDialog{Filter="PlanBuild blueprint|*.blueprint",FileName="building.blueprint",AddExtension=true,DefaultExt="blueprint"})
                {if(dialog.ShowDialog(this)==DialogResult.OK){File.WriteAllText(dialog.FileName,BlueprintFile.Export(doc));status.Text="Saved "+doc.Pieces.Count+" pieces. Import the file to preview and place it.";}}
            });page.Actions.Controls.Add(capture);gameControls.Add(capture);page.Actions.SetFlowBreak(capture,true);
            var axes=new List<NumericUpDown>();
            foreach(string axis in new[]{"X offset","Height","Z offset","Rotation"})
            {page.Actions.Controls.Add(new Label{Text=axis,AutoSize=true});var value=new NumericUpDown{Width=75,Minimum=axis=="Rotation"?-360:-100,Maximum=axis=="Rotation"?360:100,DecimalPlaces=1,Increment=axis=="Rotation"?15:0.5m};page.Actions.Controls.Add(value);axes.Add(value);}
            page.Actions.SetFlowBreak(axes[3],true);
            bool polling=false;
            Action<Response> update=r=>{status.Text=r.Message;page.Entries=r.Entries??new List<Entry>();page.Filter();polling=r.Message!=null&&r.Message.StartsWith("Building:");};
            Action<string,string,bool> add=(label,command,here)=>{
                var button=Button(label,async()=>{
                    if(command=="blueprint-place"&&MessageBox.Show(this,"Place the preview as real building pieces? Materials are consumed unless Free crafting (#36) is enabled. Pieces are visible to other players and saved by the game. Structural support still applies; overlap/terrain checks are limited. Cancel stops future pieces but does not undo those already placed.","Place blueprint",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                    var request=new Request{Command=command,X=(float)axes[0].Value,Y=(float)axes[1].Value,Z=(float)axes[2].Value,Yaw=(float)axes[3].Value,Enabled=here,Confirmed=command=="blueprint-place"};
                    await Run(()=>Client.Send(request),false,false,update);
                });page.Actions.Controls.Add(button);gameControls.Add(button);
            };
            add("Show / update preview","blueprint-preview",false);
            add("Move preview here","blueprint-preview",true);
            add("Place blueprint","blueprint-place",false);
            add("Cancel / clear preview","blueprint-cancel",false);
            var progress=new Timer{Interval=1000};progress.Tick+=async(s,e)=>{if(polling&&connected&&!busy)await Run(()=>Client.Send(new Request{Command="blueprint-status"}),false,true,update);};progress.Start();FormClosed+=(s,e)=>{progress.Stop();progress.Dispose();};
        }
    }
}
