using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>One Dramatis Persona currently "on cooldown" for one Warband - see Library.DramatisPersona.
/// RequiresCooldownBeforeResearch's own doc. Presence of a row means "this warband may not search for
/// this persona again yet"; the row is deleted once the requirement is satisfied (one End of Game has run
/// since the persona departed) rather than tracking a countdown, since the requirement is always exactly
/// "one battle" for every persona that has this flag.</summary>
public class WarbandDramatisPersonaCooldownEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarbandId { get; set; }

    public int DramatisPersonaId { get; set; }
}
