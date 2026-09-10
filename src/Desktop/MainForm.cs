using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    public sealed partial class MainForm : Form
    {
        static readonly Color Background = Color.FromArgb(19,24,32), Card = Color.FromArgb(29,36,47), Accent = Color.FromArgb(102,220,188), Muted = Color.FromArgb(169,181,196);
        readonly Label connection = new Label(), message = new Label();
        readonly Button connect = new ReadableButton(), reset = new ReadableButton();
        readonly TabControl tabs = new TabControl();
        readonly Dictionary<int,CheckBox> toggles = new Dictionary<int,CheckBox>();
        readonly Dictionary<int,NumericUpDown> values = new Dictionary<int,NumericUpDown>();
        readonly Dictionary<int,Label> hints = new Dictionary<int,Label>();
        readonly HashSet<int> editedNumbers = new HashSet<int>();
        readonly List<Control> gameControls = new List<Control>();
        readonly Timer heartbeat = new Timer { Interval = 3000 };
        readonly ListBox itemList = new ListBox(), skillList = new ListBox();
        readonly TextBox search = new TextBox();
        readonly NumericUpDown quantity = new NumericUpDown(), quality = new NumericUpDown(), skillLevel = new NumericUpDown();
        List<Entry> itemEntries = new List<Entry>(), skillEntries = new List<Entry>();
        bool busy, connected, syncing, closing, resetRequested;
        List<Feature> state = Catalog.Create();
        [DllImport("user32", SetLastError=true)] static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
        [DllImport("user32")] static extern bool UnregisterHotKey(IntPtr hWnd,int id);
        public MainForm(bool automatic=true,string settingsPath=null)
        {
            automaticConnection=automatic;if(settingsPath!=null)preferencesPath=settingsPath;
            Text = "SteamManager • Valheim"; BackColor = Background; ForeColor = Color.White;
            Font = new Font("Segoe UI",10); Size = new Size(1120,880); MinimumSize = new Size(920,650); StartPosition = FormStartPosition.CenterScreen;
            var header = new Panel { Dock = DockStyle.Top, Width=ClientSize.Width, Height = 128, Padding = new Padding(24,18,24,10), BackColor = Card };
            var title = new Label { Text = "STEAMMANAGER", Font = new Font("Segoe UI Semibold",23), AutoSize = true, Location = new Point(24,12), ForeColor = Accent };
            var subtitle = new Label { Text = "VALHEIM  /  DEEP NORTH     •     Full feature preview", AutoSize = true, Location = new Point(27,57), ForeColor = Muted };
            connection.SetBounds(27,87,730,24); connection.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top; connection.Text = "Waiting for Valheim — connects automatically.";
            connect.Text = "Reconnect now"; connect.SetBounds(825,22,225,38); connect.Anchor = AnchorStyles.Top|AnchorStyles.Right; StyleButton(connect); connect.Click += async (s,e) => await Run(() => Client.Connect(),true);
            reset.Text = "Disable all  ·  Ctrl+Alt+F12"; reset.SetBounds(825,69,225,36); reset.Anchor = AnchorStyles.Top|AnchorStyles.Right; StyleButton(reset); reset.Enabled=false; reset.Click += async(s,e)=>await Send(new Request { Command="reset" });
            header.Controls.AddRange(new Control[]{title,subtitle,connection,connect,reset});
            var footer = new Panel { Dock=DockStyle.Bottom, Height=83, Padding=new Padding(24,10,24,8), BackColor=Card };
            message.Dock=DockStyle.Top; message.Height=36; message.Text="Ready. In-game behavior and multiplayer compatibility are still being tested."; message.ForeColor=Accent;
            var note = new Label { Dock=DockStyle.Bottom, Height=23, ForeColor=Muted, Text="No server installation  •  Closing resets toggles  •  Item/skill changes remain in saves" };
            footer.Controls.AddRange(new Control[]{message,note});
            tabs.Dock=DockStyle.Fill; tabs.Padding=new Point(14,8);
            foreach(var group in state.Select(x=>x.Group).Distinct())
            {
                var tab = new TabPage(group) { BackColor=Background, Padding=new Padding(12) };
                var flow = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoScroll=true, FlowDirection=FlowDirection.TopDown, WrapContents=false };
                tab.Controls.Add(flow); tabs.TabPages.Add(tab);
                foreach(var feature in state.Where(x=>x.Group==group)) AddFeature(flow,feature);
                flow.SizeChanged += (s,e)=> { foreach(Control c in flow.Controls) c.Width=Math.Max(700,flow.ClientSize.Width-26); };
            }
            BuildItems(); BuildSkills();BuildAdvanced();BuildSettings();
            tabs.Appearance=TabAppearance.FlatButtons;tabs.SizeMode=TabSizeMode.Fixed;tabs.ItemSize=new Size(0,1);tabs.Multiline=true;
            var navigation=new ListBox{Dock=DockStyle.Left,Width=205,BackColor=Card,ForeColor=Color.White,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=33};
            foreach(TabPage page in tabs.TabPages)navigation.Items.Add(page.Text);
            navigation.DrawItem+=(s,e)=>{if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using(var brush=new SolidBrush(selected?Color.FromArgb(40,76,76):Card))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,navigation.Items[e.Index].ToString(),Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-16,e.Bounds.Height),selected?Accent:Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);};
            navigation.SelectedIndexChanged+=(s,e)=>{if(navigation.SelectedIndex>=0)tabs.SelectedIndex=navigation.SelectedIndex;};
            tabs.SelectedIndexChanged+=(s,e)=>{navigation.SelectedIndex=tabs.SelectedIndex;};navigation.SelectedIndex=0;
            Controls.Add(tabs);Controls.Add(navigation);Controls.Add(footer);Controls.Add(header);
            heartbeat.Interval=2000;heartbeat.Tick += async(s,e)=>await PollConnection();
            Shown+=async(s,e)=>{if(automaticConnection)await PollConnection();};
            heartbeat.Start(); SetEnabled(false);
            FormClosing += OnClosing;
        }
        static void StyleButton(Button b) { b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderColor=Color.FromArgb(64,89,95); b.BackColor=Color.FromArgb(36,61,63); b.ForeColor=Color.White; b.Cursor=Cursors.Hand; }
        void AddFeature(FlowLayoutPanel flow,Feature f)
        {
            var row=new Panel { Width=990, Height=91, BackColor=Card, Margin=new Padding(0,0,0,10) };
            var hint=new Label { Text=f.Hint, ForeColor=Muted, Location=new Point(17,49), Height=35, Width=940, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right }; hints[f.Id]=hint;
            if(f.Action)
            {
                var label=new Label { Text="#"+f.Id+"  "+f.Name, AutoSize=true, Location=new Point(17,17),Font=new Font(Font,FontStyle.Bold) };
                var button=new ReadableButton { Text=f.Id==31?"Repair now":"Open",Location=new Point(814,10),Size=new Size(154,32),Anchor=AnchorStyles.Top|AnchorStyles.Right }; StyleButton(button);gameControls.Add(button);
                button.Click+=async(s,e)=>await OpenAction(f.Id);
                row.Controls.AddRange(new Control[]{label,button});
            }
            else
            {
                var toggle=new ReadableCheckBox { Text="#"+f.Id+"  "+f.Name, AutoSize=true,Location=new Point(17,14),Font=new Font(Font,FontStyle.Bold) }; toggles[f.Id]=toggle;gameControls.Add(toggle);row.Controls.Add(toggle);
                if(f.Numeric)
                {
                    var number=new NumericUpDown { Minimum=(decimal)f.Min,Maximum=(decimal)f.Max,Value=(decimal)f.Value,DecimalPlaces=f.Id==6?0:2,Increment=f.Id==6?100:0.25m,Location=new Point(688,13),Width=122,Anchor=AnchorStyles.Top|AnchorStyles.Right,BackColor=Background,ForeColor=Color.White }; values[f.Id]=number;gameControls.Add(number);
                    number.ValueChanged+=async(s,e)=>{if(!syncing){editedNumbers.Add(f.Id);await Send(new Request{Command="set",Id=f.Id,Enabled=toggle.Checked,Value=(float)number.Value});}};
                    row.Controls.Add(number);
                }
                toggle.CheckedChanged+=async(s,e)=> { if(!syncing)await Send(new Request{Command="set",Id=f.Id,Enabled=toggle.Checked,Value=values.ContainsKey(f.Id)?(float)values[f.Id].Value:0}); };
                var restore=new ReadableButton{Text="Reset",Location=new Point(904,10),Size=new Size(64,32),Anchor=AnchorStyles.Top|AnchorStyles.Right};StyleButton(restore);gameControls.Add(restore);restore.Click+=async(s,e)=>{var original=Catalog.Create().Find(x=>x.Id==f.Id);await Send(new Request{Command="set",Id=f.Id,Enabled=false,Value=original.Value});};
                if(f.Numeric){foreach(Control c in row.Controls){if(c is NumericUpDown)c.Left=649;if(c is Button){c.Left=784;c.Width=108;}}}
                row.Controls.Add(restore);
                if(f.Id==6){var presets=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(474,13),Width=150,Anchor=AnchorStyles.Top|AnchorStyles.Right};presets.Items.AddRange(new object[]{"Carry presets",300,1000,3000,10000});presets.SelectedIndex=0;presets.SelectedIndexChanged+=(s,e)=>{if(presets.SelectedItem is int)values[6].Value=(int)presets.SelectedItem;};row.Controls.Add(presets);}
            }
            if(f.Numeric){row.Height=125;hint.Top=84;foreach(Control c in row.Controls){if(c is NumericUpDown)c.Top=45;else if(c is Button&&c.Text=="Apply value")c.Top=43;else if(c is ComboBox){c.Left=17;c.Top=45;c.Anchor=AnchorStyles.Top|AnchorStyles.Left;}}}
            row.Controls.Add(hint);flow.Controls.Add(row);
        }
        void BuildItems()
        {
            var page=new TabPage("Item browser") { Name="items",BackColor=Background,Padding=new Padding(16) };tabs.TabPages.Add(page);
            var top=new Panel{Dock=DockStyle.Top,Width=990,Height=56};search.SetBounds(0,8,814,28);search.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;
            var find=new ReadableButton{Text="Search items",Location=new Point(834,5),Size=new Size(150,34),Anchor=AnchorStyles.Top|AnchorStyles.Right};StyleButton(find);find.Click+=async(s,e)=>await RefreshEntries(true);gameControls.Add(find);
            search.KeyDown+=async(s,e)=> { if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await RefreshEntries(true);} };top.Controls.AddRange(new Control[]{search,find});
            itemList.Dock=DockStyle.Fill;itemList.BackColor=Card;itemList.ForeColor=Color.White;itemList.Font=new Font("Segoe UI",11);itemList.ItemHeight=26;
            itemList.SelectedIndexChanged+=(s,e)=> { if(itemList.SelectedIndex>=0){quality.Maximum=itemEntries[itemList.SelectedIndex].Max;quality.Value=Math.Min(quality.Value,quality.Maximum);} };
            var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=90,Padding=new Padding(0,12,0,0)};
            bottom.Controls.Add(new Label{Text="Quantity",AutoSize=true,Margin=new Padding(0,5,8,0)});quantity.Minimum=1;quantity.Maximum=1000;quantity.Value=1;quantity.Width=90;bottom.Controls.Add(quantity);
            bottom.Controls.Add(new Label{Text="Quality",AutoSize=true,Margin=new Padding(16,5,8,0)});quality.Minimum=1;quality.Maximum=1;quality.Value=1;quality.Width=80;bottom.Controls.Add(quality);
            var spawn=new ReadableButton{Text="Add selected item",Size=new Size(180,34),Margin=new Padding(20,0,0,0)};StyleButton(spawn);gameControls.Add(spawn);spawn.Click+=async(s,e)=> { int i=itemList.SelectedIndex;if(i<0){message.Text="Select an item first.";return;}await Send(new Request{Command="spawn",Text=itemEntries[i].Id,Quantity=(int)quantity.Value,Quality=(int)quality.Value});};bottom.Controls.Add(spawn);
            page.Controls.Add(itemList);page.Controls.Add(bottom);page.Controls.Add(top);
        }
        void BuildSkills()
        {
            var page=new TabPage("Skill editor") { Name="skills",BackColor=Background,Padding=new Padding(16) };tabs.TabPages.Add(page);
            var refresh=new ReadableButton { Text="Read current skills",Dock=DockStyle.Top,Height=36 };StyleButton(refresh);gameControls.Add(refresh);refresh.Click+=async(s,e)=>await RefreshEntries(false);
            skillList.Dock=DockStyle.Fill;skillList.BackColor=Card;skillList.ForeColor=Color.White;skillList.Font=new Font("Segoe UI",11);
            skillList.SelectedIndexChanged+=(s,e)=>{if(skillList.SelectedIndex>=0)skillLevel.Value=(decimal)Math.Max(0,Math.Min(100,skillEntries[skillList.SelectedIndex].Value));};
            var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=68,Padding=new Padding(0,12,0,0)};bottom.Controls.Add(new Label{Text="Set level",AutoSize=true,Margin=new Padding(0,5,12,0)});skillLevel.Minimum=0;skillLevel.Maximum=100;skillLevel.DecimalPlaces=1;bottom.Controls.Add(skillLevel);
            var apply=new ReadableButton{Text="Set selected skill",Size=new Size(180,34)};StyleButton(apply);gameControls.Add(apply);apply.Click+=async(s,e)=>{int i=skillList.SelectedIndex;if(i<0){message.Text="Select a skill first.";return;}await Send(new Request{Command="skill",Text=skillEntries[i].Id,Value=(float)skillLevel.Value});await RefreshEntries(false);};bottom.Controls.Add(apply);
            page.Controls.Add(skillList);page.Controls.Add(bottom);page.Controls.Add(refresh);
        }
        async Task RefreshEntries(bool items)
        {
            await Run(()=>Client.Send(new Request{Command=items?"items":"skills",Text=items?search.Text:null}),false,false,r=> {
                if(r.Entries==null)return;
                var list=items?itemList:skillList;if(items)itemEntries=r.Entries;else skillEntries=r.Entries;
                list.BeginUpdate();list.Items.Clear();foreach(var entry in r.Entries)list.Items.Add(items?entry.Name+"   ["+entry.Id+"]":entry.Name+"   —   "+entry.Value.ToString("0.0"));list.EndUpdate();
            });
        }
        async Task Send(Request request)
        {
            RememberRequest(request);
            if(request.Command=="profile")held.Clear();
            if(request.Command=="reset"){held.Clear();pendingRequests.Clear();if(busy){resetRequested=true;return;}}
            if(busy){if(request.Command=="set"){var keep=pendingRequests.Where(x=>x.Command!="set"||x.Id!=request.Id).ToArray();pendingRequests.Clear();foreach(var item in keep)pendingRequests.Enqueue(item);}pendingRequests.Enqueue(request);return;}
            if(request.Command=="set")editedNumbers.Remove(request.Id);
            await Run(()=>Client.Send(request));
        }
        async Task Run(Func<Response> action,bool isConnect=false,bool quiet=false,Action<Response> after=null)
        {
            if(busy)return;busy=true;connect.Enabled=false;SetEnabled(false);reset.Enabled=connected;
            if(connected&&lastPlayer!=null){foreach(var pair in toggles)pair.Value.Enabled=state.Find(x=>x.Id==pair.Key).Error==null;foreach(var number in values.Values)number.Enabled=true;}
            if(!quiet)message.Text=isConnect?"Connecting to the running game…":"Applying…";
            try
            {
                var response=await Task.Run(action);
                if(!response.Ok)throw new InvalidOperationException(response.Message);
                bool enteringWorld=response.Player!=null&&(lastPlayer!=response.Player||isConnect||restoreSettings);
                connected=true;
                lastPlayer=response.Player;
                if(enteringWorld&&preferences.LastSettings!=null)
                {
                    var settings=preferences.LastSettings.Where(x=>response.Features!=null&&response.Features.Any(f=>f.Id==x.Id&&f.Error==null)).ToList();
                    var restored=await Task.Run(()=>Client.Send(new Request{Command="profile",Settings=settings}));
                    if(!restored.Ok)throw new InvalidOperationException(restored.Message);
                    response=restored;restoreSettings=false;
                }
                else if(enteringWorld){restoreSettings=false;preferences.LastSettings=CopySettings(response.Features);SaveLiveSettings();}
                if(response.Features!=null)state=response.Features;
                syncing=true;
                foreach(var f in state){if(toggles.ContainsKey(f.Id)&&!pendingRequests.Any(x=>x.Command=="set"&&x.Id==f.Id))toggles[f.Id].Checked=f.Enabled;if(values.ContainsKey(f.Id)&&(!editedNumbers.Contains(f.Id)&&!pendingRequests.Any(x=>x.Command=="set"&&x.Id==f.Id)))values[f.Id].Value=Math.Max(values[f.Id].Minimum,Math.Min(values[f.Id].Maximum,(decimal)f.Value));if(hints.ContainsKey(f.Id))hints[f.Id].Text=f.Error??f.Hint;}
                syncing=false;
                connection.Text="Valheim "+response.Version+"  •  "+(response.Player??"Main menu — enter a world")+(response.Multiplayer?"  •  Multiplayer: behavior unverified":"");
                if(!quiet||settingsSaveError!=null)message.Text=settingsSaveError??response.Message;
                after?.Invoke(response);
                SetEnabled(response.Player!=null);reset.Enabled=true;
            }
            catch(Exception e)
            {
                message.Text=e.GetBaseException().Message;
                if(e is TimeoutException||e is System.IO.IOException||isConnect){connected=false;restoreSettings=true;connection.Text="Waiting to reconnect automatically…";}
                else SetEnabled(connected&&lastPlayer!=null);
                syncing=true;foreach(var f in state)if(toggles.ContainsKey(f.Id)&&!pendingRequests.Any(x=>x.Command=="set"&&x.Id==f.Id))toggles[f.Id].Checked=f.Enabled;syncing=false;
            }
            finally
            {
                Request next=null;
                if(resetRequested&&connected){resetRequested=false;pendingRequests.Clear();next=new Request{Command="reset"};}
                else if(connected&&pendingRequests.Count>0)next=pendingRequests.Dequeue();
                else if(!connected){pendingRequests.Clear();held.Clear();}
                busy=false;
                if(next!=null){if(next.Command=="set")editedNumbers.Remove(next.Id);await Run(()=>Client.Send(next));}
                else{connect.Enabled=true;SetEnabled(connected&&lastPlayer!=null);}
            }
        }
        void SetEnabled(bool enabled)
        {
            foreach(var c in gameControls)c.Enabled=enabled;
            foreach(var f in state)if(f.Error!=null&&toggles.ContainsKey(f.Id))toggles[f.Id].Enabled=false;
            SetAdvancedEnabled(enabled);
        }
        protected override void OnHandleCreated(EventArgs e)
        { base.OnHandleCreated(e);if(!RegisterHotKey(Handle,1,0x4000|0x0002|0x0001,(uint)Keys.F12))message.Text="Disable-all hotkey is already in use; use the button."; }
        protected override void WndProc(ref Message m)
        { if(m.Msg==0x0312&&m.WParam.ToInt32()==1&&connected){_=Send(new Request{Command="reset"});}else if(m.Msg==0x0312)HandleFeatureHotkey(m.WParam.ToInt32());base.WndProc(ref m); }
        async void OnClosing(object sender,FormClosingEventArgs e)
        {
            if(closing)return;
            e.Cancel=true;if(busy){message.Text="Wait for the pending game command before closing.";return;}
            automaticConnection=false;heartbeat.Stop();UnregisterHotKey(Handle,1);
            StopFeatureHotkeys();pendingRequests.Clear();
            if(connected)await Run(()=>Client.Send(new Request{Command="reset"}));
            closing=true;Close();
        }
    }
    sealed class ReadableButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            using(var pen=new Pen(Enabled?Color.FromArgb(102,220,188):Color.FromArgb(65,83,94)))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?Color.White:Color.FromArgb(153,167,181),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8));
        }
    }
    sealed class ReadableCheckBox : CheckBox
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var box=new Rectangle(0,(Height-14)/2,14,14);
            using(var pen=new Pen(Enabled?Color.FromArgb(102,220,188):Color.FromArgb(130,149,166)))e.Graphics.DrawRectangle(pen,box);
            if(Checked)using(var brush=new SolidBrush(Color.FromArgb(102,220,188)))e.Graphics.FillRectangle(brush,box.X+3,box.Y+3,9,9);
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(20,0,Width-20,Height),Enabled?Color.White:Color.FromArgb(157,174,190),TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        }
    }
}
