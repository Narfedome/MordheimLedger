using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.Spells;

/// <summary>Tuile de grille (SpellView) - même mécanisme que InjuryRow/EquipmentItemRow/SkillRow.</summary>
public partial class SpellRow : ObservableObject
{
    public Spell Item { get; }

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Ligne d'info secondaire de la tuile (CodexTileSecondaryLabelStyle) - "Jet"/"Roll" en
    /// toutes lettres plutôt qu'une icône (dé trop peu distinguable à la taille d'une tuile). Difficulty
    /// absente n'ajoute rien plutôt qu'un "Diff. " vide.</summary>
    public string RollDisplay
    {
        get
        {
            var roll = $"{LocalizationService.Instance["LibRollAbbr"]} {Item.RollValue}";
            return Item.Difficulty.HasValue
                ? $"{roll} · {LocalizationService.Instance["LibDifficultyAbbr"]} {Item.Difficulty}"
                : roll;
        }
    }

    public SpellRow(Spell item) => Item = item;
}
