using MordheimLedgerApp.Features.Library.EquipmentLists;
using MordheimLedgerApp.Features.Library.MagicSchools;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Features.Warbands;

namespace MordheimLedgerApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(WarbandDetailPage), typeof(WarbandDetailPage));
            Routing.RegisterRoute(nameof(WarriorArchetypeListPage), typeof(WarriorArchetypeListPage));
            Routing.RegisterRoute(nameof(MagicSchoolListPage), typeof(MagicSchoolListPage));
            Routing.RegisterRoute(nameof(EquipmentListListPage), typeof(EquipmentListListPage));
        }
    }
}
