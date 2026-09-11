using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    public static class Client
    {
        public static int ProcessId;
        public static Response Send(Request request)
        {
            request.Auth = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime.token")).Trim();
            using (var pipe = new NamedPipeClientStream(".", "SteamManager.Valheim.v3." + ProcessId, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                pipe.Connect(1500);
                using (var reader = new StreamReader(pipe))
                using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                {
                    writer.WriteLine(Wire.Encode(request));
                    var read = reader.ReadLineAsync();
                    if (!read.Wait(10000)) throw new TimeoutException("Game response timed out. Check status before repeating an action.");
                    var response=Wire.Decode<Response>(read.Result);
                    if(response.Protocol!=3)throw new InvalidOperationException("Trainer/runtime versions differ. Restart Valheim and use the matching build.");
                    return response;
                }
            }
        }
        public static Response Connect()
        {
            var game = MonoConnector.FindGame(); MonoConnector.Verify(game); ProcessId = game.Id;
            string tokenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime.token");
            if (!File.Exists(tokenPath))
            {
                var acl = new FileSecurity(); acl.SetAccessRuleProtection(true,false);
                acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));
                using(var stream=new FileStream(tokenPath,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.None,acl))
                using(var writer=new StreamWriter(stream))writer.Write(Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"));
            }
            try { return Send(new Request { Command = "status" }); }
            catch (TimeoutException) { }
            catch (IOException) { }
            string attemptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"attach-session.txt");
            string session = game.Id + ":" + game.StartTime.ToUniversalTime().Ticks;
            if (File.Exists(attemptPath) && File.ReadAllText(attemptPath)==session)
                throw new InvalidOperationException("This session already had an attachment attempt. Restart Valheim before retrying; check runtime.log for details.");
            File.WriteAllText(attemptPath,session);
            MonoConnector.Attach(game);
            for (int i=0;i<5;i++)
            {
                try { return Send(new Request { Command = "status" }); }
                catch (TimeoutException) { System.Threading.Thread.Sleep(250); }
            }
            throw new InvalidOperationException("Helper loaded but is not responding. Check runtime.log and restart Valheim before another attempt.");
        }
    }
    static class Program
    {
        [STAThread] static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--render")
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new MainForm(false))
                {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.ShowInTaskbar=false;
                    form.Show();Application.DoEvents();
                    using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}
                }
                return 0;
            }
            if (args.Length > 0)
            {
                try
                {
                    Response result;
                    if (args[0] == "--connect") result = Client.Connect();
                    else
                    {
                        Client.ProcessId = MonoConnector.FindGame().Id;
                        var request = args[0] == "--request" ? Wire.Decode<Request>(File.ReadAllText(args[1])) : new Request { Command = args[0] == "--reset" ? "reset" : "status" };
                        result = Client.Send(request);
                    }
                    string json = Wire.Encode(result); System.Console.WriteLine(json);
                    if (args.Length > 2 && args[args.Length-2] == "--output") File.WriteAllText(args[args.Length-1],json);
                    return result.Ok ? 0 : 1;
                }
                catch (Exception e) { System.Console.Error.WriteLine(e.GetBaseException().Message); return 1; }
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using(var instance=new System.Threading.Mutex(true,"Local\\SteamManager.Desktop.Live",out first))
            {
                if(!first){MessageBox.Show("SteamManager is already running. Use its existing window.","SteamManager");return 0;}
                Application.Run(new MainForm());
            }
            return 0;
        }
    }
}
