using MordheimLedgerApp.Core.Data.Entities;

namespace MordheimLedgerApp.Core.Data;

/// <summary>
/// Shared (Key, LanguageCode) → Value resolution/writing, used by both LibraryService (catalog
/// Get/Save) and WarbandService (resolving carried Equipment/learned Skills/tracked Injuries onto a
/// loaded Warrior) - extracted here rather than duplicated since both Core services need it.
/// </summary>
internal static class TranslationResolver
{
    /// <summary>Resolves each key to its value in `languageCode`, falling back to whatever other
    /// language is available, or the key itself as a last-resort visible placeholder. Served from
    /// AppDatabase's cached Translation table (2026-09-24 - was one IN query per call before that,
    /// itself replacing the original approach of one indexed query per distinct key fired
    /// concurrently via Task.WhenAll - fine while the catalog was tiny, but a single Get*Async can now
    /// resolve hundreds of keys at once). SetAsync's Insert/Update evicts the cache via TableChanged.</summary>
    public static async Task<Dictionary<string, string>> ResolveAsync(AppDatabase db, IEnumerable<string?> keys, string languageCode)
    {
        var keySet = keys.Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct().ToList();
        var result = new Dictionary<string, string>();
        if (keySet.Count == 0) return result;

        // Table entière depuis le cache d'AppDatabase (voir CachedTableAsync) plutôt qu'un IN par appel -
        // quelques milliers de lignes, relues des dizaines de fois à l'ouverture d'un écran sinon.
        var keyFilter = keySet.ToHashSet();
        var rowsByKey = (await db.CachedTableAsync<TranslationEntity>())
            .Where(r => keyFilter.Contains(r.Key))
            .ToLookup(r => r.Key);

        foreach (var key in keySet)
        {
            var candidates = rowsByKey[key];
            var match = candidates.FirstOrDefault(r => r.LanguageCode == languageCode) ?? candidates.FirstOrDefault();
            result[key] = match?.Value ?? key;
        }
        return result;
    }

    /// <summary>Writes `value` for `key`/`languageCode` (upsert). Pass a null `key` to allocate a new
    /// one; returns the key that was written to (existing or newly allocated).</summary>
    public static async Task<string> SetAsync(AppDatabase db, string? key, string languageCode, string value)
    {
        key ??= Guid.NewGuid().ToString("N");
        var existing = await db.Connection.Table<TranslationEntity>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        if (existing is null)
            await db.Connection.InsertAsync(new TranslationEntity { Key = key, LanguageCode = languageCode, Value = value });
        else
        {
            existing.Value = value;
            await db.Connection.UpdateAsync(existing);
        }
        return key;
    }
}
