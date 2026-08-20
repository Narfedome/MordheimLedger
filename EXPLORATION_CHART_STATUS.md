# Table d'Exploration — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Revenus/Exploration (voir le plan de séquencement).
Mis à jour à chaque avancée — dernière mise à jour : **2026-08-20** (Table des Artefacts Magiques,
La Fosse, Arène de Combat).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

## Vue d'ensemble

| Groupe | Mécanisme | Statut |
|---|---|---|
| A — branche(s) fixe(s) | Or/Objet/Pierre magique résolu par un sous-jet D6 ou une branche unique | **18/18 ✅** |
| B — choix du joueur (`RollsIndependently`) | Plusieurs branches selon le type de bande, aucune UI de sélection encore | **0/6 ⏳** |
| C — texte pur, aucun `Outcome` | Effet à appliquer à la prochaine bataille | **0/3 ⏳** (`Warband.NextGameNotes` pas construit) |
| D — sous-table Artefacts Magiques | 6 objets nommés uniques, référencée par 2 entrées | **✅ 6/6 catalogués** (Villa d'un Noble mécanisée ; Trésor Caché reste Groupe B) |

## Détail (ordre des dés, pour tester avec de vrais dés)

Colonne Testé : ✅ = vérifié en jeu par l'utilisateur, ❌ = Groupe B/C (choix conditionné à la bande/texte
pur, pas encore d'UI). Tout résultat au Statut ✅ a été rejoué en vrai (dernier point à jour :
2026-08-20).

