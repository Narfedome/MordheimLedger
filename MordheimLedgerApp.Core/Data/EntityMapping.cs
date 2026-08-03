using MordheimLedgerApp.Core.Data.Entities;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// Entity &lt;-&gt; model conversions, centralized: a field added to a model only needs mapping here
/// (see DmTools' EntityMapping for the rationale — duplicated mapping blocks in data services risk
/// silent omissions on new fields), and the round-trip is covered by unit tests.
/// </summary>
public static class EntityMapping
{
    public static Warband ToModel(this WarbandEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        WarbandType = e.WarbandType,
        Treasury = e.Treasury,
        IsCustom = e.IsCustom,
        Notes = e.Notes
    };

    public static WarbandEntity ToEntity(this Warband m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        WarbandType = m.WarbandType,
        Treasury = m.Treasury,
        IsCustom = m.IsCustom,
        Notes = m.Notes
    };
}
