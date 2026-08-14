using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.WarriorArchetypes.CreateEdit;

public partial class WarriorArchetypeEditDialogViewModel : DialogViewModel<bool>
{
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly ILibraryService _libraryService;
    private readonly Dictionary<string, EquipmentList> _equipmentListByLabel = new();

    /// <summary>Chargé à la demande (voir FindCatalogRuleAsync), une seule fois par instance de dialog -
    /// sert uniquement à retrouver une règle connue par son nom pour la pré-remplir automatiquement,
    /// pas affiché tel quel (le "+" classique reste PickSpecialRulesAsync, un vrai picker).</summary>
    private List<SpecialRule>? _specialRuleCatalog;

    protected override bool CancelResult => false;

    [ObservableProperty]
    private WarriorArchetype item;

    [ObservableProperty]
    private string title;

    /// <summary>Édité en mémoire ici, recopié sur Item.SpecialRules à la sauvegarde (voir Save) - même
    /// principe que Item lui-même (une copie, rien n'est persisté avant Enregistrer), contrairement aux
    /// Équipement/Compétences/Blessures d'un Warrior qui persistent immédiatement.</summary>
    public ObservableCollection<SpecialRule> SpecialRules { get; }

    /// <summary>Options du Picker "Liste d'équipement" - même pattern que SpellEditDialogViewModel's
    /// MagicSchool picker (label -> ligne catalogue, écrit Item.EquipmentListId via
    /// OnSelectedEquipmentListLabelChanged), avec en plus une entrée "Aucune" puisque EquipmentListId
    /// est nullable (beaucoup d'archétypes - Zombies, Trolls... - n'utilisent aucun équipement).</summary>
    public ObservableCollection<string> EquipmentListOptions { get; } = new();

    [ObservableProperty]
    private string selectedEquipmentListLabel = string.Empty;

