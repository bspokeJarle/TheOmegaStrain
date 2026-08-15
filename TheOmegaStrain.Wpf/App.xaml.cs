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
            if (SteamManager.RequestRestartThroughSteamIfNecessary(SteamGameConfig.RuntimeAppId))
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }
}
