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
            return new Window(page)
            {
                Width = 600,
                Height = 800,
#if !WINDOWS
                MinimumWidth = 600,
                MinimumHeight = 640,
                MaximumWidth = 680,
#endif
            };
        }
    }
}