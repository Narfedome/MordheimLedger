using System.Collections;
using System.Collections.Specialized;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components;

/// <summary>
/// Liste de choix au style de l'appli, à la place du Picker natif : sur Android celui-ci ouvre une
/// boîte système hors thème (bouton « Cancel » en anglais). Mêmes propriétés liables que Picker pour un
/// remplacement direct (ItemsSource, SelectedItem, SelectedIndex, ItemDisplayBinding, Title) ; au tap,
/// ouvre l'ActionSheetDialog thémé par la même pile que les autres dialogs (DialogStack), sans
/// ViewModel dédié par champ.
///
/// Différence voulue avec Picker : quand ItemsSource change, la sélection est retrouvée par égalité
/// (les listes d'options de la Fin de partie sont recalculées à chaque lecture, records comparés par
/// valeur) et jamais remplacée d'office par l'élément au même index ni remise à null.
/// </summary>
public partial class SelectFieldView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(SelectFieldView), null,
            propertyChanged: (b, o, n) => ((SelectFieldView)b).OnItemsSourceChanged(o as IList, n as IList));

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SelectFieldView), null,
            BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SelectFieldView)b).OnSelectedItemChanged());

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SelectFieldView), -1,
            BindingMode.TwoWay, propertyChanged: (b, _, _) => ((SelectFieldView)b).OnSelectedIndexChanged());

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SelectFieldView), string.Empty,
            propertyChanged: (b, _, _) => ((SelectFieldView)b).UpdateDisplay());

    /// <summary>Texte affiché tant que rien n'est choisi, et titre de la liste ouverte.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private BindingBase? _itemDisplayBinding;

    /// <summary>Même rôle que Picker.ItemDisplayBinding (ex. {Binding DisplayName}) ; sans lui,
    /// ToString() de l'élément.</summary>
    public BindingBase? ItemDisplayBinding
    {
        get => _itemDisplayBinding;
        set
        {
            _itemDisplayBinding = value;
            UpdateDisplay();
        }
    }

    private bool _syncing;
    private bool _opening;

    public SelectFieldView()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
            Field.Opacity = IsEnabled ? 1 : 0.5;
    }

    private void OnItemsSourceChanged(IList? oldList, IList? newList)
    {
        if (oldList is INotifyCollectionChanged oldObservable)
            oldObservable.CollectionChanged -= OnCollectionChanged;
        if (newList is INotifyCollectionChanged newObservable)
            newObservable.CollectionChanged += OnCollectionChanged;
        Resync();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Resync();

    /// <summary>Réaligne l'index sur la sélection après un changement de liste. Si la sélection n'est pas
    /// (encore) dans la liste, rien n'est remis à zéro : l'ordre d'application des bindings (SelectedIndex
    /// ou SelectedItem avant ItemsSource) ne doit pas effacer la valeur du ViewModel.</summary>
    private void Resync()
    {
        var items = ItemsSource;
        _syncing = true;
        try
        {
            if (SelectedItem is not null)
            {
                var index = IndexOf(items, SelectedItem);
                if (index >= 0) SelectedIndex = index;
            }
            else if (items is not null && SelectedIndex >= 0 && SelectedIndex < items.Count)
            {
                SelectedItem = items[SelectedIndex];
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateDisplay();
    }

    private void OnSelectedItemChanged()
    {
        if (!_syncing)
        {
            _syncing = true;
            try
            {
                var index = IndexOf(ItemsSource, SelectedItem);
                if (SelectedItem is null) SelectedIndex = -1;
                else if (index >= 0) SelectedIndex = index;
            }
            finally
            {
                _syncing = false;
            }
        }
        UpdateDisplay();
    }

    private void OnSelectedIndexChanged()
    {
        if (!_syncing && ItemsSource is { } items)
        {
            _syncing = true;
            try
            {
                if (SelectedIndex >= 0 && SelectedIndex < items.Count) SelectedItem = items[SelectedIndex];
                else if (SelectedIndex < 0) SelectedItem = null;
            }
            finally
            {
                _syncing = false;
            }
        }
        UpdateDisplay();
    }

    private static int IndexOf(IList? items, object? item)
    {
        if (items is null || item is null) return -1;
        for (var i = 0; i < items.Count; i++)
            if (Equals(items[i], item)) return i;
        return -1;
    }

    private string DisplayOf(object? item)
    {
        if (item is null) return string.Empty;
        if (_itemDisplayBinding is null) return item.ToString() ?? string.Empty;
        var helper = new DisplayHelper { BindingContext = item };
        helper.SetBinding(DisplayHelper.TextProperty, _itemDisplayBinding);
        var text = helper.Text;
        helper.RemoveBinding(DisplayHelper.TextProperty);
        return text ?? string.Empty;
    }

    private void UpdateDisplay()
    {
        if (ValueLabel is null) return;
        // Comme Picker : une valeur absente de la liste (ex. string.Empty, remise à zéro d'AdvanceRollEntry)
        // affiche le Title, pas un champ vide.
        var hasValue = IndexOf(ItemsSource, SelectedItem) >= 0;
        ValueLabel.Text = hasValue ? DisplayOf(SelectedItem) : Title;
        ValueLabel.SetDynamicResource(Label.TextColorProperty, hasValue ? "AppText" : "AppTextMuted");
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (_opening || !IsEnabled || ItemsSource is not { Count: > 0 } items) return;
        _opening = true;
        try
        {
            var loc = LocalizationService.Instance;
            var options = new List<ActionSheetOption>(items.Count);
            for (var i = 0; i < items.Count; i++)
                options.Add(new ActionSheetOption(i, DisplayOf(items[i]), IsSelected: Equals(items[i], SelectedItem)));
            var title = string.IsNullOrEmpty(Title) ? loc["SelectFieldDefaultTitle"] : Title;
            var dialog = new ActionSheetDialog(new ActionSheetDialogViewModel(title, options, loc["BtnCancel"]));
            var page = (Page?)Shell.Current ?? Application.Current!.Windows[0].Page!;
            var index = await DialogStack.Instance.PushAsync(dialog, page);
            // Élément de la liste affichée (une liste recalculée entre-temps contient des égaux, voir Resync).
            if (index >= 0 && index < items.Count)
                SelectedItem = items[index];
        }
        catch (Exception ex)
        {
            CrashLogger.LogException("SelectFieldView", ex);
        }
        finally
        {
            _opening = false;
        }
    }

    private sealed class DisplayHelper : BindableObject
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(DisplayHelper));

        public string? Text => (string?)GetValue(TextProperty);
    }
}
