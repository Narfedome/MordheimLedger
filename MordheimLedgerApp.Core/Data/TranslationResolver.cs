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
    /// language is available, or the key itself as a last-resort visible placeholder. One SQL query
    /// with an IN clause for the whole key set, instead of one indexed query per distinct key fired
    /// concurrently via Task.WhenAll (the original approach - it was fine while the catalog was tiny,
    /// but a single Get*Async can now resolve hundreds of keys at once - 15 seeded warbands + Trading
    /// Post/Animals/Skills - meaning hundreds of individual round-trips against the same SQLite
    /// connection every time a catalog tab loads).</summary>
    public static async Task<Dictionary<string, string>> ResolveAsync(AppDatabase db, IEnumerable<string?> keys, string languageCode)
    {
        var keySet = keys.Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct().ToList();
        var result = new Dictionary<string, string>();
        if (keySet.Count == 0) return result;

        var rows = await db.Connection.Table<TranslationEntity>().Where(t => keySet.Contains(t.Key)).ToListAsync();
        var rowsByKey = rows.ToLookup(r => r.Key);

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
