using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Entrée Modifiée par le joueur dont la version officielle a changé à une synchro : en attente du
/// choix du joueur (garder sa version ou reprendre l'officielle). La version officielle se relit dans la
/// seed.db3 embarquée au moment du choix.</summary>
public class ContentConflictEntity
{
    [PrimaryKey]
    public string OfficialId { get; set; } = string.Empty;

    /// <summary>Nom de la table (ex. "EquipmentItemEntity").</summary>
    public string TableName { get; set; } = string.Empty;
}
