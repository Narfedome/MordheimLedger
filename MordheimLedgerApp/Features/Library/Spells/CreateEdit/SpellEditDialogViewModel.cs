using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Library.Spells.CreateEdit;

public partial class SpellEditDialogViewModel : DialogViewModel<bool>
{
    private readonly Dictionary<string, MagicSchool> _magicSchoolByLabel = new();

    protected override bool CancelResult => false;

    public ObservableCollection<string> MagicSchoolOptions { get; } = new();

    [ObservableProperty]
    private Spell item;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string selectedMagicSchoolLabel = string.Empty;

    public SpellEditDialogViewModel(Spell item, string title, IReadOnlyList<MagicSchool> allMagicSchools)
    {
        this.item = item;
        this.title = title;

        foreach (var school in allMagicSchools)
        {
            _magicSchoolByLabel[school.Name] = school;
            MagicSchoolOptions.Add(school.Name);
        }

        var currentSchool = allMagicSchools.FirstOrDefault(s => s.Id == item.MagicSchoolId);
        selectedMagicSchoolLabel = currentSchool?.Name ?? string.Empty;
    }

    partial void OnSelectedMagicSchoolLabelChanged(string value)
    {
        if (_magicSchoolByLabel.TryGetValue(value, out var school))
            Item.MagicSchoolId = school.Id;
    }

    [RelayCommand]
    private void Save() => Close(true);
}
