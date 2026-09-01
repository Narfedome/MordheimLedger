using MordheimLedgerApp.Core.Models.Library;
using SQLite;

namespace MordheimLedgerApp.Core.Data.Entities.Library;

public class DramatisPersonaEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NameKey { get; set; } = string.Empty;
    public string? DescriptionKey { get; set; }
    public ContentSource Source { get; set; }
    public string? ImagePath { get; set; }

    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }

    public DramatisPersonaHireFeeKind FeeKind { get; set; }
    public int? HireCost { get; set; }
    public int? Upkeep { get; set; }
    public int RatingBonus { get; set; }
    public bool IsWanderer { get; set; }

    /// <summary>See Models.Library.DramatisPersona.RequiresRatingDisadvantage.</summary>
    public bool RequiresRatingDisadvantage { get; set; }

    /// <summary>See Models.Library.DramatisPersona.MagicSchoolId. Null for most Dramatis Personae.</summary>
    [Indexed]
    public int? MagicSchoolId { get; set; }

    /// <summary>See Models.Library.DramatisPersona.AlternativePaymentItemId. Null for most Dramatis
    /// Personae.</summary>
    [Indexed]
    public int? AlternativePaymentItemId { get; set; }
}
