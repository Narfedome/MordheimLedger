using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

/// <summary>Un usage unique proposé sur un jet (maquettes « Blessure grave » et « Jet d'Exploration ») :
/// relance ou ajustement d'un dé, derrière un bouton. Une fois utilisé, la ligne passe en « utilisé » avec ce
/// qui a changé (DoneText) et un lien d'annulation. Le jet lui-même appartient à l'étape : Act/Undo sont
/// fournis par elle, cette classe ne garde que l'état affiché.</summary>
public partial class EffectActionRow : ObservableObject
{
    private readonly Func<EffectActionRow, Task> _act;
    private readonly Action<EffectActionRow> _undo;

    public ActiveEffect Effect { get; }
    public string Title { get; }
    public string Description { get; }
    public string ActionText { get; }
    public string UndoText { get; }
    public string? Warning { get; }
    public bool HasWarning => Warning is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    private bool isDone;

    [ObservableProperty]
    private string? doneText;

    /// <summary>Ce que l'usage a remplacé (ancien jet, ancien dé), pour l'annuler.</summary>
    public object? Previous { get; set; }

    public bool IsAvailable => !IsDone;

    public EffectActionRow(ActiveEffect effect, string description, string actionText, string undoText,
        LocalizationService loc, Func<EffectActionRow, Task> act, Action<EffectActionRow> undo)
    {
        Effect = effect;
        Title = string.Format(loc["EffectSourceTitle"], effect.SourceName, effect.Warrior.Name);
        Description = description;
        ActionText = actionText;
        UndoText = undoText;
        Warning = EffectTexts.UnmetCondition(effect, loc);
        _act = act;
        _undo = undo;
    }

    [RelayCommand]
    private Task Act() => _act(this);

    [RelayCommand]
    private void Undo() => _undo(this);
}
