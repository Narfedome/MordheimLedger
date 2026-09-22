namespace MordheimLedgerApp.Features.Library.DramatisPersonae;

public partial class DramatisPersonaView : ContentView
{
    public DramatisPersonaView()
    {
        InitializeComponent();
    }

    /// <summary>True (default) : rangée Ajouter/Renommer/Supprimer normale (onglet Codex). False :
    /// rangée Confirmer/Annuler pour le mode picker (DramatisPersonaSelectorPage) - même bascule que
    /// HiredSwordView.IsCrud.</summary>
    public static readonly BindableProperty IsCrudProperty =
        BindableProperty.Create(nameof(IsCrud), typeof(bool), typeof(DramatisPersonaView), true);

    public bool IsCrud
    {
        get => (bool)GetValue(IsCrudProperty);
        set => SetValue(IsCrudProperty, value);
    }
}
