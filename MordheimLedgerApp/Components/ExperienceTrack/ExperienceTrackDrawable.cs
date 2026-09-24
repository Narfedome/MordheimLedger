namespace MordheimLedgerApp.Components.ExperienceTrack;

/// <summary>Draws an ExperienceTrackView's boxes - state pushed by ExperienceTrackView.Refresh. Mirrors
/// the old per-box Border rendering: background filled with FilledColor for boxes up to Experience,
/// then an inner stroke (1px BorderColor, or 3px MilestoneColor on milestone boxes).</summary>
internal sealed class ExperienceTrackDrawable : IDrawable
{
    public int Total { get; set; }
    public int RowSize { get; set; } = 1;
    public int Experience { get; set; }
    public IReadOnlyCollection<int> Milestones { get; set; } = Array.Empty<int>();
    public float BoxSize { get; set; } = 10;
    public float Spacing { get; set; } = 1;
    public Color BorderColor { get; set; } = Colors.Gray;
    public Color FilledColor { get; set; } = Colors.Green;
    public Color MilestoneColor { get; set; } = Colors.Goldenrod;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        for (var i = 1; i <= Total; i++)
        {
            var index = i - 1;
            var x = index % RowSize * (BoxSize + Spacing);
            var y = index / RowSize * (BoxSize + Spacing);

            if (i <= Experience)
            {
                canvas.FillColor = FilledColor;
                canvas.FillRectangle(x, y, BoxSize, BoxSize);
            }

            var isMilestone = Milestones.Contains(i);
            var thickness = isMilestone ? 3f : 1f;
            canvas.StrokeColor = isMilestone ? MilestoneColor : BorderColor;
            canvas.StrokeSize = thickness;
            // Trait à l'intérieur de la case (comme Border) : centré sur un rectangle rentré de la moitié
            // de son épaisseur.
            var inset = thickness / 2;
            canvas.DrawRectangle(x + inset, y + inset, BoxSize - thickness, BoxSize - thickness);
        }
    }
}
