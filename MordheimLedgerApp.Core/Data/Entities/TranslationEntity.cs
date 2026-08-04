using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities;

/// <summary>
/// A (Key, LanguageCode) → Value pair - one row per language a given piece of translatable Library
/// text (Name/Description) has been entered in. Shared by every Library type instead of dedicated
/// NameFr/NameEn columns per entity, so the mechanism only needs to exist once. See LibraryService's
/// ResolveTranslationsAsync/SetTranslationAsync.
/// </summary>
public class TranslationEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Key { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
