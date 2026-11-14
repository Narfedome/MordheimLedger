using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Identifiant d'échange (GUID) d'une bande ou d'une entrée Personnalisée du catalogue, attribué à son premier
/// export et gardé par l'appareil qui l'importe : la même bande renvoyée après une partie, ou la même entrée retouchée,
/// est reconnue au lieu d'être dupliquée (voir Exchange/ExchangeService). Table à part plutôt qu'une colonne sur chaque
/// entité : les sauvegardes réécrivent une ligne entière depuis son modèle (LibraryService.SaveXxxAsync,
/// WarbandService.SaveWarbandAsync) et effaceraient une colonne que le modèle ne porte pas. Une ligne supprimée laisse
/// son identifiant ici, sans effet : SQLite ne réattribue jamais un Id AUTOINCREMENT.</summary>
public class ShareIdentityEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Nom de la table (type d'entité, ex. « EquipmentItemEntity », « WarbandEntity »).</summary>
    [Indexed(Name = "ShareIdentity_Row", Order = 1, Unique = true)]
    public string TableName { get; set; } = string.Empty;

    [Indexed(Name = "ShareIdentity_Row", Order = 2, Unique = true)]
    public int LocalId { get; set; }

    [Indexed]
    public string ShareId { get; set; } = string.Empty;
}
