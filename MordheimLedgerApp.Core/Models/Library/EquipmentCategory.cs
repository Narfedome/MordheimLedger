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
    Animal = 6,

    /// <summary>Single-use items consumed on the spot for an immediate effect (e.g. Healing Herbs,
    /// Tears of Shallaya, Bugman's Ale) - split out of MiscellaneousEquipment (2026-08-18, user request)
    /// to stop it being the catch-all for everything that isn't a weapon/armour.</summary>
    Consumable = 7,

    /// <summary>Recreational drugs and blade poisons (e.g. Black Lotus, Mandrake Root, Dark Venom) -
    /// same split as Consumable above, kept distinct from it since these usually carry a real
    /// downside/risk rather than a plain benefit.</summary>
    DrugsAndPoisons = 8
}
