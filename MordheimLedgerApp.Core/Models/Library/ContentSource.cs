namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>
/// Provenance of a catalog entry, shown as a badge in the UI. Official rows are seeded at first
/// launch; editing one flips it to Modified (never silently reverts to Official) — see the roadmap's
/// "no closed content system" decision. Custom rows are created from scratch by the player.
/// </summary>
public enum ContentSource
{
    Official = 0,
    Modified = 1,
    Custom = 2
}
