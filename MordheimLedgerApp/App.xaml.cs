using MordheimLedgerApp.Features.Onboarding;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp
{
    public partial class App : Application
    {
        private readonly AppShell _shell;
        private readonly IServiceProvider _services;

        public App(AppShell shell, IServiceProvider services)
        {
            InitializeComponent();
            _shell    = shell;
            _services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            ThemeService.Instance.Initialize();

            // Premier lancement (cf. DmTools App.xaml.cs) : onboarding (langue + palette) plutôt que le
            // Shell directement - "has_launched" est marqué par OnboardingViewModel.Start().
            bool hasLaunched = Preferences.Default.Get("has_launched", false);
            var page = hasLaunched ? _shell : (Page)_services.GetRequiredService<OnboardingPage>();

            // Width/Height ne fixent qu'une taille de lancement (cf. DmTools App.xaml.cs) - librement
            // redimensionnable ensuite. Bornes (Minimum/Maximum) actives seulement sur Mac Catalyst, où
            // sans elles la fenêtre est librement redimensionnable en format très large, ce qui casse
            // une mise en page pensée pour du portrait/tablette. Windows en est exempté (demande
            // explicite dans DmTools, reprise ici) ; ignoré sur Android/iOS (le windowing n'y a pas cours).
            var window = new Window(page)
            {
                // Nom affiché dans la barre de titre Windows, Alt+Tab et la barre des tâches : sans lui,
                // MAUI le laisse vide.
                Title = "Mordheim Ledger",
                Width = 600,
                Height = 800,
#if !WINDOWS
                MinimumWidth = 600,
                MinimumHeight = 640,
                MaximumWidth = 680,
#endif
            };
#if WINDOWS
            // MAUI n'applique pas l'icône de l'appli à la fenêtre WinUI (barre des tâches, Alt+Tab) : on
            // lui donne l'appicon.ico que MauiIcon génère à côté de l'exe, aussi bien pour l'installeur
            // que pour le MSIX.
            window.HandlerChanged += (_, _) =>
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow && File.Exists(iconPath))
                    nativeWindow.AppWindow.SetIcon(iconPath);
            };
#endif
            return window;
        }
    }
}