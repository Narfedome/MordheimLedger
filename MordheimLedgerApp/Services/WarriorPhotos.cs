using MordheimLedgerApp.Core.Services;

namespace MordheimLedgerApp.Services;

/// <summary>L'unique WarriorPhotoStore de l'appli, enraciné dans FileSystem.AppDataDirectory (à côté de la
/// base) - enregistré tel quel dans le conteneur (MauiProgram) pour WarbandService/SettingsViewModel, et
/// exposé ici en statique pour WarriorRow, construit par WarriorRosterBuilder hors injection (même idiome
/// que LocalizationService.Instance).</summary>
public static class WarriorPhotos
{
    public static WarriorPhotoStore Store { get; } = new(FileSystem.AppDataDirectory);

    /// <summary>Photo réduite à l'enregistrement (MAUI redimensionne et redresse selon l'EXIF) : une carte
    /// n'affiche qu'une miniature et la visionneuse la largeur d'un écran - inutile de garder les 4000 px
    /// d'un capteur photo, multipliés par chaque guerrier de chaque bande.</summary>
    private static MediaPickerOptions Options => new()
    {
        MaximumWidth = 1280,
        MaximumHeight = 1280,
        CompressionQuality = 85,
        RotateImage = true
    };

    /// <summary>Windows : choix d'un fichier seulement, jamais la webcam (décision de l'utilisateur) - on
    /// photographie une figurine avec son téléphone, pas avec la caméra d'un PC.</summary>
    public static bool IsCaptureSupported =>
#if WINDOWS
        false;
#else
        MediaPicker.Default.IsCaptureSupported;
#endif

    /// <summary>Appareil photo (capture=true) ou galerie : copie l'image choisie dans le dossier des
    /// photos et renvoie son chemin relatif (Warrior.PhotoPath) - null si l'utilisateur a annulé.
    /// Permission caméra demandée ici plutôt que laissée à MediaPicker, pour qu'un refus renvoie
    /// simplement null au lieu d'une exception.</summary>
    public static async Task<string?> PickAsync(bool capture)
    {
        if (capture && !IsCaptureSupported) return null;
        if (capture && await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            return null;

        // PickPhotoAsync (une seule photo) est obsolète depuis .NET 10 : PickPhotosAsync limité à 1.
        var options = Options;
        options.SelectionLimit = 1;
        var file = capture
            ? await MediaPicker.Default.CapturePhotoAsync(options)
            : (await MediaPicker.Default.PickPhotosAsync(options)).FirstOrDefault();
        if (file is null) return null;

        await using var stream = await file.OpenReadAsync();
        return await Store.SaveAsync(stream, Path.GetExtension(file.FileName));
    }
}
