namespace MordheimLedgerApp.Core.Data;

/// <summary>Ids stables (voir WarbandSeedData's Id / OfficialId des entités du catalogue) des quelques
/// entrées officielles que le code doit reconnaître nommément - à comparer à l'OfficialId d'une entrée,
/// jamais à son nom (traduit, et renommable dans la Bibliothèque). Une entrée créée par l'utilisateur n'a
/// pas d'OfficialId : elle n'est jamais prise pour l'une de celles-ci, même si elle porte le même nom.</summary>
public static class OfficialIds
{
    /// <summary>Bénédiction d'une arme (Sanctuaire, Sœurs de Sigmar/Chasseurs de Sorcières), cumulable
    /// avec un matériau - voir WarriorEquipment.BlessingRule.</summary>
    public const string BlessedWeaponRule = "rule.blessed-weapon";

    /// <summary>"Une Poignée d'Or" (Ulli &amp; Marquand) - voir EndOfGamePageViewModel.PairEngagement.</summary>
    public const string FistfulOfCrownsRule = "rule.a-fistful-of-crowns";

    /// <summary>Règles proposées automatiquement par les cases à cocher de l'édition d'un archétype de
    /// guerrier (Chef, Jeteur de sorts, Grande cible, Sans équipement, Ne gagne jamais d'Expérience).</summary>
    public const string LeaderRule = "rule.leader";
    public const string WizardRule = "rule.wizard";
    public const string LargeTargetRule = "rule.large-target";
    public const string NoEquipmentRule = "rule.no-equipment";
    public const string NeverGainsExperienceRule = "rule.never-gains-experience";

    /// <summary>Zombie des Morts-Vivants - recrue gratuite de certains résultats d'Exploration et des
    /// prisonniers d'une bande de Morts-Vivants.</summary>
    public const string ZombieWarrior = "warrior.undead.zombie";

    /// <summary>Gladiateur - adversaire éphémère de "Vendu aux Fosses" (Blessure Grave 65).</summary>
    public const string PitFighterHiredSword = "hiredsword.pit-fighter";

    public const string UndeadWarband = "warband.undead";
    public const string CultOfThePossessedWarband = "warband.cult-of-the-possessed";
}
