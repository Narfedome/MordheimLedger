using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>Clé/valeur décrivant le contenu officiel présent dans la base (version, empreinte des JSON) -
/// voir SeedContent. Absente d'une base installée avant elle : lue comme version 0.</summary>
public class ContentMetaEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
