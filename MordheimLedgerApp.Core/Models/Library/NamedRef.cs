namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// Lightweight Id+Name projection for chip lists that only ever display a name and open a full detail
/// on tap (e.g. WarbandArchetypeDetailDialog's Guerriers/Équipement tabs) - avoids resolving
/// Description/SpecialRules/nested join tables for entries that stay unopened, see
/// LibraryService.GetWarriorArchetypeNamesAsync/GetEquipmentListNamesAsync.
/// </summary>
public class NamedRef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
