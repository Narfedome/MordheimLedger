namespace MordheimLedgerApp.Components.StatStepper;

/// <summary>Labeled -/value/+ stepper for a small integer stat (see WarriorEditDialog) - increment/
/// decrement is handled internally, Value is TwoWay by default so it can bind straight to a plain
/// model property (e.g. Warrior.Movement) without the caller needing its own commands.</summary>
public partial class StatStepperView : ContentView
{
    public StatStepperView()
    {
        // Set before InitializeComponent(): the XAML's {x:Reference root} bindings on the +/-
        // FaIconButtonViews resolve while the visual tree is being built, and IncrementCommand/
        // DecrementCommand are plain properties (not BindableProperty) - if InitializeComponent ran
        // first, the binding would capture null and never update since nothing raises PropertyChanged
        // for them afterward.
        IncrementCommand = new Command(() => Value++);
        DecrementCommand = new Command(() => Value = Math.Max(Minimum, Value - 1));
        InitializeComponent();
    }

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(StatStepperView), default(string));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(int), typeof(StatStepperView), 0, BindingMode.TwoWay);

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(int), typeof(StatStepperView), 0);

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public Command IncrementCommand { get; }
    public Command DecrementCommand { get; }
}
