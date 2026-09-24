using MordheimLedgerApp.Core.Rules;

namespace MordheimLedgerApp.Components.ExperienceTrack;

/// <summary>
/// Fixed-size box track (like the printed warband sheet — all boxes are always shown, filled or
/// not) with a thicker gold border on milestone boxes. Milestone spacing is not evenly spaced: for
/// Heroes the gap between milestones widens every few repeats (1x4, 2x4, 3x4, 4x3, 5x3, 6x3 boxes
/// between them), for Henchmen the gap simply widens by one box each time (2, 5, 9, 14, 20, 27...).
/// This mirrors the printed sheet's layout exactly — it is not a rule the app interprets or enforces.
///
/// Drawn by a single GraphicsView (2026-09-24) rather than one Border per box: a Hero card used to
/// create 90 native Borders (each with two DataTriggers), rebuilt three times per card as the
/// constructor/Experience/IsHero each triggered a full rebuild - the dominant cost of opening a
/// warband roster.
/// </summary>
public partial class ExperienceTrackView : ContentView
{
    private const int HeroBoxCount = 90;
    private const int HeroRowSize = 30;
    private const int HenchmanBoxCount = 14;
    private const float BoxSpacing = 1;

    private readonly ExperienceTrackDrawable _drawable = new();

    public ExperienceTrackView()
    {
        InitializeComponent();
        Track.Drawable = _drawable;
        _drawable.BoxSize = DeviceInfo.Current.Idiom == DeviceIdiom.Desktop ? 12 : 10;

        // Couleurs de thème suivies en direct (changement de palette/clair-sombre) - mêmes ressources
        // que l'ancien Border par case.
        SetDynamicResource(BoxBorderColorProperty, "AppBorder");
        SetDynamicResource(FilledColorProperty, "AppAccent");
        SetDynamicResource(MilestoneColorProperty, "AppMilestone");

        Refresh();
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

    public static readonly BindableProperty BoxBorderColorProperty =
        BindableProperty.Create(nameof(BoxBorderColor), typeof(Color), typeof(ExperienceTrackView), Colors.Gray, propertyChanged: OnTrackChanged);

    public Color BoxBorderColor
    {
        get => (Color)GetValue(BoxBorderColorProperty);
        set => SetValue(BoxBorderColorProperty, value);
    }

    public static readonly BindableProperty FilledColorProperty =
        BindableProperty.Create(nameof(FilledColor), typeof(Color), typeof(ExperienceTrackView), Colors.Green, propertyChanged: OnTrackChanged);

    public Color FilledColor
    {
        get => (Color)GetValue(FilledColorProperty);
        set => SetValue(FilledColorProperty, value);
    }

    public static readonly BindableProperty MilestoneColorProperty =
        BindableProperty.Create(nameof(MilestoneColor), typeof(Color), typeof(ExperienceTrackView), Colors.Goldenrod, propertyChanged: OnTrackChanged);

    public Color MilestoneColor
    {
        get => (Color)GetValue(MilestoneColorProperty);
        set => SetValue(MilestoneColorProperty, value);
    }

    private static void OnTrackChanged(BindableObject bindable, object oldValue, object newValue)
        => ((ExperienceTrackView)bindable).Refresh();

    /// <summary>Cheap (no view creation): recomputes the drawable's state, resizes the GraphicsView to
    /// the track's exact footprint and schedules a redraw.</summary>
    private void Refresh()
    {
        // Appelé depuis le constructeur via SetDynamicResource, avant que Track n'existe forcément.
        if (Track is null) return;

        var total = IsHero ? HeroBoxCount : HenchmanBoxCount;
        var rowSize = IsHero ? HeroRowSize : HenchmanBoxCount;

        _drawable.Total = total;
        _drawable.RowSize = rowSize;
        _drawable.Experience = Experience;
        _drawable.Milestones = IsHero ? ExperienceMilestones.HeroMilestones() : ExperienceMilestones.HenchmanMilestones(total);
        _drawable.BorderColor = BoxBorderColor;
        _drawable.FilledColor = FilledColor;
        _drawable.MilestoneColor = MilestoneColor;
        _drawable.Spacing = BoxSpacing;

        var rows = (total + rowSize - 1) / rowSize;
        Track.WidthRequest = rowSize * _drawable.BoxSize + (rowSize - 1) * BoxSpacing;
        Track.HeightRequest = rows * _drawable.BoxSize + (rows - 1) * BoxSpacing;
        Track.Invalidate();
    }
}
