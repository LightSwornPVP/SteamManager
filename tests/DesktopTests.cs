using System;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerDesktop;
using SteamManagerProtocol;
class DesktopQa {
 static int count;
 static object Get(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(obj);
 static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(obj,args);
 static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(20);while(!task.IsCompleted&&DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();}
 static void Idle(MainForm f){var end=DateTime.UtcNow.AddSeconds(15);do{Application.DoEvents();Thread.Sleep(10);}while((bool)Get(f,"busy")&&DateTime.UtcNow<end);Check(!(bool)Get(f,"busy"),"Desktop request completes");}
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);count++;Console.WriteLine(label);}
 [STAThread]static int Main(string[] args){if(args.Length!=1||args[0]!="--disposable-world"){Console.Error.WriteLine("Requires --disposable-world authorization.");return 1;}Application.EnableVisualStyles();using(var form=new MainForm(false,System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SteamManager-desktop-test-"+Guid.NewGuid().ToString("N")+".json"))){try{
 form.Location=new Point(-20000,-20000);form.StartPosition=FormStartPosition.Manual;form.ShowInTaskbar=false;form.Show();Application.DoEvents();((System.Windows.Forms.Timer)Get(form,"keyRelease")).Stop();
 Pump((Task)Call(form,"Run",new Func<Response>(Client.Connect),true,false,null));Check((bool)Get(form,"connected"),"Desktop connects");
 var browsers=(IDictionary)Get(form,"browsers");var b=browsers["achievements"];Pump((Task)Call(form,"RefreshBrowser",b));var entries=(List<Entry>)Get(b,"Entries");Check(entries.Count>0,"Achievement page populates real Steam entries");var tabs=(TabControl)Get(form,"tabs");tabs.SelectedTab=(TabPage)Get(b,"Page");var list=(ListBox)Get(b,"List");var locked=entries.FindIndex(x=>!x.Unlocked);Check(locked>=0,"Account has a selectable locked achievement");list.SelectedIndex=locked;Application.DoEvents();Check(((Button)Get(form,"unlock")).Enabled,"Unlock button enables for selected locked entry after request");
 using(var img=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(img,new Rectangle(0,0,img.Width,img.Height));img.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"achievement-ui-test.png"));}
 ((TextBox)Get(b,"Search")).Text="__no_such_achievement__";Check(list.Items.Count==0&&!((Button)Get(form,"unlock")).Enabled,"Empty achievement filter disables unlock");((TextBox)Get(b,"Search")).Text="";
 foreach(string browser in new[]{"inventory","buffs","locations"}){b=browsers[browser];Pump((Task)Call(form,"RefreshBrowser",b));Check(Get(b,"Entries")!=null,browser+" browser loads");}
 var prefs=(DesktopPreferences)Get(form,"preferences");prefs.Keys.RemoveAll(x=>x.Id==10);prefs.Keys.Add(new KeyBinding{Id=10,Key=(int)Keys.F9,Modifiers=2,Hold=true});Call(form,"HandleFeatureHotkey",110);Idle(form);Check(Client.Send(new Request{Command="status"}).Features.Single(x=>x.Id==10).Enabled,"Hold binding enables selected feature");
 var timer=(System.Windows.Forms.Timer)Get(form,"keyRelease");typeof(System.Windows.Forms.Timer).GetMethod("OnTick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(timer,new object[]{EventArgs.Empty});Idle(form);Check(!Client.Send(new Request{Command="status"}).Features.Single(x=>x.Id==10).Enabled,"Releasing hold restores previous disabled state");
 Call(form,"HandleFeatureHotkey",110);Idle(form);Pump((Task)Call(form,"Send",new Request{Command="reset"}));Check(((IDictionary)Get(form,"held")).Count==0&&Client.Send(new Request{Command="status"}).Features.All(x=>!x.Enabled),"Disable all clears holds and runtime flags");
 tabs.SelectedIndex=tabs.TabCount-1;Application.DoEvents();using(var img=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(img,new Rectangle(0,0,img.Width,img.Height));img.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-ui-test.png"));}
 Console.WriteLine(count+" desktop checks passed; no achievement unlock invoked.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Call(form,"StopFeatureHotkeys");try{Client.Send(new Request{Command="reset"});}catch{}}}}
}
