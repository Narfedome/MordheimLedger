using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Un seul écran pour toutes les armes d'un même lot d'achat (plus de
/// pagination, retour utilisateur 2026-09-25 : "plus simple d'accès et de lecture") - par arme : nom +
/// prix + ⓘ, un sélecteur segmenté Normal/Gromril/Ithilmar/Ornée (exclusif) + ⓘ de la règle du matériau,
/// une case Bénie (cumulable) + ⓘ. Aucune description en clair : le détail passe par les ⓘ.
/// Close(true) = Valider, l'appelant lit SelectedMaterial/SelectedBlessing de chaque Choices ; Close(false)
/// = Tout normal / tap à l'extérieur, l'appelant traite tout comme Normal sans bénédiction.</summary>
public partial class MaterialPickerDialogViewModel : DialogViewModel<bool>
{
    private readonly IDetailDialogService _detailDialogs;

    protected override bool CancelResult => false;

    public ObservableCollection<MaterialChoice> Choices { get; }

    public MaterialPickerDialogViewModel(IReadOnlyList<MaterialChoice> choices, IDetailDialogService detailDialogs)
    {
        Choices = new ObservableCollection<MaterialChoice>(choices);
        _detailDialogs = detailDialogs;
    }

    /// <summary>Une MaterialChoice par arme de items ayant au moins une option, dans leur ordre (relues via
    /// ResolveChoices). La première dague gratuite n'est éligible qu'une fois, qu'elle soit déjà portée
    /// (alreadyHasFreeDagger) ou plus tôt dans ce même lot. Liste vide = rien à demander, pas de dialog.
    /// "Blessed Weapon" est repérée par son nom anglais (seul identifiant stable entre langues,
    /// même idiome que EndOfGamePageViewModel._specialRulesByEnglishName) puis sortie des matériaux
    /// exclusifs. allowBlessing: false masque la case Bénie (récompenses de scénario en Fin de Partie :
    /// l'objet rejoint la réserve, qui ne conserve pas la bénédiction).</summary>
    public static async Task<List<MaterialChoice>> BuildChoicesAsync(ILibraryService libraryService, IEnumerable<EquipmentItem> items, bool alreadyHasFreeDagger, bool allowBlessing = true)
    {
        var choices = new List<MaterialChoice>();
        var weapons = items.Where(IsWeapon).ToList();
        if (weapons.Count == 0) return choices;

        var language = LocalizationService.Instance.Language;
        var rules = await libraryService.GetSpecialRulesAsync(language);
        var blessingId = (language == "en" ? rules : await libraryService.GetSpecialRulesAsync("en"))
            .FirstOrDefault(r => r.Name == "Blessed Weapon")?.Id;
        var blessingRule = allowBlessing ? rules.FirstOrDefault(r => r.Id == blessingId) : null;
        var materialRules = rules.Where(r => r.CostMultiplier.HasValue && r.Id != blessingId).ToList();

        var hasFreeDaggerSlot = alreadyHasFreeDagger;
        foreach (var item in weapons)
        {
            // Matériaux (Gromril/Ithilmar/Ornée) : corps à corps uniquement. Bénédiction : toute arme, tir
            // et poudre noire compris (même périmètre que la bénédiction du Sanctuaire en Fin de Partie,
            // EndOfGamePageViewModel.WeaponBlessingOptions). Une arme sans aucune option n'apparaît pas.
            var itemMaterials = item.Category == EquipmentCategory.MeleeWeapon ? materialRules : [];
            if (itemMaterials.Count == 0 && blessingRule is null) continue;
            choices.Add(new MaterialChoice(item, itemMaterials, blessingRule, LocalizationService.Instance["WarriorsMaterialNormal"],
                EquipmentPricing.IsFreeDaggerEligible(item.IsFreeDagger, hasFreeDaggerSlot)));
            if (item.IsFreeDagger) hasFreeDaggerSlot = true;
        }
        return choices;
    }

    /// <summary>Matériau/bénédiction retenus pour chaque objet de items, dans le même ordre (null, null pour
    /// un objet sans choix, ou pour tout le lot si confirmed != true). Correspondance par position et
    /// identité d'instance, pas par clé : items peut contenir plusieurs fois le MÊME EquipmentItem (achat
    /// de plusieurs exemplaires), chacun avec son propre choix, et une arme sans aucune option n'a pas de
    /// MaterialChoice.</summary>
    public static List<(SpecialRule? Material, SpecialRule? Blessing)> ResolveChoices(IEnumerable<EquipmentItem> items, IReadOnlyList<MaterialChoice> choices, bool? confirmed)
    {
        var result = new List<(SpecialRule?, SpecialRule?)>();
        var index = 0;
        foreach (var item in items)
        {
            if (index < choices.Count && ReferenceEquals(choices[index].Item, item))
            {
                var choice = choices[index++];
                result.Add(confirmed == true ? (choice.SelectedMaterial, choice.SelectedBlessing) : (null, null));
            }
            else
                result.Add((null, null));
        }
        return result;
    }

    /// <summary>Armes concernées par ce dialog (voir BuildChoicesAsync).</summary>
    private static bool IsWeapon(EquipmentItem item) =>
        item.Category is EquipmentCategory.MeleeWeapon or EquipmentCategory.MissileWeapon or EquipmentCategory.BlackPowderWeapon;

    [RelayCommand]
    private void SelectMaterial(MaterialOptionRow row) => row.Owner.Select(row);

    [RelayCommand]
    private Task ShowWeaponDetail(MaterialChoice choice) =>
        _detailDialogs.ShowEquipmentDetailDialogAsync(choice.Item, choice.SelectedMaterial, blessingRule: choice.SelectedBlessing);

    [RelayCommand]
    private Task ShowMaterialDetail(MaterialChoice choice) =>
        choice.SelectedMaterial is { } rule ? _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule) : Task.CompletedTask;

    [RelayCommand]
    private Task ShowBlessingDetail(MaterialChoice choice) =>
        choice.BlessingRule is { } rule ? _detailDialogs.ShowSpecialRuleDetailDialogAsync(rule) : Task.CompletedTask;

    [RelayCommand]
    private void AllNormal() => Close(false);

    [RelayCommand]
    private void Save() => Close(true);
}
