using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.SpecialRules.CreateEdit;

public partial class SpecialRuleEditDialogViewModel : DialogViewModel<bool>
{
    protected override bool CancelResult => false;

    [ObservableProperty]
    private SpecialRule item;

    [ObservableProperty]
    private string title;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsTab))]
    private int selectedTab;

    public bool IsGeneralTab => SelectedTab == 0;
    public bool IsMaterialsTab => SelectedTab == 1;

    /// <summary>Coche-t-on la règle comme "matériau" (voir SpecialRule.CostMultiplier) - purement une
    /// bascule d'affichage pour l'onglet Matériaux, initialisée depuis CostMultiplier.HasValue plutôt
    /// que persistée comme un second champ sur le modèle (qui resterait la seule source de vérité,
    /// pas de risque de désynchronisation). Décochée : les 3 champs matériau sont vidés au Save, pas
    /// à la volée (Item n'a pas d'INotifyPropertyChanged, donc les Entry qui lui sont liées ne se
    /// rafraîchiraient pas si on les vidait ici).</summary>
    [ObservableProperty]
    private bool isMaterialRule;

    public SpecialRuleEditDialogViewModel(SpecialRule item, string title)
    {
        this.item = item;
        this.title = title;
        isMaterialRule = item.CostMultiplier.HasValue;
    }

    partial void OnIsMaterialRuleChanged(bool value)
    {
        // Coché : bascule direct sur l'onglet qui vient d'apparaître, pour bien montrer où saisir le
        // multiplicateur plutôt que le laisser cliquer dessus lui-même. Décoché : retour sur Général si
        // on était en train de regarder un onglet qui vient de disparaître.
        SelectedTab = value ? 1 : 0;
    }

    [RelayCommand]
    private void ShowGeneralTab() => SelectedTab = 0;

    [RelayCommand]
    private void ShowMaterialsTab() => SelectedTab = 1;

    private bool ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(Item.Name))
        {
            NameError = Loc["LibFieldRequired"];
            return false;
        }
        NameError = null;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateRequiredFields())
        {
            SelectedTab = 0;
            return;
        }
        if (!IsMaterialRule)
        {
            Item.CostMultiplier = null;
            Item.Abbreviation = null;
            Item.Rarity = null;
        }
        Close(true);
    }
}
