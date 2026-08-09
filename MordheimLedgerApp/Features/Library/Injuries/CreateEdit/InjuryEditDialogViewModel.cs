using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Injuries.CreateEdit;

public partial class InjuryEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, InjuryCategory> _categoryByLabel = new();

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private Injury item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    /// <summary>Null = pas d'erreur. Texte affiché sous le champ Nom - même mécanisme que
    /// WarbandArchetypeEditDialogViewModel.NameError.</summary>
    [ObservableProperty]
    private string? nameError;

    public InjuryEditDialogViewModel(Injury item, string title)
    {
        this.item = item;
        this.title = title;

        foreach (var category in Enum.GetValues<InjuryCategory>())
        {
            var label = Loc[$"InjuryCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"InjuryCategory{item.Category}"];
    }

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
    }

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
        if (!ValidateRequiredFields()) return;
        Close(true);
    }
}
