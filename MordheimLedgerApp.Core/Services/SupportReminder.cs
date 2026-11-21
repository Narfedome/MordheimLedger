namespace MordheimLedgerApp.Core.Services;

/// <summary>Réponse au rappel « Soutenir le projet ».</summary>
public enum SupportReminderChoice
{
    /// <summary>Fermé sans choisir (✕, retour Android) : compte comme « Plus tard ».</summary>
    Later,
    Support,
    Never,
}

/// <summary>Où en est le rappel : Fins de partie terminées, seuil du prochain rappel, rappel coupé.</summary>
public sealed record SupportReminderState(int CompletedGames, int NextPromptAt, bool Stopped)
{
    public static SupportReminderState Initial { get; } = new(0, SupportReminder.FirstPromptAt, false);
}

/// <summary>Rappel « Soutenir le projet » (2026-11-20, décision de l'utilisateur) : une fenêtre discrète juste après une
/// Fin de partie terminée, la 3e d'abord ; « Plus tard » redemande 10 Fins de partie plus loin ; « Soutenir » ou « Ne plus
/// demander » l'arrêtent pour de bon (la carte des Paramètres reste). Règle pure, l'appli garde l'état en préférences.</summary>
public static class SupportReminder
{
    public const int FirstPromptAt = 3;
    public const int LaterDelay = 10;

    /// <summary>Une Fin de partie vient d'être terminée : nouvel état, et faut-il afficher le rappel.</summary>
    public static (SupportReminderState State, bool ShowPrompt) GameCompleted(SupportReminderState state)
    {
        var next = state with { CompletedGames = state.CompletedGames + 1 };
        return (next, !next.Stopped && next.CompletedGames >= next.NextPromptAt);
    }

    /// <summary>Réponse au rappel affiché.</summary>
    public static SupportReminderState Answer(SupportReminderState state, SupportReminderChoice choice) => choice switch
    {
        SupportReminderChoice.Later => state with { NextPromptAt = state.CompletedGames + LaterDelay },
        _ => state with { Stopped = true },
    };
}
