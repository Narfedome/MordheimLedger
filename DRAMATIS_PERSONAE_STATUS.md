# Dramatis Personae — état d'implémentation

Suivi de ce qui reste à gérer sur le catalogue Dramatis Personae (`Core/Models/Library/
DramatisPersona.cs`, `Data/SeedData/DramatisPersonae.json`, 8 personnages) au-delà du catalogue et
du recrutement de base. Mis à jour à chaque avancée — dernière mise à jour : **2026-09-04** (étape
"Vendu aux Fosses" séparée de la carte Blessure, règles spéciales de combat affichées Arène/Duel,
bug de double paiement Une Poignée d'Or corrigé).

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
| **Marquand Volker & Ulli Leitpold** | 13 bandes (paire, toutes sauf Sœurs de Sigmar/Répurgateurs) | Départ Vagabond ✅ (chacun individuellement - le départ n'est pas synchronisé entre les deux au niveau du code, mais ils ont toujours été recrutés/quittent ensemble en pratique puisqu'ils arrivent toujours ensemble). Délai de re-recherche ✅ (chacun a son propre cooldown, non partagé - sans conséquence tant qu'ils partent toujours ensemble). **Recrutement en paire imposé + frais partagé (30 CO) ✅ (2026-09-01)** : le picker "Personnage spécial" n'affiche que Marquand (`DramatisPersona.IsHiddenFromSearchPicker` masque Ulli), le recruter recrute automatiquement Ulli aussi (`PairedWithDramatisPersonaId`) pour un seul HireCost de 30 CO (jamais 60) - voir `WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync`. **"Une Poignée d'Or" (A Fistful of Crowns) ✅ (2026-09-01)** : extraite du texte libre en une vraie `SpecialRule` (chip tapotable sur la carte, comme "On n'échappe jamais à son passé..." de Marianna) - texte générique, pas de montant en dur (les 30 CO du comparatif viennent du catalogue, affichés en direct à côté du bouton). Résolu en trois blocs, l'issue n'étant connue qu'après la bataille jouée sur table (pas un mécanisme de Lancement de Partie) : (1) côté bande propriétaire, bouton "Rendre hostile" sur la carte du guerrier (`WarriorRow.CanToggleHostile`/`WarbandDetailViewModel.ToggleHostile`) exclut sa contribution de la Valeur pour cette bataille (`Warrior.IsHostileThisBattle`) sans le supprimer du roster - à la Fin de Partie, `ApplyWandererDeparturesAsync` produit une phrase d'historique différenciée puis le retire comme tout Vagabond, uniquement si la paire a effectivement changé de camp. (1b, 2026-09-01, retour utilisateur - complète le point ci-dessus) si la bande propriétaire a plutôt GARDÉ le contrôle après une tentative adverse (payé sa propre contre-offre, voir l'exemple Steiner/Albrecht du livre - "seul le camp qui obtient OU GARDE le contrôle paie"), une carte "C'est l'heure de payer !" à l'étape Dramatis Personae (`ShowPairRetentionOption`, visible seulement si cette bande possède déjà la paire) prend un montant libre (0/vide = aucune tentative cette partie) et le déduit de SA trésorerie en tout dernier ("à la fin de tous les décomptes", retour utilisateur). (2) côté n'importe quelle autre bande (même sur un autre appareil, aucun lien de données entre bandes) : case "Corruption de Marquand Volker & Ulli Leitpold" à l'étape Prisonniers **renommée "Prisonniers & Corruption"** (`EndOfGameDialogViewModel.ShowPairCorruptionOption`, visible seulement si cette bande ne possède pas déjà la paire) + montant, déduit de SA propre trésorerie. **"Où est l'Argent ?" ✅ (2026-09-01, révisée le même jour)** : repli PARTAGÉ entre (1b) et (2) - une bande ne peut jamais déclencher les deux à la fois, donc aucun risque à réutiliser le même sous-système (`WheresMoneyChoiceLabels`, déclaré dans `EndOfGameDialogViewModel.Captives.cs`) - si le montant saisi (corruption OU rétention) dépasse le solde prévisionnel de la bande à cette étape (`IsPairCorruptionUnaffordable`/`IsPairRetentionUnaffordable`, même principe que `RareItemPurchaseRemainingTreasury`). (1b) est en plus désormais bloqué si la paire est hostile CETTE bataille (`!Warrior.IsHostileThisBattle`, retour utilisateur - hostile veut dire corrompu donc de toute façon parti·e sans contre-offre possible). **Déterminé automatiquement depuis le 2026-09-02, plus de choix manuel du joueur** (retour utilisateur -
"on détermine automatiquement si on a le duel ou non, pas de choix dans le picker") : `WantsEquipmentSeizure`/`WantsDuel` (`Captives.cs`) comparent la valeur totale du magot de bande à la somme
due - le magot suffit → **"Céder du matériel"** ; sinon → **"Duel avec le meneur"**, seule alternative
qui reste. `SeizedEquipmentItems` parcourt l'inventaire de bande snapshotté à l'ouverture du wizard
(`_warbandInventory`) trié par valeur DÉCROISSANTE (`WarbandEquipment.SellValue`, révisé le 2026-09-02 -
"on prend les équipements les plus valuables d'abord", perdre un minimum d'OBJETS plutôt qu'un minimum de
valeur, ex. une Épée en Gromril à 60 CO + 3 Épées classiques à 10 CO pour une rançon de 50 CO → on ne
perd QUE l'Épée en Gromril) et accumule jusqu'à atteindre/dépasser le montant demandé, affiché en chips ;
les objets choisis sont réellement retirés de l'inventaire à l'application
(`WarbandDetailViewModel.EndOfGame.ApplyPairEquipmentSeizureIfNeededAsync`, `RemoveWarbandEquipmentAsync`
par objet). "Duel avec le meneur" a sa propre étape de wizard dédiée (`StepKind.PairDuel`,
`EndOfGameDialogViewModel.PairDuel.cs`) pour éviter de surcharger la carte Prisonniers/Dramatis
Personae : reprend le gabarit visuel de Vendu aux Fosses (retour utilisateur explicite) - une carte
profil par participant (meneur de bande via `DuelLeaderRow`, Marquand et Ulli via `DuelMarquand`/
`DuelUlli` résolus depuis le catalogue) avec `StatRowView` + chips Équipement/Compétences pour le
meneur, Compétences seules pour Marquand/Ulli (pas d'équipement résolu en objets pour eux, hors
périmètre) ; `WonDuel`/`PairDuelRoll` (toujours `InjurySubRollEntry`, D66 sur la table des Blessures
Graves Héros en cas de défaite, appliqué au meneur) peuplés/effacés automatiquement dès que `WantsDuel`
devient vrai/faux (`SyncPairDuelRoll`). Étape entièrement absente si aucun des deux montants n'est
impayable. 📝 "Inséparables" (rester à 4" l'un de l'autre, traîner le partenaire hors du champ) —
positionnement sur table, hors périmètre de l'app de toute façon. |
| **Veskit, Bourreau Suprême** | Skavens (Clan Eshin) | Frais en or (HireCost/Upkeep) ✅. Rien d'autre en attente — pas Vagabond. |

## Historique

- **2026-09-04 (retour utilisateur, "comme pour le combat avec les dramatis, les duels de l'arène
  devraient être séparés dans un step séparé") : "Vendu aux Fosses" (Blessure Grave 65) devient sa
  propre étape (`StepKind.PitFight`/`IsPitFightStep`), au lieu d'un bloc embarqué dans la carte
  Blessure du guerrier concerné.** Même principe que l'extraction Corruption/Rétention → Une Poignée
  d'Or → Duel avec le meneur (entrée juste en dessous) : `Steps` (`EndOfGameDialogViewModel.cs`)
  insère désormais `new WizardStep(StepKind.PitFight, r)` juste après `new WizardStep(StepKind.Injury,
  r)` pour tout guerrier dont `ShowSoldToThePits` est vrai (boucle `foreach`, plus un simple `Select` -
  il faut pouvoir intercaler une deuxième étape conditionnelle entre deux guerriers). `CurrentInjuryWarrior`
  (`=> Current.Warrior`) reste valable tel quel sur la nouvelle étape, indépendant du `StepKind`. La
  validation du sous-jet de relance en cas de défaite (`SoldToPitsRerollRoll`) est déplacée de
  `ValidateInjuryStep` vers une nouvelle `ValidatePitFightStep` (`EndOfGameDialogViewModel.Injury.cs`) -
  aucun changement de comportement, pur déplacement.
- **Même passe : règles spéciales de combat affichées côté Arène (Vendu aux Fosses) ET Duel avec le
  meneur, chip Compétence du Duel enfin tapable, hint "compétences autorisées" du Gladiateur retiré**
  (retour utilisateur, "pas la peine d'afficher les compétences qu'il peut gagner... les chips des
  compétences n'ouvrent pas le détail... il faut rajouter dans les 2 cas les règles spéciales du
  combat"). `WarriorOutcomeRow` gagne `SpecialRules`/`HasSpecialRules`
  (`IReadOnlyList<SpecialRuleChip>`, nouveau paramètre `specialRules` au constructeur) - transmis par
  l'appelant depuis `WarriorRow.SpecialRules` (déjà fusionné Archétype+Bande+Équipement côté roster,
  jusque-là silencieusement perdu au passage vers `WarriorOutcomeRow`). Nouvelles commandes
  `ShowWarriorSpecialRuleDetail` (le "vrai" guerrier, partagée Arène/Duel), `ShowPitFighterSpecialRuleDetail`
  (le Gladiateur, `HiredSword.SpecialRules`), `ShowDuelSpecialRuleDetail` (adversaire du Duel,
  `DramatisPersona.SpecialRules`) et `ShowDuelSkillDetail` (corrige l'oubli de `Command` sur la chip
  Compétence de `DuelOpponentDisplay` - la chip Équipement en avait déjà une). `DuelOpponentDisplay`
  gagne `HasSpecialRules`. Texte "compétences autorisées" du Gladiateur (`PitFighterAllowedSkillCategoriesText`,
  `HiredSwordAllowedSkillCategoriesPh`) retiré : jamais de compétence réellement APPRISE à afficher (ce
  Gladiateur n'est jamais recruté), cohérent avec le Duel qui n'a jamais affiché ce genre de hint.
- **2026-09-04 (retour utilisateur, "j'ai l'impression de la prise en charge des valeurs des items
  présents dans le magot n'est pas bien gérée... si je mets 413 de paiement... les items ne sont pas
  considérés") : bug de double paiement corrigé dans `ApplyDramatisPersonaUpkeepAsync`.**
  `SellValue`/`MaterialRule` étaient en réalité corrects sur le papier - le vrai bug : la Corruption/
  Rétention débitait la trésorerie du montant COMPLET même quand `IsPairCorruptionUnaffordable`/
  `IsPairRetentionUnaffordable` était vrai ET que la saisie d'équipement/le duel s'appliquaient EN PLUS,
  pour la MÊME dette (or perdu ET objets/meneur perdus). Confirmé via `AskUserQuestion` : lecture
  stricte de "the pair INSTEAD seizes an equal value of equipment" - aucun or ne sort quand le paiement
  est impossible, tout part en objets (ou duel). Corrigé en gardant `!IsPairCorruptionUnaffordable`/
  `!IsPairRetentionUnaffordable` en garde sur les deux blocs de débit d'or.
- **2026-09-04 (retour utilisateur, "rendre le truc générique... si on a la règle Une Poignée d'Or...
  si le personnage a la règle c'est l'heure de payer, on réutilise la mécanique du combat/vol de
  magot") : tout le mécanisme piloté par la SpecialRule, plus par `FeeKind.Pair`.** Jusqu'ici,
  `ShowPairCorruptionOption`/`ShowPairRetentionOption`/`IsPairBeingRecruitedThisWizard` filtraient le
  catalogue par `FeeKind == DramatisPersonaHireFeeKind.Pair` - un futur Dramatis Persona SOLO (sans
  partenaire) portant la même règle "A Fistful of Crowns"/"Une Poignée d'Or" n'aurait rien déclenché.
  Remplacé par `_fistfulOfCrownsPersonaIds` (`EndOfGameDialogViewModel.PairEngagement.cs`) : l'id de la
  SpecialRule est résolu une fois via `_specialRulesByEnglishName` (même idiome que le reste du wizard
  pour identifier une règle précise indépendamment de la langue d'affichage), puis tout Dramatis Persona
  du catalogue portant cette règle rejoint l'ensemble "corruptible" - `FeeKind.Pair` reste un sujet
  séparé (le recrutement COMBINÉ à 30 CO propre à Ulli &amp; Marquand, pas la SpecialRule elle-même).
  Le step Duel (`EndOfGameDialogViewModel.PairDuel.cs`) suit : `DuelMarquand`/`DuelUlli` (2 propriétés
  fixes) remplacées par `DuelOpponents` (`List<DuelOpponentDisplay>`, nouveau record de haut niveau) -
  1 carte si le Dramatis Persona concerné n'a pas de `PairedWithDramatisPersona`, 2 s'il en a un ; le
  XAML boucle désormais sur cette liste (`BindableLayout`) au lieu de deux `Border` recopiés à
  l'identique. Limite assumée, documentée dans le code : le mécanisme reste pensé pour UN SEUL
  personnage/groupe "corruptible" actif par bande à la fois (`FirstOrDefault` sur le catalogue) - si
  plusieurs personnages distincts portaient un jour cette règle simultanément, il faudrait généraliser
  en vraie collection (même schéma que `DramatisPersonaUpkeepEntries`), pas fait faute de second cas
  réel aujourd'hui.
- **2026-09-04 (retour utilisateur, "je me tate sur l'écran de corruption à le faire après dans un autre
  step pour éviter de mélanger et de surcharger") : Corruption/Rétention regroupées dans leur propre
  étape "Une Poignée d'Or".** Avant ce découpage, la Corruption vivait sur l'étape Prisonniers et la
  Rétention sur l'étape Dramatis Personae - retour utilisateur explicite du 2026-09-01 ("dans la section
  des frais d'entretien, on doit les avoir"), reconsidéré le lendemain : "il faut la mettre au moment du
  paiement de recrutement... si ils sont déjà recrutés, on fait la proposition de paiement de la contre-
  offre (c'est ce qu'on a déjà), si ils ne sont pas recrutés, on met la case à cocher dans les frais
  d'engagement". Confirmé via un mock (deux cartes, une par cas) puis la question de la position : même
  logique que le Duel (sa propre étape pour ne pas surcharger), donc nouvelle étape dédiée
  `StepKind.PairEngagement` ("Une Poignée d'Or"), insérée juste après l'étape Dramatis Personae (upkeep
  de Johann/Veskit/Marianna/Nicodemus, qui redevient son seul sujet) - la Corruption s'apparente à un
  "hire" (irait naturellement côté Recrutement) et la Rétention à un "upkeep" (irait naturellement côté
  Dramatis Personae), mais comme les deux sont mutuellement exclusives et partagent tout leur état, un
  seul emplacement fixe plutôt que deux. Nouveau fichier `EndOfGameDialogViewModel.PairEngagement.cs`
  (tout le sous-système "Où est l'Argent ?", auparavant scindé entre `Captives.cs`/`DramatisPersonae.cs`)
  - ces deux fichiers reviennent à leur périmètre d'origine (Prisonniers ennemis capturés / Upkeep
  uniquement). Effet de bord positif : le Duel n'a plus besoin que d'UN SEUL point d'ancrage dynamique
  (juste après "Une Poignée d'Or") au lieu de deux selon Corruption/Rétention. L'étape Prisonniers
  redevient "Prisonniers ennemis" (perd son titre composite "Prisonniers & Corruption").
- **2026-09-03 (retour utilisateur, "les armes ne sont pas affichées... dans le combat contre la
  paire") : équipement de départ de Marquand &amp; Ulli enfin affiché dans le step Duel.** En creusant :
  `DramatisPersona.StartingEquipmentIds` (le modèle) n'a JAMAIS porté sa propre résolution en vrais
  `EquipmentItem` - seulement une liste d'ids catalogue brute, résolue à la demande par chaque appelant
  (même principe que `HiredSword.StartingEquipmentIds`/`PitFighterEquipment` pour le Gladiateur de Vendu
  aux Fosses, qui lui EST déjà résolu à l'ouverture du wizard - l'arène elle-même n'était pas cassée).
  Nouveau : `WarbandDetailViewModel.EndOfGame` résout maintenant `dramatisPersonaStartingEquipmentById`
  (`Dictionary<int, List<EquipmentQuantityChip>>`, tout le catalogue) via
  `EquipmentQuantityChip.GroupFrom` - le même helper que `DetailDialogService.
  ShowDramatisPersonaDetailDialogAsync` (Codex), qui gère correctement les doublons (ex. futur "x2") -
  passé au constructeur du wizard, exposé via `DuelMarquandEquipment`/`DuelUlliEquipment`
  (`EndOfGameDialogViewModel.PairDuel.cs`) et affiché en chips tapables (nouveau
  `ShowDuelEquipmentDetailCommand`) sur les deux cartes profil du step Duel, juste avant leurs
  Compétences (déjà affichées).
- **2026-09-03 (retour utilisateur, "pour le combat en cas de défaite, le chef de bande est forcément
  mort") : duel simplifié, plus de jet.** La toute première version du step Duel réutilisait par erreur
  le gabarit "Vendu aux Fosses" jusqu'au bout, y compris son sous-jet D66 sur la table des Blessures
  Graves en cas de défaite (avec ses propres sous-cas Blessure Profonde/Capturé-rançon) - absent du texte
  de la règle "Une Poignée d'Or"/"Où est l'Argent ?", qui ne prévoit que victoire ou mort. Simplifié :
  `WonDuel` (case à cocher) reste le seul état à saisir ; côté application
  (`WarbandDetailViewModel.EndOfGame.ApplyPairDuelIfNeededAsync`), une défaite bascule directement
  `warrior.Status = WarriorStatus.Dead` sans rien d'autre à résoudre. Retiré : `PairDuelRoll`/
  `HasPairDuelRoll`/`PopulatePairDuelRoll` (PairDuel.cs), tout le bloc XAML de sous-jet (Entry+dés+
  sous-cas Blessure Profonde/Capturé), et `ValidatePairDuelStep` n'a plus rien à valider (retourne
  `true`). `SyncPairDuelRoll` (Captives.cs/DramatisPersonae.cs) renommée `ResetWonDuelIfNoLongerNeeded` -
  ne fait plus que remettre `WonDuel` à faux si `WantsDuel` redevient faux, sans plus de collection de
  sous-jet à vider.
- **2026-09-03 (retour utilisateur, "on a toujours la case à cocher dans corruption/prisonnier alors
  qu'on a la paire de recruté") : vrai bug, le binding IsVisible manquait purement et simplement.**
  `EndOfGameDialog.xaml` : le `VerticalStackLayout` englobant toute la case "Corruption de Marquand
  Volker & Ulli Leitpold" (étape Prisonniers) n'avait PAS de `IsVisible="{Binding ShowPairCorruptionOption}"`
  - le commentaire juste au-dessus décrivait cette intention ("visible seulement pour une bande qui n'a
  PAS déjà Ulli & Marquand"), mais l'attribut XAML lui-même était absent, perdu au fil des nombreuses
  éditions de ce bloc cette même journée (Céder du matériel automatique, retrait du Picker, etc.) - la
  case s'affichait donc TOUJOURS, y compris pour une bande qui possède déjà la paire. `ShowPairCorruptionOption`
  côté C# (Captives.cs) était pourtant correcte (vérifiée par relecture) - seul le binding manquait côté
  vue. Corrigé en une ligne.
- **2026-09-03 (retour utilisateur, citant le livre : "The warband should sell any Wyrdstone necessary
  in order to pay the hire or bribe") : vente de pierre magique automatique avant Céder du matériel/
  Duel.** Nouveau maillon dans la cascade "Où est l'Argent ?" - avant de tomber sur Céder du matériel/
  Duel avec le meneur, la bande vend maintenant AUTOMATIQUEMENT juste assez de pierre magique en plus de
  ce que le joueur a déjà choisi à l'étape Vente (`ShardsToSell`) pour couvrir le manque en or de la
  Corruption/Rétention, dans la limite du stock disponible (`MaxShardsToSell`) - `AutoSellWyrdstoneIfNeeded`
  (Captives.cs), une recherche croissante simple sur `Core.Rules.WyrdstoneSaleTable` (non linéaire par
  palier, pas une formule). N'augmente jamais `ShardsToSell` au-delà du nécessaire, ne le diminue jamais.
  Appelée depuis `NotifyTreasuryChanged` (donc réévaluée à chaque décision financière du wizard, pas
  seulement au montant de corruption/rétention) ; `ShardsToSell` notifie désormais aussi
  `NotifyTreasuryChanged` en retour (`OnShardsToSellChanged`, absent avant ce correctif - un ajustement
  manuel du joueur sur l'étape Vente ne se répercutait pas ailleurs). Décision confirmée via
  `AskUserQuestion` : 100% automatique (même philosophie que Céder du matériel/Duel), jamais un choix
  manuel. **Point jumeau explicitement écarté par l'utilisateur le même jour** : un Objet rare tout juste
  acheté cette partie (pas encore réellement dans le stash) pouvant être saisi par la paire - pas
  implémenté, revert appliqué après une première tentative.
- **2026-09-03 (retour utilisateur, "le duel n'a pas une position fixe, on l'insère là où on en a
  besoin") : position du step Duel rendue dynamique.** Après le correctif trésorerie ci-dessous, le
  joueur a testé un cas Corruption réel : la note "Le magot ne suffit pas..." s'affichait bien
  (`WantsDuel` correctement vrai), mais cliquer Suivant depuis Prisonniers menait à Expérience, pas au
  Duel - qui n'apparaissait qu'en réalité bien plus tard (après Francs-Tireurs), sa position étant
  jusque-là FIXE (toujours juste avant Récapitulatif, après toutes les étapes financières) plutôt que
  liée au contexte qui l'a réellement déclenché. `EndOfGameDialogViewModel.Steps` insère désormais
  `StepKind.PairDuel` à DEUX endroits distincts et mutuellement exclusifs : juste après
  `StepKind.Captives` si `IsPairCorruptionUnaffordable && WantsDuel` (bande qui ne possède pas la
  paire), ou juste après `StepKind.DramatisPersonae` si `IsPairRetentionUnaffordable && WantsDuel`
  (bande qui la possède déjà) - jamais les deux en même temps par bande. Le texte
  `EndOfGameWheresMoneyDuelNote` ("le magot ne suffit pas...") repasse de "plus tard dans cet assistant"
  (formulation intermédiaire, devenue fausse une fois cette position dynamique en place) à "étape
  suivante" (à nouveau vrai dans les deux contextes).
- **2026-09-03 (retour utilisateur, "on n'utilise pas le même solde de trésorerie à la corruption et à
  l'achat d'objet rare") : trésorerie du wizard Fin de Partie unifiée en une seule source de vérité.**
  Avant ce correctif, chaque étape financière du wizard (Achat d'Objets rares, Francs-Tireurs, Homme de
  main équipé d'Exploration, Corruption/Rétention d'Ulli & Marquand) calculait sa PROPRE version
  partielle du solde restant, ne tenant compte QUE de ce qui avait déjà été décidé dans SA branche -
  payer 200 CO de corruption à l'étape Prisonniers (tôt dans le wizard) ne faisait pas baisser le solde
  affiché ensuite à l'étape Achat d'Objets rares (bien plus tard), et symétriquement un Franc-Tireur
  engagé ne faisait pas baisser le solde de corruption. Nouvelle propriété unique
  `EndOfGameDialogViewModel.EndOfGameTreasuryRemaining` (partie du fichier principal, puisqu'elle croise
  des données de 4 étapes) : part de la trésorerie de la bande avant cette Fin de Partie + or
  d'Exploration + Vente de pierres magiques, retranche TOUT ce qui a déjà été décidé n'importe où dans ce
  même wizard (l'addition ne dépend pas de l'ordre des étapes) - Homme de main équipé, Objets rares/
  personnages recrutés à frais en or, engagement + soldes de Francs-Tireurs, soldes de Dramatis Personae
  en or, Corruption/Rétention de Marquand & Ulli. `RareItemPurchaseRemainingTreasury`/
  `EquippedHenchmanTreasuryAfter` deviennent de simples alias ; `HiredSwordTreasuryAfter` (jamais affiché,
  seulement consommé en interne) est retiré, `CanAffordNewHiredSword`/`CanAffordEquippedHenchman`
  comparent directement `EndOfGameTreasuryRemaining >= 0` (le coût de la sélection en cours est déjà
  retranché du solde unique) plutôt que leur propre formule isolée. `DramatisPersonaeBaselineTreasury`
  (Captives.cs) redéfinie comme `EndOfGameTreasuryRemaining + PairPaymentAmountCommitted` (nouveau
  helper qui "remet en circulation" le montant de corruption/rétention déjà retranché, pour comparer CE
  montant précis au solde restant une fois tout le reste connu). Nouveau `NotifyTreasuryChanged()`
  centralise toutes les notifications `OnPropertyChanged` qui dépendent de ce solde (remplace plusieurs
  listes `[NotifyPropertyChangedFor]`/blocs manuels dupliqués - décision délibérée après avoir trouvé le
  bug `IsPairDuelStep` la même journée, causé par exactement ce genre de liste manuelle incomplète) ;
  appelée depuis chaque point de décision financière du wizard, y compris deux abonnements
  `PropertyChanged` qui manquaient purement et simplement (`HiredSwordUpkeepEntries`, jamais souscrite
  avant ce correctif).
- **2026-09-03 (retour utilisateur, "il manque le step de combat avec Ulli et Marquand") : vrai bug
  trouvé, `IsPairDuelStep` n'était jamais notifiée.** L'ajout du step `StepKind.PairDuel` (2026-09-01)
  avait oublié d'ajouter `IsPairDuelStep` à la liste `[NotifyPropertyChangedFor(...)]` de `StepIndex`
  (`EndOfGameDialogViewModel.cs`) - présente pour TOUS les autres `IsXxxStep`, sauf celui-ci. Conséquence
  concrète : `StepIndex` avançait correctement jusqu'à la position du step Duel (`StepLabel`, lui bien
  notifié, affichait le bon compteur "Étape X/Y"), mais la vue liée à `IsPairDuelStep` ne recevait jamais
  le signal pour s'afficher - page blanche, avec son bouton "Suivant" toujours actif. Cliquer dessus
  avançait au step RÉEL suivant (Récapitulatif), donnant l'impression que le step Duel n'existait pas du
  tout ("on arrive à la fin du wizard"). Repéré en confirmant d'abord que le bloc "Où est l'Argent ?"
  s'affichait bien avec la mention "le magot ne suffit pas" (donc `WantsDuel` correctement vrai) avant de
  chercher plus loin. Corrigé en une ligne. Au passage, le texte d'accompagnement
  (`EndOfGameWheresMoneyDuelNote`) disait "étape suivante" - trompeur pour le cas Corruption
  (`Captives`, tôt dans l'assistant : le step Duel est inséré bien plus tard, après Dramatis Personae,
  pas juste après) même s'il est exact pour le cas Rétention (`DramatisPersonae`, déjà juste avant) -
  reformulé en "plus tard dans cet assistant", vrai dans les deux cas.
- **2026-09-03 (retour utilisateur, "les dramatis personae ne peuvent pas trouver des objets rares/
  personnages spéciaux, c'est que les héros")** : `RareItemSearchEntries` (étapes Recherche/Achat,
  `EndOfGameDialogViewModel.cs`/`.RareItems.cs`) se construisait sur `WarriorRows.Where(r =>
  r.Warrior.IsHero)`, or `IsHero` vaut TRUE pour un Dramatis Persona (même flux Blessures Graves/XP
  qu'un vrai Héros) - ils obtenaient donc à tort leur propre carte de recherche. Exclus explicitement
  (`&& !r.Warrior.IsDramatisPersona`) à la fois à la construction des entrées et dans
  `HasEligibleHeroesForRareItems` (qui pilote la présence de l'étape elle-même - sans ce second
  correctif, une bande dont le seul "Héros" éligible serait un Dramatis Persona aurait affiché une
  étape Recherche vide). Même limite déjà connue/corrigée pour `SurvivingHeroCount` (Exploration,
  2026-09-01) et pour le décompte de tête à la vente de pierre magique (toujours ouverte, voir
  Transverse ci-dessus).
- **2026-09-02 (suite, "si on a engager la paire, on ne doit pas voir la case de corruption")** :
  `ShowPairCorruptionOption` (étape Prisonniers) restait vraie même quand le joueur venait de recruter
  Marquand &amp; Ulli via la recherche "Personnage spécial" DANS LA MÊME Fin de Partie - l'étape
  Prisonniers précède l'étape Achat/Recrutement dans l'ordre du wizard (Steps), et cette propriété était
  figée une fois pour toutes à la construction (avant que `RareItemSearchEntries` existe même). Convertie
  en propriété CALCULÉE : reste fausse si la bande possède déjà la paire dans son roster (comme avant),
  ou si `IsPairBeingRecruitedThisWizard` (une entrée de recherche a `IsRecruited` = vrai ET vise Marquand/
  Ulli) - notifiée depuis le handler `WantsToRecruit`/`IsCharacterFound` déjà existant des
  `RareItemSearchEntries` (`EndOfGameDialogViewModel.cs`), donc si le joueur revient en arrière à l'étape
  Prisonniers après avoir recruté la paire plus loin dans le wizard, la case a disparu.
- **2026-09-02 (suite, "on détermine automatiquement si on a le duel ou non, pas de choix dans le
  picker" + tri par valeur inversé pour Céder du matériel).** Deux changements sur "Où est l'Argent ?"
  (`EndOfGameDialogViewModel.Captives.cs`). (1) Le Picker manuel (WheresMoneyChoiceLabels/
  SelectedWheresMoneyChoice, retiré entièrement) est remplacé par une détermination automatique :
  `WantsEquipmentSeizure`/`WantsDuel` comparent la valeur totale du magot de bande
  (`WarbandInventoryTotalValue`) au montant dû - le magot suffit → céder du matériel (jamais de duel
  dans ce cas) ; sinon → duel avec le meneur, seule alternative qui reste. (2) `SeizedEquipmentItems`
  trie désormais le magot en ordre DÉCROISSANT de valeur (`OrderByDescending`, remplace l'ancien tri
  croissant) : "on prend les équipements les plus valuables d'abord jusqu'à atteindre la somme" (retour
  utilisateur, exemple donné - une Épée en Gromril à 60 CO + 3 Épées classiques à 10 CO pour une rançon
  de 50 CO → on ne perd QUE l'Épée en Gromril). Perdre un minimum d'OBJETS (même si ça dépasse un peu la
  somme due) plutôt qu'un minimum de VALEUR, contrairement à l'ancien tri. `WonDuel`/`PairDuelRoll` sont
  désormais peuplés/effacés automatiquement (`SyncPairDuelRoll`, appelé depuis les handlers de
  changement de PairCorruptionAmount/PairRetentionAmount/WantsToRecordPairCorruption - `WantsDuel` est
  une propriété calculée, pas un ObservableProperty, donc pas de `partial void OnXxxChanged` direct
  possible dessus).
- **2026-09-02 (retour utilisateur, "peu importe la somme que je mets, pas de choix") : trésorerie
  seuil rendue visible + rafraîchissement corrigé.** Le joueur testait le bloc "C'est l'heure de
  payer !" (rétention) - trésorerie de la bande affichée sur sa fiche : 599, montant saisi : 600 -
  et n'obtenait ni blocage ni bloc "Où est l'Argent ?". Cause probable : rien à l'écran ne montrait
  le VRAI seuil comparé (`DramatisPersonaeBaselineTreasury`), qui peut différer de la trésorerie
  persistée affichée sur la fiche de bande dès que CETTE Fin de Partie a rapporté de l'or
  (Exploration/Vente de pierres magiques, pas encore enregistré au moment du test). Ajout d'un
  affichage direct (`DramatisPersonaeBaselineTreasuryDisplay`, "Trésorerie disponible pour ce
  paiement : X CO") au-dessus du champ de montant, dans les deux blocs (Prisonniers/Corruption ET
  Dramatis Personae/Rétention). Corrigé au passage un vrai bug de fraîcheur trouvé en creusant :
  `IsPairCorruptionUnaffordable`/`IsPairRetentionUnaffordable` (dérivées de ce même seuil) n'étaient
  jamais notifiées quand la trésorerie changeait AILLEURS dans le wizard (achat/recrutement d'Objet
  rare, ou case Payer/Renvoyer d'un Johann/Veskit/Marianna/Nicodemus coché APRÈS la saisie du montant
  de corruption/rétention) - le calcul était toujours juste au moment de le relire, mais la Vue
  n'était jamais prévenue qu'il fallait le relire dans ce cas précis.
- **2026-09-01 (suite, révision "Où est l'Argent ?" - rétention hostile-gatée, sélection auto de
  matériel, duel en étape dédiée)** : trois retours utilisateur groupés sur le repli "Où est l'Argent ?"
  livré plus tôt le même jour. (1) `BuildPairRetentionOption` exclut désormais un couple actuellement
  hostile (`!Warrior.IsHostileThisBattle`) - hostile veut dire déjà corrompu cette bataille, donc "payer
  pour le garder" n'a plus de sens. (2) "Céder du matériel" n'est plus un simple rappel textuel : nouvel
  algorithme glouton (`SeizedEquipmentItems`, `EndOfGameDialogViewModel.Captives.cs`) qui parcourt
  l'inventaire de bande (nouveau paramètre `warbandInventory` au constructeur du dialog) trié par valeur
  croissante et accumule jusqu'au montant demandé sans dépasser inutilement, affiché en chips ; les
  objets sélectionnés sont réellement retirés de l'inventaire à l'application
  (`ApplyPairEquipmentSeizureIfNeededAsync`, `WarbandDetailViewModel.EndOfGame.cs`). (3) "Duel avec le
  meneur" déménagé dans sa propre étape de wizard (`StepKind.PairDuel`, nouveau fichier
  `EndOfGameDialogViewModel.PairDuel.cs`) pour éviter une carte Prisonniers/Dramatis Personae trop
  chargée, avec le même gabarit visuel de comparaison de profils que "Vendu aux Fosses" (une carte par
  participant - meneur/Marquand/Ulli - `StatRowView` + chips) plutôt que le simple duo case-à-cocher/
  sous-jet d'avant. `WonDuel`/`PairDuelRoll` (mécanique D66 inchangée) déplacés de fichier avec l'étape,
  toujours partagés entre les deux contextes déclencheurs (corruption/rétention) puisque mutuellement
  exclusifs pour une même bande.
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
