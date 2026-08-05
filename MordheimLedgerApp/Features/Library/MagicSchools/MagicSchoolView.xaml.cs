namespace MordheimLedgerApp.Features.Library.MagicSchools;

public partial class MagicSchoolView : ContentView
{
    public MagicSchoolView()
    {
        InitializeComponent();
    }

    /// <summary>True (default): normal Add/Edit/Delete row. False: Confirm/Cancel row for picker mode.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(MagicSchoolView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
