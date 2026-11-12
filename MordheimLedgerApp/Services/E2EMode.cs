namespace MordheimLedgerApp.Services;

/// <summary>Mode des tests de bout en bout (MordheimLedgerApp.UITests) : activé au lancement par l'extra d'Intent
/// « mordheim_e2e » (Android, voir MainActivity). Coupe ce qui viendrait se mettre en travers d'un scénario
/// automatisé - aujourd'hui les visites guidées (AppTourService). Jamais activé par un lancement normal.</summary>
public static class E2EMode
{
    public const string IntentExtra = "mordheim_e2e";

    public static bool IsEnabled { get; set; }
}
