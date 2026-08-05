using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Join row between a Warrior and a Spell — see Models.WarriorSpell.</summary>
public class WarriorSpellEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WarriorId { get; set; }

    public int SpellId { get; set; }
}
