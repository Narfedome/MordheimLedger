# Table d'Exploration — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Revenus/Exploration (voir le plan de séquencement).
Mis à jour à chaque avancée — dernière mise à jour : **2026-08-18** (refactor du wizard Fin de Partie).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

## Vue d'ensemble

| Groupe | Mécanisme | Statut |
|---|---|---|
| A — branche(s) fixe(s) | Or/Objet/Pierre magique résolu par un sous-jet D6 ou une branche unique | **18/18 ✅** |
| B — choix du joueur (`RollsIndependently`) | Plusieurs branches selon le type de bande, aucune UI de sélection encore | **0/6 ⏳** |
| C — texte pur, aucun `Outcome` | Effet à appliquer à la prochaine bataille | **0/3 ⏳** (`Warband.NextGameNotes` pas construit) |
| D — sous-table Artefacts Magiques | 6 objets nommés uniques, référencée par 2 entrées | **⏳** (pas commencé) |

## Détail (ordre des dés, pour tester avec de vrais dés)

| Dés | Résultat | Statut | Note |
|---|---|---|---|
| 2,1 | Puits | ✅ | Vrai test d'Endurance (choix du Héros, jet comparé à sa stat) - réussite = pierre magique, échec = statut `Malade` (indisponible prochaine partie, effacé auto au Fin de Partie suivant) |
| 2,2 | Boutique | ✅ | Or (D6) + Porte-bonheur bonus si le même jet vaut 1 (`BonusItemOutcome`) |
| 2,3 | Cadavre | ✅ | Premier cas validé (sous-jet à branches exclusives) |
| 2,4 | Traînard | ⏳ | Groupe B — Skavens/Possédés/Morts-Vivants/autres |
| 2,5 | Charrette Renversée | ✅ | Branche 5-6 : Épée + Dague ornées, réellement ajoutées à l'inventaire (`SecondaryEquipmentItemName`) et vendables à x2 leur valeur normale (`SellMultiplier`, bouton "Vendre" dans l'inventaire de bande) |
| 2,6 | Masures en Ruine | ✅ | Branche unique |
| 3,1 | Taverne | ⏳ | Groupe B — test de Commandement |
| 3,2 | Forge | ✅ | |
| 3,3 | Prisonniers | ⏳ | Groupe B |
| 3,4 | Atelier du Fléchier | ✅ | |
| 3,5 | Halle du Marché | ✅ | Branche unique |
| 3,6 | Une Faveur Rendue | ⏳ | Groupe C — Mercenaire à Louer gratuit, `NextGameNotes` pas construit |
| 4,1 | Armurier à Poudre | ✅ | |
| 4,2 | Sanctuaire | ✅ | Branche unique |
| 4,3 | Maison de Ville | ✅ | Branche unique |
| 4,4 | Armurerie | ✅ | |
| 4,5 | Cimetière | ⏳ | Groupe B |
| 4,6 | Catacombes | ⏳ | Groupe C — déploiement spécial, `NextGameNotes` pas construit |
| 5,1 | Maison du Prêteur | ✅ | Branche unique |
| 5,2 | Laboratoire de l'Alchimiste | ✅ | Branche unique |
| 5,3 | Bijoutier | ✅ | |
| 5,4 | Maison du Marchand | ✅ | Branche unique |
| 5,5 | Bâtiment Éventré | ✅ | Pierre magique |
| 5,6 | Entrée des Catacombes | ⏳ | Groupe C — relance permanente, `NextGameNotes`/`HasCatacombReroll` pas construits |
| 6,1 | La Fosse | ✅ | Branche 1 (Héros dévoré) consignée en Historique |
| 6,2 | Trésor Caché | ⏳ | Groupe B + référence la table Artefacts Magiques (Groupe D) |
| 6,3 | Forge Naine | ✅ | Règle Gromril attachée sur les 3 branches concernées |
| 6,4 | Bande Massacrée | ⏳ | Groupe B |
| 6,5 | Arène de Combat | ✅ | Branche unique |
| 6,6 | Villa d'un Noble | ✅* | *Branches 1-4 complètes ; branche 5-6 (artefact magique) consignée en Historique mais ne donne pas encore un objet nommé réel (Groupe D) |

## Hors périmètre pour l'instant

- **Groupe B** (Traînard/Taverne/Prisonniers/Cimetière/Trésor Caché/Bande Massacrée) : UI de sélection de branche par le joueur, plus les sous-mécaniques Recrutement gratuit (`Kind.Recruit`) et Expérience répartie (`Kind.Experience`) qu'elles impliquent (voir le plan de séquencement).
- **Groupe C** (Une Faveur Rendue/Catacombes/Entrée des Catacombes) : `Warband.NextGameNotes` (pense-bête sur la fiche de bande) + `Warband.HasCatacombReroll`/`PendingExplorationBonusDie` pour les effets mécanisables.
- **Groupe D** (Artefacts Magiques) : 6 `EquipmentItem` à ajouter au catalogue, référencés par Trésor Caché et Villa d'un Noble.
- **Vente de la pierre magique** : `Warband.WyrdstoneShards` s'accumule déjà (Puits/Bâtiment Éventré/La Fosse), mais l'étape wizard dédiée à la revente n'existe pas encore — table de prix pas encore fournie.

