# Dramatis Personae — état d'implémentation

Suivi de ce qui reste à gérer sur le catalogue Dramatis Personae (`Core/Models/Library/
DramatisPersona.cs`, `Data/SeedData/DramatisPersonae.json`, 8 personnages) au-delà du catalogue et
du recrutement de base. Mis à jour à chaque avancée — dernière mise à jour : **2026-09-01** (frais
d'engagement en or + paiement alternatif de Johann).

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
| Frais d'engagement en or (HireCost à l'engagement + Upkeep récurrent) | ✅ | 2026-09-01, `FeeKind.Gold` uniquement (Johann/Veskit/Marianna) - voir Transverse pour la portée exacte |
| Paiement alternatif de Johann (Ombre Cramoisie au lieu d'or) | ✅ | 2026-09-01, `DramatisPersona.AlternativePaymentItemId` + picker Codex + retrait réel de l'inventaire de bande, généralisable à tout futur personnage Gold via le même champ |
| Exclusion du décompte de tête pour la vente de pierre magique | ⏳ | Voir note dans `Warrior.cs` (même limite que Franc-Tireur) |
| Mécaniques propres à un seul personnage (Marianna, Nicodemus, Johann, Ulli & Marquand) | ⏳ / 📝 | Détail ci-dessous, personnage par personnage |
| Doc `DramatisPersona.cs` (commentaire de classe) mentionne encore "not yet wired into recruitment" | ⏳ | Obsolète depuis le branchement du wizard — à corriger au prochain passage sur ce fichier |

## Transverse (tous personnages)

- **Frais d'engagement en or : câblé, mais scopé à `FeeKind.Gold` uniquement (2026-09-01).** À
  l'engagement (recherche "Personnage spécial" de Fin de Partie), `HireCost` est déduit de la
  trésorerie (bloque le step si ça dépasserait le budget, même bandeau que l'achat d'objets rares -
  `RareItemSearchEntry.EffectiveHireCostForTreasury`/`RareItemPurchaseTotalCost`). Nouvelle étape
  "Dramatis Personae" en Fin de Partie (`EndOfGameDialogViewModel.DramatisPersonae.cs`, entièrement
  absente si aucun personnage concerné) règle ensuite la solde récurrente à chaque partie suivante -
  Payer/Renvoyer comme les Francs-Tireurs, impayée = retrait complet du roster (`ApplyDramatisPersonaUpkeepAsync`).
  Concrètement : Johann (70 CO + 30 solde) et Veskit (80 CO + 35 solde) sont couverts ; Marianna
  (150 CO + 75 solde variable) aussi pour le HireCost/Upkeep fixe, mais sa propre mécanique
  "On n'échappe jamais à son passé..." (qui fait varier l'upkeep réel) reste hors périmètre (texte
  libre, voir sa ligne plus bas). **Toujours hors périmètre, décision explicite** : Bertha (`None`,
  rien à payer de toute façon), Nicodemus (`Wyrdstone` - paiement par bataille en pierre magique,
  mécanique bien plus élaborée), Ulli & Marquand (`Pair` - engagement à deux, enchère "Une Poignée
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
- **Vagabond : départ automatisé (2026-09-01), délai de re-recherche toujours manuel.** `IsWanderer=true`
  (Aenur, Bertha, Ulli & Marquand) déclenche désormais un retrait COMPLET du roster à chaque Fin de
  Partie (`ApplyWandererDeparturesAsync` - décision utilisateur via `AskUserQuestion` : suppression pure,
  pas de statut "Parti(e)" dédié, une future recherche recrée une fiche neuve sans historique conservé).
  Scope volontairement le roster figé à l'OUVERTURE du wizard : un personnage retrouvé PENDANT cette même
  Fin de Partie (étape Recherche) n'est jamais concerné, il reste au moins jusqu'à la bataille suivante.
  Ce qui reste manuel : la nuance "délai avant de pouvoir re-rechercher" (Aenur/Ulli & Marquand - "can't
  be sought again until the warband has fought at least one battle without them" - contre aucun délai
  pour Bertha, qui peut être re-cherchée dès la bataille suivante) n'est pas trackée ; rien n'empêche
  aujourd'hui de la re-rechercher trop tôt pour Aenur/Ulli & Marquand.
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
| **Aenur, l'Épée du Crépuscule** | 12 bandes humaines/Ordre | Départ Vagabond ✅. ⏳ Délai de re-recherche (1 bataille sans lui) non tracké, voir Transverse. |
| **Bertha Bestraufrung** | Sœurs de Sigmar | Aide conditionnelle ✅. Départ Vagabond ✅ (pas de délai de re-recherche pour elle, conforme au livre). Recrutement "gratuit en or" (`FeeKind.None`) déjà correct puisqu'aucun paiement n'est prélevé de toute façon. |
| **Comtesse Marianna Chevaux** | 12 bandes | Frais en or (HireCost/Upkeep) ✅. 📝 **"On n'échappe jamais à son passé..."** : jet 1D6 au dernier tour de partie / à la déroute (reste-part / reste-si-solde-payée / embuscade Zombies+Goules+Vampire pour D3 tours) — entièrement absent du wizard, aucun écran ne couvre "pendant" une partie sur table ; c'est aussi ce qui fait varier son upkeep réel dans le livre, non reflété par l'Upkeep fixe (75) utilisé pour la solde récurrente. 📝 Haine personnelle des Vampires envers elle (déjà une `SpecialRule` dédiée, "Hated by Vampires") — non modélisable via `HatredTargetWarbandArchetypeIds` (ciblage par bande, pas par Vampires-en-tant-qu'individus). |
| **Johann le Couteau** | 12 bandes | Frais en or + paiement alternatif (Ombre Cramoisie) ✅. 📝 Dague comptée comme 2 Épées en CaC — effet de combat, hors moteur de règles V1. |
| **Nicodemus, le Pèlerin Maudit** | 11 bandes | ⏳ **Paiement en éclat de pierre magique à chaque bataille (y compris la première), sinon il part définitivement.** `FeeKind.Wyrdstone` existe côté modèle mais rien ne consomme réellement un éclat ni ne retire Nicodemus du roster si la bande ne peut/veut pas payer — mécanique à part entière (délibérément hors périmètre de la passe Gold du 2026-09-01), plus impactant qu'une simple solde puisque c'est une contrainte par-bataille. |
| **Marquand Volker & Ulli Leitpold** | 12 bandes (paire) | Départ Vagabond ✅ (chacun individuellement - voir "recrutement en paire non imposé" ci-dessous, le départ n'est pas non plus synchronisé entre les deux). ⏳ Délai de re-recherche non tracké, voir Transverse. ⏳ Frais en or non prélevés (`FeeKind.Pair`, délibérément hors périmètre de la passe Gold du 2026-09-01). ⏳ **Recrutement en paire non imposé** : les deux fiches catalogue sont indépendantes, rien n'empêche d'en recruter un seul dans l'UI actuelle (le livre les impose comme un bloc). ⏳ **"Une Poignée d'Or" / "Où est l'Argent ?"** (enchère secrète adverse en début de partie pour retourner la paire, saisie ou combat en duel si la bande ne peut pas payer) — aucun écran de Lancement de Partie ne couvre ce mécanisme. 📝 "Inséparables" (rester à 4" l'un de l'autre, traîner le partenaire hors du champ) — positionnement sur table, hors périmètre de l'app de toute façon. |
| **Veskit, Bourreau Suprême** | Skavens (Clan Eshin) | Frais en or (HireCost/Upkeep) ✅. Rien d'autre en attente — pas Vagabond. |

## Historique

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
