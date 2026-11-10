using System.Windows.Input;

namespace MordheimLedgerApp.Components.FormPage;

/// <summary>Mise en page commune des formulaires plein écran (voir FormPageLayout.xaml) : le contenu posé
/// entre les balises va dans la zone qui défile (Body), le reste se règle par propriétés. Pas de style
/// propre à un formulaire ici : chaque formulaire range lui-même son contenu en cartes
/// (SectionCardBorderStyle).</summary>
[ContentProperty(nameof(Body))]
public partial class FormPageLayout : ContentView
{
    public FormPageLayout()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FormPageLayout), string.Empty);
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(FormPageLayout), string.Empty);
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>« Étape 2/6 » en haut à droite - vide pour un formulaire d'un seul tenant.</summary>
    public static readonly BindableProperty StepTextProperty =
        BindableProperty.Create(nameof(StepText), typeof(string), typeof(FormPageLayout), string.Empty);
    public string StepText { get => (string)GetValue(StepTextProperty); set => SetValue(StepTextProperty, value); }

    /// <summary>Avancement 0..1 de la barre sous l'en-tête - négatif (défaut) : pas de barre.</summary>
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(FormPageLayout), -1d,
            propertyChanged: (b, _, _) => ((FormPageLayout)b).OnPropertyChanged(nameof(HasProgress)));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool HasProgress => Progress >= 0;

    /// <summary>✕ de l'en-tête : même chemin que le retour Android (CancelCommand du ViewModel, qui demande
    /// confirmation seulement s'il y a des modifications).</summary>
    public static readonly BindableProperty CloseCommandProperty =
        BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(FormPageLayout));
    public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }

    /// <summary>Boutons icône facultatifs à droite du titre (ex. Supprimer).</summary>
    public static readonly BindableProperty HeaderActionsProperty =
        BindableProperty.Create(nameof(HeaderActions), typeof(View), typeof(FormPageLayout),
            propertyChanged: (b, _, n) => ((FormPageLayout)b).HeaderActionsHost.Content = n as View);
    public View? HeaderActions { get => (View?)GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }

    /// <summary>Zone fixe entre l'en-tête et le contenu (onglets d'un formulaire à onglets).</summary>
    public static readonly BindableProperty TopContentProperty =
        BindableProperty.Create(nameof(TopContent), typeof(View), typeof(FormPageLayout),
            propertyChanged: (b, _, n) =>
            {
                var layout = (FormPageLayout)b;
                layout.TopContentHost.Content = n as View;
                layout.OnPropertyChanged(nameof(HasTopContent));
            });
    public View? TopContent { get => (View?)GetValue(TopContentProperty); set => SetValue(TopContentProperty, value); }
    public bool HasTopContent => TopContent is not null;

    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(nameof(Body), typeof(View), typeof(FormPageLayout),
            propertyChanged: (b, _, n) => ((FormPageLayout)b).BodyHost.Content = n as View);
    public View? Body { get => (View?)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    public static readonly BindableProperty SecondaryTextProperty =
        BindableProperty.Create(nameof(SecondaryText), typeof(string), typeof(FormPageLayout), string.Empty);
    public string SecondaryText { get => (string)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }

    public static readonly BindableProperty SecondaryCommandProperty =
        BindableProperty.Create(nameof(SecondaryCommand), typeof(ICommand), typeof(FormPageLayout));
    public ICommand? SecondaryCommand { get => (ICommand?)GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }

    /// <summary>Précédent grisé à la première étape d'un assistant.</summary>
    public static readonly BindableProperty SecondaryEnabledProperty =
        BindableProperty.Create(nameof(SecondaryEnabled), typeof(bool), typeof(FormPageLayout), true);
    public bool SecondaryEnabled { get => (bool)GetValue(SecondaryEnabledProperty); set => SetValue(SecondaryEnabledProperty, value); }

    public static readonly BindableProperty PrimaryTextProperty =
        BindableProperty.Create(nameof(PrimaryText), typeof(string), typeof(FormPageLayout), string.Empty);
    public string PrimaryText { get => (string)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }

    public static readonly BindableProperty PrimaryCommandProperty =
        BindableProperty.Create(nameof(PrimaryCommand), typeof(ICommand), typeof(FormPageLayout));
    public ICommand? PrimaryCommand { get => (ICommand?)GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }

    /// <summary>Libellé « Étape n/N » et pied à 2 boutons, exposés pour y poser les cibles des visites
    /// guidées (OnboardingTargetBehavior) depuis le formulaire qui utilise ce gabarit.</summary>
    public View StepTextView => StepTextLabel;
    public View FooterView => Footer;

    /// <summary>Remonte en haut du contenu (changement d'étape d'un assistant).</summary>
    public Task ScrollToTopAsync() => BodyScroll.ScrollToAsync(0, 0, false);
}
