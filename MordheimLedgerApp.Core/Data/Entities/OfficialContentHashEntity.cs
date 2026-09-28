using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Empreinte de la version OFFICIELLE d'une entrée du catalogue, telle que reçue à la dernière
/// synchro (ou au seed). Sert à savoir si l'officiel a changé depuis, pour une entrée que le joueur a
/// modifiée - voir AppDatabase.SyncOfficialContentAsync.</summary>
public class OfficialContentHashEntity
{
    [PrimaryKey]
    public string OfficialId { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
}
