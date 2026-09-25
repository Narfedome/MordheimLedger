using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.CreateEdit;

/// <summary>Un segment du sélecteur de matériau d'une arme dans MaterialPickerDialog - "Normal" (Rule ==
/// null) ou un matériau exclusif (Gromril/Ithilmar/Ornée : SpecialRule avec CostMultiplier, hors
/// "Blessed Weapon" qui est cumulable et passe par MaterialChoice.IsBlessed).</summary>
public partial class MaterialOptionRow : ObservableObject
{
    public MaterialChoice Owner { get; }

    public SpecialRule? Rule { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;

    public MaterialOptionRow(MaterialChoice owner, SpecialRule? rule, string name)
    {
        Owner = owner;
        Rule = rule;
        Name = name;
    }
}

/// <summary>Une arme du lot d'achat dans MaterialPickerDialog : un matériau exclusif (corps à corps uniquement)
/// + une bénédiction cumulable (IsBlessed, seulement si la règle "Blessed Weapon" existe).
/// Le prix affiché suit le choix en direct : un matériau rend payante la première dague gratuite
/// (livre - "in addition to his free dagger"), la bénédiction (×1) ne change pas le prix.</summary>
public partial class MaterialChoice : ObservableObject
{
    private readonly bool _isFreeEligible;

    public EquipmentItem Item { get; }
    public ObservableCollection<MaterialOptionRow> Options { get; }

    /// <summary>Null si la règle "Blessed Weapon" est absente du catalogue - la ligne Bénie est alors masquée.</summary>
    public SpecialRule? BlessingRule { get; }
    public bool CanBeBlessed => BlessingRule is not null;

    /// <summary>False pour une arme de tir/poudre noire (seul "Normal" dans Options) - la ligne de segments est masquée.</summary>
    public bool HasMaterialOptions => Options.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBlessing))]
    private bool isBlessed;

    public SpecialRule? SelectedMaterial => Options.FirstOrDefault(o => o.IsSelected)?.Rule;
    public bool HasMaterial => SelectedMaterial is not null;
    public SpecialRule? SelectedBlessing => IsBlessed ? BlessingRule : null;

    public int SelectedCost => EquipmentPricing.CalculateCost(Item.Cost, SelectedMaterial?.CostMultiplier, _isFreeEligible && SelectedMaterial is null);

    public string CostDisplay => SelectedCost == 0
        ? LocalizationService.Instance["LibFreePh"]
        : $"{SelectedCost} {LocalizationService.Instance["LibGoldCrownsAbbr"]}";

    public MaterialChoice(EquipmentItem item, IReadOnlyList<SpecialRule> materialRules, SpecialRule? blessingRule, string normalLabel, bool isFreeEligible = false)
    {
        Item = item;
        BlessingRule = blessingRule;
        _isFreeEligible = isFreeEligible;
        var options = new List<MaterialOptionRow> { new(this, null, normalLabel) };
        options.AddRange(materialRules.Select(r => new MaterialOptionRow(this, r, ShortName(r.Name, materialRules))));
        Options = new ObservableCollection<MaterialOptionRow>(options);
        Options[0].IsSelected = true;
    }

    /// <summary>Libellé court d'un segment : "Arme en Gromril"/"Gromril Weapon" -> "Gromril". Retire les mots
    /// communs à tous les matériaux ("Arme"/"Weapon") puis les petits mots de liaison en tête ("en"), sans
    /// dépendre d'une langue précise ; retombe sur le nom complet si rien ne reste (un seul matériau).</summary>
    private static string ShortName(string name, IReadOnlyList<SpecialRule> materialRules)
    {
        var common = materialRules
            .Select(r => r.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).AsEnumerable())
            .Aggregate((a, b) => a.Intersect(b, StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !common.Contains(w))
            .SkipWhile(w => w.Length <= 3 && w.All(char.IsLower))
            .ToList();
        return words.Count > 0 ? string.Join(' ', words) : name;
    }

    public void Select(MaterialOptionRow row)
    {
        foreach (var o in Options) o.IsSelected = ReferenceEquals(o, row);
        OnPropertyChanged(nameof(SelectedMaterial));
        OnPropertyChanged(nameof(HasMaterial));
        OnPropertyChanged(nameof(SelectedCost));
        OnPropertyChanged(nameof(CostDisplay));
    }
}
