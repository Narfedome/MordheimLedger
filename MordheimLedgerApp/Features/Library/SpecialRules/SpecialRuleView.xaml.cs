namespace MordheimLedgerApp.Features.Library.SpecialRules;

public partial class SpecialRuleView : ContentView
{
    public SpecialRuleView()
    {
        InitializeComponent();
    }

    /// <summary>True (default): normal Add/Edit/Delete row. False: Confirm/Cancel row for picker mode.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(SpecialRuleView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
