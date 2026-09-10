using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SteamManagerProtocol;

namespace SteamManagerDesktop
{
    public sealed partial class MainForm
    {
        bool automaticConnection,restoreSettings=true;
        string settingsSaveError;

        static List<Feature> CopySettings(IEnumerable<Feature> source)
        {
            return (source??Catalog.Create()).Where(x=>!x.Action).Select(x=>new Feature{Id=x.Id,Enabled=x.Enabled,Value=x.Value}).ToList();
        }

        void SaveLiveSettings()
        {
            try{SavePreferences();settingsSaveError=null;}
            catch(Exception error){settingsSaveError="Settings could not be saved: "+error.GetBaseException().Message;message.Text=settingsSaveError;}
        }

        void RememberRequest(Request request)
        {
            if(request.Command!="set"&&request.Command!="profile"&&request.Command!="reset")return;
            // Held hotkeys are temporary, even if the app exits before key release.
            if(request.Command=="set"&&held.ContainsKey(request.Id))return;
            if(preferences.LastSettings==null)preferences.LastSettings=CopySettings(state);
            if(request.Command=="reset")foreach(var f in preferences.LastSettings)f.Enabled=false;
            else if(request.Command=="profile")preferences.LastSettings=CopySettings(request.Settings);
            else
            {
                var definition=Catalog.Create().Find(x=>x.Id==request.Id);
                if(definition==null||definition.Action)return;
                Catalog.Validate(definition,request.Value);
                var target=preferences.LastSettings.Find(x=>x.Id==request.Id);
                if(target==null){target=new Feature{Id=request.Id};preferences.LastSettings.Add(target);}
                target.Enabled=request.Enabled;target.Value=request.Value;
            }
            SaveLiveSettings();
        }

        async Task PollConnection()
        {
            if(!automaticConnection||busy||closing)return;
            var games=Process.GetProcessesByName("valheim");
            try
            {
                if(games.Length==0)
                {
                    connected=false;restoreSettings=true;lastPlayer=null;SetEnabled(false);reset.Enabled=false;
                    connection.Text="Waiting for Valheim — your settings are saved.";return;
                }
                if(!connected||games.Length!=1||games[0].Id!=Client.ProcessId)
                    await Run(()=>Client.Connect(),true,true);
                else await Run(()=>Client.Send(new Request{Command="status"}),false,true);
            }
            finally{foreach(var game in games)game.Dispose();}
        }
    }
}
