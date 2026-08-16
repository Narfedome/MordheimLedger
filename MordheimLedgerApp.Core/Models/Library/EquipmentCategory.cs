namespace MordheimLedgerApp.Core.Models.Library;

public enum EquipmentCategory
{
    MeleeWeapon = 0,
    MissileWeapon = 1,
    BlackPowderWeapon = 2,
    Ammunition = 3,
    Armour = 4,
    MiscellaneousEquipment = 5,

    /// <summary>Mounts (Riding Horse, Warhorse, Wardog, War Boar...) - considered equipment by the
    /// rulebook, folded into this same catalog rather than a separate content type. See EquipmentItem's
    /// profile-stat fields, only meaningful for this category.</summary>
    Animal = 6
}
