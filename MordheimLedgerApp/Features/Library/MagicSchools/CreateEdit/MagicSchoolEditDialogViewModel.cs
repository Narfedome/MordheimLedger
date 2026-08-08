using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Components.Dialogs;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.Spells.CreateEdit;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Library.MagicSchools.CreateEdit;

/// <summary>Sorts édités en mémoire (Add/Edit/Remove sans appel service), sauvegardés en bloc avec
/// l'école au Save - même architecture que WarbandArchetypeEditDialogViewModel.Warriors/EquipmentLists.
/// Nécessaire pour pouvoir créer une nouvelle école ET ses sorts dans le même passage : tant que l'école
/// n'a pas d'Id réel (Item.Id == 0), aucun Spell.MagicSchoolId réel n'existe encore pour les rattacher.</summary>
public partial class MagicSchoolEditDialogViewModel : DialogViewModel<bool>
{
    private readonly ILibraryService _libraryService;

    /// <summary>Ids des sorts qui existaient déjà en base (Id != 0) et ont été retirés pendant cette
    /// session d'édition - une ObservableCollection seule ne suffit pas à distinguer "jamais existé" de
    /// "existait, supprimé ici", nécessaire pour savoir quoi supprimer réellement au Save.</summary>
    private readonly List<int> _removedSpellIds = new();

    protected override bool CancelResult => false;

    [ObservableProperty]
    private MagicSchool item;

    [ObservableProperty]
    private string title;

    public ObservableCollection<Spell> Spells { get; }

    public MagicSchoolEditDialogViewModel(MagicSchool item, string title, ILibraryService libraryService,
        IReadOnlyList<Spell> initialSpells)
    {
        this.item = item;
        this.title = title;
        _libraryService = libraryService;
        Spells = new ObservableCollection<Spell>(initialSpells);
    }

    [RelayCommand]
    private async Task AddSpell()
    {
        var newSpell = new Spell { MagicSchoolId = Item.Id };
        var dialogViewModel = new SpellEditDialogViewModel(newSpell, Loc["SpellCreateTitle"], new[] { Item });
        if (await ShowDialogAsync(new SpellEditDialog(dialogViewModel)) != true) return;

        Spells.Add(newSpell);
    }

    [RelayCommand]
    private async Task EditSpell(Spell spell)
    {
        var copy = new Spell
        {
            Id = spell.Id,
            MagicSchoolId = spell.MagicSchoolId,
            Name = spell.Name,
            NameKey = spell.NameKey,
            RollValue = spell.RollValue,
            Difficulty = spell.Difficulty,
            Description = spell.Description,
            DescriptionKey = spell.DescriptionKey,
            Source = spell.Source,
            ImagePath = spell.ImagePath
        };

        var dialogViewModel = new SpellEditDialogViewModel(copy, Loc["SpellEditTitle"], new[] { Item });
        if (await ShowDialogAsync(new SpellEditDialog(dialogViewModel)) != true) return;

        var index = Spells.IndexOf(spell);
        if (index >= 0) Spells[index] = copy;
    }

    [RelayCommand]
    private void RemoveSpell(Spell spell)
    {
        Spells.Remove(spell);
        if (spell.Id != 0) _removedSpellIds.Add(spell.Id);
    }

    [RelayCommand]
    private async Task Save()
    {
        var language = LocalizationService.Instance.Language;
        await Loading.RunAsync(async () =>
        {
            await _libraryService.SaveMagicSchoolAsync(Item, language);

            foreach (var spell in Spells)
            {
                spell.MagicSchoolId = Item.Id;
                await _libraryService.SaveSpellAsync(spell, language);
            }
            foreach (var id in _removedSpellIds)
                await _libraryService.DeleteSpellAsync(id);
        });

        Close(true);
    }
}
