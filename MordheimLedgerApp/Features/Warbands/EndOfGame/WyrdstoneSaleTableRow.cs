namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>One row of the reference table shown under the Wyrdstone Sale step (see
/// EndOfGamePageViewModel.WyrdstoneSaleTableRows) - BucketN mirrors Core.Rules.WyrdstoneSaleTable's 6
/// warrior-count columns (1-3/4-6/7-9/10-12/13-15/16+), in order. HighlightedBucketNumber (0 = none,
/// else 1-6) singles out the ONE cell that matches both the player's current ShardsToSell choice (this
/// row) and the warband's current warrior-count bucket (this column) - user feedback 2026-08-28: "juste
/// la case de valeur concerné" (just the one relevant cell), not the whole row.</summary>
public sealed class WyrdstoneSaleTableRow
{
    public required string ShardsLabel { get; init; }
    public required int Bucket1 { get; init; }
    public required int Bucket2 { get; init; }
    public required int Bucket3 { get; init; }
    public required int Bucket4 { get; init; }
    public required int Bucket5 { get; init; }
    public required int Bucket6 { get; init; }
    public int HighlightedBucketNumber { get; init; }
}
