namespace MordheimLedgerApp.Components.FormPage;

/// <summary>Page Shell qui héberge UN formulaire (voir FormNavigator) : barre de navigation native masquée
/// (le FormPageLayout porte son propre en-tête, comme les pages de détail - voir DetailPageHeaderView), barre
/// d'onglets laissée visible (décision de l'utilisateur, 2026-11-10).</summary>
public sealed class FormPage : ContentPage
{
    private readonly Action _onBack;

    public FormPage(View content, Action onBack)
    {
        _onBack = onBack;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false });
        SetDynamicResource(BackgroundColorProperty, "AppBackground");
        Content = content;
    }

    /// <summary>Retour Android : même chemin que le ✕ (Annuler, avec confirmation s'il y a des
    /// modifications) plutôt que de laisser la plateforme dépiler la page d'office.</summary>
    protected override bool OnBackButtonPressed()
    {
        _onBack();
        return true;
    }
}
