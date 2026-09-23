using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.DramatisPersonae.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

/// <summary>Catalog of Dramatis Personae/special characters (e.g. "Aenur, the Sword of Twilight") -
/// CRUD AND picker mode (added when the End of Game wizard's "Personnage spécial" search step started
/// referencing the real catalog instead of free-text - see EndOfGamePageViewModel.RareItems.cs) - same
/// IsSelectorMode/SelectedRows/ConfirmSelection bascule as HiredSwordViewModel. Flat list, no grouping,
/// same "too few entries to warrant it" call as HiredSwordView. Single-select only for now (see
/// IDramatisPersonaPickerService) - SelectedRows still exists for symmetry with HiredSwordViewModel's
/// Multiple mode, but nothing currently opens this picker with SelectionMode.Multiple.</summary>
public partial class DramatisPersonaViewModel : BaseViewModel
{
    private readonly ILibraryService _libraryService;
    private readonly IDetailDialogService _detailDialogs;
    private readonly IWarbandArchetypePickerService _warbandPicker;
    private readonly IEquipmentPickerService _equipmentPicker;
    private readonly ISkillPickerService _skillPicker;
    private readonly ISpecialRulePickerService _specialRulePicker;
    private readonly IMagicSchoolPickerService _magicSchoolPicker;
    private readonly IDramatisPersonaPickerNavigationService _pickerNavigation;
    private List<WarbandArchetype> _warbandArchetypes = new();
    private List<EquipmentItem> _equipmentItems = new();

    [ObservableProperty]
    private ObservableCollection<DramatisPersonaRow> dramatisPersonaRows = new();

    [ObservableProperty]
    private DramatisPersonaRow? selectedRow;

    /// <summary>Set by DramatisPersonaSelectorPage right after construction - même bascule que
    /// HiredSwordViewModel.IsSelectorMode.</summary>
    public bool IsSelectorMode { get; set; }

    /// <summary>Multi-sélection en mode picker uniquement - alimentée par Select, vidée par LoadData.
    /// Non utilisée pour l'instant (voir la doc de classe) mais gardée pour cohérence avec
    /// HiredSwordViewModel si un futur appelant a besoin du mode Multiple.</summary>
    public ObservableCollection<DramatisPersonaRow> SelectedRows { get; } = new();

    public bool HasSelectedRows => SelectedRows.Count > 0 || SelectedRow != null;

    /// <summary>Set by DramatisPersonaSelectorPage. Toujours Single pour l'instant.</summary>
    public SelectionMode SelectionMode { get; set; }

    /// <summary>Set by DramatisPersonaPickerService - narrowe aux Dramatis Personae éligibles à CETTE
    /// bande (RestrictedToWarbandArchetypeIds vide ou la contenant). Null en usage Codex normal (CRUD),
    /// où tout le catalogue doit rester visible.</summary>
    public int? AllowedWarbandArchetypeId { get; set; }

    /// <summary>Set by DramatisPersonaPickerService (2026-09-01, délai de re-recherche - voir
    /// DramatisPersona.RequiresCooldownBeforeResearch) - masque tout personnage actuellement en
    /// cooldown pour la bande qui cherche (ex. Aenur parti la Fin de Partie précédente). Null/vide en
    /// usage Codex normal, où tout le catalogue doit rester visible.</summary>
    public IReadOnlyCollection<int>? ExcludedDramatisPersonaIds { get; set; }

