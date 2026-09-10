using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerDesktop;
using SteamManagerProtocol;

class LiveDesktopTests
{
    static int checks;
    static object Get(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine(label);}
    static void Wait(Func<bool> condition){var deadline=DateTime.UtcNow.AddSeconds(25);while(!condition()&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}if(!condition())throw new Exception("UI operation timed out.");}
    static void Idle(MainForm form)=>Wait(()=>!(bool)Get(form,"busy")&&((Queue<Request>)Get(form,"pendingRequests")).Count==0);
    static Feature Current(int id)=>Client.Send(new Request{Command="status"}).Features.Single(x=>x.Id==id);
    static DesktopPreferences Saved(string path)=>Wire.Decode<DesktopPreferences>(File.ReadAllText(path));
    static MainForm Open(string path){var form=new MainForm(true,path);form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();Wait(()=>(bool)Get(form,"connected")&&!(bool)Get(form,"busy"));return form;}
    [STAThread]static int Main(string[] args)
    {
        if(args.Length!=1||args[0]!="--disposable-world")return 1;
        Application.EnableVisualStyles();var baseline=Client.Connect();if(baseline.Player==null||baseline.Multiplayer)throw new Exception("Disposable single-player world required.");
        string path=Path.Combine(Path.GetTempPath(),"SteamManager-live-tests-"+Guid.NewGuid().ToString("N")+".json");MainForm form=null;
        try
        {
            form=Open(path);Check((bool)Get(form,"connected"),"Connects automatically on launch without clicking Connect");
            var values=(Dictionary<int,NumericUpDown>)Get(form,"values");var toggles=(Dictionary<int,CheckBox>)Get(form,"toggles");
            values[6].Value=2468;Idle(form);toggles[6].Checked=true;Idle(form);
            Check(Current(6).Enabled&&Current(6).Value==2468,"Control edits apply live without an Apply button");
            Check(Saved(path).LastSettings.Single(x=>x.Id==6).Enabled&&Saved(path).LastSettings.Single(x=>x.Id==6).Value==2468,"Enabled state and numeric value saved immediately");
            values[6].Value=2700;values[6].Value=3000;values[6].Value=3333;Idle(form);
            Check(Current(6).Value==3333&&Saved(path).LastSettings.Single(x=>x.Id==6).Value==3333,"Rapid edits coalesce to latest runtime and saved value");
            Client.Send(new Request{Command="reset"});Set(form,"connected",false);var reconnect=(Task)Call(form,"PollConnection");Wait(()=>reconnect.IsCompleted);Idle(form);
            Check(Current(6).Enabled&&Current(6).Value==3333,"Automatic reconnection restores saved controls");
            var prefs=(DesktopPreferences)Get(form,"preferences");prefs.Keys.RemoveAll(x=>x.Id==10);prefs.Keys.Add(new KeyBinding{Id=10,Key=(int)Keys.F9,Modifiers=2,Hold=true});
            var off=(Task)Call(form,"Send",new Request{Command="set",Id=10,Enabled=false,Value=1});Wait(()=>off.IsCompleted);Idle(form);
            ((System.Windows.Forms.Timer)Get(form,"keyRelease")).Stop();Call(form,"HandleFeatureHotkey",110);Idle(form);
            Check(Current(10).Enabled&&!Saved(path).LastSettings.Single(x=>x.Id==10).Enabled,"Held hotkey does not save temporary enabled state");
            form.Close();Wait(()=>form.IsDisposed);form=null;
            Check(!Current(6).Enabled&&Saved(path).LastSettings.Single(x=>x.Id==6).Enabled,"Closing resets runtime but preserves saved controls");
            form=Open(path);Check(Current(6).Enabled&&Current(6).Value==3333&&!Current(10).Enabled,"Reopening automatically restores saved values and excludes held toggle");
            var reset=(Task)Call(form,"Send",new Request{Command="reset"});Wait(()=>reset.IsCompleted);Idle(form);
            Check(Saved(path).LastSettings.All(x=>!x.Enabled),"Disable all persists disabled states");
            form.Close();Wait(()=>form.IsDisposed);form=null;form=Open(path);
            Check(Client.Send(new Request{Command="status"}).Features.All(x=>!x.Enabled),"Disabled settings remain off after reopening");
            Console.WriteLine(checks+" live desktop checks passed.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally
        {
            if(form!=null){Idle(form);form.Close();Wait(()=>form.IsDisposed);}
            Client.Send(new Request{Command="profile",Settings=baseline.Features.Where(x=>!x.Action).ToList()});
            if(File.Exists(path))File.Delete(path);
        }
    }
}
