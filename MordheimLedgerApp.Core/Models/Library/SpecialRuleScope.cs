namespace MordheimLedgerApp.Core.Models.Library;

/// <summary>Où cette règle a un sens à être ajoutée depuis un sélecteur - filtre le picker de
/// WarbandArchetypeEditDialog (Warband/Both) vs WarriorArchetypeEditDialog (Warrior/Both). N'affecte
/// pas les règles attachées via Équipement/Animal (matériaux d'arme type "Gromril", traits de créature) :
/// ces attachs ne passent jamais par un sélecteur de règles, seul le classement Warband/Warrior compte
/// ici - voir SpecialRuleSeedData.Scope et le script de classification par contexte d'attache utilisé
/// pour peupler les JSON existants.</summary>
public enum SpecialRuleScope
{
    Warrior = 0,
    Warband = 1,
    Both = 2
}
