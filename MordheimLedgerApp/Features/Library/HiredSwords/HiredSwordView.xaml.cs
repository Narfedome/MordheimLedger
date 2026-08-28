namespace MordheimLedgerApp.Features.Library.HiredSwords;

public partial class HiredSwordView : ContentView
{
    public HiredSwordView()
    {
        InitializeComponent();
    }

    /// <summary>True (default) : rangée Ajouter/Renommer/Supprimer normale (onglet Codex). False :
    /// rangée Confirmer/Annuler pour le mode picker (HiredSwordSelectorPage) - même bascule que
    /// MagicSchoolView.IsCrud.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(HiredSwordView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
