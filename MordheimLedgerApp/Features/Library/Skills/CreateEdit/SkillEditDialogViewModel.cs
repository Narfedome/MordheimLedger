using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Skills.CreateEdit;

public partial class SkillEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, SkillCategory> _categoryByLabel = new();

    protected override bool CancelResult => false;

    public ObservableCollection<string> CategoryOptions { get; } = new();

    [ObservableProperty]
    private Skill item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedCategoryLabel = string.Empty;

    public SkillEditDialogViewModel(Skill item, string title)
    {
        this.item = item;
        this.title = title;

        foreach (var category in Enum.GetValues<SkillCategory>())
        {
            var label = Loc[$"SkillCategory{category}"];
            _categoryByLabel[label] = category;
            CategoryOptions.Add(label);
        }

        selectedCategoryLabel = Loc[$"SkillCategory{item.Category}"];
    }

    partial void OnSelectedCategoryLabelChanged(string value)
    {
        if (_categoryByLabel.TryGetValue(value, out var category))
            Item.Category = category;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
