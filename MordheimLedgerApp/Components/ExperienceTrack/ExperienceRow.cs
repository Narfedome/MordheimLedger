namespace MordheimLedgerApp.Components.ExperienceTrack;

/// <summary>A fixed-length slice of boxes forced onto one visual line — see ExperienceTrackView.Rebuild.</summary>
public class ExperienceRow
{
    public List<ExperienceBox> Boxes { get; init; } = new();
}
