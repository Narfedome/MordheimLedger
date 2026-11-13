using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MordheimLedgerApp.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Captures du Microsoft Store (Deploy/StoreListing/Capture-WindowsScreenshots.ps1) : argument
            // « --mordheim-e2e », pendant Windows de l'extra d'Intent Android, voir Services.E2EMode.
            if (Environment.GetCommandLineArgs().Contains("--" + MordheimLedgerApp.Services.E2EMode.IntentExtra.Replace('_', '-')))
                MordheimLedgerApp.Services.E2EMode.IsEnabled = true;

            // Diagnostic du crash natif intermittent au sélecteur de règles spéciales (voir
            // Services/CrashLogger) - spécifique WinUI : capte les exceptions du dispatcher UI qui
            // échappent à AppDomain.UnhandledException/au débogueur managé. e.Handled=true pour ne PAS
            // changer le comportement (l'app crashe quand même après log) - juste observer, pas masquer.
            this.UnhandledException += (_, e) =>
                MordheimLedgerApp.Services.CrashLogger.LogException("WinUI Application.UnhandledException", e.Exception);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
