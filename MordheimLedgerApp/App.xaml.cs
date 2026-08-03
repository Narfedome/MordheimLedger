using MordheimLedgerApp.Services;

namespace MordheimLedgerApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            ThemeService.Instance.Initialize();

            // Width/Height ne fixent qu'une taille de lancement (cf. DmTools App.xaml.cs) - librement
            // redimensionnable ensuite. Bornes (Minimum/Maximum) actives seulement sur Mac Catalyst, où
            // sans elles la fenêtre est librement redimensionnable en format très large, ce qui casse
            // une mise en page pensée pour du portrait/tablette. Windows en est exempté (demande
            // explicite dans DmTools, reprise ici) ; ignoré sur Android/iOS (le windowing n'y a pas cours).
            return new Window(new AppShell())
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