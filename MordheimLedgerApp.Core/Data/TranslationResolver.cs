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
    /// language is available, or the key itself as a last-resort visible placeholder. One indexed SQL
    /// query per distinct key (TranslationEntity.Key is [Indexed]), fired concurrently via
    /// Task.WhenAll, rather than loading/caching the whole Translation table up front - a single
    /// upfront load pays its cost on whichever screen happens to be opened first, with no loading
    /// indicator there to explain the pause; many small local queries (each cheap - this is an embedded
    /// SQLite db, not a network round-trip) spread the cost proportionally to what each screen actually
    /// needs instead.</summary>
    public static async Task<Dictionary<string, string>> ResolveAsync(AppDatabase db, IEnumerable<string?> keys, string languageCode)
    {
        var keySet = keys.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
        var result = new Dictionary<string, string>();
        if (keySet.Count == 0) return result;

        var lookups = await Task.WhenAll(keySet.Select(async key =>
        {
            var rows = await db.Connection.Table<TranslationEntity>().Where(t => t.Key == key).ToListAsync();
            var match = rows.FirstOrDefault(r => r.LanguageCode == languageCode) ?? rows.FirstOrDefault();
            return (Key: key!, Value: match?.Value ?? key!);
        }));

        foreach (var (key, value) in lookups)
            result[key] = value;
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