| Dés | Résultat | Statut | Testé | Note |
|---|---|---|---|---|
| 2,1 | Puits | ✅ | ✅ | Vrai test d'Endurance (choix du Héros, jet comparé à sa stat) - réussite = pierre magique, échec = statut `Malade` (indisponible prochaine partie, effacé auto au Fin de Partie suivant). Un jet de 6 est un échec automatique quelle que soit la stat (RulesReference "Tests de caractéristiques", `Core.Rules.ExplorationChart.PassesStatTest` - manquant jusqu'à cette passe, corrigé aussi pour le test de Commandement additionnel de Bâtiment Éventré, où l'exception ne s'applique PAS) |
| 2,2 | Boutique | ✅ | ✅ | Or (D6) + Porte-bonheur bonus si le même jet vaut 1 (`BonusItemOutcome`) |
| 2,3 | Cadavre | ✅ | ✅ | Premier cas validé (sous-jet à branches exclusives) |
| 2,4 | Traînard | ⏳ | ❌ | Groupe B — Skavens/Possédés/Morts-Vivants/autres |
| 2,5 | Charrette Renversée | ✅ | ✅ | Branche 5-6 : Épée + Dague ornées, réellement ajoutées à l'inventaire (`SecondaryEquipmentItemName`) et vendables à x2 leur valeur normale (matériau "Arme Ornée", `SpecialRule.IsResaleUpgrade`, bouton "Vendre" dans l'inventaire de bande) |
| 2,6 | Masures en Ruine | ✅ | ✅ | Branche unique |
| 3,1 | Taverne | ⏳ | ❌ | Groupe B — test de Commandement |
| 3,2 | Forge | ✅ | ✅ | |
| 3,3 | Prisonniers | ⏳ | ❌ | Groupe B |
| 3,4 | Atelier du Fléchier | ✅ | ✅ | |
| 3,5 | Halle du Marché | ✅ | ✅ | Branche unique |
| 3,6 | Une Faveur Rendue | ⏳ | ❌ | Groupe C — Mercenaire à Louer gratuit, `NextGameNotes` pas construit |
| 4,1 | Armurier à Poudre | ✅ | ✅ | |
| 4,2 | Sanctuaire | 🔧 | ❌ | Or (3D6) fait ; la bénédiction d'arme (Sœurs de Sigmar/Chasseurs de Sorcières uniquement) rejoint le Groupe B, en attente de son UI de choix - voir Hors périmètre |
| 4,3 | Maison de Ville | ✅ | ✅ | Branche unique |
| 4,4 | Armurerie | ✅ | ✅ | Branche 1-2 : choix du joueur Bouclier ou Rondache (`AlternativeEquipmentItemName`) |
| 4,5 | Cimetière | ⏳ | ❌ | Groupe B |
| 4,6 | Catacombes | ⏳ | ❌ | Groupe C — déploiement spécial, `NextGameNotes` pas construit |
| 5,1 | Maison du Prêteur | ✅ | ✅ | Branche unique |
| 5,2 | Laboratoire de l'Alchimiste | ✅ | ✅ | Or (3D6) + carnet trouvé (`SecondaryEquipmentItemName`) - le Héros qui le porte débloque Érudition en plus de ses listes habituelles dès sa prochaine compétence gagnée (`EquipmentItem.GrantsSkillCategory`, voir `Core.Rules.SkillEligibility`) |
| 5,3 | Bijoutier | ✅ | ✅ | 4 gemmes réelles (Pierres de Quartz/Améthyste/Collier/Rubis), vendables directement (`EquipmentItem.IsSellable`, pas un matériau - ce sont les objets eux-mêmes qui ont de la valeur) - valeur trouvée fixe au catalogue pour Améthyste/Collier, jetée en D6x5/D6x15 pour Quartz/Rubis (`ExplorationOutcome.FoundValueFormula` → `WarbandEquipment.FoundValueOverride`, affichée dans l'inventaire de bande et dans le popup détail au lieu du prix catalogue). Si gardée sur un Héros plutôt que vendue : +1 sur les jets d'objets rares (`EquipmentItem.GrantsRareItemSearchBonus`, `Core.Rules.RareItemSearchBonus` - la recherche d'objets rares elle-même n'est pas encore construite, ce n'est qu'une préparation de données) |
| 5,4 | Maison du Marchand | ✅ | ✅ | Or (2D6x5) normalement, mais un double sur le 2D6 donne le Symbole de l'Ordre des Libres Marchands (`ExplorationResult.RequiresDoubleRoll` + `ExplorationOutcome.RequiresDoubleRoll`, 2 champs de dé dédiés - le total seul ne suffit pas à détecter un double). Un seul jet de 2D6 sert aux deux fins : si ce n'est pas un double, l'or (2D6x5) est calculé directement depuis ces 2 dés (`Core.Rules.DiceFormula.Apply`) plutôt que redemander un jet séparé - le livre ne prévoit qu'un seul lancer ici. Le Héros qui porte le symbole peut choisir Marchandage à sa prochaine Progression, même hors de ses listes habituelles (`EquipmentItem.GrantsSpecificSkillName`, `Core.Rules.SkillEligibility.EffectiveExtraSkillNames`, picker de compétence étendu) |
| 5,5 | Bâtiment Éventré | ✅ | ✅ | Pierre magique (D3) toujours trouvée + test de Commandement ADDITIONNEL du chef de bande (`ExplorationResult.BonusStatTestField`, `Core.Rules.ExplorationOutcomeResolver.ResolveBonusStatTestOutcome` - s'ajoute à la pierre magique plutôt que la remplacer, contrairement à Puits) - en cas de réussite, un Chien de guerre rejoint la bande. Toujours le chef de bande, jamais un choix du joueur : nouveau `Warrior.IsLeader`/`WarriorArchetype.IsLeader` (flag explicite dans chaque JSON de bande, un seul par bande), pas de concept de "chef" avant cette passe. Jet en 2D6 (pas 1D6) : les tests de Commandement sont une exception explicite à la règle générale des tests de caractéristique (RulesReference `Regles.md`) - `Core.Rules.ExplorationChart.RollStatTest`, appliqué aussi rétroactivement au test de Puits (Endurance, resté en 1D6, correct) pour que le même mécanisme serve les deux |
| 5,6 | Entrée des Catacombes | ⏳ | ❌ | Groupe C — relance permanente, `NextGameNotes`/`HasCatacombReroll` pas construits |
| 6,1 | La Fosse | ✅ | ✅ | Envoi d'un Héros optionnel (`ExplorationResult.RequiresSentHero`, Picker sans stat comparée - "si vous le souhaitez", refuser d'envoyer quelqu'un est valide) - sous-jet 1 : le Héros envoyé est dévoré, `Warrior.Status` passe à `Dead` + phrase d'Historique dédiée (`ExplorationOutcome.CausesDeath`, même mécanisme que Sick du Puits - posé après `ApplyWarriorOutcomesAsync` pour éviter l'écrasement par sa resynchronisation de statut) ; sous-jet 2-6 : D6+1 pierres magiques, indépendant de qui a été envoyé |
| 6,2 | Trésor Caché | ⏳ | ❌ | Groupe B — branche Artefact (sous-jet 5-6) désormais data-complète (`TriggersArtefactRoll`, Groupe D résolu), mais l'entrée entière reste non câblée dans le wizard (sélection de branche par le joueur, comme le reste du Groupe B) |
| 6,3 | Forge Naine | ✅ | ✅ | Règle Gromril attachée sur les 3 branches concernées |
| 6,4 | Bande Massacrée | ⏳ | ❌ | Groupe B |
| 6,5 | Arène de Combat | ✅ | ✅ | Manuel d'Entraînement réel et vendable (100 CG, `EquipmentItem.IsSellable`, comme les gemmes du Bijoutier) plutôt que de l'or brut - le Héros qui le porte débloque Combat en plus de ses listes habituelles (`GrantsSkillCategory`, comme le Carnet de l'Alchimiste). Le bonus "+1 CC au-delà du maximum racial" reste purement descriptif (texte de la SpecialRule "Formation au Combat") - au bon vouloir des joueurs, pas de plafond de caractéristiques suivi par l'appli |
| 6,6 | Villa d'un Noble | ✅ | ✅ | Branches 1-4 inchangées ; branche 5-6 déclenche un second jet de D6 sur la table des Artefacts Magiques (`ExplorationOutcome.TriggersArtefactRoll`, `Core.Rules.MagicalArtefactTable.RollForItemName`) qui résout un objet réel parmi les 6 (Groupe D, désormais construit) |

## Hors périmètre pour l'instant

- **Groupe B** (Traînard/Taverne/Prisonniers/Cimetière/Trésor Caché/Bande Massacrée) : UI de sélection de branche par le joueur, plus les sous-mécaniques Recrutement gratuit (`Kind.Recruit`) et Expérience répartie (`Kind.Experience`) qu'elles impliquent (voir le plan de séquencement). **Sanctuaire** (4,2) rejoint cette liste pour sa bénédiction d'arme (Sœurs de Sigmar/Chasseurs de Sorcières uniquement, choix du joueur sur un objet déjà porté, pas une trouvaille) - même mécanique de choix conditionné à la bande que le reste du Groupe B, discuté puis explicitement reporté au 2026-08-18 plutôt que traité comme un cas isolé. Sa branche Or (3D6, toutes bandes) reste ✅.
- **Groupe C** (Une Faveur Rendue/Catacombes/Entrée des Catacombes) : `Warband.NextGameNotes` (pense-bête sur la fiche de bande) + `Warband.HasCatacombReroll`/`PendingExplorationBonusDie` pour les effets mécanisables.
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
- **2026-08-18 (Sanctuaire reclassé, choix Bouclier/Rondache de l'Armurerie)** — Sanctuaire (4,2) repassé à 🔧 : sa branche Or reste faite, mais sa bénédiction d'arme (Sœurs de Sigmar/Chasseurs de Sorcières, choix du joueur sur une arme déjà portée) a la même forme que le Groupe B (choix conditionné à la bande) et est explicitement reportée avec lui plutôt que traitée comme un cas isolé. Séparément, l'Armurerie (4,4) avait une vraie divergence au texte du livre : la branche 1-2 ("D3 Shields or Bucklers, choose which") ne proposait que le Bouclier (Shield), jamais la Rondache (Buckler). Contrairement à Sanctuaire, ce choix ne dépend d'aucune bande et ne modifie rien d'existant - juste "lequel des deux objets trouvés", donc pas concerné par le report Groupe B. Nouveau champ `ExplorationOutcome.AlternativeEquipmentItemName` (un "OU", à distinguer de `SecondaryEquipmentItemName` qui est un "ET" - Charrette Renversée) : la branche Item du wizard affiche 2 `RadioButton` (masqués pour toute autre branche) quand il est renseigné, chacun accompagné d'un vrai `ChipView` tapable (icône + popup détail) en frère, pas en `RadioButton.Content` (un View arbitraire posé en Content ne s'affiche pas côté Windows/WinUI - repéré en testant), et le nom réellement ajouté à l'inventaire (`EndOfGameDialogViewModel.ChosenExplorationItemName`) suit le choix du joueur plutôt que l'`EquipmentItemName` brut. Correctif texte du livre au passage : la description FR (déjà en place avant cette session) disait "Boucliers ou **Écus**" - "Écu" ne correspond à aucun objet du catalogue, la traduction réelle de Buckler est **Rondache** (voir Equipment.json) - corrigé pour matcher ce que les RadioButton affichent réellement.
- **2026-08-18 (Laboratoire de l'Alchimiste)** — Deuxième mécanique "permanente" de la table après le
  test de caractéristique du Puits, mais de forme différente : pas un jet, un choix optionnel ("un de
  vos Héros PEUT l'étudier") qui modifie durablement ce Héros plutôt que de produire un gain immédiat.
  Nouveau champ `ExplorationOutcome.GrantsSkillCategory` (`SkillCategory?`, "Academic" ici) - coexiste
  avec la branche Or existante (Or 3D6 + ce choix, comme le Porte-bonheur de Boutique coexiste avec son
  Or) plutôt que de remplacer le Kind. Étape wizard : `ChipListView` des Héros vivants (même
  éligibilité que le test du Puits), tap pour choisir - ne rien choisir est un état valide, aucune
  validation ne bloque Suivant. À la sauvegarde, la catégorie s'ajoute une fois à
  `Warrior.AllowedSkillCategories` du Héros choisi (persistée aussitôt) : l'étape Progression existante
  (`PickAdvanceSkill`) filtre déjà par cette liste, donc l'accès à Érudition s'applique automatiquement
  dès la prochaine compétence gagnée par ce Héros, sans aucun câblage supplémentaire ni maintenant ni
  lors d'une future Fin de Partie. Traduction FR de la branche également corrigée sur demande de
  l'utilisateur (le texte alors en place divergeait de la sienne).
- **2026-08-20 (tests de caractéristique - règle du 6, La Fosse, Arène de Combat, artefacts)** —
  Plusieurs passes. (1) `Core.Rules.ExplorationChart.PassesStatTest` encode l'exception du livre "un 6
  est toujours un échec" pour les tests de caractéristique en 1D6 (Puits) - n'affecte PAS les tests de
  Commandement en 2D6 (aucune exception équivalente au livre). (2) La Fosse (6,1) : Héros envoyé
  optionnel (`ExplorationResult.RequiresSentHero`, "Passer son chemin" comme choix par défaut explicite
  plutôt qu'un picker vide) - sous-jet 1 dévore le Héros (`ExplorationOutcome.CausesDeath`,
  `Warrior.Status = Dead`, phrase d'Historique dédiée) ; sous-jet 2-6 : D6+1 pierres magiques,
  indépendant du Héros envoyé. (3) Terminologie pierre magique reconfirmée cohérente (Puits/Bâtiment
  Éventré/La Fosse/Wyrdstone Shards) - gap trouvé au passage : `Warband.WyrdstoneShards` ne s'affichait
  nulle part dans l'UI, corrigé. (4) Devise FR corrigée : **"CO" (Couronne d'Or) est la bonne
  abréviation française, "CG"/"gc" (Gold Crown) est anglais uniquement** - toute occurrence "CG" dans
  du texte FR (9 clés `AppStrings.resx`, `Equipment.json`, `ExplorationResults.json`) remplacée par
  "CO" ; `AppStrings.en.resx` déjà correct, non touché. (5) Arène de Combat (6,5) : Manuel
  d'Entraînement réel et vendable (100 CO, `IsSellable`) qui débloque Combat en plus des listes
  habituelles (`GrantsSkillCategory`, même mécanisme que le Carnet de l'Alchimiste) - le bonus "+1 CC
  au-delà du maximum racial" reste purement descriptif, aucun plafond de caractéristiques suivi par
  l'appli (décision explicite, pas de moteur de règles V1). (6) **Table des Artefacts Magiques**
  (Groupe D) : 6 objets uniques catalogués (Bottes et Corde de Pieter, Miséricorde du Comte de
  Ventimiglia, Armure d'Att'la, Arc Traqueur, Cagoule d'Exécuteur, Œil Omniscient de Numas), noms FR
  alignés sur la traduction officielle du livre (VF fournie par l'utilisateur, corrige 3 noms inventés
  faute de référence au moment de l'écriture initiale : "Cotte de Mailles d'Att'la"→"Armure d'Att'la",
  "Arc de la Traque"→"Arc Traqueur", "Cagoule du Bourreau"→"Cagoule d'Exécuteur"), résolue par
  `Core.Rules.MagicalArtefactTable.RollForItemName` (lookup pur, pas en base - référencée par 2 entrées
  de la table d'Exploration, pas liée à un seul jet). Réutilise les règles déjà en base quand pertinent
  (Parade (Épée), Pointes Barbelées, Modificateur de sauvegarde (Arc Elfique) comme stubs ; **Frénésie** promue de
  règle inline (Champignons Fous, Horde Orque) en entrée partagée `SpecialRules.json`, la Cagoule
  d'Exécuteur gardant sa propre règle distincte pour l'override "ne prend jamais fin"). Œil Omniscient de
  Numas câblé pour de vrai : `EquipmentItem.GrantsBonusExplorationDice` + nouveau
  `Core.Rules.ExplorationDiceBonus.EffectiveBonusDice`, branché sur le paramètre `bonusDice` de
  `ComputeDiceCount` (jusque-là jamais alimenté). Villa d'un Noble (6,6) branche 5-6 et Trésor Caché
  (6,2) branche Artefact gagnent `ExplorationOutcome.TriggersArtefactRoll` (l'objet précis vient d'un
  second D6, `EquipmentItemName` reste null sur cette branche) - Villa d'un Noble est désormais
  entièrement câblée (second jet + ajout à l'inventaire), Trésor Caché reste Groupe B dans son
  ensemble. (7) Nouvelle catégorie `EquipmentCategory.MagicalArtefact` créée pour ces 6 objets plutôt
  que de les répartir dans Arme/Armure/Divers (demande explicite) - exclue des pickers d'achat/liste de
  départ (`EquipmentItemViewModel.ApplyFilter`, uniquement dans le bloc `AllowedWarbandArchetypeId`,
  donc toujours visible en consultation Codex), icône `RaRuneStone` (glyphe pas encore utilisé
  ailleurs, cf. convention "un glyphe = un concept").
- **2026-08-20 (Artefacts Magiques - complément de description après texte officiel complet)** —
  L'utilisateur a fourni le texte anglais intégral du livre + la VF officielle (p.141) pour les 6
  artefacts, permettant de corriger des lacunes passées inaperçues faute de référence complète au
  moment de l'écriture initiale (§ ci-dessus). Trois noms FR renommés pour matcher la traduction
  officielle plutôt qu'une traduction improvisée : "Cotte de Mailles d'Att'la"→**"Armure d'Att'la"**,
  "Arc de la Traque"→**"Arc Traqueur"**, "Cagoule du Bourreau"→**"Cagoule d'Exécuteur"** (mis à jour
  partout : nom de l'objet, nom de sa règle propre, et la description de Villa d'un Noble qui énumère
  les 6 noms). Deux vraies lacunes mécaniques comblées : (1) l'Armure d'Att'la est explicitement une
  **Armure de Gromril** gravée de 3 runes (précision absente du texte initial, ajoutée en description -
  purement descriptif, aucun système de sauvegarde d'armure suivi par l'appli pour l'instant, comme
  pour toute autre armure) ; (2) l'Arc Traqueur doit être traité comme un **Arc Elfique** (modificateur
  de sauvegarde -1, règle déjà existante "Save Modifier (Elf Bow)"/"Modificateur de sauvegarde (Arc
  Elfique)", ajoutée en stub) et ses flèches comptent comme des **Flèches de Chasse** (+1 aux jets de
  Blessure) - ce second point était déjà correctement câblé (stub "Barbed Arrowheads"/"Pointes
  Barbelées" déjà attaché), seul le texte de la règle propre à l'objet ne le rendait pas explicite,
  corrigé. Cagoule d'Exécuteur et Œil Omniscient de Numas : texte affiné pour coller de plus près au
  libellé officiel (aucun changement mécanique, les deux étaient déjà complets).
