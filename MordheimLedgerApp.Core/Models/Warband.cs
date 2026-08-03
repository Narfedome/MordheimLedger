namespace MordheimLedgerApp.Core.Models;

/// <summary>
/// A player's warband. WarbandType references a catalog entry (not modeled yet — see roadmap V1)
/// rather than being an enum, so custom/house-ruled warband types work the same way as official ones.
/// </summary>
public class Warband
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WarbandType { get; set; } = string.Empty;
    public int Treasury { get; set; }
    public bool IsCustom { get; set; }
    public string? Notes { get; set; }
}
