using System;
using System.IO;
using SteamManagerDesktop;
class ProbeLauncher
{
    static int Main(string[] args)
    {
        if(args.Length<1||args[0]!="--disposable-world"){Console.Error.WriteLine("Requires --disposable-world authorization.");return 1;}
        string probe=args.Length==2?args[1]:"SteamManager.BehaviorProbe.dll";
        if(Path.GetFileName(probe)!=probe||!probe.StartsWith("SteamManager.BehaviorProbe")||!probe.EndsWith(".dll"))return 1;
        try{MonoConnector.LoadHelper(MonoConnector.FindGame(),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,probe),"SteamManagerRuntimeTests","Entry","Start");Console.WriteLine("Behavior probe loaded; results will be written beside this launcher.");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
