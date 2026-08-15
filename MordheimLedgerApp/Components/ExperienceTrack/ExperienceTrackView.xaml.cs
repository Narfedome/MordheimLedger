using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Components.ExperienceTrack;

/// <summary>
/// Fixed-size box track (like the printed warband sheet — all boxes are always shown, filled or
/// not) with a thicker gold border on milestone boxes. Milestone spacing is not evenly spaced: for
/// Heroes the gap between milestones widens every few repeats (1x4, 2x4, 3x4, 4x3, 5x3, 6x3 boxes
/// between them), for Henchmen the gap simply widens by one box each time (2, 5, 9, 14, 20, 27...).
/// This mirrors the printed sheet's layout exactly — it is not a rule the app interprets or enforces.
/// </summary>
public partial class ExperienceTrackView : ContentView
{
    private const int HeroBoxCount = 90;
    private const int HeroRowSize = 30;
    private const int HenchmanBoxCount = 14;

    public ExperienceTrackView()
    {
        InitializeComponent();
        Rebuild();
    }

    public static readonly BindableProperty ExperienceProperty =
        BindableProperty.Create(nameof(Experience), typeof(int), typeof(ExperienceTrackView), 0, propertyChanged: OnTrackChanged);

    public int Experience
    {
        get => (int)GetValue(ExperienceProperty);
        set => SetValue(ExperienceProperty, value);
    }

    public static readonly BindableProperty IsHeroProperty =
        BindableProperty.Create(nameof(IsHero), typeof(bool), typeof(ExperienceTrackView), false, propertyChanged: OnTrackChanged);

    public bool IsHero
    {
        get => (bool)GetValue(IsHeroProperty);
        set => SetValue(IsHeroProperty, value);
    }

    private static void OnTrackChanged(BindableObject bindable, object oldValue, object newValue)
        => ((ExperienceTrackView)bindable).Rebuild();

    private void Rebuild()
    {
        var total = IsHero ? HeroBoxCount : HenchmanBoxCount;
        var rowSize = IsHero ? HeroRowSize : HenchmanBoxCount;
        var milestones = IsHero ? ExperienceMilestones.HeroMilestones() : ExperienceMilestones.HenchmanMilestones(total);

        var boxes = new List<ExperienceBox>(total);
        for (var i = 1; i <= total; i++)
            boxes.Add(new ExperienceBox { IsFilled = i <= Experience, IsMilestone = milestones.Contains(i) });

        // Chunk into fixed-size rows so the layout stays 30-per-line regardless of the container's
        // width, instead of a FlexLayout Wrap that reflows differently depending on available space.
        var rows = boxes
            .Chunk(rowSize)
            .Select(chunk => new ExperienceRow { Boxes = chunk.ToList() })
            .ToList();

        BindableLayout.SetItemsSource(TrackLayout, rows);
    }
}