    /// <summary>Champ texte unique pour le Mouvement - accepte un nombre ("4") ou une surcharge libre
    /// ("2D6" pour les Squigs des cavernes). Résolu vers Item.Movement/Item.MovementOverride au Save
    /// selon que ça parse comme int ou non, plutôt que 2 champs séparés (Entry numérique + Entry
    /// texte) - un seul champ, comme sur la fiche officielle.</summary>
    [ObservableProperty]
    private string movementInput;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsProfileTab))]
    [NotifyPropertyChangedFor(nameof(IsRulesTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsProfileTab => SelectedTab == 1;
    public bool IsRulesTab => SelectedTab == 2;

    /// <summary>Proxy observable de Item.IsHero - Item (WarriorArchetype) n'a pas d'INotifyPropertyChanged,
    /// donc une CheckBox liée directement à Item.IsHero ne ferait jamais réagir les sections
    /// conditionnelles Héros/Homme de main de l'onglet Type. Repoussé sur Item.IsHero à la volée (pas
    /// seulement au Save) via OnIsHeroChanged - même principe que SelectedEquipmentListLabel/
    /// OnSelectedEquipmentListLabelChanged pour EquipmentListId.</summary>
    [ObservableProperty]
    private bool isHero;

    /// <summary>Mêmes proxies observables que IsHero, mais avec en plus une synchronisation de règle
    /// spéciale avec l'état de la case (voir OnIsSpellcasterChanged/OnIsLargeCreatureChanged/
    /// OnCanUseEquipmentChanged) - coché ajoute la règle correspondante à la liste de chips (comme un
    /// clic "+" suivi d'une sélection dans le picker), décoché la retire si présente. Pas un moteur de
    /// règles : l'utilisateur reste libre d'ajouter/retirer n'importe quelle règle indépendamment via le
    /// "+"/"x" habituel (ex. remplacer "Chef" par "Discipline Militaire" pour le Capitaine Reiklander) -
    /// seule la case elle-même reste strictement synchronisée avec sa règle associée.</summary>
    [ObservableProperty]
    private bool isSpellcaster;

    [ObservableProperty]
    private bool isLargeCreature;

    [ObservableProperty]
    private bool canUseEquipment;

    /// <summary>Mêmes proxies observables que IsSpellcaster/IsLargeCreature/CanUseEquipment, mais sans
    /// équivalent sur WarriorArchetype - "Chef" et "Ne gagne jamais d'Expérience" ne correspondent à
    /// aucun champ propre au modèle, juste à la présence de la règle dans SpecialRules. Initialisées
    /// depuis SpecialRules plutôt qu'un flag persisté, même principe que
    /// SpecialRuleEditDialogViewModel.IsMaterialRule (dérivé de CostMultiplier.HasValue) - pas de nouvel
    /// état redondant à synchroniser.</summary>
    [ObservableProperty]
    private bool isChief;

    [ObservableProperty]
    private bool neverGainsExperience;

    public WarriorArchetypeEditDialogViewModel(WarriorArchetype item, string title, ISpecialRulePickerService specialRulePicker,
        IReadOnlyList<EquipmentList> allEquipmentLists, ILibraryService libraryService)
    {
        this.item = item;
        this.title = title;
        _specialRulePicker = specialRulePicker;
        _libraryService = libraryService;
        SpecialRules = new ObservableCollection<SpecialRule>(item.SpecialRules);
        movementInput = item.MovementOverride ?? item.Movement.ToString();
        isHero = item.IsHero;
        isSpellcaster = item.IsSpellcaster;
        isLargeCreature = item.IsLargeCreature;
        canUseEquipment = item.CanUseEquipment;
        isChief = item.SpecialRules.Any(r => r.Name is "Leader" or "Chef");
        neverGainsExperience = item.SpecialRules.Any(r => r.Name is "Never Gains Experience" or "Ne gagne jamais d'Expérience");

        var noneLabel = Loc["WarriorArchetypeEquipmentListNone"];
        EquipmentListOptions.Add(noneLabel);
        foreach (var list in allEquipmentLists)
        {
            _equipmentListByLabel[list.Name] = list;
            EquipmentListOptions.Add(list.Name);
        }
        var currentList = allEquipmentLists.FirstOrDefault(l => l.Id == item.EquipmentListId);
        selectedEquipmentListLabel = currentList?.Name ?? noneLabel;
    }

    partial void OnSelectedEquipmentListLabelChanged(string value) =>
        Item.EquipmentListId = _equipmentListByLabel.TryGetValue(value, out var list) ? list.Id : null;

    /// <summary>Basculer Héros/Homme de main réinitialise les cases propres au type qu'on quitte (et leur
    /// chip associée, via leurs propres OnXxxChanged - re-déclenchés en cascade par ces assignations)
    /// plutôt que de les laisser cochées pour un type qui ne les affiche plus - CanUseEquipment fait
    /// exception (reste tel quel, voir demande explicite) car pas propre à un seul type au même titre.</summary>
    partial void OnIsHeroChanged(bool value)
    {
        Item.IsHero = value;
        if (value)
        {
            IsLargeCreature = false;
        }
        else
        {
            IsChief = false;
            IsSpellcaster = false;
        }
    }

    partial void OnIsSpellcasterChanged(bool value)
    {
        Item.IsSpellcaster = value;
        if (value) _ = AddSuggestedRuleAsync("Wizard", "Sorcier");
        else RemoveSuggestedRule("Wizard", "Sorcier");
    }

    partial void OnIsLargeCreatureChanged(bool value)
    {
        Item.IsLargeCreature = value;
        if (value) _ = AddSuggestedRuleAsync("Large Target", "Grande Cible");
        else RemoveSuggestedRule("Large Target", "Grande Cible");
    }

    partial void OnCanUseEquipmentChanged(bool value)
    {
        Item.CanUseEquipment = value;
        if (!value) _ = AddSuggestedRuleAsync("No Equipment", "Sans équipement");
        else RemoveSuggestedRule("No Equipment", "Sans équipement");
    }

    partial void OnIsChiefChanged(bool value)
    {
        if (value) _ = AddSuggestedRuleAsync("Leader", "Chef");
        else RemoveSuggestedRule("Leader", "Chef");
    }

    partial void OnNeverGainsExperienceChanged(bool value)
    {
        if (value) _ = AddSuggestedRuleAsync("Never Gains Experience", "Ne gagne jamais d'Expérience");
        else RemoveSuggestedRule("Never Gains Experience", "Ne gagne jamais d'Expérience");
    }

    /// <summary>Recherche par nom (anglais OU français - Name est déjà résolu dans la langue courante,
    /// voir LibraryService.GetSpecialRulesAsync) plutôt que par Id, aucun identifiant stable exposé côté
    /// modèle pour ces règles communes. Catalogue chargé une seule fois puis mis en cache (_specialRuleCatalog).</summary>
    private async Task<SpecialRule?> FindCatalogRuleAsync(string englishName, string frenchName)
    {
        _specialRuleCatalog ??= await _libraryService.GetSpecialRulesAsync(LocalizationService.Instance.Language);
        return _specialRuleCatalog.FirstOrDefault(r => r.Name == englishName || r.Name == frenchName);
    }

    private async Task AddSuggestedRuleAsync(string englishName, string frenchName)
    {
        if (SpecialRules.Any(r => r.Name == englishName || r.Name == frenchName)) return;
        var rule = await FindCatalogRuleAsync(englishName, frenchName);
        if (rule is not null) SpecialRules.Add(rule);
    }

    /// <summary>Décocher IsSpellcaster/IsLargeCreature/CanUseEquipment retire la règle associée si
    /// présente - évite qu'une case décochée après une coche accidentelle laisse une chip orpheline et
    /// contradictoire derrière elle.</summary>
    private void RemoveSuggestedRule(string englishName, string frenchName)
    {
        var existing = SpecialRules.FirstOrDefault(r => r.Name == englishName || r.Name == frenchName);
        if (existing is not null) SpecialRules.Remove(existing);
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowProfileTab() => SelectedTab = 1;

    [RelayCommand]
    private void ShowRulesTab() => SelectedTab = 2;

    [RelayCommand]
    private async Task AddSpecialRule()
    {
        var picked = await _specialRulePicker.PickSpecialRulesAsync(SpecialRuleFilterKind.Warrior);
        foreach (var rule in picked)
        {
            if (SpecialRules.Any(r => r.Id == rule.Id)) continue;
            SpecialRules.Add(rule);
        }
    }


    [RelayCommand]
    private Task ShowSpecialRuleDetail(SpecialRule rule) => ShowChipDetailAsync(rule.Name, rule.Description);

    [RelayCommand]
    private void RemoveSpecialRule(SpecialRule rule) => SpecialRules.Remove(rule);

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields())
        {
            SelectedTab = 0;
            return;
        }

        Item.SpecialRules = SpecialRules.ToList();

        if (int.TryParse(MovementInput, out var movement))
        {
            Item.Movement = movement;
            Item.MovementOverride = null;
        }
        else
        {
            Item.MovementOverride = MovementInput;
        }

        Close(true);
    }
}
