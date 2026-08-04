using MordheimLedgerApp.Core.Data.Entities;
using SQLite;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// Shared (Key, LanguageCode) → Value resolution/writing, used by both LibraryService (catalog
/// Get/Save) and WarbandService (resolving carried Equipment/learned Skills/tracked Injuries onto a
/// loaded Warrior) - extracted here rather than duplicated since both Core services need it.
/// </summary>
internal static class TranslationResolver
{
    /// <summary>Resolves each key to its value in `languageCode`, falling back to whatever other
    /// language is available, or the key itself as a last-resort visible placeholder. Loads the whole
    /// Translation table rather than filtering in SQL - catalogs/rosters are small enough that this
    /// beats fighting sqlite-net-pcl's limited LINQ translation (no reliable `.Contains()` support).</summary>
    public static async Task<Dictionary<string, string>> ResolveAsync(SQLiteAsyncConnection db, IEnumerable<string?> keys, string languageCode)
    {
        var keySet = keys.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToHashSet()!;
        var result = new Dictionary<string, string>();
        if (keySet.Count == 0) return result;

        var rows = await db.Table<TranslationEntity>().ToListAsync();
        foreach (var key in keySet)
        {
            var forKey = rows.Where(r => r.Key == key).ToList();
            var match = forKey.FirstOrDefault(r => r.LanguageCode == languageCode) ?? forKey.FirstOrDefault();
            result[key!] = match?.Value ?? key!;
        }
        return result;
    }

    /// <summary>Writes `value` for `key`/`languageCode` (upsert). Pass a null `key` to allocate a new
    /// one; returns the key that was written to (existing or newly allocated).</summary>
    public static async Task<string> SetAsync(SQLiteAsyncConnection db, string? key, string languageCode, string value)
    {
        key ??= Guid.NewGuid().ToString("N");
        var existing = await db.Table<TranslationEntity>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        if (existing is null)
            await db.InsertAsync(new TranslationEntity { Key = key, LanguageCode = languageCode, Value = value });
        else
        {
            existing.Value = value;
            await db.UpdateAsync(existing);
        }
        return key;
    }
}
