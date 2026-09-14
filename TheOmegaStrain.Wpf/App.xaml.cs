using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Steam;
using System.Windows;

namespace TheOmegaStrain.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Disabled for local Release runs: without a steam_appid.txt beside the exe
            // (only copied in Debug), this asks Steam to relaunch the app, which triggers
            // the Windows "open steam:// link" prompt. Re-enable for real Steam builds.
            //if (SteamManager.RequestRestartThroughSteamIfNecessary(SteamGameConfig.RuntimeAppId))
            //{
            //    Shutdown();
            //    return;
            //}

            base.OnStartup(e);
        }
    }
}
