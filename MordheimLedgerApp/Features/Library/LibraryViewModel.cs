using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.Spells;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Features.Library.Animals;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;

namespace MordheimLedgerApp.Features.Library;

/// <summary>
/// Single "Codex" Shell tab hosting the 8 catalog sections (types de bande, Place du Marché,
/// Compétences, Blessures, Sorts, Règles spéciales, Mutations, Animaux) that used to each be their own
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
    /// <summary>Translation keys for the 8 sections, in SelectedTab order - single source of truth for
    /// both the ActionSheet options and SelectedTabLabel, see SelectTab/RefreshSelectedTabLabel.</summary>
    private static readonly string[] TabKeys =
    [
        "TabWarbands", "LibTabTradingPost", "TabSkills", "TabInjuries",
        "LibTabSpells", "LibTabSpecialRules", "LibTabMutations", "LibTabAnimals"
    ];

    public WarbandArchetypeViewModel WarbandArchetypes { get; }
    public EquipmentItemViewModel EquipmentItems { get; }
    public SkillViewModel Skills { get; }
    public InjuryViewModel Injuries { get; }
    public SpellViewModel Spells { get; }
    public SpecialRuleViewModel SpecialRules { get; }
    public MutationViewModel Mutations { get; }
    public AnimalViewModel Animals { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarbandsTab))]
    [NotifyPropertyChangedFor(nameof(IsTradingPostTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    [NotifyPropertyChangedFor(nameof(IsSpellsTab))]
    [NotifyPropertyChangedFor(nameof(IsSpecialRulesTab))]
    [NotifyPropertyChangedFor(nameof(IsMutationsTab))]
    [NotifyPropertyChangedFor(nameof(IsAnimalsTab))]
    private int selectedTab;

    /// <summary>Displayed on the single section-picker Button - see SelectTab. Distinct from a direct
    /// {Binding SelectedTab} text lookup so it can be recomputed on language change, cf.
    /// SkillViewModel.SelectedCategoryLabel.</summary>
    [ObservableProperty]
    private string selectedTabLabel = string.Empty;

    public bool IsWarbandsTab => SelectedTab == 0;
    public bool IsTradingPostTab => SelectedTab == 1;
    public bool IsSkillsTab => SelectedTab == 2;
    public bool IsInjuriesTab => SelectedTab == 3;
    public bool IsSpellsTab => SelectedTab == 4;
    public bool IsSpecialRulesTab => SelectedTab == 5;
    public bool IsMutationsTab => SelectedTab == 6;
    public bool IsAnimalsTab => SelectedTab == 7;

    public LibraryViewModel(WarbandArchetypeViewModel warbandArchetypes, EquipmentItemViewModel equipmentItems,
        SkillViewModel skills, InjuryViewModel injuries, SpellViewModel spells, SpecialRuleViewModel specialRules,
        MutationViewModel mutations, AnimalViewModel animals)
    {
        WarbandArchetypes = warbandArchetypes;
        EquipmentItems = equipmentItems;
        Skills = skills;
        Injuries = injuries;
        Spells = spells;
        SpecialRules = specialRules;
        Mutations = mutations;
        Animals = animals;
        RefreshSelectedTabLabel();
    }

    private void RefreshSelectedTabLabel() => SelectedTabLabel = Loc[TabKeys[SelectedTab]];

    /// <summary>Single entry point replacing the former 8 separate ShowXTab buttons - one Button (see
    /// LibraryPage.xaml) showing the active section, opening an ActionSheet of all 8 on tap. Same idiom
    /// as SkillViewModel.SelectCategory, picked to keep the section switcher compact regardless of how
    /// many Codex sections exist.</summary>
    [RelayCommand]
    private async Task SelectTab()
    {
        var options = TabKeys.Select(k => Loc[k]).ToArray();
        var index = await ShowActionSheetIndexAsync(Loc["LibSelectSectionTitle"], options);
        if (index < 0) return;

        SelectedTab = index;
        RefreshSelectedTabLabel();
    }

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
        await Animals.InitializeAsync();
        RefreshSelectedTabLabel();
    }
}
