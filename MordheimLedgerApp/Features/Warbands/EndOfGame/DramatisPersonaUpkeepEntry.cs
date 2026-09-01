using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>A Gold-fee Dramatis Persona already actively in the roster (Johann/Veskit/Marianna - NOT
/// Bertha/None, Nicodemus/Wyrdstone, Ulli &amp; Marquand/Pair, all three still out of scope, see
/// DRAMATIS_PERSONAE_STATUS.md), at the "Dramatis Personae" step of the End of Game wizard - see
/// EndOfGameDialogViewModel.DramatisPersonae.cs.BuildDramatisPersonaUpkeepEntries. Same Payer/Renvoyer
/// choice idiom as HiredSwordUpkeepEntry (a Picker with two options, not a raw bool?) - simpler than that
/// class though: no IsPrepaidFree equivalent exists for a Dramatis Persona (nothing like "Une Faveur
/// Rendue" grants one of these for free), so HasChoice is always true here.</summary>
public partial class DramatisPersonaUpkeepEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public DramatisPersona Persona { get; }

    public string DisplayName => Warrior.Name;
    public int UpkeepCost => Persona.Upkeep!.Value;

    public List<string> ChoiceLabels { get; }
    private readonly string _payLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WillPay))]
    private string? selectedChoiceLabel;

    partial void OnSelectedChoiceLabelChanged(string? value)
    {
        if (value is not null) ChoiceError = null;
    }

    /// <summary>Résolu depuis SelectedChoiceLabel - null tant qu'aucune option n'est choisie. Consommé
    /// par WarbandDetailViewModel.EndOfGame.ApplyDramatisPersonaUpkeepAsync.</summary>
    public bool? WillPay => SelectedChoiceLabel is null ? null : SelectedChoiceLabel == _payLabel;

    [ObservableProperty]
    private string? choiceError;

    public DramatisPersonaUpkeepEntry(Warrior warrior, DramatisPersona persona, string payLabel, string dismissLabel)
    {
        Warrior = warrior;
        Persona = persona;
        _payLabel = payLabel;
        ChoiceLabels = new List<string> { payLabel, dismissLabel };
    }
}
