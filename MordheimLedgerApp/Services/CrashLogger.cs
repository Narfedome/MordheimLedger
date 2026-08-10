namespace MordheimLedgerApp.Services;

/// <summary>Logger fichier minimal actif en Debug ET Release (contrairement à builder.Logging.AddDebug(),
/// DEBUG-only, invisible une fois l'app lancée hors Visual Studio) - pour diagnostiquer le crash natif
/// intermittent au sélecteur de règles spéciales (pas d'exception CLR visible sous débogueur, disparaît
/// sous débogueur - signature d'une race condition, pas reproductible à l'aveugle). Écrit ligne par
/// ligne, flush immédiat (un vrai crash ne laisse pas le temps à un buffer de se vider) dans
/// FileSystem.AppDataDirectory/crash.log - consultable après coup sans débogueur attaché.</summary>
public static class CrashLogger
{
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "crash.log");
    private static readonly object Lock = new();

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Le logging ne doit jamais devenir lui-même une source de crash.
        }
    }

    public static void LogException(string context, Exception ex) =>
        Log($"EXCEPTION in {context}: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
}
