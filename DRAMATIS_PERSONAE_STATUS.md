# Dramatis Personae — état d'implémentation

Suivi de ce qui reste à gérer sur le catalogue Dramatis Personae (`Core/Models/Library/
DramatisPersona.cs`, `Data/SeedData/DramatisPersonae.json`, 8 personnages) au-delà du catalogue et
du recrutement de base. Mis à jour à chaque avancée — dernière mise à jour : **2026-09-01** ("Une
Poignée d'Or" + "Où est l'Argent ?" - Ulli & Marquand désormais complets).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire ·
📝 Volontairement laissé en texte libre (voir CLAUDE.md § Règles de collaboration : pas de moteur de
règles/calculateur de combat en V1)

## Vue d'ensemble

| Sujet | Statut | Note |
|---|---|---|
| Catalogue (fiche, profil, équipement de départ, compétences, règles spéciales, restriction par bande) | ✅ | Codex Bibliothèque, dialog récap lecture seule |
| Recherche + recrutement depuis le wizard Fin de Partie ("Personnage spécial") | ✅ | Étape Recherche + Achat, voir `EndOfGameDialogViewModel.RareItems.cs` |
| Bloc de roster dédié dans la fiche de bande | ✅ | `WarbandDetailPage`, comme Héros/Hommes de main/Francs-Tireurs |
| Blessures Graves / XP (statut Héros, ne gagne pas d'XP) | ✅ | `IsHero=true` + `GainsExperience=false`, voir `Warrior.DramatisPersonaId` |
| Haine dynamique (blessure/objets/règles/compétences) | ✅ | 2026-09-01 |
| Aide conditionnelle de Bertha (jet de Rating au Lancement de Partie) | ✅ | 2026-09-01, `RatingGapAidTable` + `StartGameDialog` - un échec (écart trop faible ou jet raté) la retire IMMÉDIATEMENT du roster à la confirmation, pas seulement à la Fin de Partie suivante |
| Équipement de départ dupliqué (2 Marteaux de Sigmarite) | ✅ | 2026-09-01, `Quantity` + backfill base existante (catalogue) + chip "x2" dans les dialogs récap Codex |
| Cycle de vie Vagabond (retrait auto après une bataille) | ✅ | 2026-09-01, `ApplyWandererDeparturesAsync` (Fin de Partie) - retrait complet, généralisé aux 3 Vagabonds (Aenur/Bertha/Ulli & Marquand) |
| Frais d'engagement (HireCost à l'engagement + Upkeep récurrent) | ✅ | 2026-09-01, `FeeKind.Gold` (Johann/Veskit/Marianna) ET `FeeKind.Wyrdstone` (Nicodemus) - voir Transverse pour la portée exacte |
| Paiement alternatif de Johann (Ombre Cramoisie au lieu d'or) | ✅ | 2026-09-01, `DramatisPersona.AlternativePaymentItemId` + picker Codex + retrait réel de l'inventaire de bande, généralisable à tout futur personnage Gold via le même champ |
| Exclusion du décompte de tête pour la vente de pierre magique | ⏳ | Voir note dans `Warrior.cs` (même limite que Franc-Tireur) |
| Mécaniques propres à un seul personnage (Marianna, Nicodemus, Johann, Ulli & Marquand) | ⏳ / 📝 | Détail ci-dessous, personnage par personnage |
| Doc `DramatisPersona.cs` (commentaire de classe) mentionne encore "not yet wired into recruitment" | ⏳ | Obsolète depuis le branchement du wizard — à corriger au prochain passage sur ce fichier |

## Transverse (tous personnages)

- **Frais d'engagement : câblé pour `FeeKind.Gold` ET `FeeKind.Wyrdstone` (2026-09-01).** À
  l'engagement (recherche "Personnage spécial" de Fin de Partie), le coût est déduit - `HireCost` sur
  la trésorerie pour un personnage Gold, 1 éclat de pierre magique pour Nicodemus (Wyrdstone, jamais
  d'option en or pour lui) - bloque le step si ça dépasserait le budget/stock disponible, même bandeau
  que l'achat d'objets rares côté or (`RareItemSearchEntry.EffectiveHireCostForTreasury`/
  `RareItemPurchaseTotalCost`) et un bandeau parallèle côté pierre magique
  (`EffectiveWyrdstoneCostForShards`/`RareItemPurchaseTotalWyrdstoneCost`, calculé après le choix fait
  à l'étape Vente de pierres magiques du même wizard). Nouvelle étape "Dramatis Personae" en Fin de
  Partie (`EndOfGameDialogViewModel.DramatisPersonae.cs`, entièrement absente si aucun personnage
  concerné) règle ensuite la solde récurrente à chaque partie suivante - Payer/Renvoyer comme les
  Francs-Tireurs (1 pierre magique pour Nicodemus, montant en or pour les autres), impayée = retrait
  complet du roster (`ApplyDramatisPersonaUpkeepAsync`). Concrètement : Johann (70 CO + 30 solde),
  Veskit (80 CO + 35 solde) et Nicodemus (1 pierre à l'engagement + 1/bataille) sont couverts ; Marianna
  (150 CO + 75 solde variable) aussi pour le HireCost/Upkeep fixe, mais sa propre mécanique
  "On n'échappe jamais à son passé..." (qui fait varier l'upkeep réel) reste hors périmètre (texte
  libre, voir sa ligne plus bas). **Toujours hors périmètre, décision explicite** : Bertha (`None`,
  rien à payer de toute façon), Ulli & Marquand (`Pair` - engagement à deux, enchère "Une Poignée
  d'Or").
- **Paiement alternatif : généralisé, pas seulement Johann.** `DramatisPersona.AlternativePaymentItemId`
  (nouveau champ, éditable via un picker Codex - `EquipmentQuantityChip`-style 0/1 élément comme
  MagicSchoolId) permet à n'importe quel futur personnage `FeeKind.Gold` de proposer "payer avec cet
  objet à la place de l'or" - visible sur la carte de recrutement UNIQUEMENT si la bande possède déjà
  l'objet en stock (`RareItemSearchEntry.HasAlternativePaymentOption`). Retrait RÉEL d'un exemplaire de
  l'inventaire de bande à la confirmation (pas juste déclaratif - décision utilisateur via
  `AskUserQuestion`) ; retrait de la pile ENTIÈRE si la bande en possède plusieurs (pas de mécanisme de
  pile partielle nulle part ailleurs dans l'app, même simplification que Vendre/Assigner un objet).
  Johann est le seul personnage à l'utiliser aujourd'hui (Ombre Cramoisie), le champ est prêt pour un
  futur cas similaire sans travail supplémentaire.
- **Vagabond : départ automatisé (2026-09-01).** `IsWanderer=true` (Aenur, Bertha, Ulli & Marquand)
  déclenche un retrait COMPLET du roster à chaque Fin de Partie (`ApplyWandererDeparturesAsync` -
  décision utilisateur via `AskUserQuestion` : suppression pure, pas de statut "Parti(e)" dédié, une
  future recherche recrée une fiche neuve sans historique conservé). Scope volontairement le roster figé
  à l'OUVERTURE du wizard : un personnage retrouvé PENDANT cette même Fin de Partie (étape Recherche)
  n'est jamais concerné, il reste au moins jusqu'à la bataille suivante.
- **Délai de re-recherche (2026-09-01) : câblé pour Aenur et Ulli & Marquand, pas Bertha.**
  `DramatisPersona.RequiresCooldownBeforeResearch` ("can't be sought again until the warband has fought
  at least one battle without them") - une ligne `WarbandDramatisPersonaCooldownEntity` (WarbandId,
  DramatisPersonaId) est posée dès que ce type de personnage quitte la bande
  (`ApplyWandererDeparturesAsync`), et exclut le personnage du picker "Personnage spécial"
  (`IDramatisPersonaPickerService.PickDramatisPersonaAsync`, nouveau paramètre `excludedDramatisPersonaIds`)
  tant qu'elle existe. Effacée en bloc pour toute la bande (`ClearAllDramatisPersonaCooldownsAsync`) au
  DÉBUT de chaque Fin de Partie suivante, AVANT `ApplyWandererDeparturesAsync` (qui peut reposer un
  cooldown pour un NOUVEAU départ de cette même Fin de Partie) : atteindre cette Fin de Partie veut dire
  qu'une bataille a été jouée sans ce personnage (il était exclu du picker), donc la condition "au moins
  une bataille sans lui" est satisfaite. Bertha n'a pas ce flag - "A request for Bertha... must be made
  for each battle" ne mentionne aucun délai, elle reste re-cherchable dès la bataille suivante.
  **Bug trouvé en passant** : `DramatisPersonaViewModel.Edit()` (copie défensive avant ouverture du
  dialog d'édition Codex) omettait déjà `AlternativePaymentItemId`/`AlternativePaymentItem` depuis le
  travail sur Johann - éditer Johann via le Codex effaçait silencieusement son objet de paiement
  alternatif au premier Enregistrer. Corrigé au passage (même classe de bug que la copie de `Warrior`
  documentée dans `EditWarrior`).
- **Décompte de tête à la vente de pierre magique.** Même limite connue que pour les Francs-Tireurs
  (voir `Warrior.cs`) : un Dramatis Persona compte probablement dans le calcul actuel alors que le
  livre exclut ce type de guerrier du partage. Pas vérifié/corrigé.
- **Aide conditionnelle : mécanisme générique mais un seul personnage l'utilise.** `RequiresRatingDisadvantage`
  + `Core.Rules.RatingGapAidTable` sont câblés bout en bout (recherche du delta de Rating au Lancement
  de Partie, jet, résultat) mais seule Bertha a le flag à `true` aujourd'hui — prêt à être réutilisé
  tel quel si un futur import de personnage partage exactement ce mécanisme.

## Par personnage

| Personnage | Bande(s) | Mécanique propre restant à gérer |
|---|---|---|
| **Aenur, l'Épée du Crépuscule** | 12 bandes humaines/Ordre | Départ Vagabond ✅. Délai de re-recherche (1 bataille sans lui) ✅. |
| **Bertha Bestraufrung** | Sœurs de Sigmar | Aide conditionnelle ✅. Départ Vagabond ✅ (pas de délai de re-recherche pour elle, conforme au livre). Recrutement "gratuit en or" (`FeeKind.None`) déjà correct puisqu'aucun paiement n'est prélevé de toute façon. |
| **Comtesse Marianna Chevaux** | 12 bandes | Frais en or (HireCost/Upkeep) ✅. 📝 **"On n'échappe jamais à son passé..."** : jet 1D6 au dernier tour de partie / à la déroute (reste-part / reste-si-solde-payée / embuscade Zombies+Goules+Vampire pour D3 tours) — entièrement absent du wizard, aucun écran ne couvre "pendant" une partie sur table ; c'est aussi ce qui fait varier son upkeep réel dans le livre, non reflété par l'Upkeep fixe (75) utilisé pour la solde récurrente. 📝 Haine personnelle des Vampires envers elle (déjà une `SpecialRule` dédiée, "Hated by Vampires") — non modélisable via `HatredTargetWarbandArchetypeIds` (ciblage par bande, pas par Vampires-en-tant-qu'individus). |
| **Johann le Couteau** | 12 bandes | Frais en or + paiement alternatif (Ombre Cramoisie) ✅. Dagues comptant comme Épées (Parade uniquement, pas le bonus de sauvegarde) ✅ - `Dagger (Johann)`, objet unique dans Equipment.json portant la règle partagée `Parry (Sword)`, même principe que Ienh-Khain (Aenur). |
| **Nicodemus, le Pèlerin Maudit** | 11 bandes | Paiement en éclat de pierre magique (à l'engagement + solde après chaque bataille) ✅ - mêmes étapes Achat/Recrutement et Dramatis Personae que Johann/Veskit/Marianna, juste une devise différente (aucune option en or pour lui, "il n'a aucun intérêt pour l'or"). Bâton de Sorcier (deux mains = Gourdin + Parade comme rondache ; une main = libère l'autre pour l'Épée de Rezhebel) déjà modélisé (`Wizard's Staff (Nicodemus)`, Equipment.json). |
| **Marquand Volker & Ulli Leitpold** | 13 bandes (paire, toutes sauf Sœurs de Sigmar/Répurgateurs) | Départ Vagabond ✅ (chacun individuellement - le départ n'est pas synchronisé entre les deux au niveau du code, mais ils ont toujours été recrutés/quittent ensemble en pratique puisqu'ils arrivent toujours ensemble). Délai de re-recherche ✅ (chacun a son propre cooldown, non partagé - sans conséquence tant qu'ils partent toujours ensemble). **Recrutement en paire imposé + frais partagé (30 CO) ✅ (2026-09-01)** : le picker "Personnage spécial" n'affiche que Marquand (`DramatisPersona.IsHiddenFromSearchPicker` masque Ulli), le recruter recrute automatiquement Ulli aussi (`PairedWithDramatisPersonaId`) pour un seul HireCost de 30 CO (jamais 60) - voir `WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync`. **"Une Poignée d'Or" (A Fistful of Crowns) ✅ (2026-09-01)** : extraite du texte libre en une vraie `SpecialRule` (chip tapotable sur la carte, comme "On n'échappe jamais à son passé..." de Marianna) - texte générique, pas de montant en dur (les 30 CO du comparatif viennent du catalogue, affichés en direct à côté du bouton). Résolu en deux blocs, l'issue n'étant connue qu'après la bataille jouée sur table (pas un mécanisme de Lancement de Partie) : (1) côté bande propriétaire, bouton "Rendre hostile" sur la carte du guerrier (`WarriorRow.CanToggleHostile`/`WarbandDetailViewModel.ToggleHostile`) exclut sa contribution de la Valeur pour cette bataille (`Warrior.IsHostileThisBattle`) sans le supprimer du roster - à la Fin de Partie, `ApplyWandererDeparturesAsync` produit une phrase d'historique différenciée puis le retire comme tout Vagabond (aucun paiement de son côté, "seul le camp qui gagne le contrôle paie"). (2) côté n'importe quelle autre bande (même sur un autre appareil, aucun lien de données entre bandes) : case "Corruption de Marquand Volker & Ulli Leitpold" à la Fin de Partie (`EndOfGameDialogViewModel.ShowPairCorruptionOption`, visible seulement si cette bande ne possède pas déjà la paire) + montant, déduit de SA propre trésorerie. **"Où est l'Argent ?" ✅ (2026-09-01)** : repli si le montant de corruption saisi dépasse le solde prévisionnel de la bande à cette étape (`EndOfGameDialogViewModel.IsPairCorruptionUnaffordable`, même principe que `RareItemPurchaseRemainingTreasury`). Deux choix : "Céder du matériel" (pas de sélecteur de valeur automatique, juste un rappel textuel invitant à retirer du matériel équivalent depuis l'Inventaire - décision explicite, aucun mécanisme de vente partielle ailleurs dans l'app) ; "Duel avec le meneur" (même principe que Vendu aux Fosses/D66-65 côté Blessures, retour utilisateur explicite - `WonDuel`/`PairDuelRoll` réutilisent `InjurySubRollEntry` tel quel : victoire = rien de plus, défaite = un sous-jet D66 sur la table des Blessures Graves Héros appliqué au meneur de bande, résolu par la même mécanique que la relance de Vendu aux Fosses mais sans sa perte d'équipement inconditionnelle, absente du texte de cette règle-ci). 📝 "Inséparables" (rester à 4" l'un de l'autre, traîner le partenaire hors du champ) — positionnement sur table, hors périmètre de l'app de toute façon. |
| **Veskit, Bourreau Suprême** | Skavens (Clan Eshin) | Frais en or (HireCost/Upkeep) ✅. Rien d'autre en attente — pas Vagabond. |

## Historique

- **2026-09-01 (suite, "Une Poignée d'Or", mécanique par mécanique)** : la corruption elle-même
  ("A Fistful of Crowns" - enchère secrète adverse à un tour quelconque de la partie, l'app ne peut
  simuler ni le tour par tour ni le secret) est traitée après-coup plutôt qu'au Lancement de Partie,
  puisque son issue n'est connue qu'une fois la bataille jouée sur table. Extraite en vraie `SpecialRule`
  (chip tapotable, texte générique sans montant en dur). Côté propriétaire : bouton "Rendre hostile" sur
  la carte du guerrier (`Warrior.IsHostileThisBattle`, `WarriorRow.CanToggleHostile` limité à
  `FeeKind.Pair`) exclut sa Valeur pour cette bataille sans le retirer du roster ; `ApplyWandererDeparturesAsync`
  produit une phrase d'historique différenciée puis le retire comme tout Vagabond, sans paiement de son
  côté ("seul le camp qui gagne le contrôle paie"). Côté n'importe quelle autre bande (même sur un autre
  appareil - retour utilisateur explicite, donc aucun lien de données entre bandes) : case "Corruption de
  Marquand Volker & Ulli Leitpold" en Fin de Partie (`EndOfGameDialogViewModel.ShowPairCorruptionOption`,
  visible seulement si cette bande ne possède pas déjà la paire) + montant, déduit de SA trésorerie. "Où
  est l'Argent ?" (repli si le camp contrôlant ne peut pas payer) volontairement laissé de côté - règle
  suivante de la série, pas encore donnée par l'utilisateur. Bug trouvé en passant :
  `WarbandDetailViewModel.EditWarrior`'s copie défensive omettait déjà `DramatisPersonaId`/
  `DramatisPersonaRatingBonus` (pas seulement le nouveau `IsHostileThisBattle`) - éditer un guerrier
  Dramatis Persona via le bouton Éditer du roster effaçait silencieusement son lien catalogue au premier
  Enregistrer, corrigé au passage (même classe de bug que `DramatisPersonaViewModel.Edit()` déjà
  documentée plus haut, cette fois côté roster plutôt que Codex).

- **2026-09-01 (suite, "on va bien s'amuser pour finaliser le duo", partie 2)** : recrutement en paire
  imposé pour Ulli & Marquand ("vous devez les recruter tous les deux pour une bataille"), confirmé via
  `AskUserQuestion` (un seul choix "Ulli & Marquand" dans le picker plutôt que deux cartes synchronisées).
  Nouveaux champs `PairedWithDramatisPersonaId` (navigation self-référente, résolue après coup dans
  `LibraryService.GetDramatisPersonaeAsync`, même principe qu'`AlternativePaymentItem`) et
  `IsHiddenFromSearchPicker` (Ulli seule, masquée du picker de recrutement mais pas du Codex normal).
  `HasHireCost` généralisé de Gold à Gold-ou-Pair (même devise, même mécanisme de déduction - contrairement
  à Wyrdstone qui reste séparé) : recruter Marquand recrute automatiquement Ulli pour le même HireCost de
  30 CO, jamais doublé. Au passage, confirmé par le texte du livre que la restriction de bande existante
  (13/15 bandes, exclut Sœurs de Sigmar + Répurgateurs) et le RatingBonus (30+30=60) étaient déjà corrects.
  Reste pour le duo : l'enchère "Une Poignée d'Or"/"Où est l'Argent ?" (prochain chantier).
- **2026-09-01 (suite, "on va bien s'amuser pour finaliser le duo")** : délai de re-recherche câblé
  (Aenur/Ulli & Marquand, pas Bertha) - nouvelle table `WarbandDramatisPersonaCooldownEntity`, exclusion
  côté picker "Personnage spécial", pose/nettoyage à la Fin de Partie. Bug trouvé et corrigé en passant
  dans `DramatisPersonaViewModel.Edit()` (copie défensive oubliant `AlternativePaymentItemId`). Reste
  ouvert pour le duo : recrutement en paire non imposé, enchère "Une Poignée d'Or"/"Où est l'Argent ?" -
  prochain chantier annoncé par l'utilisateur.
- **2026-09-01** : création de ce fichier. Session ayant branché la recherche/recrutement du wizard
  Fin de Partie sur le vrai catalogue, ajouté le bloc de roster dédié (+ Francs-Tireurs, qui n'en avait
  pas non plus), corrigé `IsHero`/`GainsExperience` d'après le texte du livre, découplé la section
  Haine (blessures/objets/règles/compétences) des Règles spéciales affichées, ajouté l'Aide
  conditionnelle de Bertha au Lancement de Partie, et corrigé la consolidation des objets de départ
  dupliqués (2 Marteaux de Sigmarite) + un backfill pour les bases déjà seedées.
- **2026-09-01 (suite, même jour)** : chips d'équipement de départ dupliqués corrigés aussi côté Codex
  (dialogs récap Dramatis Personae ET Francs-Tireurs, même bug qu'au recrutement). Écran "Avant la
  partie" de Bertha nettoyé (nom en double retiré, barème d'Aide conditionnelle affiché - généré depuis
  `RatingGapAidTable.Tiers`, plus un simple resx statique). Départ automatique des personnages Vagabonds
  en Fin de Partie (`ApplyWandererDeparturesAsync`), généralisé aux 3 Vagabonds du catalogue. Puis
  affiné : un échec de l'Aide conditionnelle (écart trop faible/jet raté) retire désormais Bertha
  IMMÉDIATEMENT à la confirmation du Lancement de Partie plutôt que d'attendre la Fin de Partie suivante
  (`DramatisPersonaAidEntry.WontFight`) - reflète que le personnage n'a jamais rejoint la bande pour
  cette bataille précise, avec un avertissement visible sur la carte avant confirmation.
- **2026-09-01 (suite, dagues de Johann)** : "compte comme deux Épées" d'abord modélisé comme une règle
  spéciale directement sur le Dramatis Persona ("Toujours Deux Épées") - revenu dessus après retour
  utilisateur sur deux points : (1) la nuance du livre est plus précise ("censé avoir Parade et pas la
  sauvegarde" - les vraies Épées combinées donnent aussi un bonus de sauvegarde d'armure que les dagues
  de Johann n'ont PAS) ; (2) même principe que l'arme d'Aenur (Ienh-Khain) - un vrai objet catalogue
  unique, pas une règle de personnage. Remplacé par `Dagger (Johann)` (`Equipment.json`, `IsUniqueArtefact`,
  coût 0) portant la règle partagée `Parry (Sword)` déjà utilisée par Ienh-Khain/d'autres armes, avec sa
  propre description qui précise "pour la règle Parade uniquement, pas le bonus de sauvegarde". Ses 2
  "Dagger" génériques (`startingEquipmentNames`) remplacés par 2 `Dagger (Johann)` - consolidés en une
  seule ligne "x2" comme les 2 Marteaux de Sigmarite de Bertha. Nouveau backfill général
  `BackfillNewEquipmentItemsAsync` au passage : `Equipment.json` n'avait jusqu'ici aucun mécanisme pour
  qu'un objet VRAIMENT NOUVEAU (jamais vu avant) atteigne une base déjà seedée - seul un objet déjà
  existant pouvait être corrigé (Bertha/Johann plus haut). S'applique à toute future entrée neuve, pas
  seulement Dagger (Johann).
- **2026-09-01 (suite, "on a qu'a brancher la wyrstone à son paiement")** : Nicodemus rejoint Johann/
  Veskit/Marianna dans les étapes Achat/Recrutement et Dramatis Personae du wizard Fin de Partie, payé
  en pierre magique (1 éclat, jamais d'or) plutôt qu'en or - `RareItemSearchEntry.HasWyrdstoneCost`/
  `EffectiveWyrdstoneCostForShards`, bandeau "pierres magiques restantes" parallèle à celui de la
  trésorerie (calculé après le choix de vente de l'étape Vente de pierres magiques, qui précède celle-ci
  dans le wizard). `DramatisPersonaUpkeepEntry` généralisé aux deux devises (`IsWyrdstoneFee`). Confirmé
  au passage : le Bâton de Sorcier de Nicodemus (deux mains = Gourdin + Parade comme rondache ; une main
  = libère l'autre pour l'Épée de Rezhebel, qui ne peut elle-même pas parer) était déjà modélisé
  correctement dans le catalogue (`Wizard's Staff (Nicodemus)`, Equipment.json) lors d'une session
  antérieure - rien à refaire.
- **2026-09-01 (suite, bug de test utilisateur)** : l'étape "Achat/Recrutement" du wizard Fin de Partie
  était entièrement sautée dès qu'un Héros n'avait trouvé qu'un Personnage spécial (pas d'Objet rare) -
  `Steps` ne l'incluait que via `RareItemSearchEntries.Any(e => e.IsSuccess)`, qui ne couvre que le mode
  Objet (`IsCharacterFound` pour le mode Personnage n'était jamais vérifié). Bug préexistant (depuis
  l'ajout du mode Personnage le 2026-08-31), révélé en testant le paiement de Johann - corrigé en
  remplaçant par `IsFound` (déjà utilisé correctement par `RareItemsWithResults`, le contenu affiché À
  L'INTÉRIEUR de cette étape - seule la condition d'inclusion de l'étape elle-même avait été oubliée).
  Au passage, `RareItemPurchaseTotalCost`/`RareItemPurchaseRemainingTreasury` ne se rafraîchissaient pas
  en direct quand le joueur cochait/décochait "Recruter" ou basculait le paiement alternatif d'un
  personnage (notification `PropertyChanged` incomplète) - complétée en même temps.
- **2026-09-01 (suite, "passons à Johann")** : frais d'engagement en or réellement câblés (HireCost à
  l'engagement + nouvelle étape "Dramatis Personae" en Fin de Partie pour l'Upkeep récurrent), scopé à
  `FeeKind.Gold` (Johann/Veskit/Marianna) après confirmation via `AskUserQuestion` - Bertha/Nicodemus/
  Ulli & Marquand restent hors périmètre (mécaniques propres bien plus élaborées). Nouveau champ
  générique `DramatisPersona.AlternativePaymentItemId` (picker Codex) pour le paiement alternatif de
  Johann (Ombre Cramoisie au lieu d'or) - retrait réel de l'inventaire de bande, confirmé via
  `AskUserQuestion` plutôt qu'un simple choix déclaratif. Au passage : traduction FR de "Crimson Shade"
  harmonisée ("Ombre Cramoisie", déjà le nom du catalogue Equipment.json - Johann utilisait jusque-là
  "Ombre Écarlate", une traduction concurrente du même objet).
