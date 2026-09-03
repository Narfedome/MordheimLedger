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
| **Marquand Volker & Ulli Leitpold** | 13 bandes (paire, toutes sauf Sœurs de Sigmar/Répurgateurs) | Départ Vagabond ✅ (chacun individuellement - le départ n'est pas synchronisé entre les deux au niveau du code, mais ils ont toujours été recrutés/quittent ensemble en pratique puisqu'ils arrivent toujours ensemble). Délai de re-recherche ✅ (chacun a son propre cooldown, non partagé - sans conséquence tant qu'ils partent toujours ensemble). **Recrutement en paire imposé + frais partagé (30 CO) ✅ (2026-09-01)** : le picker "Personnage spécial" n'affiche que Marquand (`DramatisPersona.IsHiddenFromSearchPicker` masque Ulli), le recruter recrute automatiquement Ulli aussi (`PairedWithDramatisPersonaId`) pour un seul HireCost de 30 CO (jamais 60) - voir `WarbandDetailViewModel.EndOfGame.ApplyRareItemSearchAsync`. **"Une Poignée d'Or" (A Fistful of Crowns) ✅ (2026-09-01)** : extraite du texte libre en une vraie `SpecialRule` (chip tapotable sur la carte, comme "On n'échappe jamais à son passé..." de Marianna) - texte générique, pas de montant en dur (les 30 CO du comparatif viennent du catalogue, affichés en direct à côté du bouton). Résolu en trois blocs, l'issue n'étant connue qu'après la bataille jouée sur table (pas un mécanisme de Lancement de Partie) : (1) côté bande propriétaire, bouton "Rendre hostile" sur la carte du guerrier (`WarriorRow.CanToggleHostile`/`WarbandDetailViewModel.ToggleHostile`) exclut sa contribution de la Valeur pour cette bataille (`Warrior.IsHostileThisBattle`) sans le supprimer du roster - à la Fin de Partie, `ApplyWandererDeparturesAsync` produit une phrase d'historique différenciée puis le retire comme tout Vagabond, uniquement si la paire a effectivement changé de camp. (1b, 2026-09-01, retour utilisateur - complète le point ci-dessus) si la bande propriétaire a plutôt GARDÉ le contrôle après une tentative adverse (payé sa propre contre-offre, voir l'exemple Steiner/Albrecht du livre - "seul le camp qui obtient OU GARDE le contrôle paie"), une carte "C'est l'heure de payer !" à l'étape Dramatis Personae (`ShowPairRetentionOption`, visible seulement si cette bande possède déjà la paire) prend un montant libre (0/vide = aucune tentative cette partie) et le déduit de SA trésorerie en tout dernier ("à la fin de tous les décomptes", retour utilisateur). (2) côté n'importe quelle autre bande (même sur un autre appareil, aucun lien de données entre bandes) : case "Corruption de Marquand Volker & Ulli Leitpold" à l'étape Prisonniers **renommée "Prisonniers & Corruption"** (`EndOfGameDialogViewModel.ShowPairCorruptionOption`, visible seulement si cette bande ne possède pas déjà la paire) + montant, déduit de SA propre trésorerie. **"Où est l'Argent ?" ✅ (2026-09-01, révisée le même jour)** : repli PARTAGÉ entre (1b) et (2) - une bande ne peut jamais déclencher les deux à la fois, donc aucun risque à réutiliser le même sous-système (`WheresMoneyChoiceLabels`, déclaré dans `EndOfGameDialogViewModel.Captives.cs`) - si le montant saisi (corruption OU rétention) dépasse le solde prévisionnel de la bande à cette étape (`IsPairCorruptionUnaffordable`/`IsPairRetentionUnaffordable`, même principe que `RareItemPurchaseRemainingTreasury`). (1b) est en plus désormais bloqué si la paire est hostile CETTE bataille (`!Warrior.IsHostileThisBattle`, retour utilisateur - hostile veut dire corrompu donc de toute façon parti·e sans contre-offre possible). **Déterminé automatiquement depuis le 2026-09-02, plus de choix manuel du joueur** (retour utilisateur -
"on détermine automatiquement si on a le duel ou non, pas de choix dans le picker") : `WantsEquipmentSeizure`/`WantsDuel` (`Captives.cs`) comparent la valeur totale du stash de bande à la somme
due - le stash suffit → **"Céder du matériel"** ; sinon → **"Duel avec le meneur"**, seule alternative
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
  `WantsEquipmentSeizure`/`WantsDuel` comparent la valeur totale du stash de bande
  (`WarbandInventoryTotalValue`) au montant dû - le stash suffit → céder du matériel (jamais de duel
  dans ce cas) ; sinon → duel avec le meneur, seule alternative qui reste. (2) `SeizedEquipmentItems`
  trie désormais le stash en ordre DÉCROISSANT de valeur (`OrderByDescending`, remplace l'ancien tri
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
