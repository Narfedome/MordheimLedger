namespace MordheimLedgerApp.Services;

/// <summary>Android filtre par défaut les paquets UDP de diffusion reçus en Wi-Fi (économie d'énergie) : sans ce verrou,
/// l'écoute des parties ouvertes (LobbyDiscovery) ne reçoit rien. Pris le temps de la recherche seulement.</summary>
public static class NetworkLocks
{
    public static IDisposable AcquireMulticast()
    {
#if ANDROID
        var wifi = Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService) as Android.Net.Wifi.WifiManager;
        var multicastLock = wifi?.CreateMulticastLock("mordheim-ledger-lobby");
        if (multicastLock is null) return new Release(() => { });
        multicastLock.SetReferenceCounted(false);
        multicastLock.Acquire();
        return new Release(() =>
        {
            if (multicastLock.IsHeld) multicastLock.Release();
        });
#else
        return new Release(() => { });
#endif
    }

    private sealed class Release(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
