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
            return new Window(new AppShell());
        }
    }
}