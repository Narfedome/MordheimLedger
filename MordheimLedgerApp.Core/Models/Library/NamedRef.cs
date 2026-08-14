namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// Lightweight Id+Name projection for chip lists that only ever display a name and open a full detail
/// on tap (e.g. WarbandArchetypeDetailDialog's Équipement tab) - avoids resolving member items/nested
/// join tables for entries that stay unopened, see LibraryService.GetEquipmentListNamesAsync.
/// </summary>
public class NamedRef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
