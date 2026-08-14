using System.Windows.Input;

namespace MordheimLedgerApp.Components.PickerSelectorLayout;

/// <summary>Coque commune aux ~10 pages sélecteur (EquipmentItemSelectorPage, SpecialRuleSelectorPage,
/// SkillSelectorPage...) : en-tête maison (DetailPageHeaderView, avec le filigrane WatermarkedLayout
/// intégré) au lieu de compter sur la barre de titre native d'un NavigationPage. Ces pages sont
/// désormais poussées nues (pas enveloppées dans un NavigationPage, voir SpecialRulePickerService pour
/// le pourquoi) - un NavigationPage déjà au sommet de la pile modale absorbait le push modal suivant
/// (une dialog imbriquée ouverte depuis le sélecteur) au lieu de l'empiler correctement, la nouvelle page
/// héritant du chrome du NavigationPage à la place (flèche retour intempestive sur Android, mauvaise
/// page affichée sur Windows - reproduit et confirmé via crash.log avant ce correctif). Centralisé après
/// le premier test sur le sélecteur de Règles Spéciales, pour éviter de dupliquer identiquement
/// Shell.NavBarIsVisible/BackButtonBehavior/Grid/DetailPageHeaderView dans les 10 pages.</summary>
public partial class PickerSelectorLayout : ContentView
{
    public PickerSelectorLayout()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PickerSelectorLayout), default(string));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Ferme le picker, même comportement qu'un retour natif - à lier au CancelCommand du
    /// ViewModel de la page hôte.</summary>
    public static readonly BindableProperty BackCommandProperty =
        BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(PickerSelectorLayout));

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }
}
