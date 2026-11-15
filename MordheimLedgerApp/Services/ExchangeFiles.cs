using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Exchange;

namespace MordheimLedgerApp.Services;

/// <summary>Fichiers .mordheim (v2, lot 2) : envoi par la feuille de partage du système (choix de l'utilisateur), et
/// fichiers reçus d'une autre appli (« Ouvrir avec » sous Android, double-clic sous Windows) mis en attente jusqu'à
/// ce que la liste des bandes les prenne (WarbandListViewModel.ImportPendingFileAsync).</summary>
public static class ExchangeFiles
{
    /// <summary>Type MIME annoncé à l'envoi : les applis de messagerie ne connaissent pas l'extension et le
    /// transmettent de toute façon comme un fichier binaire (filtre d'Intent de MainActivity aligné dessus).</summary>
    public const string MimeType = "application/octet-stream";

    private static string ShareFolder => Path.Combine(FileSystem.CacheDirectory, "share");

    /// <summary>Écrit l'archive puis ouvre la feuille de partage. Le fichier reste dans le cache (l'appli qui le reçoit
    /// le lit après coup) et part au partage suivant.</summary>
    public static async Task ShareAsync(ExchangePackage package, string name)
    {
        if (Directory.Exists(ShareFolder)) Directory.Delete(ShareFolder, recursive: true);
        Directory.CreateDirectory(ShareFolder);
        var path = Path.Combine(ShareFolder, SafeFileName(name) + ExchangeArchive.FileExtension);
        await using (var file = File.Create(path))
            await ExchangeArchive.WriteAsync(package, file);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = name, File = new ShareFile(path, MimeType) });
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return safe.Length == 0 ? "MordheimLedger" : safe;
    }

    // --- Fichiers reçus -------------------------------------------------------------------------------------

    private static readonly Lock Gate = new();
    private static (string Path, string Name, bool IsTemporary)? _pending;

    /// <summary>Un fichier arrive d'une autre appli. isTemporary : copie dans le cache (Android), supprimée après
    /// import ; sinon le fichier de l'utilisateur (Windows), jamais touché.</summary>
    public static void Receive(string path, string name, bool isTemporary)
    {
        lock (Gate) _pending = (path, name, isTemporary);
        // Appli pas encore démarrée (lancement par « Ouvrir avec ») : la liste des bandes le prendra à son premier affichage.
        if (Application.Current is null) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Pas Shell.Current, qui lève une exception dès que l'appli a plus d'une fenêtre.
            if (Application.Current?.Windows.FirstOrDefault()?.Page is not Shell shell) return;
            // Déjà sur la liste : elle importe tout de suite (pas de nouvel affichage pour la prévenir).
            if (shell.CurrentPage is Features.Warbands.WarbandListPage)
            {
                WeakReferenceMessenger.Default.Send(new IncomingExchangeFileMessage());
                return;
            }
            // Formulaire ou dialog ouvert (Fin de partie en cours...) : on ne ferme rien, le fichier attend le retour
            // sur la liste. Sinon (fiche, Codex, Paramètres), retour à la liste, qui l'importe à son affichage -
            // l'écran d'import ouvert pendant cette navigation faisait planter Shell (recette du 2026-11-15).
            if (shell.Navigation.ModalStack.Count > 0 || shell.CurrentPage is Components.FormPage.FormPage) return;
            try
            {
                await shell.GoToAsync("//WarbandListPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExchangeFiles] {ex}");
            }
        });
    }

    /// <summary>Le fichier en attente, retiré de l'attente (une seule prise).</summary>
    public static (string Path, string Name, bool IsTemporary)? TakePending()
    {
        lock (Gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}

/// <summary>Un fichier .mordheim attend d'être importé (voir ExchangeFiles.Receive).</summary>
public sealed record IncomingExchangeFileMessage;

/// <summary>Un import a ajouté ou remplacé des entrées du Codex (LibraryViewModel recharge ses sections).</summary>
public sealed record CatalogImportedMessage;
