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

    [RelayCommand]
    private void Save() => Close(true);
}
