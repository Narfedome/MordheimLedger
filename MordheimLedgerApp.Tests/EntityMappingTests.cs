using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Models;

namespace MordheimLedgerApp.Tests;

public class EntityMappingTests
{
    [Fact]
    public void Warband_RoundTrips_ThroughEntity()
    {
        var warband = new Warband
        {
            Id = 1,
            Name = "The Bleeding Roses",
            WarbandType = "Reiklander Mercenaries",
            Treasury = 250,
            IsCustom = true,
            Notes = "House-ruled starting gold"
        };

        var roundTripped = warband.ToEntity().ToModel();

        Assert.Equal(warband.Id, roundTripped.Id);
        Assert.Equal(warband.Name, roundTripped.Name);
        Assert.Equal(warband.WarbandType, roundTripped.WarbandType);
        Assert.Equal(warband.Treasury, roundTripped.Treasury);
        Assert.Equal(warband.IsCustom, roundTripped.IsCustom);
        Assert.Equal(warband.Notes, roundTripped.Notes);
    }
}
