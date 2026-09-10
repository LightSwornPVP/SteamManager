using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    [DataContract] public class SavedProfile { [DataMember] public string Name; [DataMember] public List<Feature> Features; public override string ToString()=>Name; }
    [DataContract] public class KeyBinding { [DataMember] public int Id; [DataMember] public int Key; [DataMember] public uint Modifiers; [DataMember] public bool Hold; public override string ToString()=>"#"+Id+"  "+(Keys)Key+"  "+((Modifiers&2)!=0?"Ctrl ":"")+((Modifiers&1)!=0?"Alt ":"")+((Modifiers&4)!=0?"Shift ":"")+(Hold?"(hold)":"(toggle)"); }
    [DataContract] public class DesktopPreferences { [DataMember] public List<Feature> LastSettings; [DataMember] public List<SavedProfile> Profiles=new List<SavedProfile>(); [DataMember] public List<KeyBinding> Keys=new List<KeyBinding>(); }
    public sealed partial class MainForm
    {
        readonly Queue<Request> pendingRequests=new Queue<Request>();
        string lastPlayer;
        sealed class BrowserPage
        {
            public string Command; public TabPage Page; public ListBox List; public Label Detail; public TextBox Search; public FlowLayoutPanel Actions;
            public List<Entry> Entries=new List<Entry>(),Visible=new List<Entry>();
            public Entry Selected=>List.SelectedIndex<0||List.SelectedIndex>=Visible.Count?null:Visible[List.SelectedIndex];
            public void Filter(){string id=Selected?.Id;Visible=Entries.Where(x=>((x.Name??"")+" "+x.Id+" "+x.Description).IndexOf(Search.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();List.Items.Clear();foreach(var e in Visible)List.Items.Add((Command=="achievements"?(e.Unlocked?"✓ Unlocked  ·  ":"Locked  ·  "):Command=="buffs"&&e.Unlocked?"Active  ·  ":"")+e.Name+(Command=="inventory"?"  ×"+e.Value:""));int index=Visible.FindIndex(x=>x.Id==id);if(index>=0)List.SelectedIndex=index;Detail.Text=Selected?.Description??"Select an entry.";}
        }
        readonly Dictionary<string,BrowserPage> browsers=new Dictionary<string,BrowserPage>();
        readonly List<Control> achievementControls=new List<Control>();
        Button unlock;
        BrowserPage Browser(string command,string title)
        {
            var b=new BrowserPage{Command=command,Page=new TabPage(title){BackColor=Background,Padding=new Padding(16)},List=new ListBox{Dock=DockStyle.Fill,BackColor=Card,ForeColor=Color.White,ItemHeight=27},Detail=new Label{Dock=DockStyle.Bottom,Height=100,ForeColor=Muted},Search=new TextBox{Width=390},Actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,AutoScroll=true}};
            var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=46};top.Controls.Add(b.Search);var refresh=Button("Refresh",async()=>await RefreshBrowser(b));top.Controls.Add(refresh);b.Search.TextChanged+=(s,e)=>{b.Filter();UpdateUnlock();};b.List.SelectedIndexChanged+=(s,e)=>{b.Detail.Text=b.Selected?.Description??"Select an entry.";UpdateUnlock();};
            b.Page.Controls.Add(b.List);b.Page.Controls.Add(b.Detail);b.Page.Controls.Add(b.Actions);b.Page.Controls.Add(top);tabs.TabPages.Add(b.Page);browsers[command]=b;
            if(command=="achievements")achievementControls.Add(refresh);else gameControls.Add(refresh);return b;
        }
        Button Button(string text,Func<Task> click){var b=new ReadableButton{Text=text,Width=180,Height=34,Margin=new Padding(3,3,10,3)};StyleButton(b);b.Click+=async(s,e)=>{try{await click();}catch(Exception error){message.Text=error.GetBaseException().Message;}};return b;}
        void ActionButton(BrowserPage b,string text,string command,Func<Request> create=null)
        {
            var button=Button(text,async()=>{if(b.Selected==null&&create==null){message.Text="Select an entry first.";return;}var req=create==null?new Request{Text=b.Selected.Id}:create();req.Command=command;await Send(req);await RefreshBrowser(b);});b.Actions.Controls.Add(button);gameControls.Add(button);
        }
        async Task RefreshBrowser(BrowserPage b){await Run(()=>Client.Send(new Request{Command=b.Command}),false,false,r=>{b.Entries=r.Entries??new List<Entry>();b.Filter();});}
        void BuildAdvanced()
        {
            var inventory=Browser("inventory","Carried inventory");var count=new NumericUpDown{Minimum=1,Maximum=100000,Value=1,Width=90};inventory.Actions.Controls.Add(count);inventory.List.SelectedIndexChanged+=(s,e)=>{if(inventory.Selected!=null){count.Maximum=Math.Max(1,inventory.Selected.Max);count.Value=Math.Max(1,Math.Min(count.Maximum,(decimal)inventory.Selected.Value));}};
            ActionButton(inventory,"Set stack quantity","inventory-edit",()=>new Request{Text=inventory.Selected?.Id,Quantity=(int)count.Value});ActionButton(inventory,"Repair selected","inventory-repair");
            var buffs=Browser("buffs","Status effects");var duration=new NumericUpDown{Minimum=0,Maximum=86400,Width=100};buffs.Actions.Controls.Add(new Label{Text="Seconds (0 = default)",AutoSize=true});buffs.Actions.Controls.Add(duration);ActionButton(buffs,"Apply selected effect","buff-add",()=>new Request{Text=buffs.Selected?.Id,Value=(float)duration.Value});ActionButton(buffs,"Remove selected effect","buff-remove");
            var locations=Browser("locations","Saved locations");var name=new TextBox{Width=240,MaxLength=80};locations.Actions.Controls.Add(name);ActionButton(locations,"Save current location","save-location",()=>new Request{Text=name.Text});ActionButton(locations,"Teleport to selected","teleport");ActionButton(locations,"Delete selected","delete-location");var back=Button("Return to previous",()=>Send(new Request{Command="return"}));locations.Actions.Controls.Add(back);gameControls.Add(back);
            var achievements=Browser("achievements","Steam achievements");unlock=Button("Unlock selected",async()=>{var selected=achievements.Selected;if(selected==null||selected.Unlocked)return;if(MessageBox.Show(this,"Unlock ‘"+selected.Name+"’ on your Steam account? This account change is permanent.","Unlock selected achievement",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;await Run(()=>Client.Send(new Request{Command="unlock-achievement",Text=selected.Id,Confirmed=true}),false,false,r=>{achievements.Entries=r.Entries??achievements.Entries;achievements.Filter();});});achievements.Actions.Controls.Add(unlock);achievementControls.Add(unlock);
        }
        void UpdateUnlock(){if(unlock!=null)unlock.Enabled=connected&&!busy&&browsers["achievements"].Selected!=null&&!browsers["achievements"].Selected.Unlocked;}
        void SetAdvancedEnabled(bool enabled){foreach(var c in achievementControls)c.Enabled=connected&&!busy;UpdateUnlock();}
        async Task OpenAction(int id)
        {
            if(id==31||id==42){await Send(new Request{Command=id==31?"repair":"return"});return;}
            if(id==33||id==35){tabs.SelectedTab=tabs.TabPages[id==33?"items":"skills"];await RefreshEntries(id==33);return;}
            string command=id==18?"buffs":id==32?"inventory":id==41?"locations":"achievements";var browser=browsers[command];tabs.SelectedTab=browser.Page;await RefreshBrowser(browser);
        }
        DesktopPreferences preferences=new DesktopPreferences();
        readonly string preferencesPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamManager","desktop.json");
        readonly Dictionary<int,Request> held=new Dictionary<int,Request>();
        readonly Timer keyRelease=new Timer{Interval=50};
        [DllImport("user32")]static extern short GetAsyncKeyState(int key);
        void SavePreferences(){Directory.CreateDirectory(Path.GetDirectoryName(preferencesPath));string temp=preferencesPath+".tmp";File.WriteAllText(temp,Wire.Encode(preferences));if(File.Exists(preferencesPath))File.Replace(temp,preferencesPath,null);else File.Move(temp,preferencesPath);}
        void BuildSettings()
        {
            try{if(File.Exists(preferencesPath)){preferences=Wire.Decode<DesktopPreferences>(File.ReadAllText(preferencesPath));if(preferences==null||preferences.Keys==null||preferences.Profiles==null)throw new InvalidDataException("Invalid preferences.");}}catch(Exception e){preferences=new DesktopPreferences();message.Text="Could not load saved settings: "+e.Message;}
            var page=new TabPage("Profiles & hotkeys"){BackColor=Background,Padding=new Padding(18)};tabs.TabPages.Add(page);var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};page.Controls.Add(flow);
            flow.Controls.Add(new Label{Text="Changes apply live and save automatically. Your last settings restore on connection.",AutoSize=true});var profiles=new ComboBox{Width=360,DropDownStyle=ComboBoxStyle.DropDownList};flow.Controls.Add(profiles);
            var presets=new List<SavedProfile>();foreach(string title in new[]{"Normal","Builder","Explorer","Recovery"}){var fs=Catalog.Create().Where(x=>!x.Action).ToList();int[] enabled=title=="Builder"?new[]{6,36,38,39}:title=="Explorer"?new[]{3,6,9,43}:title=="Recovery"?new[]{1,3,5,6}:new int[0];foreach(var f in fs)f.Enabled=enabled.Contains(f.Id);presets.Add(new SavedProfile{Name=title,Features=fs});}
            Action fill=()=>{profiles.Items.Clear();profiles.Items.AddRange(presets.Concat(preferences.Profiles).Cast<object>().ToArray());profiles.SelectedIndex=0;};fill();var apply=Button("Apply profile",()=>{var p=profiles.SelectedItem as SavedProfile;return p==null?Task.CompletedTask:Send(new Request{Command="profile",Settings=p.Features});});flow.Controls.Add(apply);gameControls.Add(apply);
            var profileName=new TextBox{Width=360,MaxLength=80};flow.Controls.Add(profileName);flow.Controls.Add(Button("Save current as profile",()=>{string name=profileName.Text.Trim();if(name.Length==0||presets.Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase))){message.Text="Enter a custom profile name.";return Task.CompletedTask;}preferences.Profiles.RemoveAll(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));preferences.Profiles.Add(new SavedProfile{Name=name,Features=Wire.Decode<List<Feature>>(Wire.Encode(state.Where(x=>!x.Action).ToList()))});SavePreferences();fill();message.Text="Profile saved.";return Task.CompletedTask;}));
            flow.Controls.Add(new Label{Text="Feature hotkeys — use Ctrl or Alt. Ctrl+Alt+F12 always disables all.",AutoSize=true,Margin=new Padding(3,24,3,10)});var feature=new ComboBox{Width=420,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var f in state.Where(x=>!x.Action))feature.Items.Add("#"+f.Id+"  "+f.Name);feature.SelectedIndex=0;flow.Controls.Add(feature);
            var input=new TextBox{Width=360,ReadOnly=true};Keys chosen=Keys.None;uint modifiers=0;input.KeyDown+=(s,e)=>{e.SuppressKeyPress=true;chosen=e.KeyCode;modifiers=(uint)((e.Alt?1:0)|(e.Control?2:0)|(e.Shift?4:0));input.Text=e.KeyData.ToString();};flow.Controls.Add(input);var hold=new CheckBox{Text="Hold to enable; release restores previous state",AutoSize=true};flow.Controls.Add(hold);var bindings=new ListBox{Width=530,Height=150,BackColor=Card,ForeColor=Color.White};Action fillKeys=()=>{bindings.Items.Clear();bindings.Items.AddRange(preferences.Keys.Cast<object>().ToArray());};fillKeys();
            flow.Controls.Add(Button("Assign hotkey",()=>{if((modifiers&3)==0||chosen==Keys.None||chosen==Keys.ControlKey||chosen==Keys.Menu||chosen==Keys.ShiftKey){message.Text="Choose Ctrl/Alt plus another key.";return Task.CompletedTask;}int id=state.Where(x=>!x.Action).ElementAt(feature.SelectedIndex).Id;var binding=new KeyBinding{Id=id,Key=(int)chosen,Modifiers=modifiers,Hold=hold.Checked};UnregisterHotKey(Handle,100+id);if(!RegisterHotKey(Handle,100+id,0x4000|modifiers,(uint)chosen)){var old=preferences.Keys.FirstOrDefault(x=>x.Id==id);if(old!=null)RegisterHotKey(Handle,100+id,0x4000|old.Modifiers,(uint)old.Key);message.Text="That hotkey is already in use.";return Task.CompletedTask;}preferences.Keys.RemoveAll(x=>x.Id==id);preferences.Keys.Add(binding);SavePreferences();fillKeys();message.Text="Hotkey saved.";return Task.CompletedTask;}));flow.Controls.Add(bindings);flow.Controls.Add(Button("Remove selected binding",()=>{var key=bindings.SelectedItem as KeyBinding;if(key!=null){UnregisterHotKey(Handle,100+key.Id);preferences.Keys.Remove(key);SavePreferences();fillKeys();}return Task.CompletedTask;}));
            Shown+=(s,e)=>{foreach(var key in preferences.Keys)if(!RegisterHotKey(Handle,100+key.Id,0x4000|key.Modifiers,(uint)key.Key))message.Text="Some saved hotkeys are unavailable.";keyRelease.Start();};keyRelease.Tick+=(s,e)=>{foreach(var id in held.Keys.ToArray()){var key=preferences.Keys.FirstOrDefault(x=>x.Id==id);if(key==null||(GetAsyncKeyState(key.Key)&0x8000)==0||((key.Modifiers&2)!=0&&(GetAsyncKeyState(17)&0x8000)==0)||((key.Modifiers&1)!=0&&(GetAsyncKeyState(18)&0x8000)==0)||((key.Modifiers&4)!=0&&(GetAsyncKeyState(16)&0x8000)==0)){var req=held[id];held.Remove(id);if(connected)_=Send(req);}}};
        }
        void HandleFeatureHotkey(int hotkey){if(!connected||lastPlayer==null)return;int id=hotkey-100;var key=preferences.Keys.FirstOrDefault(x=>x.Id==id);var f=state.Find(x=>x.Id==id);if(key==null||f==null||f.Action||held.ContainsKey(id))return;if(key.Hold)held[id]=new Request{Command="set",Id=id,Enabled=f.Enabled,Value=f.Value};_=Send(new Request{Command="set",Id=id,Enabled=key.Hold||!f.Enabled,Value=f.Value});}
        void StopFeatureHotkeys(){keyRelease.Stop();held.Clear();foreach(var key in preferences.Keys)UnregisterHotKey(Handle,100+key.Id);}
    }
}
