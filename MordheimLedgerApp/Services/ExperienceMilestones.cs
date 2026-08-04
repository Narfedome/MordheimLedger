namespace MordheimLedgerApp.Services;

/// <summary>
/// Milestone box positions on the printed warband sheet's XP track - shared by
/// Components/ExperienceTrack/ExperienceTrackView (renders them as the thicker-bordered boxes) and
/// the End of Game wizard (detects when a warrior's XP gain crosses one, to offer the Advance roll).
/// Spacing mirrors the printed sheet exactly, see CLAUDE.md § Roster de WarbandDetailPage.
/// </summary>
public static class ExperienceMilestones
{
    public static List<int> HeroMilestones()
    {
        (int gap, int repeats)[] tiers = [(1, 4), (2, 4), (3, 4), (4, 3), (5, 3), (6, 3)];
        var positions = new List<int>();
        var pos = 0;
        foreach (var (gap, repeats) in tiers)
        {
            var unit = gap + 1;
            for (var r = 0; r < repeats; r++)
            {
                pos += unit;
                positions.Add(pos);
            }
        }
        return positions;
    }

    public static List<int> HenchmanMilestones(int max)
    {
        var positions = new List<int>();
        var pos = 0;
        var gap = 1;
        while (true)
        {
            pos += gap + 1;
            if (pos > max) break;
            positions.Add(pos);
            gap++;
        }
        return positions;
    }

    /// <summary>Number of milestone boxes strictly after `from` and at or before `to` - a warrior can
    /// cross more than one at once (e.g. survival XP + Challenger bonus stacked), each requiring its
    /// own separate 2D6 progression roll (see EndOfGameDialogViewModel's Progression step).</summary>
    public static int MilestonesCrossedCount(bool isHero, int from, int to)
    {
        if (to <= from) return 0;
        var milestones = isHero ? HeroMilestones() : HenchmanMilestones(to);
        return milestones.Count(m => m > from && m <= to);
    }
}
