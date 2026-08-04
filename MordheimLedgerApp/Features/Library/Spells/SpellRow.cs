using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Spells;

/// <summary>Tuile de grille (SpellView) - même mécanisme que InjuryRow/EquipmentItemRow/SkillRow.</summary>
public partial class SpellRow : ObservableObject
{
    public Spell Item { get; }

    [ObservableProperty]
    private bool isSelected;

    public SpellRow(Spell item) => Item = item;
}
