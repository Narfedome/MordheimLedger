namespace MordheimLedgerApp.Core.Services;

/// <summary>Files behind Warrior.PhotoPath: one image per photo under "&lt;root&gt;/warrior-photos/", the
/// root being the app data directory (FileSystem.AppDataDirectory in the MAUI head, a temp folder in
/// tests) - passed in rather than resolved here so Core stays free of any MAUI dependency. Warrior only
/// ever stores the RELATIVE path returned by SaveAsync, so the database survives the data directory
/// moving (reinstall, other device). Each saved photo gets a fresh file name rather than one per warrior:
/// replacing a photo then never overwrites a file an Image control may still have cached, and a
/// cancelled edit just deletes its own new file without touching the photo already saved.</summary>
public class WarriorPhotoStore
{
    public const string FolderName = "warrior-photos";

    private readonly string _rootDirectory;

    public WarriorPhotoStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    private string FolderPath => Path.Combine(_rootDirectory, FolderName);

    /// <summary>Copies the picked/captured image into the photo folder and returns its relative path, to
    /// store on Warrior.PhotoPath.</summary>
    public async Task<string> SaveAsync(Stream source, string? extension = null)
    {
        Directory.CreateDirectory(FolderPath);
        var ext = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.StartsWith('.') ? extension : "." + extension;
        var relativePath = $"{FolderName}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        await using var target = File.Create(GetFullPath(relativePath)!);
        await source.CopyToAsync(target);
        return relativePath;
    }

    /// <summary>Absolute path for display, or null when there's no photo or its file has gone missing
    /// (the card then falls back to the placeholder silhouette instead of a broken image).</summary>
    public string? GetFullPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        return Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public string? GetExistingFullPath(string? relativePath)
    {
        var full = GetFullPath(relativePath);
        return full is not null && File.Exists(full) ? full : null;
    }

    /// <summary>Best-effort: a photo that can't be deleted (locked, already gone) must never block deleting
    /// the warrior itself. Refuses anything outside the photo folder, so a hand-edited PhotoPath can't
    /// delete an arbitrary file.</summary>
    public void Delete(string? relativePath)
    {
        var full = GetFullPath(relativePath);
        if (full is null) return;
        var folder = Path.GetFullPath(FolderPath) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(full).StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (File.Exists(full)) File.Delete(full);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Settings' full database reset - every warrior is gone, so are all their photos.</summary>
    public void DeleteAll()
    {
        try
        {
            if (Directory.Exists(FolderPath)) Directory.Delete(FolderPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
