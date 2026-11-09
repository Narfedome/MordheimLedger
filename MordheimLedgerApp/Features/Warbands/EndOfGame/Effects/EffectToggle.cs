using CommunityToolkit.Mvvm.ComponentModel;
using MordheimLedgerApp.Core.Rules;
using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Features.Warbands.EndOfGame.Effects;

/// <summary>Un bonus permanent détecté (maquette « Recherche d'objet rare », « Jet d'Exploration ») : pré-coché
/// quand sa condition est remplie, décochable, jamais caché - une condition non remplie le laisse décoché avec
/// un message ambre. Amount est la valeur lue par Core (EffectValues), le ViewModel ne fait que l'additionner.</summary>
public partial class EffectToggle : ObservableObject
{
    public ActiveEffect Effect { get; }
    public string Title => Effect.SourceName;
    public string Subtitle { get; }
    public int Amount { get; }
    public string ValueText { get; }
    public string? Warning { get; }
    public bool HasWarning => Warning is not null;

    [ObservableProperty]
    private bool isOn;

    public event Action? Changed;

    partial void OnIsOnChanged(bool value) => Changed?.Invoke();

    public EffectToggle(ActiveEffect effect, int amount, string valueText, LocalizationService loc)
    {
        Effect = effect;
        Amount = amount;
        ValueText = valueText;
        Subtitle = EffectTexts.SourceOf(effect, loc);
        Warning = EffectTexts.UnmetCondition(effect, loc);
        isOn = effect.ConditionMet;
    }
}
