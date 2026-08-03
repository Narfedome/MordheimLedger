using MordheimLedgerApp.Features.Warbands;

namespace MordheimLedgerApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(WarbandDetailPage), typeof(WarbandDetailPage));
        }
    }
}
