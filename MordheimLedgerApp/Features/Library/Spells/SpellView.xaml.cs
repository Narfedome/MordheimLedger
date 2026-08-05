namespace MordheimLedgerApp.Features.Library.Spells;

public partial class SpellView : ContentView
{
    public SpellView()
    {
        InitializeComponent();
    }

    /// <summary>True (default): normal Add/Edit/Delete row. False: Confirm/Cancel row for picker mode.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(SpellView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
