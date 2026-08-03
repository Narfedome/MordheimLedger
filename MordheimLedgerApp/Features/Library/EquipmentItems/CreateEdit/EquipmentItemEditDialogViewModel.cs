using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.EquipmentItems.CreateEdit;

public partial class EquipmentItemEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, EquipmentCategory> _categoryByLabel = new();

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private EquipmentItem item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    public EquipmentItemEditDialogViewModel(EquipmentItem item, string title)
    {
        this.item = item;
        this.title = title;

        foreach (var category in Enum.GetValues<EquipmentCategory>())
        {
            var label = Loc[$"EquipmentCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"EquipmentCategory{item.Category}"];
    }

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
