using System.Globalization;
using MordheimLedgerApp.Core.Models.Library;
using MordheimLedgerApp.Resources.Icons;

namespace MordheimLedgerApp.Converters
{
    /// <summary>
    /// Category -> RPG Awesome glyph shown on a warrior card's equipment/skill chips (see
    /// WarbandDetailPage). First pass, not exhaustive - RPG Awesome has no perfect match for every
    /// category, so a few of these are best-effort placeholders to refine later.
    /// </summary>
    public class EquipmentCategoryIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is EquipmentCategory category ? category switch
            {
                EquipmentCategory.MeleeWeapon => RpgFont.RaSword,
                EquipmentCategory.MissileWeapon => RpgFont.RaCrossbow,
                EquipmentCategory.BlackPowderWeapon => RpgFont.RaMusket,
                EquipmentCategory.Ammunition => RpgFont.RaArrowCluster,
                EquipmentCategory.Armour => RpgFont.RaVest,
                EquipmentCategory.MiscellaneousEquipment => RpgFont.RaPotion,
                EquipmentCategory.Animal => RpgFont.RaPawprint,
                EquipmentCategory.Consumable => RpgFont.RaVial,
                EquipmentCategory.DrugsAndPoisons => RpgFont.RaPoisonCloud,
                _ => RpgFont.RaPotion
            } : RpgFont.RaPotion;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class SkillCategoryIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is SkillCategory category ? category switch
            {
                SkillCategory.Combat => RpgFont.RaCrossedSwords,
                SkillCategory.Shooting => RpgFont.RaArcheryTarget,
                SkillCategory.Academic => RpgFont.RaBook,
                SkillCategory.Strength => RpgFont.RaMuscleUp,
                SkillCategory.Speed => RpgFont.RaShoePrints,
                SkillCategory.Special => RpgFont.RaAura,
                _ => RpgFont.RaAura
            } : RpgFont.RaAura;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
