using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.Spells;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Features.Library.Mounts;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;

namespace MordheimLedgerApp.Features.Library;

/// <summary>
/// Single "Codex" Shell tab hosting the 8 catalog sections (types de bande, Place du Marché,
/// Compétences, Blessures, Sorts, Règles spéciales, Mutations, Montures) that used to each be their own
/// top-level TabBar tab - consolidated to declutter the bottom nav bar on Android. Same toggle pattern
/// (index + IsXTab, no real TabbedPage) already used by WarbandDetailPage's Roster/Historique and
/// WarriorEditDialog's Équipement/Compétences/Blessures. Each section keeps its own existing
/// ViewModel/*View ContentView unchanged - this container only owns the toggle and BindingContext
/// wiring, no catalog logic of its own.
///
/// MagicSchool is deliberately NOT one of these tabs, unlike the other 8 catalogs - too nested a
/// concept for a top-level Codex entry (same call as DmTools' Category, managed from within the Track
/// library rather than its own TabBar tab). It's reached instead via SpellViewModel.ManageMagicSchools
/// (see SpellView.xaml's "Gérer les écoles de magie" icon), which pushes MagicSchoolListPage as a
/// route - see AppShell.xaml.cs.
/// </summary>
public partial class LibraryViewModel : BaseViewModel
{
    public WarbandArchetypeViewModel WarbandArchetypes { get; }
    public EquipmentItemViewModel EquipmentItems { get; }
    public SkillViewModel Skills { get; }
    public InjuryViewModel Injuries { get; }
    public SpellViewModel Spells { get; }
    public SpecialRuleViewModel SpecialRules { get; }
    public MutationViewModel Mutations { get; }
    public MountViewModel Mounts { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarbandsTab))]
    [NotifyPropertyChangedFor(nameof(IsTradingPostTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    [NotifyPropertyChangedFor(nameof(IsSpellsTab))]
    [NotifyPropertyChangedFor(nameof(IsSpecialRulesTab))]
    [NotifyPropertyChangedFor(nameof(IsMutationsTab))]
    [NotifyPropertyChangedFor(nameof(IsMountsTab))]
    private int selectedTab;

    public bool IsWarbandsTab => SelectedTab == 0;
    public bool IsTradingPostTab => SelectedTab == 1;
    public bool IsSkillsTab => SelectedTab == 2;
    public bool IsInjuriesTab => SelectedTab == 3;
    public bool IsSpellsTab => SelectedTab == 4;
    public bool IsSpecialRulesTab => SelectedTab == 5;
    public bool IsMutationsTab => SelectedTab == 6;
    public bool IsMountsTab => SelectedTab == 7;

    public LibraryViewModel(WarbandArchetypeViewModel warbandArchetypes, EquipmentItemViewModel equipmentItems,
        SkillViewModel skills, InjuryViewModel injuries, SpellViewModel spells, SpecialRuleViewModel specialRules,
        MutationViewModel mutations, MountViewModel mounts)
    {
        WarbandArchetypes = warbandArchetypes;
        EquipmentItems = equipmentItems;
        Skills = skills;
        Injuries = injuries;
        Spells = spells;
        SpecialRules = specialRules;
        Mutations = mutations;
        Mounts = mounts;
    }

    [RelayCommand]
    private void ShowWarbandsTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowTradingPostTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowSkillsTab() => SelectedTab = 2;

    [RelayCommand]
    private void ShowInjuriesTab() => SelectedTab = 3;

    [RelayCommand]
    private void ShowSpellsTab() => SelectedTab = 4;

    [RelayCommand]
    private void ShowSpecialRulesTab() => SelectedTab = 5;

    [RelayCommand]
    private void ShowMutationsTab() => SelectedTab = 6;

    [RelayCommand]
    private void ShowMountsTab() => SelectedTab = 7;

    /// <summary>All 8 sections load up front (catalogs are tiny, no lazy-load complexity needed) so
    /// switching between them is instant.</summary>
    public async Task InitializeAsync()
    {
        await WarbandArchetypes.InitializeAsync();
        await EquipmentItems.InitializeAsync();
        await Skills.InitializeAsync();
        await Injuries.InitializeAsync();
        await Spells.InitializeAsync();
        await SpecialRules.InitializeAsync();
        await Mutations.InitializeAsync();
        await Mounts.InitializeAsync();
    }
}
