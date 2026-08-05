namespace MordheimLedgerApp.Features.Library.WarbandArchetypes;

public partial class WarbandArchetypeView : ContentView
{
    public WarbandArchetypeView()
    {
        InitializeComponent();
    }

    /// <summary>True (default): normal Add/Edit/Delete/Guerriers row. False: Confirm/Cancel row for
    /// picker mode.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(WarbandArchetypeView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
