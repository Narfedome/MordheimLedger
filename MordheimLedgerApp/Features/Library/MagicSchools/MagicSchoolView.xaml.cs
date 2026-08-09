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

    /// <summary>False (default) : liste à plat façon WarbandListPage, utilisée par MagicSchoolListPage
    /// ("Gérer les écoles de magie" depuis l'onglet Sorts - pas de grille de tuiles à filtrer là-bas).
    /// True : grille de tuiles façon SpecialRuleView/MutationView, utilisée par MagicSchoolSelectorPage
    /// (même présentation que les autres pickers de bande).</summary>
    public static readonly BindableProperty UseTileLayoutProperty =
        BindableProperty.Create(nameof(UseTileLayout), typeof(bool), typeof(MagicSchoolView), false);

    public bool UseTileLayout
    {
        get => (bool)GetValue(UseTileLayoutProperty);
        set => SetValue(UseTileLayoutProperty, value);
    }
}
