namespace MordheimLedgerApp.Features.Library.Mutations;

public partial class MutationView : ContentView
{
    public MutationView()
    {
        InitializeComponent();
    }

    /// <summary>True (default): normal Add/Edit/Delete row. False: Confirm/Cancel row for picker mode.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(MutationView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
