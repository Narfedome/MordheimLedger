using MordheimLedgerApp.Features.Library.MagicSchools;
using MordheimLedgerApp.Features.Library.RacialProfiles;
using MordheimLedgerApp.Features.Library.Races;
using MordheimLedgerApp.Features.Warbands;

namespace MordheimLedgerApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(WarbandDetailPage), typeof(WarbandDetailPage));
            Routing.RegisterRoute(nameof(MagicSchoolListPage), typeof(MagicSchoolListPage));
            Routing.RegisterRoute(nameof(RaceListPage), typeof(RaceListPage));
            Routing.RegisterRoute(nameof(RacialProfileListPage), typeof(RacialProfileListPage));
        }
    }
}
