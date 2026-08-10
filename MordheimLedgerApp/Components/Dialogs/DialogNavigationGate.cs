using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>
/// Sérialise tous les appels Navigation.PushModalAsync/PopModalAsync de l'appli : DialogStack (chaque
/// dialog est maintenant une vraie DialogPage modale, voir DialogStack) ET les ~10 XxxPickerService
/// (Special Rule, Spell, Equipment, Warrior Archetype...) qui poussent chacun leur propre page
/// plein écran de sélection - tous partagent la même pile modale unique (Shell.Current.Navigation).
/// Deux Push/PopModalAsync concurrents dessus (ex. un dialog encore en train de s'installer via
/// DialogStack pendant qu'un picker pousse sa page par-dessus, cas fréquent : le sélecteur de règles
/// spéciales est ouvert depuis un bouton du dialog lui-même) sont un piège connu de MAUI/Shell - plus
/// probable maintenant que chaque dialog est aussi une Page sur cette même pile.
/// </summary>
public static class DialogNavigationGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RunAsync(Func<Task> operation, string label)
    {
        await Gate.WaitAsync();
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            CrashLogger.LogException($"NavGate '{label}'", ex);
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }
}