    public DramatisPersonaViewModel(ILibraryService libraryService, IDetailDialogService detailDialogs,
        IWarbandArchetypePickerService warbandPicker, IEquipmentPickerService equipmentPicker, ISkillPickerService skillPicker,
        ISpecialRulePickerService specialRulePicker, IMagicSchoolPickerService magicSchoolPicker,
        IDramatisPersonaPickerNavigationService pickerNavigation)
    {
        _libraryService = libraryService;
        _detailDialogs = detailDialogs;
        _warbandPicker = warbandPicker;
        _equipmentPicker = equipmentPicker;
        _skillPicker = skillPicker;
        _specialRulePicker = specialRulePicker;
        _magicSchoolPicker = magicSchoolPicker;
        _pickerNavigation = pickerNavigation;

        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
            (r, m) => _ = ((DramatisPersonaViewModel)r).LoadData());
    }

    public async Task InitializeAsync() => await Loading.RunAsync(LoadData);

    private async Task LoadData()
    {
        var items = await _libraryService.GetDramatisPersonaeAsync(LocalizationService.Instance.Language);
        _warbandArchetypes = await _libraryService.GetWarbandArchetypesAsync(LocalizationService.Instance.Language);
        _equipmentItems = await _libraryService.GetEquipmentItemsAsync(LocalizationService.Instance.Language);

        IEnumerable<DramatisPersona> filtered = items;
        if (AllowedWarbandArchetypeId is { } warbandId)
            filtered = filtered.Where(p => p.RestrictedToWarbandArchetypeIds.Count == 0 || p.RestrictedToWarbandArchetypeIds.Contains(warbandId));
        if (ExcludedDramatisPersonaIds is { Count: > 0 } excludedIds)
            filtered = filtered.Where(p => !excludedIds.Contains(p.Id));
        // Ulli (2026-09-01, "un seul choix « Ulli & Marquand » dans le picker") - jamais son propre choix
        // dans le picker de recrutement (IsSelectorMode), Marquand seul représente la paire (voir
        // DramatisPersona.IsHiddenFromSearchPicker's own doc). Reste visible dans le Codex normal
        // (IsSelectorMode false) pour la consultation/l'édition.
        if (IsSelectorMode)
            filtered = filtered.Where(p => !p.IsHiddenFromSearchPicker);

        // Nom de tuile combiné pour la paire, "Marquand Volker & Ulli Leitpold" plutôt que "Marquand
        // Volker" seul (2026-09-01, user request) - UNIQUEMENT dans le picker de recrutement : Ulli n'y a
        // pas sa propre tuile (filtrée juste au-dessus), donc celle de Marquand doit se lire comme
        // représentant les deux. Dans le Codex normal (IsSelectorMode false), les deux gardent leur
        // propre tuile/nom séparé - voir DramatisPersonaRow.DisplayName's own doc.
        DramatisPersonaRows = new ObservableCollection<DramatisPersonaRow>(filtered.Select(i => new DramatisPersonaRow(i,
            IsSelectorMode && i.PairedWithDramatisPersona is { } partner ? $"{i.Name} & {partner.Name}" : null)));
        SelectedRow = null;
        SelectedRows.Clear();
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    partial void OnSelectedRowChanged(DramatisPersonaRow? oldValue, DramatisPersonaRow? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
    }

    [RelayCommand]
    private void Select(DramatisPersonaRow row)
    {
        if (!IsSelectorMode || SelectionMode == SelectionMode.Single)
        {
            SelectedRow = row;
            OnPropertyChanged(nameof(HasSelectedRows));
            return;
        }

        row.IsSelected = !row.IsSelected;
        if (row.IsSelected) SelectedRows.Add(row);
        else SelectedRows.Remove(row);
        OnPropertyChanged(nameof(HasSelectedRows));
    }

    [RelayCommand]
    private async Task Create()
    {
        var newItem = new DramatisPersona();
        var dialogViewModel = new DramatisPersonaEditDialogViewModel(newItem, Loc["DramatisPersonaCreateTitle"],
            _warbandPicker, _equipmentPicker, _skillPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs,
            _libraryService, _warbandArchetypes, Array.Empty<EquipmentItem>());
        if (await ShowDialogAsync(new DramatisPersonaEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveDramatisPersonaAsync(newItem, LocalizationService.Instance.Language);
        await LoadData();

        // Sélecteur : le "+" doit se comporter comme si on avait tapé la nouvelle tuile - coché et
        // ajouté à SelectedRows/SelectedRow, sans fermer le picker.
        if (IsSelectorMode)
        {
            var row = DramatisPersonaRows.FirstOrDefault(r => r.Item.Id == newItem.Id);
            if (row != null) Select(row);
        }
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (SelectedRow is not { } row) return;

        var s = row.Item;
        var copy = new DramatisPersona
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            PairDescription = s.PairDescription,
            NameKey = s.NameKey,
            DescriptionKey = s.DescriptionKey,
            PairDescriptionKey = s.PairDescriptionKey,
            Source = s.Source,
            ImagePath = s.ImagePath,
            Movement = s.Movement,
            WeaponSkill = s.WeaponSkill,
            BallisticSkill = s.BallisticSkill,
            Strength = s.Strength,
            Toughness = s.Toughness,
            Wounds = s.Wounds,
            Initiative = s.Initiative,
            Attacks = s.Attacks,
            Leadership = s.Leadership,
            FeeKind = s.FeeKind,
            HireCost = s.HireCost,
            Upkeep = s.Upkeep,
            RatingBonus = s.RatingBonus,
            IsWanderer = s.IsWanderer,
            RequiresRatingDisadvantage = s.RequiresRatingDisadvantage,
            RequiresCooldownBeforeResearch = s.RequiresCooldownBeforeResearch,
            // Bug trouvé en passant (2026-09-01, même classe que AlternativePaymentItemId juste en
            // dessous) : ces trois champs manquaient déjà depuis l'ajout du pairage Ulli & Marquand -
            // éditer Marquand ou Ulli via le Codex effaçait silencieusement son lien de paire au premier
            // Enregistrer.
            PairedWithDramatisPersonaId = s.PairedWithDramatisPersonaId,
            PairedWithDramatisPersona = s.PairedWithDramatisPersona,
            IsHiddenFromSearchPicker = s.IsHiddenFromSearchPicker,
            RestrictedToWarbandArchetypeIds = new List<int>(s.RestrictedToWarbandArchetypeIds),
            SpecialRules = new List<SpecialRule>(s.SpecialRules),
            StartingEquipmentIds = new List<int>(s.StartingEquipmentIds),
            Skills = new List<Skill>(s.Skills),
            MagicSchoolId = s.MagicSchoolId,
            MagicSchool = s.MagicSchool,
            // Bug trouvé en passant (2026-09-01) : manquaient déjà tous les deux depuis l'ajout du
            // paiement alternatif de Johann - la copie défensive les omettait, donc éditer Johann via le
            // Codex effaçait silencieusement son AlternativePaymentItemId au premier Enregistrer (même
            // classe de bug que la copie de Warrior dans WarbandDetailViewModel.EditWarrior, déjà
            // documentée là-bas).
            AlternativePaymentItemId = s.AlternativePaymentItemId,
            AlternativePaymentItem = s.AlternativePaymentItem
        };

        var initialEquipment = _equipmentItems.Where(e => s.StartingEquipmentIds.Contains(e.Id)).ToList();
        var dialogViewModel = new DramatisPersonaEditDialogViewModel(copy, Loc["DramatisPersonaEditTitle"],
            _warbandPicker, _equipmentPicker, _skillPicker, _specialRulePicker, _magicSchoolPicker, _detailDialogs,
            _libraryService, _warbandArchetypes, initialEquipment);
        if (await ShowDialogAsync(new DramatisPersonaEditDialog(dialogViewModel)) != true) return;

        await _libraryService.SaveDramatisPersonaAsync(copy, LocalizationService.Instance.Language);
        await LoadData();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedRow is not { } row) return;
        if (!await ConfirmDeleteAsync(row.Item.Name)) return;

        await _libraryService.DeleteDramatisPersonaAsync(row.Item.Id);
        await LoadData();
    }

    /// <summary>Read-only recap popup (tile info button). AllowConcurrentExecutions : voir
    /// WarbandArchetypeViewModel.ShowDetails. Résumé de paire seulement en mode picker (IsSelectorMode -
    /// 2026-09-01, user request : le Codex normal doit montrer directement la fiche du personnage tapé,
    /// le picker de recrutement montre le résumé "Description + règles communes + 2 profils" à la place,
    /// puisque c'est là que la paire n'a qu'une seule tuile combinée - voir DramatisPersonaRow.
    /// DisplayName).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ShowDetails(DramatisPersonaRow row) =>
        IsSelectorMode && row.Item.PairedWithDramatisPersona is not null
            ? _detailDialogs.ShowDramatisPersonaPairDetailDialogAsync(row.Item)
            : _detailDialogs.ShowDramatisPersonaDetailDialogAsync(row.Item);

    [RelayCommand]
    private async Task ConfirmSelection()
    {
        if (SelectionMode == SelectionMode.Single && SelectedRow != null)
        {
            await _pickerNavigation.ClosePickerAsync(new[] { SelectedRow.Item });
            return;
        }

        var items = SelectedRows.Select(r => r.Item).ToList();
        await _pickerNavigation.ClosePickerAsync(items);
    }

    [RelayCommand]
    private async Task Cancel() => await _pickerNavigation.ClosePickerAsync(Array.Empty<DramatisPersona>());
}
