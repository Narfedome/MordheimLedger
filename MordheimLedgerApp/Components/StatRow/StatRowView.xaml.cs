using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Extensions;

namespace MordheimLedgerApp.Components;

public partial class StatRowView : ContentView
{
    // WarriorArchetype/Animal/Warrior - la seule contrainte est d'exposer WeaponSkill/BallisticSkill/
    // Strength/Toughness/Wounds/Initiative/Attacks/Leadership sous ces noms (déjà le cas des 3).
    public static readonly BindableProperty ItemProperty =
        BindableProperty.Create(nameof(Item), typeof(object), typeof(StatRowView), propertyChanged: (b, _, _) => ((StatRowView)b).OnItemChanged());

    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    // À fournir explicitement par l'appelant (ex. Item.MovementDisplay pour WarriorArchetype/Warrior,
    // Item.Movement pour Animal) - pas de nom de propriété uniforme à binder faiblement ici, voir le
    // commentaire dans StatRowView.xaml.
    public static readonly BindableProperty MovementTextProperty =
        BindableProperty.Create(nameof(MovementText), typeof(string), typeof(StatRowView), string.Empty, BindingMode.TwoWay);

    public string MovementText
    {
        get => (string)GetValue(MovementTextProperty);
        set => SetValue(MovementTextProperty, value);
    }

    // Bascule Label (lecture seule, tous les appelants existants - WarbandDetailPage/AnimalDetailDialog/
    // WarriorArchetypeDetailDialog) / Entry (édition - AnimalEditDialog/WarriorArchetypeEditDialog) par
    // colonne, même Grid/mêmes tailles de police dans les deux cas - un seul composant plutôt qu'une
    // grille dupliquée par dialog d'édition.
    public static readonly BindableProperty IsEditableProperty =
        BindableProperty.Create(nameof(IsEditable), typeof(bool), typeof(StatRowView), false, propertyChanged: (b, _, _) => ((StatRowView)b).OnIsEditableChanged());

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    // Défauts calés sur les dialogs récap (Animal/WarriorArchetypeDetailDialog) - WarbandDetailPage
    // (une ligne par guerrier dans un roster qui peut être long) les resserre à 10/13 pour rester dense.
    public static readonly BindableProperty AbbrFontSizeProperty =
        BindableProperty.Create(nameof(AbbrFontSize), typeof(double), typeof(StatRowView), 12.0);

    public double AbbrFontSize
    {
        get => (double)GetValue(AbbrFontSizeProperty);
        set => SetValue(AbbrFontSizeProperty, value);
    }

    public static readonly BindableProperty ValueFontSizeProperty =
        BindableProperty.Create(nameof(ValueFontSize), typeof(double), typeof(StatRowView), 14.0);

    public double ValueFontSize
    {
        get => (double)GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    // Colonnes de stat après Mouvement, dans l'ordre de la feuille : chemin de binding sur Item + clé de
    // l'abréviation localisée.
    private static readonly (string Path, string AbbrKey)[] Stats =
    [
        (nameof(Warrior.WeaponSkill), "StatWeaponSkillAbbr"),
        (nameof(Warrior.BallisticSkill), "StatBallisticSkillAbbr"),
        (nameof(Warrior.Strength), "StatStrengthAbbr"),
        (nameof(Warrior.Toughness), "StatToughnessAbbr"),
        (nameof(Warrior.Wounds), "StatWoundsAbbr"),
        (nameof(Warrior.Initiative), "StatInitiativeAbbr"),
        (nameof(Warrior.Attacks), "StatAttacksAbbr"),
        (nameof(Warrior.Leadership), "StatLeadershipAbbr"),
    ];

    private Grid? _grid;

    // Valeurs en lecture seule, Mouvement en tête - recolorées par ApplyValueColors.
    private readonly List<Label> _valueLabels = new();

    public StatRowView()
    {
        InitializeComponent();
    }

    // Construite au premier rattachement plutôt qu'au constructeur : IsEditable n'est connu qu'une fois
    // les attributs XAML de l'appelant appliqués - ne construire que la variante utile est tout l'intérêt
    // (voir StatRowView.xaml).
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (_grid is null && Parent is not null) Build();
    }

    private void OnIsEditableChanged()
    {
        if (_grid is not null) Build();
    }

    private void OnItemChanged()
    {
        if (_grid is null) return;
        _grid.BindingContext = Item;
        ApplyValueColors();
    }

