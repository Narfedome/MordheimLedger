# Table d'Exploration — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Revenus/Exploration (voir le plan de séquencement).
Mis à jour à chaque avancée — dernière mise à jour : **2026-08-21** (Cimetière ; Progression reconnectée
sur l'XP d'Exploration ; pense-bête "prochaine partie" `Warband.NextGameNote` ; Sanctuaire/bénédiction
d'arme).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

## Vue d'ensemble

| Groupe | Mécanisme | Statut |
|---|---|---|
| A — branche(s) fixe(s) | Or/Objet/Pierre magique résolu par un sous-jet D6 ou une branche unique | **18/18 ✅** |
| B — conditionné par la bande | Une branche résolue automatiquement depuis l'archétype de la bande (pas un choix libre du joueur, confirmé le 2026-08-20), un test de caractéristique (gating, cible le chef, autopass par bande), ou des jets indépendants par objet selon l'entrée | **5/6 ✅** |
| C — texte pur, aucun `Outcome` | Effet à appliquer à la prochaine bataille | **0/3 ⏳** (`Warband.NextGameNotes` pas construit) |
| D — sous-table Artefacts Magiques | 6 objets nommés uniques, référencée par 2 entrées | **✅ 6/6 catalogués** (Villa d'un Noble mécanisée ; Trésor Caché reste Groupe B) |

## Détail (ordre des dés, pour tester avec de vrais dés)

Colonne Testé : ✅ = vérifié en jeu par l'utilisateur, ❌ = Groupe B/C (choix conditionné à la bande/texte
pur, pas encore d'UI), — = codé mais pas encore rejoué en vrai (dernier point à jour : 2026-08-20).

| Dés | Résultat | Statut | Testé | Note |
|---|---|---|---|---|
| 2,1 | Puits | ✅ | ✅ | Vrai test d'Endurance (choix du Héros, jet comparé à sa stat) - réussite = pierre magique, échec = statut `Malade` (indisponible prochaine partie, effacé auto au Fin de Partie suivant). Un jet de 6 est un échec automatique quelle que soit la stat (RulesReference "Tests de caractéristiques", `Core.Rules.ExplorationChart.PassesStatTest` - manquant jusqu'à cette passe, corrigé aussi pour le test de Commandement additionnel de Bâtiment Éventré, où l'exception ne s'applique PAS) |
| 2,2 | Boutique | ✅ | ✅ | Or (D6) + Porte-bonheur bonus si le même jet vaut 1 (`BonusItemOutcome`) |
| 2,3 | Cadavre | ✅ | ✅ | Premier cas validé (sous-jet à branches exclusives) |
| 2,4 | Traînard | ✅ | ✅ | Groupe B — branche résolue automatiquement depuis l'archétype de la bande jouée (`Core.Rules.ExplorationOutcomeResolver.ResolveWarbandOutcome`, `ExplorationOutcome.RestrictedToWarbandArchetypeNames`) : Skavens (Or 2D6), Possédés (+1 XP au chef, `GrantsLeaderExperience`), Morts-Vivants (Zombie gratuit avec ChipView tapable, fusionné dans un groupe existant s'il y en a un, `GrantsFreeHenchmanArchetypeName`), toute autre bande (dé bonus au prochain jet d'Exploration - `Warband.PendingExplorationBonusDie`, rappel textuel affiché au jet suivant, ne change pas le nombre de dés gardés). L'étape Résultat n'affiche que la phrase d'intro partagée (`ExplorationResult.ShortDescription`) + la vraie phrase de la branche résolue (`ExplorationOutcome.BranchText`, traduit EN/FR), pas le paragraphe complet du livre qui énumère les 4 branches - `Description` reste inchangée, gardée comme référence complète |
| 2,5 | Charrette Renversée | ✅ | ✅ | Branche 5-6 : Épée + Dague ornées, réellement ajoutées à l'inventaire (`SecondaryEquipmentItemName`) et vendables à x2 leur valeur normale (matériau "Arme Ornée", `SpecialRule.IsResaleUpgrade`, bouton "Vendre" dans l'inventaire de bande) |
| 2,6 | Masures en Ruine | ✅ | ✅ | Branche unique |
| 3,1 | Taverne | ✅ | ✅ | Réutilise le test de caractéristique gating de Puits (`StatTestField`), avec deux ajouts : cible toujours le chef (`ExplorationResult.StatTestTargetsLeader`, pas de Picker - même ciblage que le test additionnel de Bâtiment Éventré, `Warrior.IsLeader`) et certaines bandes du livre réussissent automatiquement sans jet (`AutoPassStatTestWarbandArchetypeNames` : Morts-Vivants/Chasseurs de Sorcières/Sœurs de Sigmar) - bannière textuelle affichée à la place du jet dans ce cas. Réussite = 4D6 CO ; un Échec ne produit rien (une seule branche dans `Outcomes`, même forme que le test additionnel de Bâtiment Éventré - corrigé le 2026-08-20, voir Journal) |
| 3,2 | Forge | ✅ | ✅ | |
| 3,3 | Prisonniers | ✅ | ✅ | Groupe B — même principe que Traînard (`ResolveWarbandOutcome`) : Skavens (Or 3D6), Morts-Vivants (D3 Zombies gratuits - `GrantsFreeHenchmanArchetypeName`+`ItemQuantityFormula` réutilisé pour la quantité, jet+dé si D3 plutôt que fixe, ChipView tapable), Possédés (D3 Expérience répartis entre les Héros au choix du joueur via un steppeur +/- par Héros - `GrantsDistributedHeroExperienceFormula`, forme confirmée par mockup avant implémentation), toute autre bande (Or 2D6 + recrutement optionnel du prisonnier comme Homme de main gratuit dans un groupe EXISTANT de la bande choisi par le joueur - `GrantsOptionalEquippedHenchman`, seul le coût de l'équipement répliqué du groupe est déduit de la trésorerie, jamais de Cost d'archétype ; bloqué si le solde passerait négatif, `Core.Rules.RecruitmentRules.CanAffordEquippedHenchman`, forme confirmée par mockup 2026-08-21) |
| 3,4 | Atelier du Fléchier | ✅ | ✅ | |
| 3,5 | Halle du Marché | ✅ | ✅ | Branche unique |
| 3,6 | Une Faveur Rendue | ⏳ | ❌ | Groupe C — Mercenaire à Louer gratuit, `NextGameNotes` pas construit |
| 4,1 | Armurier à Poudre | ✅ | ✅ | |
| 4,2 | Sanctuaire | ✅ | ✅ | Groupe B — les deux branches donnent le même Or (3D6), seule la bénédiction distingue Sœurs de Sigmar/Chasseurs de Sorcières (`GrantsWeaponBlessing`) : le joueur choisit une arme déjà portée par un Héros (jamais un groupe d'Hommes de main ni la réserve de la bande) via un Picker, la SpecialRule "Blessed Weapon" (`Abbreviation` "B") s'attache à cette `WarriorEquipment` précise via `MaterialRule` - même mécanisme qu'un achat en Gromril/Ithilmar, aucun nouveau champ/affichage |
| 4,3 | Maison de Ville | ✅ | ✅ | Branche unique |
| 4,4 | Armurerie | ✅ | ✅ | Branche 1-2 : choix du joueur Bouclier ou Rondache (`AlternativeEquipmentItemName`) |
| 4,5 | Cimetière | ✅ | ✅ | Groupe B — miroir de Prisonniers : le catch-all (Or D6x10, toute bande) est ici la branche non restreinte, Chasseurs de Sorcières/Sœurs de Sigmar (seule branche `RestrictedToWarbandArchetypeNames`) prennent D6 Expérience répartis entre les Héros à la place (`GrantsDistributedHeroExperienceFormula` réutilisé tel quel - aucun code Core.Rules/wizard nouveau, l'infrastructure Groupe B existante couvre déjà cette forme). La conséquence "haïe par les Chasseurs de Sorcières/Sœurs de Sigmar à la prochaine partie" du catch-all est mécanisée en pense-bête (`ExplorationOutcome.NextGameNoteText` → `Warband.NextGameNote`, bannière sur la fiche de bande - voir Journal 2026-08-21) |
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

- **Groupe B restant** : Trésor Caché (6,2) et Bande Massacrée (6,4) - mécanique DIFFÉRENTE du reste du
  Groupe B (Traînard/Taverne/Prisonniers/Cimetière/Sanctuaire, tous ✅ - une seule branche résolue depuis
  l'archétype de la bande) : ici chaque objet/montant de la liste se jette INDÉPENDAMMENT contre son
  propre seuil ("4+", "5+"...), plusieurs peuvent se déclencher à la fois - aucune UI de ce genre
  n'existe encore dans le wizard, nécessite un mockup avant implémentation (voir le plan de séquencement).
- **Groupe C** (Une Faveur Rendue/Catacombes/Entrée des Catacombes) : `Warband.NextGameNote` (pense-bête sur la fiche de bande, voir Journal 2026-08-21 - construit et déjà branché pour Cimetière, réutilisable tel quel) + `Warband.HasCatacombReroll` (relance permanente, encore à construire) pour les effets mécanisables. Une Faveur Rendue exige en plus les Mercenaires à Louer (Hired Swords), pas encore implémentés.
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
- **2026-08-20 (démarrage du Groupe B - Traînard)** — Confirmé avec l'utilisateur : le texte du livre
  ("une bande Skaven PEUT...") signifie que la branche applicable à un résultat "conditionné par la
  bande" est strictement déterminée par l'archétype de la bande jouée, jamais un choix libre entre
  toutes les branches listées - contredit un plan antérieur (décrit dans une ancienne révision du
  commentaire de `ExplorationOutcome.Note`) qui prévoyait d'afficher toutes les branches et de laisser
  le joueur pointer la sienne. Nouveau mécanisme générique réutilisable par les 3 autres entrées de
  cette forme (Prisonniers/Cimetière/bénédiction du Sanctuaire) : `ExplorationOutcome.
  RestrictedToWarbandArchetypeNames` (liste de noms anglais, référence par nom comme EquipmentItemName -
  contenu figé sans éditeur, pas besoin d'une vraie table de jointure comme pour Équipement/Compétence/
  Mutation) + `Core.Rules.ExplorationOutcomeResolver.ResolveWarbandOutcome` (branche la plus spécifique
  au nom de la bande, sinon la branche catch-all sans restriction). **Bug latent corrigé au passage** :
  `ResolveAutoOutcome` ne vérifiait pas `RollsIndependently` - pour Traînard (4 branches, toutes sans
  sous-jet), il aurait silencieusement résolu la PREMIÈRE branche (Or Skavens) quelle que soit la bande
  jouée, dès le premier jet déclenchant ce résultat en jeu réel (jamais observé, ce résultat n'avait
  jamais été testé). La branche "autres bandes" ("lancez un dé de plus que d'habitude et écartez-en un")
  n'ajoute PAS de dé à `ExplorationDiceCount` - contrairement à l'Œil Omniscient de Numas (un vrai dé
  de plus gardé), c'est un dé physique en trop que le joueur écarte lui-même avant de saisir ses valeurs
  finales ; nouveau `Warband.PendingExplorationBonusDie` (bool) porte juste un rappel textuel affiché au
  prochain jet d'Exploration, consommé (qu'il ait servi ou non) à la Fin de Partie suivante. Les
  branches Possédés/Morts-Vivants sont automatisées dans la passe suivante (voir plus bas), pas restées
  en texte pur.
- **2026-08-20 (Traînard - automatisation XP/recrutement + description filtrée)** — Retour utilisateur :
  "on a les infos nécessaires" pour automatiser les branches Possédés/Morts-Vivants plutôt que les
  laisser en texte pur. Deux nouveaux champs `ExplorationOutcome` : `GrantsLeaderExperience` (int?, +1
  fixe pour Possédés - appliqué directement au guerrier `IsLeader`, comme `BonusStatTestLeader`, sans
  erreur bloquante si le chef est indisponible cette partie) et `GrantsFreeHenchmanArchetypeName`
  (string?, "Zombie" pour Morts-Vivants - résolu via `ILibraryService.GetWarriorArchetypesAsync` scopé à
  la bande jouée, recruté via `IWarbandService.RecruitWarriorAsync`/`archetype.ToWarrior`). Fusionne dans
  un groupe d'Hommes de main déjà existant du même archétype plutôt que de créer une ligne séparée (les
  Zombies ne portent jamais d'équipement, `CanUseEquipment: false` - deux groupes seraient de toute façon
  rigoureusement identiques) - incrémente juste `Warrior.HeadCount`. Le Zombie s'affiche dans le wizard en
  vrai `ChipView` tapable (icône `SolidFont.UserGroup`, déjà la convention Homme de main ailleurs dans
  l'app) ouvrant le dialog récap existant (`IDetailDialogService.ShowWarriorArchetypeDetailDialogAsync`,
  déjà là pour le Codex - juste jamais branché depuis ce wizard avant) plutôt qu'un simple texte, même
  langage d'interaction que tout objet trouvé. Second retour, même échange : la description complète du
  livre énumère les 4 branches (Skavens/Possédés/Morts-Vivants/autres) alors qu'une seule s'applique
  réellement à la bande jouée - première version du correctif basée sur `ResolvedExplorationOutcome.Note`
  (voir `ExplorationResultDescriptionText`), revue juste après (voir entrée suivante).
- **2026-08-20 (Traînard - Note n'était pas la bonne base pour l'affichage)** — Retour utilisateur sur le
  correctif ci-dessus : deux défauts. (1) `Note` n'est **jamais traduit** ("Deliberately not localized",
  décision d'origine documentée sur le champ) - un joueur EN aurait vu le texte FR ("Skavens : vente aux
  agents du Clan Eshin") tel quel. (2) La phrase d'intro partagée ("Votre bande croise l'un des
  survivants de Mordheim...") disparaissait complètement, `Note` ne portant que le texte propre à la
  branche. Solution retenue par l'utilisateur : garder deux versions distinctes de la description -
  `Description` (déjà là, verbatim complète, gardée intacte comme trace/référence de l'évènement réel du
  livre) + deux nouveaux champs traduits (même mécanisme `XxxKey`/`TranslationEntity` que Name/
  Description, pas le raccourci non-traduit de Note) : `ExplorationResult.ShortDescription` (juste la
  phrase d'intro, un seul champ pour tout le résultat) et `ExplorationOutcome.BranchText` (la vraie
  phrase complète du livre pour CETTE branche seule, un par branche). `ExplorationResultDescriptionText`
  concatène maintenant ShortDescription + BranchText de la branche résolue pour un résultat "conditionné
  par la bande", les deux correctement résolus dans la langue courante via `LibraryService.
  GetExplorationResultsAsync` (élargi pour inclure `ShortDescriptionKey`/`BranchTextKey` dans sa
  résolution de traductions, et pour passer les traductions à `ExplorationOutcomeEntity.ToModel`,
  jusque-là parameterless puisque rien sur Outcome n'était traduit). `Note` reste tel quel (tag interne
  court, toujours non traduit) pour ses autres usages existants ailleurs dans la table - pas touché.
- **2026-08-20 (Taverne)** — Deuxième entrée Groupe B, forme différente de Traînard : un vrai test de
  caractéristique gating comme Puits (`StatTestField`, réutilisé tel quel - `ResolveStatTestOutcome`,
  `PassesStatTest`, `RollStatTest` en 2D6 pour Commandement, aucun changement côté Core.Rules), pas une
  résolution par identité de bande. Demande explicite de l'utilisateur : reprendre le ciblage "toujours
  le chef" déjà construit pour le test additionnel de Bâtiment Éventré (`Warrior.IsLeader`) plutôt que le
  Picker de Puits. Deux champs neufs sur `ExplorationResult` : `StatTestTargetsLeader` (bool - la
  résolution auto-remplit `StatTestHero` avec le chef au lieu d'afficher `StatTestEligibleHeroes`, voir
  `ShowStatTestHeroPicker`/`StatTestHeroDisplayPrefix` pour le nom réinjecté dans le libellé du jet à la
  place du Picker disparu) et `AutoPassStatTestWarbandArchetypeNames` (liste de noms anglais - Morts-
  Vivants/Chasseurs de Sorcières/Sœurs de Sigmar réussissent sans lancer de dé, `StatTestAutoPasses`
  résout directement la branche Réussite dès que le résultat se déclenche, bannière textuelle affichée à
  la place du jet). Chef indisponible cette partie (mort/malade/hors de combat) : même idiome que
  `BonusStatTestLeader` - rien à valider, pas d'erreur bloquante, juste indisponible
  (`StatTestLeaderUnavailable`). `RollsIndependently` repassé à `false` (c'était un reliquat de l'ancien
  plan "Groupe B = choix libre du joueur", abandonné le 2026-08-20 pour Traînard mais pas encore
  nettoyé ici) - `ResolveWarbandOutcome`/`IsWarbandConditionedResult` restent inertes pour cette entrée
  (aucun Outcome n'a de `RestrictedToWarbandArchetypeNames`), donc la description complète du livre
  continue de s'afficher normalement, pas de ShortDescription/BranchText nécessaires ici.
- **2026-08-20 (Taverne - correction Échec)** — Retour utilisateur après premier test en jeu : "peu
  importe le résultat, on peut tirer l'or gagné" - la branche Échec (D6 CO, texte "les hommes en
  boivent l'essentiel") restait accessible/tirable quel que soit le jet, comme si le test n'avait aucun
  effet réel. Confirmé par l'utilisateur : la vraie règle est "réussi = 4D6 CO, sinon rien" (le texte
  "D6 CO d'alcool restant" de l'import initial était inexact) - demande explicite de reprendre la forme
  déjà utilisée pour le test additionnel de Bâtiment Éventré (Chien de guerre) : une SEULE `Outcome`
  (Réussite) dans le tableau, aucune pour l'Échec. `Core.Rules.ExplorationOutcomeResolver.
  ResolveStatTestOutcome` gère déjà ce cas nativement (`Outcomes.FirstOrDefault(o => o.StatTestPass ==
  passed)` renvoie `null` s'il n'y a aucune branche Échec, donc rien ne se résout/s'affiche) - seul un
  vrai bug annexe restait : `ValidateExplorationResultStep` bloquait la progression après un Échec parce
  qu'il vérifiait `ResolvedExplorationOutcome is null` (indiscernable de "pas encore joué" une fois
  l'Échec sans branche) au lieu de `StatTestRoll` (a-t-on tapé un jet, peu importe son issue) - même
  correctif déjà appliqué à `BonusStatTestField`/`BonusStatTestRoll` pour exactement la même raison,
  généralisé ici. Description FR/EN corrigée en cohérence ("il ne reste rien à récupérer" plutôt que
  "D6 CO d'alcool").
- **2026-08-20 (Prisonniers)** — Troisième entrée Groupe B, même mécanisme que Traînard ("même principe
  que le Traînard", confirmé par l'utilisateur) : `ResolveWarbandOutcome`/`RestrictedToWarbandArchetypeNames`
  inchangés, aucun nouveau concept côté Core.Rules. Seule vraie extension : Morts-Vivants gagnent D3
  Zombies (pas 1 fixe comme le Zombie de Traînard) - `ExplorationOutcome.ItemQuantityFormula` (déjà
  utilisé pour la quantité d'un Objet trouvé) réutilisé tel quel pour la quantité d'un Homme de main
  gratuit plutôt qu'un champ dédié : `ApplyResolvedOutcome` l'auto-remplit pour une quantité fixe (ex.
  Traînard, "1") ou laisse le champ+dé au joueur si c'est un vrai jet (D3), `ValidateExplorationResultStep`
  bloque tant qu'il est vide dans ce second cas, et `WarbandDetailViewModel.EndOfGame` parse
  `ExplorationItemQuantity` (repli sur 1) plutôt que d'ajouter toujours 1 Zombie. Décision initiale de
  NE PAS automatiser deux branches, contrairement à Traînard : la répartition de D3 Expérience entre
  PLUSIEURS Héros (contrairement au +1 XP fixe au seul chef de Traînard, ici un vrai choix de répartition
  du joueur) et le recrutement optionnel "un prisonnier peut rejoindre la bande comme Homme de main s'il
  peut être équipé" (dépend du choix ET du budget du joueur, contrairement au Zombie gratuit sans
  condition) - les deux laissées en texte pur (`Note`/`BranchText`) dans cette première passe, la
  répartition d'XP automatisée juste après (voir entrée suivante), le recrutement conditionné à
  l'équipement toujours en attente (l'utilisateur creuse comment le brancher sur la réserve
  d'équipement de la bande).
- **2026-08-20 (Prisonniers - répartition d'XP entre Héros)** — Retour utilisateur : "on va pousser pour
  la répartition de point d'xp... on lance le d3 et on assigne comme on le souhaite la valeur" - confirmé
  via un mockup (steppeur +/- par Héros, compteur "Restant" qui doit tomber à 0) avant implémentation,
  même démarche que le mockup jet-double du 2026-08-20 plus tôt dans la session. Nouveau champ
  `ExplorationOutcome.GrantsDistributedHeroExperienceFormula` (contraste avec `GrantsLeaderExperience` de
  Traînard : un total jeté par le joueur - jamais auto-rempli, même idiome que tout autre jet du wizard -
  réparti librement entre plusieurs Héros plutôt qu'un montant fixe donné au seul chef). Nouveau
  `WarriorOutcomeRow.DistributedExplorationExperience` (int, distinct d'`ExperienceGained` de l'étape
  Expérience plus tôt dans le wizard - deux sources d'XP différentes, jamais mélangées pour qu'un retour
  en arrière sur cette étape n'affiche pas une valeur modifiée par l'étape Exploration plus tardive).
  `EndOfGameDialogViewModel.DistributedExperienceRemaining` (Total - somme des allocations) bloque la
  progression tant qu'il n'est pas exactement 0 (`ValidateExplorationResultStep`) - chaque
  `WarriorOutcomeRow` remonte son changement au ViewModel parent via `PropertyChanged` (même principe que
  les `ExplorationDieEntry`/`AdvanceRollEntry` ailleurs dans ce fichier, puisque c'est un objet différent
  du ViewModel qui expose la somme). Steppeurs +/- (`IncrementDistributedExperienceCommand`/
  `DecrementDistributedExperienceCommand`) copiés du motif déjà utilisé pour Hors de Combat
  (`IncrementOutOfAction`/`DecrementOutOfAction`), sans désactivation visuelle des boutons aux bornes -
  la garde vit dans la commande elle-même (silencieusement sans effet une fois à 0), pas dans l'état
  `IsEnabled` du bouton.
- **2026-08-20 (nouveau catalogue Race)** — En creusant la branche "autres bandes" de Prisonniers
  ("un prisonnier peut rejoindre la bande comme Homme de main s'il peut être équipé"), l'utilisateur a
  relevé une vraie ambiguïté RAW : certaines retranscriptions précisent "one of your **human** Henchman
  groups" - une bande Skaven/Orque/Naine n'a par définition aucun groupe humain, donc RAW elle gagne
  l'or (2D6) mais jamais ce recrutement. L'appli n'avait jusque-là aucune notion de race/espèce sur
  `WarbandArchetype` pour trancher cette question. Demande explicite de l'utilisateur : un vrai
  catalogue Library éditable (pas un enum en dur) - "un peu la même chose que ce qu'on a pour les
  écoles de magie" (confirmé par question posée). Construit : `Models.Library.Race` (Id/Nom/
  Description, Official/Modified/Custom, même forme que `MagicSchool` - aucune liste de restriction,
  contrairement aux catalogues à 8 qui en ont une) + `WarbandArchetype.RaceId`/`Race` (résolu via
  `LibraryService.GetWarbandArchetypesAsync`, comme les Écoles de Magie mais en un-vers-un, pas une
  liste). Seedé (`Data/SeedData/Races.json` : Humain/Skaven/Orque/Nain/Elfe/Mort-Vivant/Homme-Bête -
  Elfe et Homme-Bête ajoutés en prévision, aucune bande actuelle ne les utilise sauf Homme-Bête pour
  Pillards Hommes-Bêtes) et chacune des 15 bandes existantes déclare désormais son "race" dans son
  propre JSON. UI complète calquée sur `MagicSchoolListPage`/`MagicSchoolEditDialog` mais simplifiée
  (pas de picker multi-sélection ni de sous-onglet, une bande n'a jamais qu'UNE race assignée par un
  simple Picker sur `WarbandArchetypeEditDialog`) : nouvel onglet "Bandes" gagne un bouton "Gérer les
  Races" (icône `SolidFont.PersonRays`, glyphe pas encore utilisé ailleurs) ouvrant `RaceListPage`
  (Créer/Renommer/Supprimer, popup récap générique `ChipDetailDialog`). **Migration nécessaire** : les
  15 `WarbandArchetypeEntity` déjà seedées sur la machine de l'utilisateur (sessions précédentes)
  n'avaient pas ce nouveau champ - `AppDatabase.BackfillWarbandArchetypeRaceAsync` (même idiome que
  `BackfillNeverGainsExperienceAsync`, tourne inconditionnellement à chaque lancement) sème Races.json
  si besoin (via une variante DB-aware de `FindOrCreateRaceAsync`, pas seulement le dictionnaire en
  mémoire habituel - nécessaire puisque `SeedOfficialContentAsync` entier est sauté sur une base déjà
  peuplée) puis mappe chaque bande à sa race via une table nom-anglais→race codée en dur (les 15 bandes
  sont fixes et connues, pas besoin de re-parser leur JSON pour ce correctif ponctuel). Bug annexe
  corrigé au passage : `RaceEntity` manquait de `CreateTableAsync` dans `CreateAllTablesAsync`
  ("no such table" en test). **Le recrutement conditionné ("si vous pouvez l'équiper") lui-même n'est
  toujours PAS câblé** - seule l'infrastructure Race existe maintenant ; l'utilisateur creuse encore
  comment vérifier "la bande peut payer son équipement" (réserve d'équipement de la bande ?) avant
  d'implémenter la branche complète.
- **2026-08-21 (Prisonniers, "autres bandes" - recrutement conditionné)** — Tranché : "regarder la
  trésorerie de la bande et donner la possibilité de recruter si on a la trésorerie pour acheter les
  items (et uniquement les items, pas de prix sur le henchman)" - le recrutement lui-même est
  **gratuit** (jamais de `Cost` d'archétype déduit, contrairement à un recrutement normal), seul le coût
  de l'équipement à répliquer doit tenir dans la trésorerie. Forme confirmée par mockup en 3 itérations
  avant implémentation : (1) Picker des groupes d'Hommes de main **réellement existants** dans la bande
  plutôt qu'un choix d'archétype - contourne l'ambiguïté RAW "groupe humain" en listant simplement ce
  que CETTE bande a vraiment (une bande Naine/Skaven/Elfe n'a pas de groupe humain de toute façon, donc
  la liste est naturellement vide pour elle, pas besoin d'un gate Race explicite) ; (2) "Ne pas recruter"
  déplacé en première option/valeur par défaut, tableau de trésorerie masqué tant qu'elle reste
  choisie ; (3) bouton dédié Recruter/Refuser supprimé - seul le "Suivant" du wizard valide, bloqué si
  le solde choisi passerait négatif (`CanAffordEquippedHenchman`, même idiome d'erreur que les autres
  jets de cette étape, pas de bouton désactivé). Nouveau champ `ExplorationOutcome.
  GrantsOptionalEquippedHenchman` (bool, catch-all uniquement) ; le coût de l'équipement à répliquer est
  calculé en direct depuis le loadout ACTUEL du groupe choisi (`Warrior.Equipment`, partagé pour tout le
  groupe - pas de nouvelle donnée à saisir), via `Core.Rules.EquipmentPricing.CalculateCost` déjà
  existant. Nouvelle règle pure `Core.Rules.RecruitmentRules.CanAffordEquippedHenchman` (comparaison
  simple, mais gardée testable/centralisée comme le reste des règles de recrutement). À la sauvegarde :
  `+1 HeadCount` sur le groupe choisi, trésorerie décrémentée du coût d'équipement (jamais du Cost
  d'archétype).
- **2026-08-21 (correction Race)** — L'utilisateur a repéré que Kermesse du Chaos et Culte des Possédés
  avaient été seedées avec la race "Humain" comme n'importe quelle bande de Mercenaires - erreur : ce
  sont des humains corrompus par le Chaos, une race à part ("Humain du Chaos" créée dans `Races.json`,
  FR/EN, description dédiée) plutôt que le même `Race` que Reiklander/Witch Hunters/Sœurs de Sigmar. Les
  deux bandes + la table de backfill (`_raceNameByWarbandEnglishName`, pour une base déjà migrée sur une
  autre machine) mis à jour en conséquence ; nouveau test `WarbandArchetype_ChaosWarbands_
  ResolveChaosHumanRace`.
- **2026-08-21 (Cimetière)** — Avant de traiter Une Faveur Rendue (Groupe C), l'utilisateur a demandé de
  faire "le reste" d'abord : Une Faveur Rendue exige les Mercenaires à Louer (Hired Swords), pas encore
  implémentés et porteurs de leur propre mécanique de progression (mélange Héros/Homme de main) - un
  chantier à part entière, alors que les entrées Groupe B restantes semblaient plus simples. Cimetière
  confirme cette intuition : forme miroir de Prisonniers déjà bien rodée (`ResolveWarbandOutcome`), sauf
  que le catch-all (Or D6x10, toute bande) est cette fois la branche SANS restriction et Chasseurs de
  Sorcières/Sœurs de Sigmar la seule branche restreinte (D6 Expérience répartis entre les Héros,
  `GrantsDistributedHeroExperienceFormula` réutilisé tel quel - déjà généralisé, aucune modification de
  code requise, uniquement `ExplorationResults.json` + tests). La conséquence "haine à la prochaine
  partie contre Chasseurs de Sorcières/Sœurs de Sigmar" du catch-all est restée en texte pur (`Note`/
  `BranchText`) dans cette première passe - voir l'entrée suivante pour sa mécanisation en pense-bête,
  demandée par l'utilisateur juste après.
- **2026-08-21 (Progression reconnectée sur l'XP d'Exploration)** — Bug de fond repéré par l'utilisateur
  en relisant les mécaniques d'XP des tables (Traînard/Prisonniers/Cimetière) : `WarriorOutcomeRow.
  MilestoneCount` (qui pilote l'étape Progression) ne lisait QUE `ExperienceGained` (l'étape Expérience,
  XP de bataille) - l'XP accordée directement en Exploration (`GrantsLeaderExperience`, chef fixe ;
  `GrantsDistributedHeroExperienceFormula`, répartition libre) était ajoutée à `Warrior.Experience` à la
  sauvegarde SANS jamais repasser par la détection de palier, donc sans jamais déclencher de jet de
  Progression - un palier franchi par cet XP-là était silencieusement perdu, pour toujours (la partie
  suivante repart du nouveau total déjà au-delà du palier). Confirmé par l'utilisateur : il faut
  déclencher l'Advance si le palier est franchi, "on reprend la mécanique d'Advance déjà existante, on la
  rebranche" - pas une nouvelle mécanique.
  Contrainte de taille : Steps() est recalculée à chaud (voir la doc de classe d'EndOfGameDialogViewModel)
  et la carte Progression normale est insérée à une position FIXE, juste après Expérience - donc AVANT
  l'étape Exploration où cet XP-là devient connu. Réutiliser le même HasMilestone/MilestoneCount incluant
  l'XP d'Exploration aurait fait apparaître une carte Progression à une position déjà dépassée par le
  joueur au moment où le palier est franchi, décalant silencieusement tous les StepIndex suivants
  (StepIndex est un entier brut, sans identité de step stable - `Next()` incrémente juste ce nombre).
  Résolu par un DEUXIÈME passage de la même mécanique (mêmes `AdvanceRollEntry`/`HeroAdvanceTable`/
  `HenchmanAdvanceTable`/commandes `AutoRollAdvance`/`PickAdvanceSkill`, rien dupliqué), placé après
  l'étape Exploration plutôt qu'avant : `WarriorOutcomeRow.ExplorationMilestoneCount`/
  `HasExplorationMilestone`/`ExplorationAdvanceRolls` comptent les paliers franchis UNIQUEMENT par
  `ExplorationBonusExperience` (= `DistributedExplorationExperience` + le nouveau
  `LeaderExplorationExperience`, pour `GrantsLeaderExperience`), à partir du point où la Progression
  normale s'est déjà arrêtée (`Warrior.Experience + ExperienceGained`) plutôt que depuis `Warrior.
  Experience` directement - évite tout recomptage/rejeu d'un palier déjà traité par le premier passage.
  `WizardStep` gagne `IsExplorationAdvance` (bool) pour distinguer les deux passages du même
  `StepKind.Advance` ; `EndOfGameDialogViewModel.CurrentAdvanceRolls` bascule entre `AdvanceRolls`/
  `ExplorationAdvanceRolls` selon ce flag - seul point de bascule, le XAML (un seul bloc Progression) et
  les commandes existantes n'ont pas besoin d'être dupliqués. `ValidateAdvanceStep`/`PickAdvanceSkill`
  ajustés pour accepter/chercher dans les deux collections. Côté sauvegarde
  (`WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync`) : `row.AdvanceRolls.Concat(row.
  ExplorationAdvanceRolls)` appliqué en un seul passage, aucune distinction nécessaire une fois les jets
  faits. Hors couverture de `MordheimLedgerApp.Tests` (logique ViewModel/tête MAUI, pas Core) - à
  vérifier manuellement au prochain test en jeu d'un résultat Traînard/Prisonniers/Cimetière qui fait
  franchir un palier.
- **2026-08-21 (pense-bête "prochaine partie" - `Warband.NextGameNote`)** — Demandé par l'utilisateur
  juste après Cimetière : comme prévu pour les Catacombes (Groupe C, "Hors périmètre" ci-dessus), la
  conséquence "haine à la prochaine partie" de la branche catch-all a besoin d'un encart sur la fiche de
  bande plutôt que de rester seulement lisible dans le texte du wizard, sans quoi le joueur n'a plus
  aucun moyen de s'en souvenir une fois sorti du wizard. Nouveau champ `ExplorationOutcome.
  NextGameNoteText`/`NextGameNoteTextKey` (même paire Text/TextKey que `BranchText`/`BranchTextKey`,
  localisé EN/FR) posé sur Cimetière catch-all uniquement pour l'instant ; `Warband.NextGameNote`
  (string?, même idiome que `PendingExplorationBonusDie` : posé à la résolution de la branche, consommé
  sans condition à la Fin de Partie SUIVANTE - `WarbandDetailViewModel.EndOfGame.
  ApplyExplorationOutcomeAsync`, juste à côté de la consommation de `PendingExplorationBonusDie`). Un
  pense-bête plutôt qu'un effet appliqué : l'appli n'a aucune notion d'identité d'adversaire (jamais
  demandé jusqu'ici), donc "la prochaine fois que vous jouez contre les Chasseurs de Sorcières/Sœurs de
  Sigmar" ne peut pas être détecté automatiquement - le joueur lit la bannière et applique la règle
  lui-même le moment venu. Bannière sur `WarbandDetailPage` (juste sous la ligne Trésorerie/Pierres
  magiques/Rating, bordure `AppDanger`, masquée si `Warband.NextGameNote` est vide -
  `WarbandDetailViewModel.HasNextGameNote`) plutôt qu'une simple entrée d'Historique, pour rester visible
  tant qu'elle s'applique (l'Historique défile et se perd dans la liste). Infrastructure volontairement
  générique (`Warband.NextGameNote`/`ExplorationOutcome.NextGameNoteText`, pas un champ dédié "haine
  Sœurs/Chasseurs") pour être réutilisée telle quelle par Catacombes/Une Faveur Rendue plus tard, sans
  nouveau champ ni nouvelle bannière à construire.
- **2026-08-21 (Sanctuaire - bénédiction d'arme)** — Repris juste après Cimetière ("le reste avant Une
  Faveur Rendue, qui exige les Mercenaires à Louer pas encore implémentés"). Converti de branche unique
  (`rollsIndependently: false`, Or 3D6 pour tout le monde) vers la forme Groupe B habituelle : les DEUX
  branches donnent le même Or 3D6 (contraste avec Cimetière/Prisonniers où le montant diffère aussi) -
  seule `GrantsWeaponBlessing` distingue la branche Sœurs de Sigmar/Chasseurs de Sorcières. Mockup en 2
  itérations avant implémentation : (1) Picker "Guerrier — Arme" listant les armes du roster, "Ne pas
  bénir d'arme" par défaut ; (2) restreint aux HÉROS uniquement sur retour utilisateur - un groupe
  d'Hommes de main partage son Equipment entre plusieurs figurines, "une arme au choix" n'y désigne pas
  une pièce unique à bénir ; l'inventaire de bande (réserve non assignée) explicitement écarté aussi
  ("Oublie l'inventaire"). Décision de conception clé (suggérée par l'utilisateur) : la bénédiction
  réutilise l'ESPRIT du mécanisme MaterialRule existant (comme Gromril/Ithilmar/Arme Ornée - un
  `SpecialRule` avec `Abbreviation` attaché à la `WarriorEquipment`, affiché via `NameDisplay`) plutôt
  qu'un bool ad-hoc - nouvelle `SpecialRule` "Blessed Weapon"/"Arme Bénie" (`Abbreviation` "B", pas de
  `CostMultiplier`, pas `IsResaleUpgrade`) dans `SpecialRules.json`. Filtré aux catégories arme
  (corps-à-corps/tir/poudre noire) - une armure n'a pas de sens à bénir ici.
  **Corrigé dans la foulée** : première passe posait la bénédiction directement sur `MaterialRule`,
  écrasant un matériau déjà présent - l'utilisateur a signalé qu'une arme en Gromril/Ithilmar/Ornée peut
  très bien être bénie EN PLUS ("attention une arme bénie peut être aussi en gromril/ithilmar/ornée").
  `WarriorEquipment` gagne donc un second slot indépendant, `BlessingRule` (jamais dérivé de/ne touche
  jamais `MaterialRule`) - `NameDisplay` combine désormais les deux abréviations si présentes ("Épée (G,
  B)"), plain si aucune. Nouvelle colonne `WarriorEquipmentEntity.BlessingSpecialRuleId` + méthode
  `IWarbandService.SetWarriorEquipmentBlessingRuleAsync` (le service n'avait jusque-là aucun moyen de
  modifier une arme DÉJÀ possédée après coup, seulement à l'achat) ; `WarbandService.
  ResolveMaterialRuleAsync` renommé `ResolveSpecialRuleAsync` (partagé par les deux slots, plus
  seulement le matériau). Nouveau test `BlessedWeapon_CoexistsWithExistingMaterialRule` couvrant
  exactement ce cas (Hache en Gromril qui se fait aussi bénir → "Axe (G, B)", les deux `SpecialRule`
  résolues indépendamment). Retour utilisateur additionnel : aperçu en direct dans le wizard
  (`EndOfGameDialogViewModel.BlessedWeaponPreview`, une `WarriorEquipment` jetable jamais persistée
  combinant le `MaterialRule` réel de l'arme choisie + la SpecialRule "Blessed Weapon" déjà résolue) -
  un vrai `ChipView` sous le Picker montre le résultat ("Épée (G, B)") avant même de valider le wizard.
  **3 oublis corrigés dans la foulée** (signalés par l'utilisateur après test) : (1) le chip d'aperçu du
  wizard n'était pas tapable, aucun popup détail au clic - nouvelle commande `ShowBlessedWeaponDetail` ;
  (2) `EquipmentItemDetailDialogViewModel` (popup détail, ouvert aussi bien depuis la carte guerrier de
  `WarbandDetailPage` que depuis le wizard) ne connaissait que `materialRule`, jamais `blessingRule` - le
  titre du popup n'affichait donc que "(G)" (jamais "(G, B)") et la règle "Béni" n'apparaissait pas dans
  la liste des règles spéciales affichées, malgré le chip lui-même montrant bien "(G, B)" à l'extérieur.
  `IDetailDialogService.ShowEquipmentDetailDialogAsync` gagne un 4e paramètre optionnel `blessingRule`,
  fil tiré jusqu'aux 2 appelants qui passent une vraie `WarriorEquipment` (`WarbandDetailViewModel.
  ShowEquipmentDetail`, `WarriorEditDialogViewModel.ShowEquipmentDetail`) - `EquipmentPick` (choix en
  mémoire avant recrutement) n'a délibérément pas cette règle, une bénédiction ne peut par construction
  s'appliquer qu'à un guerrier déjà recruté.
