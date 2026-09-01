# Dramatis Personae — état d'implémentation

Suivi de ce qui reste à gérer sur le catalogue Dramatis Personae (`Core/Models/Library/
DramatisPersona.cs`, `Data/SeedData/DramatisPersonae.json`, 8 personnages) au-delà du catalogue et
du recrutement de base. Mis à jour à chaque avancée — dernière mise à jour : **2026-09-01** (départ
automatique des Vagabonds).

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
| Frais d'engagement (paiement HireCost/Upkeep, quel que soit le `FeeKind`) | ⏳ | Aucun prélèvement de trésorerie à l'engagement, quel que soit le personnage |
| Solde récurrente (Franc-Tireur a son étape dédiée "Francs-Tireurs" en Fin de Partie, pas les Dramatis Personae) | ⏳ | Pas d'étape équivalente |
| Exclusion du décompte de tête pour la vente de pierre magique | ⏳ | Voir note dans `Warrior.cs` (même limite que Franc-Tireur) |
| Mécaniques propres à un seul personnage (Marianna, Nicodemus, Johann, Ulli & Marquand) | ⏳ / 📝 | Détail ci-dessous, personnage par personnage |
| Doc `DramatisPersona.cs` (commentaire de classe) mentionne encore "not yet wired into recruitment" | ⏳ | Obsolète depuis le branchement du wizard — à corriger au prochain passage sur ce fichier |

## Transverse (tous personnages)

- **Frais d'engagement jamais prélevés.** `RecruitDramatisPersonaAsync` ajoute le guerrier au roster
  sans toucher `Warband.Treasury`, quel que soit `FeeKind` (Gold/None/Wyrdstone/Pair). Concrètement :
  Johann (70 CO + 30 solde), Veskit (80 CO + 35 solde) et Marianna (150 CO + 75 solde) sont recrutés
  gratuitement aujourd'hui, comme s'ils étaient tous `FeeKind.None`. Décision explicite (voir
  `DramatisPersona.cs`) : flux de paiement volontairement pas encore tranché par l'utilisateur, à
  reprendre dans une passe dédiée plutôt qu'à deviner ici.
- **Pas de solde récurrente.** Le Franc-Tireur a sa propre étape "Francs-Tireurs" en Fin de Partie
  (paiement de la solde, `HiredSwordUpkeepPrepaid`) ; rien d'équivalent n'existe pour un Dramatis
  Persona avec `Upkeep` non nul (Johann/Veskit/Marianna).
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
| **Comtesse Marianna Chevaux** | 12 bandes | 📝 **"On n'échappe jamais à son passé..."** : jet 1D6 au dernier tour de partie / à la déroute (reste-part / reste-si-solde-payée / embuscade Zombies+Goules+Vampire pour D3 tours) — entièrement absent du wizard, aucun écran ne couvre "pendant" une partie sur table. 📝 Haine personnelle des Vampires envers elle ("une inimitié personnelle, pas une Haine de bande") — non modélisable via `HatredTargetWarbandArchetypeIds` (ciblage par bande, pas par Vampires-en-tant-qu'individus), resterait de toute façon du texte libre. |
| **Johann le Couteau** | 12 bandes | 📝 Dague comptée comme 2 Épées en CaC — effet de combat, hors moteur de règles V1. ⏳ Paiement alternatif "une dose d'Ombre Écarlate au lieu d'or" — `FeeKind` ne modélise qu'une seule devise par personnage, pas d'alternative ; de toute façon couvert par "aucun frais prélevé" ci-dessus. |
| **Nicodemus, le Pèlerin Maudit** | 11 bandes | ⏳ **Paiement en éclat de pierre magique à chaque bataille (y compris la première), sinon il part définitivement.** `FeeKind.Wyrdstone` existe côté modèle mais rien ne consomme réellement un éclat ni ne retire Nicodemus du roster si la bande ne peut/veut pas payer — plus impactant que le "frais d'engagement" générique des autres puisque c'est une contrainte par-bataille, pas une simple solde. |
| **Marquand Volker & Ulli Leitpold** | 12 bandes (paire) | Départ Vagabond ✅ (chacun individuellement - voir "recrutement en paire non imposé" ci-dessous, le départ n'est pas non plus synchronisé entre les deux). ⏳ Délai de re-recherche non tracké, voir Transverse. ⏳ **Recrutement en paire non imposé** : les deux fiches catalogue sont indépendantes, rien n'empêche d'en recruter un seul dans l'UI actuelle (le livre les impose comme un bloc). ⏳ **"Une Poignée d'Or" / "Où est l'Argent ?"** (enchère secrète adverse en début de partie pour retourner la paire, saisie ou combat en duel si la bande ne peut pas payer) — aucun écran de Lancement de Partie ne couvre ce mécanisme. 📝 "Inséparables" (rester à 4" l'un de l'autre, traîner le partenaire hors du champ) — positionnement sur table, hors périmètre de l'app de toute façon. |
| **Veskit, Bourreau Suprême** | Skavens (Clan Eshin) | Rien au-delà du frais d'engagement générique (Gold, non prélevé) — pas Wanderer. |

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