    private void Build()
    {
        _valueLabels.Clear();
        // Grille 17 colonnes (9 stats + 8 fines colonnes séparatrices) x 3 lignes (Abrégé / filet
        // horizontal / Valeur), pour un vrai rendu "tableau" en lecture seule (demande explicite -
        // comparé à 3 pistes visuelles, celle-ci choisie). Colonne séparatrice plus large (8) que le filet
        // lui-même (1, centré dedans) pour laisser un peu d'air de chaque côté. Pas de filets en
        // édition : un formulaire à cases Entry n'a pas besoin du même quadrillage.
        var grid = new Grid
        {
            BindingContext = Item,
            RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(1), new RowDefinition(GridLength.Auto)),
            ColumnSpacing = 0,
            RowSpacing = 4,
        };
        for (var column = 0; column < 17; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(column % 2 == 0 ? GridLength.Star : new GridLength(8)));

        if (!IsEditable)
        {
            var horizontalRule = new BoxView { HeightRequest = 1 };
            horizontalRule.SetDynamicResource(BoxView.ColorProperty, "AppBorder");
            grid.Add(horizontalRule, 0, 1);
            Grid.SetColumnSpan(horizontalRule, 17);
            for (var column = 1; column < 17; column += 2)
            {
                var verticalRule = new BoxView { WidthRequest = 1, HorizontalOptions = LayoutOptions.Center };
                verticalRule.SetDynamicResource(BoxView.ColorProperty, "AppBorder");
                grid.Add(verticalRule, column, 0);
                Grid.SetRowSpan(verticalRule, 3);
            }
        }

        AddColumn(grid, 0, "StatMovementAbbr", new Binding(nameof(MovementText), source: this), isNumeric: false);
        for (var i = 0; i < Stats.Length; i++)
            AddColumn(grid, (i + 1) * 2, Stats[i].AbbrKey, new Binding(Stats[i].Path), isNumeric: true);

        _grid = grid;
        Content = grid;
        ApplyValueColors();
    }

    private void AddColumn(Grid grid, int column, string abbrKey, Binding valueBinding, bool isNumeric)
    {
        var abbr = new Label { HorizontalTextAlignment = TextAlignment.Center };
        abbr.SetBinding(Label.TextProperty, new Binding(nameof(LocalizedString.Value), source: new LocalizedString(abbrKey)));
        abbr.SetBinding(Label.FontSizeProperty, new Binding(nameof(AbbrFontSize), source: this));
        abbr.SetDynamicResource(Label.TextColorProperty, "AppTextMuted");
        grid.Add(abbr, column, 0);

        View value;
        if (IsEditable)
        {
            var entry = new Entry { HorizontalTextAlignment = TextAlignment.Center };
            if (isNumeric) entry.Keyboard = Keyboard.Numeric;
            entry.SetBinding(Entry.TextProperty, valueBinding);
            entry.SetBinding(Entry.FontSizeProperty, new Binding(nameof(ValueFontSize), source: this));
            value = entry;
        }
        else
        {
            var label = new Label { FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
            label.SetBinding(Label.TextProperty, valueBinding);
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(ValueFontSize), source: this));
            _valueLabels.Add(label);
            value = label;
        }
        grid.Add(value, column, 2);
    }

    // Item = Warrior uniquement (Animal/WarriorArchetype n'ont pas de profil de départ) : colore la
    // valeur si elle a bougé depuis le recrutement (Warrior.StartingMovement etc.) - AppSuccess (vert
    // fixe, pas AppAccent : reste vert quelle que soit la palette active, voir sa doc dans Colors.xaml)
    // pour un gain, AppDanger (rouge fixe, déjà utilisé pour les chips Malade/Mort) pour une perte, ex.
    // Blessure Grave. Demande explicite de l'utilisateur (2026-08-25). Warrior n'est pas observable :
    // calculé une fois par Item, comme le faisaient les anciens DataTrigger.
    private void ApplyValueColors()
    {
        if (_valueLabels.Count == 0) return;
        var warrior = Item as Warrior;
        (bool Increased, bool Decreased)[] changes =
        [
            (warrior?.MovementIncreased ?? false, warrior?.MovementDecreased ?? false),
            (warrior?.WeaponSkillIncreased ?? false, warrior?.WeaponSkillDecreased ?? false),
            (warrior?.BallisticSkillIncreased ?? false, warrior?.BallisticSkillDecreased ?? false),
            (warrior?.StrengthIncreased ?? false, warrior?.StrengthDecreased ?? false),
            (warrior?.ToughnessIncreased ?? false, warrior?.ToughnessDecreased ?? false),
            (warrior?.WoundsIncreased ?? false, warrior?.WoundsDecreased ?? false),
            (warrior?.InitiativeIncreased ?? false, warrior?.InitiativeDecreased ?? false),
            (warrior?.AttacksIncreased ?? false, warrior?.AttacksDecreased ?? false),
            (warrior?.LeadershipIncreased ?? false, warrior?.LeadershipDecreased ?? false),
        ];
        for (var i = 0; i < _valueLabels.Count; i++)
            _valueLabels[i].SetDynamicResource(Label.TextColorProperty,
                changes[i].Decreased ? "AppDanger" : changes[i].Increased ? "AppSuccess" : "AppText");
    }
}
