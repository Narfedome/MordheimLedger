using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Models;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame;

/// <summary>A recurring-fee Dramatis Persona already actively in the roster, at the "Dramatis Personae"
/// step of the End of Game wizard - see EndOfGamePageViewModel.DramatisPersonae.cs.
/// BuildDramatisPersonaUpkeepEntries. Covers two currencies: Gold (Johann/Veskit/Marianna) and Wyrdstone
/// (Nicodemus, 2026-09-01 - "he has no interest in gold... must be paid a wyrdstone shard... after every
/// battle he fights") - NOT Bertha/None nor Ulli &amp; Marquand/Pair, both still out of scope, see
/// DRAMATIS_PERSONAE_STATUS.md. Same Payer/Renvoyer choice idiom as HiredSwordUpkeepEntry (a Picker with
/// two options, not a raw bool?) - simpler than that class though: no IsPrepaidFree equivalent exists for
/// a Dramatis Persona (nothing like "Une Faveur Rendue" grants one of these for free), so HasChoice is
/// always true here.</summary>
public partial class DramatisPersonaUpkeepEntry : ObservableObject
{
    public Warrior Warrior { get; }
    public DramatisPersona Persona { get; }

    public string DisplayName => Warrior.Name;

    public bool IsWyrdstoneFee => Persona.FeeKind == DramatisPersonaHireFeeKind.Wyrdstone;

    /// <summary>1 for Nicodemus (always exactly one shard, never varies), otherwise the persona's real
    /// gold Upkeep.</summary>
    public int UpkeepCost => IsWyrdstoneFee ? 1 : Persona.Upkeep!.Value;

    /// <summary>"1 pierre magique" for Nicodemus (reuses RareItemSearchEntry's own currency label, same
    /// resx key) - the raw gold number otherwise, unchanged from before (HiredSwordUpkeepEntry shows its
    /// own UpkeepCost the same bare way, no "CO" suffix either).</summary>
    public string UpkeepCostDisplay => IsWyrdstoneFee ? LocalizationService.Instance["EndOfGameCharacterWyrdstoneCostValue"] : UpkeepCost.ToString();

    public List<string> ChoiceLabels { get; }
    private readonly string _payLabel;
    private readonly string _noneLabel;

    private string? selectedChoiceLabel;

    /// <summary>Backing field manuel plutôt qu'un simple [ObservableProperty] (2026-09-23, même correctif
    /// qu'EndOfGamePageViewModel.SelectedResult - voir sa doc) : Picker.SelectedItem
    /// (DramatisPersonaeStepView.xaml) est lié TwoWay - la vue mise en cache par étape (StepViewConverter)
    /// peut faire remonter un null transitoire au réattachement après un retour en arrière, écrasant
    /// silencieusement le vrai choix. Le setter ignore toute valeur absente de ChoiceLabels.</summary>
    public string? SelectedChoiceLabel
    {
        get => selectedChoiceLabel;
        set
        {
            if (value is null || !ChoiceLabels.Contains(value)) return;
            if (!SetProperty(ref selectedChoiceLabel, value)) return;

            OnPropertyChanged(nameof(WillPay));
            ChoiceError = null;
        }
    }

    /// <summary>Résolu depuis SelectedChoiceLabel - null tant que "Aucun" (le choix par défaut, voir le
    /// constructeur) ou rien du tout n'est sélectionné. Consommé par EndOfGamePageViewModel.Apply.
    /// ApplyDramatisPersonaUpkeepAsync.</summary>
    public bool? WillPay => SelectedChoiceLabel is null || SelectedChoiceLabel == _noneLabel ? null : SelectedChoiceLabel == _payLabel;

    [ObservableProperty]
    private string? choiceError;

    public DramatisPersonaUpkeepEntry(Warrior warrior, DramatisPersona persona, string payLabel, string dismissLabel)
    {
        Warrior = warrior;
        Persona = persona;
        _payLabel = payLabel;
        _noneLabel = LocalizationService.Instance["LibNoneOption"];
        // "Aucun" en premier, présélectionné (retour utilisateur 2026-09-22) - même raisonnement que
        // CapturedEnemyEntry.FateLabels, voir sa doc.
        ChoiceLabels = new List<string> { _noneLabel, payLabel, dismissLabel };
        SelectedChoiceLabel = _noneLabel;
    }
}
