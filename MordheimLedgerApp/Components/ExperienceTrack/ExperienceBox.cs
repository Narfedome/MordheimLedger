namespace MordheimLedgerApp.Components.ExperienceTrack;

/// <summary>One square of an ExperienceTrackView.</summary>
public class ExperienceBox
{
    public bool IsFilled { get; init; }

    /// <summary>Milestone boxes get a thicker gold border — a purely visual reading aid copied
    /// from the printed warband sheet, not a rule the app enforces (see ExperienceTrackView).</summary>
    public bool IsMilestone { get; init; }
}
