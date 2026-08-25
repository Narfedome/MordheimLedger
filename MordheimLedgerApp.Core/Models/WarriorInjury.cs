using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A permanent injury carried by a specific warrior (join between Warrior and the Library catalog).</summary>
public class WarriorInjury
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Injury Item { get; set; } = null!;

    /// <summary>True only for the "misses next game(s)" Palier 1 outcomes (Arm Wound/Smashed Leg's
    /// light branch, Deep Wound - see Core.Rules.SeriousInjuryEffectKind.MissNextGame/
    /// MissGamesRollD3) - purely descriptive of WHY the warrior is currently WarriorStatus.Sick, not a
    /// permanent injury like the rest of this list. Deleted automatically once
    /// Warrior.SickGamesRemaining reaches 0 (WarbandDetailViewModel.EndOfGame.ApplySicknessLifecycleAsync)
    /// rather than kept forever - confirmed explicitly by the user (2026-08-25): "cette chip
    /// disparaîtrait lorsque l'on a refini une fin de partie". False (default) for every permanent
    /// injury, including the severe/permanent branch of the same Arm Wound/Smashed Leg roll.</summary>
    public bool IsTemporary { get; set; }

    /// <summary>Passe-plat vers Item.Name - ChipView (composant de puce partagé) lie son Label
    /// directement sur Name, quel que soit le type réel qu'on lui passe.</summary>
    public string Name => Item.Name;
}
