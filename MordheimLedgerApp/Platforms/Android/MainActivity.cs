using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;

namespace MordheimLedgerApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    // Fichier .mordheim reçu (v2, lot 2) : « Ouvrir avec » depuis Discord, un mail, le gestionnaire de fichiers
    // (VIEW), ou « Partager vers » (SEND). Les applis le transmettent comme fichier binaire (extension inconnue
    // d'Android) : filtre sur application/octet-stream, la lecture refuse ce qui n'est pas un fichier de l'appli.
    // SingleTask (et non SingleTop) : l'appli qui envoie lançait sinon une seconde activité dans sa propre tâche,
    // donc une seconde fenêtre MAUI sur la même base (plantage vu en recette le 2026-11-15) ; le fichier arrive
    // maintenant toujours dans l'instance existante, par OnNewIntent.
    [IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault], DataMimeType = Services.ExchangeFiles.MimeType)]
    [IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = Services.ExchangeFiles.MimeType)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Tests de bout en bout (Appium, extra d'Intent « mordheim_e2e ») : voir Services.E2EMode.
            if (Intent?.GetBooleanExtra(MordheimLedgerApp.Services.E2EMode.IntentExtra, false) == true)
                MordheimLedgerApp.Services.E2EMode.IsEnabled = true;
            base.OnCreate(savedInstanceState);
            // Activité recréée (rotation...) : l'Intent d'origine a déjà été traité.
            if (savedInstanceState is null) ReceiveFile(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            ReceiveFile(intent);
        }

        /// <summary>Copie le fichier reçu dans le cache (l'URI n'est lisible que le temps de cette activation), puis le
        /// confie à ExchangeFiles, qui l'importe depuis la liste des bandes.</summary>
        private void ReceiveFile(Intent? intent)
        {
            var uri = intent?.Action switch
            {
                Intent.ActionView => intent.Data,
                Intent.ActionSend => OperatingSystem.IsAndroidVersionAtLeast(33)
                    ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
#pragma warning disable CA1422 // remplacée à partir d'Android 13 par la surcharge typée ci-dessus
                    : intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri,
#pragma warning restore CA1422
                _ => null,
            };
            if (uri is null || ContentResolver is not { } resolver) return;
            try
            {
                var folder = Path.Combine(CacheDir!.AbsolutePath, "incoming");
                // Un seul fichier en attente à la fois : ce qui reste d'une réception interrompue part avec.
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{Guid.NewGuid():N}{Core.Exchange.ExchangeArchive.FileExtension}");
                using (var source = resolver.OpenInputStream(uri) ?? throw new IOException("Fichier illisible"))
                using (var target = File.Create(path))
                    source.CopyTo(target);
                Services.ExchangeFiles.Receive(path, DisplayName(resolver, uri) ?? Path.GetFileName(path), isTemporary: true);
            }
            catch (Exception ex)
            {
                Services.CrashLogger.LogException("MainActivity.ReceiveFile", ex);
            }
        }

        private static string? DisplayName(ContentResolver resolver, Android.Net.Uri uri)
        {
            try
            {
                using var cursor = resolver.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
                return cursor is not null && cursor.MoveToFirst() ? cursor.GetString(0) : uri.LastPathSegment;
            }
            catch (Exception)
            {
                return uri.LastPathSegment;
            }
        }
    }
}
