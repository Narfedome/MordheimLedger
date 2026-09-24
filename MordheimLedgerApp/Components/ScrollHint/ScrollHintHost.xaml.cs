namespace MordheimLedgerApp.Components;

/// <summary>Hôte générique "zone défilante + indice ⌄" (2026-09-24, retour utilisateur - sur mobile la
/// barre de défilement native est à peine visible, "tous les endroits où il y a une scrollbar, on met
/// l'indicateur") : envelopper n'importe quel ScrollView vertical ou CollectionView suffit, le badge est
/// piloté par VerticalScrollHint ou CollectionScrollHint selon le type du contenu.</summary>
public partial class ScrollHintHost : ContentView
{
    private View? _downHint;
    private View? _attachedContent;

    public ScrollHintHost()
    {
        InitializeComponent();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _downHint ??= GetTemplateChild("DownHint") as View;
        TryAttach();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Content)) TryAttach();
    }

    private void TryAttach()
    {
        if (_downHint is null || Content is null || ReferenceEquals(Content, _attachedContent)) return;
        _attachedContent = Content;

        switch (Content)
        {
            case CollectionView collectionView:
                CollectionScrollHint.Attach(collectionView, _downHint);
                break;
            case ScrollView scrollView:
                VerticalScrollHint.Attach(scrollView, _downHint);
                break;
        }
    }
}
