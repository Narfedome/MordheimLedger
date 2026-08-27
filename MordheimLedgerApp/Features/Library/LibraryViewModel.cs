using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.Spells;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Features.Library.HiredSwords;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;

namespace MordheimLedgerApp.Features.Library;

/// <summary>
/// Single "Codex" Shell tab hosting the 7 catalog sections (types de bande, Place du Marché,
/// Compétences, Blessures, Sorts, Règles spéciales, Mutations) that used to each be their own
/// top-level TabBar tab - consolidated to declutter the bottom nav bar on Android. Same toggle pattern
/// (index + IsXTab, no real TabbedPage) already used by WarbandDetailPage's Roster/Historique and
/// WarriorEditDialog's Équipement/Compétences/Blessures. Each section keeps its own existing
/// ViewModel/*View ContentView unchanged - this container only owns the toggle and BindingContext
/// wiring, no catalog logic of its own. Animals used to be an 8th section here (AnimalView) - now just
/// an EquipmentCategory, browsed/edited via the Place du Marché tab's category filter instead.
///
/// MagicSchool is deliberately NOT one of these tabs, unlike the other 7 catalogs - too nested a
/// concept for a top-level Codex entry (same call as DmTools' Category, managed from within the Track
/// library rather than its own TabBar tab). It's reached instead via SpellViewModel.ManageMagicSchools
/// (see SpellView.xaml's "Gérer les écoles de magie" icon), which pushes MagicSchoolListPage as a
/// route - see AppShell.xaml.cs.
/// </summary>
public partial class LibraryViewModel : BaseViewModel
{
    /// <summary>Translation keys for the 7 sections, in SelectedTab order - single source of truth for
    /// both the ActionSheet options and SelectedTabLabel, see SelectTab/RefreshSelectedTabLabel.</summary>
    private static readonly string[] TabKeys =
    [
        "TabWarbands", "LibTabTradingPost", "TabSkills", "TabInjuries",
        "LibTabSpells", "LibTabSpecialRules", "LibTabMutations", "LibTabHiredSwords"
    ];

    public WarbandArchetypeViewModel WarbandArchetypes { get; }
    public EquipmentItemViewModel EquipmentItems { get; }
    public SkillViewModel Skills { get; }
    public InjuryViewModel Injuries { get; }
    public SpellViewModel Spells { get; }
    public SpecialRuleViewModel SpecialRules { get; }
    public MutationViewModel Mutations { get; }
    public HiredSwordViewModel HiredSwords { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarbandsTab))]
    [NotifyPropertyChangedFor(nameof(IsTradingPostTab))]
    [NotifyPropertyChangedFor(nameof(IsSkillsTab))]
    [NotifyPropertyChangedFor(nameof(IsInjuriesTab))]
    [NotifyPropertyChangedFor(nameof(IsSpellsTab))]
    [NotifyPropertyChangedFor(nameof(IsSpecialRulesTab))]
    [NotifyPropertyChangedFor(nameof(IsMutationsTab))]
    [NotifyPropertyChangedFor(nameof(IsHiredSwordsTab))]
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
    public bool IsHiredSwordsTab => SelectedTab == 7;

    // Bandes (onglet 0) est chargé dès InitializeAsync (voir plus bas) - les 7 autres ne le sont qu'au
    // premier passage sur leur onglet, voir EnsureTabLoadedAsync.
    private bool _equipmentItemsLoaded;
    private bool _skillsLoaded;
    private bool _injuriesLoaded;
    private bool _spellsLoaded;
    private bool _specialRulesLoaded;
    private bool _mutationsLoaded;
    private bool _hiredSwordsLoaded;

    public LibraryViewModel(WarbandArchetypeViewModel warbandArchetypes, EquipmentItemViewModel equipmentItems,
        SkillViewModel skills, InjuryViewModel injuries, SpellViewModel spells, SpecialRuleViewModel specialRules,
        MutationViewModel mutations, HiredSwordViewModel hiredSwords)
    {
        WarbandArchetypes = warbandArchetypes;
        EquipmentItems = equipmentItems;
        Skills = skills;
        Injuries = injuries;
        Spells = spells;
        SpecialRules = specialRules;
        Mutations = mutations;
        HiredSwords = hiredSwords;
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
        await EnsureTabLoadedAsync(index);
    }

    /// <summary>Seule la section Bandes (onglet par défaut) charge à l'ouverture du Codex - les 7 autres
    /// chargeaient toutes en même temps ici avant ce correctif (catalogues "petits" à l'origine, plus
    /// vrai avec 15 bandes + Trading Post/Animaux/Compétences seedés), d'où la sensation de freeze à
    /// l'ouverture même quand un seul onglet est réellement visible. Chaque section garde son propre
    /// indicateur de chargement (Loading.IsLoading, déjà câblé dans chaque XxxView), donc passer sur un
    /// onglet pas encore chargé montre son spinner plutôt que de bloquer la page entière.</summary>
    public async Task InitializeAsync()
    {
        await WarbandArchetypes.InitializeAsync();
        RefreshSelectedTabLabel();
    }

    private async Task EnsureTabLoadedAsync(int index)
    {
        switch (index)
        {
            case 1:
                if (!_equipmentItemsLoaded) { await EquipmentItems.InitializeAsync(); _equipmentItemsLoaded = true; }
                break;
            case 2:
                if (!_skillsLoaded) { await Skills.InitializeAsync(); _skillsLoaded = true; }
                break;
            case 3:
                if (!_injuriesLoaded) { await Injuries.InitializeAsync(); _injuriesLoaded = true; }
                break;
            case 4:
                if (!_spellsLoaded) { await Spells.InitializeAsync(); _spellsLoaded = true; }
                break;
            case 5:
                if (!_specialRulesLoaded) { await SpecialRules.InitializeAsync(); _specialRulesLoaded = true; }
                break;
            case 6:
                if (!_mutationsLoaded) { await Mutations.InitializeAsync(); _mutationsLoaded = true; }
                break;
            case 7:
                if (!_hiredSwordsLoaded) { await HiredSwords.InitializeAsync(); _hiredSwordsLoaded = true; }
                break;
        }
    }
}
