using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Services;

/// <summary>État du rappel « Soutenir le projet » dans les préférences (règle : <see cref="SupportReminder"/>). Propre à
/// l'appareil, comme les repères d'accueil et de nouveautés : pas dans la sauvegarde.</summary>
public static class SupportReminderService
{
    private const string GamesKey = "support_games";
    private const string NextKey = "support_next";
    private const string StoppedKey = "support_stopped";

    /// <summary>Page de dons, aussi ouverte par la carte « Soutenir le projet » des Paramètres.</summary>
    public static Uri SupportUri { get; } = new("https://buymeacoffee.com/narfedome");

    private static SupportReminderState Load() => new(
        Preferences.Default.Get(GamesKey, 0),
        Preferences.Default.Get(NextKey, SupportReminder.FirstPromptAt),
        Preferences.Default.Get(StoppedKey, false));

    private static void Save(SupportReminderState state)
    {
        Preferences.Default.Set(GamesKey, state.CompletedGames);
        Preferences.Default.Set(NextKey, state.NextPromptAt);
        Preferences.Default.Set(StoppedKey, state.Stopped);
    }

    /// <summary>Une Fin de partie vient d'être enregistrée : vrai s'il faut afficher le rappel maintenant. Jamais en mode
    /// test E2E.</summary>
    public static bool GameCompleted()
    {
        if (E2EMode.IsEnabled) return false;
        var (state, show) = SupportReminder.GameCompleted(Load());
        Save(state);
        return show;
    }

    public static void Answer(SupportReminderChoice choice) => Save(SupportReminder.Answer(Load(), choice));
}
