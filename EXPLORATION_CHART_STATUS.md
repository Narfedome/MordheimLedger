# Table d'Exploration — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Revenus/Exploration (voir le plan de séquencement).
Mis à jour à chaque avancée — dernière mise à jour : **2026-08-18** (test de caractéristique du Puits).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

## Vue d'ensemble

| Groupe | Mécanisme | Statut |
|---|---|---|
| A — branche(s) fixe(s) | Or/Objet/Pierre de sorcière résolu par un sous-jet D6 ou une branche unique | **18/18 ✅** |
| B — choix du joueur (`RollsIndependently`) | Plusieurs branches selon le type de bande, aucune UI de sélection encore | **0/6 ⏳** |
| C — texte pur, aucun `Outcome` | Effet à appliquer à la prochaine bataille | **0/3 ⏳** (`Warband.NextGameNotes` pas construit) |
| D — sous-table Artefacts Magiques | 6 objets nommés uniques, référencée par 2 entrées | **⏳** (pas commencé) |

## Détail (ordre des dés, pour tester avec de vrais dés)

| Dés | Résultat | Statut | Note |
|---|---|---|---|
| 2,1 | Puits | ✅ | Vrai test d'Endurance (choix du Héros, jet comparé à sa stat) - réussite = pierre de sorcière, échec = statut `Malade` (indisponible prochaine partie, effacé auto au Fin de Partie suivant) |
| 2,2 | Boutique | ✅ | Or (D6) + Porte-bonheur bonus si le même jet vaut 1 (`BonusItemOutcome`) |
| 2,3 | Cadavre | ✅ | Premier cas validé (sous-jet à branches exclusives) |
| 2,4 | Traînard | ⏳ | Groupe B — Skavens/Possédés/Morts-Vivants/autres |
| 2,5 | Charrette Renversée | ✅ | Branche 5-6 (épée/dague ornées) consignée en Historique |
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
| 5,5 | Bâtiment Éventré | ✅ | Pierre de sorcière |
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
- **Vente de la pierre de sorcière** : `Warband.WyrdstoneShards` s'accumule déjà (Puits/Bâtiment Éventré/La Fosse), mais l'étape wizard dédiée à la revente n'existe pas encore — table de prix pas encore fournie.

## Journal

- **2026-08-18** — Socle (jet de dés, détection du résultat unique, sous-jet à branches exclusives) validé sur Cadavre ; reste du Groupe A à branches fixes complété ; correctifs Wyrdstone/branches `None`/règle Gromril ; découpage en deux étapes (jet puis résultat) ; suppression du tirage automatique (le joueur tape son jet ou clique le dé) ; quantité d'objet fixe (`x1`) sans jet sauf indication contraire ; Boutique (forme mixte Or+Objet sur le même dé) ; correction de l'affichage des noms d'objet (anglais → langue courante) ; inventaire de bande (`WarbandEquipment`) pour les objets trouvés non assignés à un guerrier.
- **2026-08-18 (suite)** — Corrigé un bug où le jet d'or de n'importe quel résultat (ex. Cadavre) pouvait déclencher à tort le Porte-bonheur bonus de Boutique (`BonusItemOutcome` ne vérifiait pas que la branche Or résolue était bien la branche Auto de Boutique). Puits : vrai test de caractéristique (`ExplorationResult.StatTestField`/`ExplorationOutcome.StatTestPass`, réutilisable pour Taverne/Bâtiment Éventré) - choix d'un Héros, jet comparé à son Endurance (calcul automatique, pas un tirage aléatoire), échec → nouveau statut `WarriorStatus.Sick` (« Malade », badge sur la fiche, effacé automatiquement au Fin de Partie suivant).
