using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;

namespace MordheimLedgerApp.Features.Library;

/// <summary>
/// Single "Codex" Shell tab hosting the 4 catalog sections (types de bande, Place du Marché,
/// Compétences, Blessures) that used to each be their own top-level TabBar tab - consolidated to
/// declutter the bottom nav bar on Android. Same toggle pattern (index + IsXTab, no real TabbedPage)
/// already used by WarbandDetailPage's Roster/Historique and WarriorEditDialog's Équipement/
/// Compétences/Blessures. Each section keeps its own existing ViewModel/*View ContentView unchanged -
/// this container only owns the toggle and BindingContext wiring, no catalog logic of its own.
/// </summary>
public partial class LibraryViewModel : BaseViewModel
{
    public WarbandArchetypeViewModel WarbandArchetypes { get; }
    public EquipmentItemViewModel EquipmentItems { get; }
    public SkillViewModel Skills { get; }
    public InjuryViewModel Injuries { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarbandsTab))]
    [NotifyPropertyChangedFor(nameof(IsTradingPostTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    private int selectedTab;

    public bool IsWarbandsTab => SelectedTab == 0;
    public bool IsTradingPostTab => SelectedTab == 1;
    public bool IsSkillsTab => SelectedTab == 2;
    public bool IsInjuriesTab => SelectedTab == 3;

    public LibraryViewModel(WarbandArchetypeViewModel warbandArchetypes, EquipmentItemViewModel equipmentItems,
        SkillViewModel skills, InjuryViewModel injuries)
    {
        WarbandArchetypes = warbandArchetypes;
        EquipmentItems = equipmentItems;
        Skills = skills;
        Injuries = injuries;
    }

    [RelayCommand]
    private void ShowWarbandsTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowTradingPostTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowSkillsTab() => SelectedTab = 2;

    [RelayCommand]
    private void ShowInjuriesTab() => SelectedTab = 3;

    /// <summary>All 4 sections load up front (catalogs are tiny, no lazy-load complexity needed) so
    /// switching between them is instant.</summary>
    public async Task InitializeAsync()
    {
        await WarbandArchetypes.InitializeAsync();
        await EquipmentItems.InitializeAsync();
        await Skills.InitializeAsync();
        await Injuries.InitializeAsync();
    }
}
