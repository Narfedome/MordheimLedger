using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Core.Models;

/// <summary>A mutation bought by a specific warrior (join between Warrior and the Mutation catalog) -
/// stackable, a Mutant/Possessed can have several.</summary>
public class WarriorMutation
{
    public int Id { get; set; }
    public int WarriorId { get; set; }
    public Mutation Item { get; set; } = null!;

    /// <summary>Passe-plat vers Item.Name - voir WarriorInjury.Name.</summary>
    public string Name => Item.Name;
}