## Journal

- **2026-08-18** — Socle (jet de dés, détection du résultat unique, sous-jet à branches exclusives) validé sur Cadavre ; reste du Groupe A à branches fixes complété ; correctifs Wyrdstone/branches `None`/règle Gromril ; découpage en deux étapes (jet puis résultat) ; suppression du tirage automatique (le joueur tape son jet ou clique le dé) ; quantité d'objet fixe (`x1`) sans jet sauf indication contraire ; Boutique (forme mixte Or+Objet sur le même dé) ; correction de l'affichage des noms d'objet (anglais → langue courante) ; inventaire de bande (`WarbandEquipment`) pour les objets trouvés non assignés à un guerrier.
- **2026-08-18 (suite)** — Corrigé un bug où le jet d'or de n'importe quel résultat (ex. Cadavre) pouvait déclencher à tort le Porte-bonheur bonus de Boutique (`BonusItemOutcome` ne vérifiait pas que la branche Or résolue était bien la branche Auto de Boutique). Puits : vrai test de caractéristique (`ExplorationResult.StatTestField`/`ExplorationOutcome.StatTestPass`, réutilisable pour Taverne/Bâtiment Éventré) - choix d'un Héros, jet comparé à son Endurance (calcul automatique, pas un tirage aléatoire), échec → nouveau statut `WarriorStatus.Sick` (« Malade », badge sur la fiche, effacé automatiquement au Fin de Partie suivant).
- **2026-08-18 (refactor)** — Les deux bugs ci-dessus venaient de règles réelles mélangées à de l'orchestration UI dans des fichiers devenus gargantuesques, sans couverture de test possible. Chantier dédié (plan complet, pas de nouvelle mécanique) : (1) résolution des `Outcome` (Auto/sous-jet/test de caractéristique/objet bonus) extraite vers `Core.Rules.ExplorationOutcomeResolver`, testée (9 tests, dont une régression explicite du bug Boutique/Cadavre) ; (2) `EndOfGameDialogViewModel.cs` (1211 lignes) découpé en fichier central + `.Injury.cs`/`.Advance.cs`/`.Exploration.cs` (partial class) + 4 fichiers de classes annexes (`WarriorOutcomeRow`/`AdvanceRollEntry`/`InjurySubRollEntry`/`ExplorationDieEntry`) ; (3) `WarbandDetailViewModel.EndOfGame()` (une méthode de ~250 lignes) déplacé vers `WarbandDetailViewModel.EndOfGame.cs` et décomposé en 3 phases nommées (`ApplyExplorationOutcomeAsync`/`ApplyWarriorOutcomesAsync`/`ApplySicknessLifecycleAsync`), avec l'invariant d'ordre (Sickness après Warriors) rendu explicite en commentaire à l'appel plutôt qu'implicite. Aucun changement de comportement voulu - 140/140 tests passent, build MAUI clean après chaque étape.
- **2026-08-18 (terminologie)** — "Wyrdstone Shard(s)" reste en anglais tel quel côté EN (déjà le cas partout). Côté FR, remplacé "pierre de sorcière" par **"pierre magique"** (terme officiel du core book, confirmé dans `RulesReference/Campagne.md`/`MarcheEtMagie.md` - "fragments de pierre magique", "Pendule en pierre magique") - correction dans `ExplorationResults.json` (Puits/Bâtiment Éventré/La Fosse/Trésor Caché) et 2 clés `AppStrings.resx`. Le reste du catalogue (Skavens, Morts-Vivants, Culte des Possédés, Nains Chasseurs de Trésors, compétence Chasseur de Pierre Magique) utilisait déjà ce terme - seul le contenu Exploration ajouté cette session avait dévié.
- **2026-08-18 (revente Charrette Renversée)** — Premier cas réel de revente à valeur majorée du livre : `ExplorationOutcome` gagne `SecondaryEquipmentItemName` (un second objet réel du catalogue, ex. Dague en plus de l'Épée - pas un objet bundle inventé, pour rester équipable/vendable séparément) et `SellMultiplier` (2 pour ce résultat, null pour tout le reste - aucune mécanique générique de revente d'équipement dans le livre). Porté jusqu'à `Models.WarbandEquipment.SellMultiplier`/`IsSellable` et un nouveau `IWarbandService.SellWarbandItemAsync` (crédite Trésorerie = Cost×Quantity×SellMultiplier, supprime la ligne). `WarbandInventoryDialog` gagne un bouton "Vendre" à côté d'"Équiper", visible uniquement si `IsSellable`. Au passage : ajout de **Flèches de Chasse** au catalogue (`Equipment.json`, catégorie `Ammunition` - jusque-là déclarée dans l'enum mais jamais utilisée par aucun objet), suite à un test utilisateur de l'Atelier du Fléchier qui l'a repérée manquante.
- **2026-08-18 (catégories d'équipement)** — Suite au repérage de Flèches de Chasse manquante, ajout de deux catégories `EquipmentCategory` : `Consumable`/"Consommable" et `DrugsAndPoisons`/"Drogues et Poisons", pour désengorger `MiscellaneousEquipment` (30 objets avant ce passage). `Ammunition` (déjà dans l'enum, jamais utilisée) renommée en FR "Projectiles et Munitions" plutôt que dupliquée. Reclassement complet des 30 objets Divers : **Consommable** (Bière de Bugman, Larmes de Shallaya, Grimoire, Herbes Curatives - bu/appliqué/lu une fois) ; **Drogues et Poisons** (Ombre Cramoisie, Racine de Mandragore, Lotus Noir, Venin Sombre) ; **Projectiles et Munitions** (Chausse-trapes, Flèches Incendiaires, Bombe Incendiaire, Poudre Éclair, Poudre Noire Supérieure - jeté/tiré sur l'ennemi ou rechargé dans une arme) ; le reste (17 objets : Bannière, Cape Elfique, Lanterne, Rossignols, Porte-bonheur, Carte de Mordheim, Filet, Patte de lapin, Corde à grappin, Longue-vue, Torche, Cor de Guerre, Pendule de Malepierre, Ail, Livre de Cuisine Halfling, Cartes de Tarot, Vêtements en Soie de Cathay) reste en Équipement Divers - porté/outil permanent, rien à consommer/tirer.
- **2026-08-18 (Atelier du Fléchier + chips)** — Bug trouvé par test utilisateur : la branche 5 de l'Atelier du Fléchier référençait `"Quiver of Hunting Arrows"`, un nom absent du catalogue (résolution par nom exact - voir ExplorationOutcome.EquipmentItemName), donc silencieusement sans effet ; corrigé vers `"Hunting Arrows"` (le nom réellement seedé). Un balayage systématique JSON de toutes les références `equipmentItemName`/`secondaryEquipmentItemName` de la table contre le catalogue confirme que c'était la seule branche déjà **✅** (implémentée) cassée - Trésor Caché référence aussi `Holy Relic`/`Holy Tome`, absents du catalogue, mais ce résultat est encore Groupe B (⏳), pas un bug actif. Au passage, la Vue Fin de Partie affiche maintenant l'objet trouvé (branche Item + objet bonus) via un vrai `ChipView` tapable (icône de catégorie + popup détail) plutôt qu'un `Label` texte - même langage d'interaction que le reste de l'app pour toute référence Équipement (`EndOfGameDialogViewModel` reçoit désormais un dictionnaire nom-anglais→`EquipmentItem` entier, plus seulement un nom affiché).
- **2026-08-18 (Charrette Renversée révisée - matériau plutôt que champ ad-hoc)** — Retour de l'utilisateur : le `SellMultiplier` ajouté plus haut dupliquait un mécanisme déjà existant (Gromril/Ithilmar, `SpecialRule.CostMultiplier` appliqué via `MaterialRuleName`). Remplacé par un vrai matériau **"Arme Ornée"** (`SpecialRules.json`, `costMultiplier: 2`, même forme que Gromril/Ithilmar) référencé par `MaterialRuleName` sur la branche 5-6 (au lieu de `sellMultiplier: 2`). Nouveau champ `SpecialRule.IsResaleUpgrade` (bool, false pour Gromril/Ithilmar - un achat normal ne se revend pas - true uniquement pour Arme Ornée) : `WarbandEquipment.IsSellable` en dérive directement (`MaterialRule?.IsResaleUpgrade == true`), et `IWarbandService.SellWarbandItemAsync` calcule le prix de revente avec **la même formule que l'achat** (`Core.Rules.EquipmentPricing.CalculateCost`) plutôt qu'un calcul de revente à part - `ExplorationOutcome.SellMultiplier`/`WarbandEquipment.SellMultiplier`/`WarbandEquipmentEntity.SellMultiplier` supprimés entièrement, plus petite surface de code pour un résultat identique. Bénéfice annexe : l'inventaire affiche maintenant "Épée (O)"/"Dague (O)" comme n'importe quel objet en Gromril/Ithilmar (`WarbandEquipment.NameDisplay`), au lieu d'un nom nu.
