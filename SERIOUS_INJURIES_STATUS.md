# Blessures Graves — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Blessure (voir CLAUDE.md § Règles dans Core, plan de
séquencement "Mécaniser les Blessures Graves — Palier 1"). Mis à jour à chaque avancée — dernière
mise à jour : **2026-08-27** (Vendu aux Fosses, voir Journal).

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

Reprise "règle par règle" (comme pour `EXPLORATION_CHART_STATUS.md`) plutôt qu'un gros palier d'un
coup - trop de résultats pour avancer autrement sans perdre le fil.

## Vue d'ensemble

| Palier | Mécanisme | Statut |
|---|---|---|
| 1 — effet direct sur stat/statut/équipement/XP déjà modélisé | Pénalité de caractéristique permanente, perte d'équipement, +1 XP, Indisponible (1 ou D3 parties) ; Œil crevé (31) étendu (2026-08-26) au cas du second œil, seul résultat dont l'effet dépend de l'état déjà porté par le guerrier (`WarriorStatus.Retired`, nouveau) | **10/10 ✅** |
| 2 — règle spéciale permanente attachée à l'Injury | Frénésie/Stupidité (Folie, 24) + Bras amputé (23 grave) + Ne peut plus courir (25 grave) + Endurci (62-63) + Provoque la Peur (Horribles balafres, 64) via `Injury.SpecialRules` - pas de nouvelle table `WarriorSpecialRule`, voir Journal | **5/5 ✅** |
| 3 — branches complexes / jet récurrent | Capturé (61) - mécanisé (2026-08-27), 5 issues nommées choisies par le joueur (pas un jet, voir `Core.Rules.CapturedOutcomeTable`) ; Vendu aux Fosses (65) - mécanisé (2026-08-27), combat de gladiateur contre un Franc-Tireur (nouveau catalogue `HiredSword`), choix victoire/défaite par le joueur (pas de moteur de combat) | **2/2 ✅** |
| Récurrent (jet avant CHAQUE partie, pas seulement à la Fin de Partie où il est obtenu) | Vieille blessure (32) - mécanisé (2026-08-26) via le nouvel écran "Lancer la partie", voir Journal | **1/1 ✅** |
| Déjà mécanisé avant ce chantier | Mort (11-15), Blessures multiples (16/21), Rancune (56) | **✅** |
| No-op (déjà correct) | Récupération totale (41-55, Héros), Récupération totale (3-6, Homme de main) | **2/2 ✅** |

## Détail — Héros (D66)

Colonne Testé : ✅ = vérifié en jeu par l'utilisateur, — = codé mais pas encore rejoué en vrai,
❌ = pas encore codé.

| Dés | Résultat | Palier | Statut | Testé | Note |
|---|---|---|---|---|---|
| 11-15 | Mort | — | ✅ | — | `WarriorStatus.Dead`, fait avant ce chantier |
| 16, 21 | Blessures multiples | — | ✅ | — | Relance 1D6 fois sur cette même table ; chaque sous-jet applique aussi son propre effet Palier 1 s'il y en a un |
| 22 | Blessure à la jambe | 1 | ✅ | ✅ | Mouvement -1 permanent |
| 23 (sous-jet 2-6) | Blessure au bras : légère | 1 | ✅ | ✅ | Statut Indisponible, 1 partie ratée ; chip catalogue dédiée ("Blessure au bras : légère"), temporaire - supprimée automatiquement dès que le guerrier redevient Actif |
| 23 (sous-jet 1) | Blessure au bras : amputé | 2 | ✅ | — | Chip catalogue dédiée portant une vraie `SpecialRule` "Bras amputé" (nouvelle, purement informative - pas de blocage actif à l'équipement) - même traitement que Folie, y compris la non-fusion dans les Règles spéciales (2026-08-26) |
| 24 (sous-jet 1-3) | Folie : Stupidité | 2 | ✅ | ✅ | Chip catalogue dédiée portant une vraie `SpecialRule` "Stupidité" (trouvée/réutilisée depuis le catalogue commun) - PAS fusionnée dans les Règles spéciales du guerrier (revenu dessus le 2026-08-26), surfacée uniquement en puce imbriquée tapable à l'intérieur du dialog récap de la Blessure |
| 24 (sous-jet 4-6) | Folie : Frénésie | 2 | ✅ | ✅ | Idem avec la `SpecialRule` "Frénésie" (déjà dans le catalogue commun, réutilisée telle quelle) |
| 25 (sous-jet 2-6) | Jambe écrasée : légère | 1 | ✅ | ✅ | Statut Indisponible, 1 partie ratée ; chip catalogue dédiée, temporaire - supprimée dès le retour Actif |
| 25 (sous-jet 1) | Jambe écrasée : grave | 2 | ✅ | — | Chip catalogue dédiée portant une vraie `SpecialRule` "Ne peut plus courir" (nouvelle, purement informative - même traitement que Bras amputé/Folie) - PAS fusionnée dans les Règles spéciales du guerrier, surfacée en puce imbriquée tapable dans le dialog récap de la Blessure |
| 26 | Blessure au torse | 1 | ✅ | — | Endurance -1 permanent |
| 31 | Œil crevé | 1 | ✅ | — | Tir -1 permanent ; **second œil crevé mécanisé** (2026-08-26) : si le guerrier porte déjà cette Injury, un nouveau 31 déclenche `WarriorStatus.Retired` (nouveau statut, groupe "Retraités" dédié) au lieu d'un second -1 Tir |
| 32 | Vieille blessure | Récurrent | ✅ | — | Jet 1D6 avant CHAQUE partie future - nouvel écran "Lancer la partie" (`StartGameDialog`), échec journalisé dans l'Historique, pas de statut ni de blocage persisté (le joueur ne fielde simplement pas ce guerrier sur table cette fois) |
| 33 | Traumatisme nerveux | 1 | ✅ | — | Initiative -1 permanent |
| 34 | Blessure à la main | 1 | ✅ | — | Capacité de Combat -1 permanent |
| 35 | Blessure profonde | 1 | ✅ | — | Malade, D3 parties (`Warrior.SickGamesRemaining`) - cumulatif si plusieurs occurrences dans la même résolution "Blessures multiples" (corrigé 2026-08-26) ; le 1D3 est désormais un jet visible/saisi par le joueur dans le wizard (plus tiré à l'aveugle), et le nombre de parties restantes s'affiche sur la puce Indisponible |
| 36 | Dépouillé | 1 | ✅ | — | Perd tout l'équipement porté, sans remboursement |
| 41-55 | Récupération totale | — | ✅ | — | No-op, déjà correct |
| 56 | Rancune | — | ✅ | — | Cible D6 + `WarriorHatred`, fait avant ce chantier |
| 61 | Capturé | 3 | ✅ | — | Portée revue à la baisse (2026-08-27) : case à cocher "Racheté contre rançon" + montant saisi (déduit de la trésorerie), guerrier revenu avec équipement si coché ; sinon considéré Mort, équipement perdu - les 4 autres issues du livre (Échange/Vendu/Tué/Sacrifié) ne sont plus distinguées, toutes racontent une décision de la bande adverse non modélisée |
| 62-63 | Endurci | 2 | ✅ | — | Chip catalogue portant une nouvelle `SpecialRule` "Endurci" (permanente, distincte du "Immunisé à la Peur" temporaire de la Bière de Bugman) |
| 64 | Horribles balafres | 2 | ✅ | — | Chip catalogue portant la `SpecialRule` "Provoque la Peur" déjà enrichie dans le catalogue commun, réutilisée telle quelle |
| 65 | Vendu aux arènes | 3 | ✅ | — | Combat de gladiateur contre un Franc-Tireur (nouveau catalogue Bibliothèque `HiredSword`, adversaire éphémère jamais recruté) : deux profils `StatRowView` côte à côte, case Victoire (+50 CO, +2 PX, équipement intact) sinon Défaite (un sous-jet D66 sur la même table, "ne garder que 11-35" indicatif non vérifié - survit = équipement perdu sans condition, mort = traité comme toute autre Mort) |
| 66 | Survie miraculeuse | 1 | ✅ | — | +1 Expérience |

## Détail — Hommes de main (D6)

| Dé | Résultat | Statut | Testé | Note |
|---|---|---|---|---|
| 1-2 | Perdu | ✅ | — | `HeadCount -= 1` / suppression du groupe, fait avant ce chantier |
| 3-6 | Récupération totale | ✅ | — | No-op, déjà correct |

## Hors périmètre pour l'instant

- Aucun résultat du Palier 3 restant à faire - les 36 résultats du D66 Héros sont désormais tous
  mécanisés (Palier 1/2/3 + récurrent) ou correctement no-op.
- Le catalogue `HiredSword` (Franc-Tireur) reste volontairement limité au profil statique + à cette
  seule comparaison de Vendu aux Fosses - aucun vrai flux d'engagement (coût/entretien/Rating dans la
  bande) n'existe encore, chantier séparé déjà noté pour "Une Faveur Rendue" (voir
  `EXPLORATION_CHART_STATUS.md`).

## Journal

- **2026-08-25 (Palier 1 complet)** — `Core.Rules.SeriousInjuryEffectTable` (nouveau, additif à
  `SeriousInjuryTable`) ; `Warrior.SickGamesRemaining` (compteur multi-parties - `Warrior.cs`/
  `WarriorEntity.cs`/`EntityMapping.cs`, `ApplySicknessLifecycleAsync` décrémente au lieu d'effacer
  inconditionnellement) ; câblage dans `WarriorOutcomeRow`/`EndOfGameDialog.xaml`/
  `WarbandDetailViewModel.EndOfGame.cs` (branche 23/25 : sous-jet 1D6 supplémentaire, même patron que
  le sous-jet de Rancune). **Bug corrigé au passage** : la chip de blessure (`WarbandDetailPage`,
  `ChipListView` sur `WarriorRow.Injuries`) affichait la phrase descriptive complète au lieu du nom
  court du catalogue Injury - `GetOrCreateInjuryAsync` comparait par égalité de texte
  (`row.InjuryResultText`, la phrase résolue) contre `Injury.Name` (déjà le nom court dans le
  catalogue seedé), qui ne matchait donc jamais et créait un doublon avec la phrase entière comme
  Name. Corrigé pour matcher par jet contre `Injury.RollRange` (`RollRangeMatches`, parseur tolérant
  "22"/"16, 21"/"11-15"). 281/281 tests passent, build clean. Passe suivante demandée par
  l'utilisateur : reprendre "règle par règle" (comme l'Exploration) pour Palier 2/3 plutôt qu'un gros
  palier d'un coup - ce fichier remplace le suivi "3 paliers" du plan initial.
- **2026-08-25 (Blessure au bras/Jambe écrasée - catalogue à 2 branches + chip temporaire)** — Retour
  utilisateur sur 22 (validé en jeu ✅) puis sur 23 : le catalogue Injury n'avait qu'UNE entrée par roll
  "23"/"25" (texte décrivant les deux branches à la fois), donc la chip affichée sur la fiche de bande
  ne disait jamais laquelle des deux s'était réellement produite - repéré comme un vrai manque
  ("dommage de ne pas avoir le detail... sur quel type de blessure on a"). Corrigé par le même système
  d'embranchement qu'ailleurs dans l'app (Exploration) : `Injury.BranchRange` (nouveau, même convention
  que RollRange) sépare chaque roll branché en 2 entrées catalogue ("2-6"/"1"), nommées au format
  "Blessure au bras : légère"/"Blessure au bras : amputé" (même convention que "Haine : {cible}").
  `Core.Rules.SeriousInjuryTable.TryGetBranchTextKey` résout le texte propre à la branche
  (`InjurySerious23Light`/`Severe`, `25Light`/`Severe`) plutôt que le texte combiné des deux branches
  (gardé comme intro affichée avant le sous-jet). `WarbandDetailViewModel.EndOfGame.GetOrCreateInjuryAsync`
  matche désormais par (RollRange, BranchRange) ; `BackfillBranchedInjuriesAsync` (nouveau, même idiome
  que `BackfillWarbandArchetypeRaceAsync`) insère les 4 nouvelles entrées sur une base déjà seedée -
  `SeedInjuriesAsync` seul ne suffit pas (ne tourne que sur base vide). **Chip temporaire** (nouveau
  `WarriorInjury.IsTemporary`/`WarriorInjuryEntity.IsTemporary`) : demande explicite de l'utilisateur -
  la chip "légère" (Indisponible 1 partie) doit disparaître une fois la partie ratée jouée, contrairement
  à la chip "amputé"/permanente. Posée `true` uniquement pour les résultats Palier 1 qui rendent
  `WarriorStatus.Sick` (branche légère de 23/25, Blessure profonde 35 par généralisation - même
  principe), supprimée par le nouveau `IWarbandService.RemoveTemporaryInjuriesAsync` au moment précis
  où `ApplySicknessLifecycleAsync` repasse le guerrier Actif (`SickGamesRemaining` à 0). **Renommage
  terminologique** (même retour utilisateur, confirmé par `AskUserQuestion`) : "Malade"/"Sick" →
  **"Indisponible"/"Unavailable"** partout (`WarriorStatusSick`, `HistorySicknessSentence`) - terme
  couvrant aussi bien la maladie du Puits que ces blessures légères, `WarriorStatus.Sick` (l'identifiant
  C#) inchangé, seul le texte affiché change. Build clean, tests en cours de vérification.
- **2026-08-25 (Folie - règle spéciale attachée à l'Injury, pas de nouvelle table `WarriorSpecialRule`)**
  — Même embranchement que Blessure au bras/Jambe écrasée (sous-jet 1D6, 1-3 "Folie : Stupidité"/4-6
  "Folie : Frénésie", 2 nouvelles entrées catalogue avec `BranchRange`) mais avec une idée plus simple
  suggérée par l'utilisateur : "il me semble qu'on a les special rules pour ces trucs là, on peut
  peut-être les rajouter aux injuries (comme les règles spéciales d'un item)" - plutôt que le
  `WarriorSpecialRule` (mirror `WarriorSkill`) envisagé initialement pour tout le Palier 2, nouveau
  `Injury.SpecialRules` (même idiome que `EquipmentItem.SpecialRules` : `InjurySpecialRuleEntity`,
  `LibraryService.LoadInjurySpecialRulesAsync`, seed via `FindOrCreateSpecialRuleAsync` réutilisé tel
  quel) - Stupidité/Frénésie existaient déjà comme `SpecialRule` (Frénésie dans le catalogue commun,
  Stupidité en stub inline sur 4 bandes, texte repris à l'identique). `WarbandDetailViewModel.ToRow`
  fusionne désormais `warrior.Injuries.SelectMany(i => i.Item.SpecialRules)` dans les mêmes règles
  spéciales que bande/archétype/équipement : la règle apparaît comme une puce **tapable** (même
  popup détail que n'importe quelle règle spéciale) sur la fiche guerrier dès que la Blessure Grave est
  enregistrée - "rappel de règle" gratuit, aucun mécanisme de jointure par guerrier à construire.
  **Bug annexe corrigé au passage** : `WarbandService.GetWarriorsAsync` résolvait chaque Injury portée
  via un `FindAsync`+`ToModel` minimal par ligne (comme Equipment/Skill/Mutation avant leur correctif
  antérieur) - laissait donc `SpecialRules` vide pour toute Injury déjà attachée à un guerrier existant.
  Corrigé pour réutiliser `ILibraryService.GetInjuriesAsync` (déjà pleinement résolu), même motif que le
  correctif Equipment/Skill/Mutation déjà en place. 299/299 tests passent, build clean (compilation
  confirmée - copie finale bloquée par l'instance de l'appli/Visual Studio ouverts en parallèle).
  **Reporté à une prochaine passe** (suggestion de l'utilisateur, pas encore traité) : rendre les chips
  Haine de la fiche guerrier tapables (portée 6 seulement, celle qui référence un vrai
  `WarbandArchetype` - actuellement `HatredChips` n'a aucune `Command`, contrairement à la même chip
  dans le wizard qui l'est déjà).
- **2026-08-25 (chip plutôt que texte DANS le wizard + Bras amputé rejoint le mécanisme)** — Deux
  retours utilisateur sur la même session. (1) La confirmation de branche affichée dans l'assistant Fin
  de Partie ("Devient Frénétique de façon permanente.") restait un simple `Label` en gras même quand la
  branche accorde une vraie `SpecialRule` - "ici il faut mettre le chip plutôt que du texte" (capture
  d'écran à l'appui). Nouveau `Core.Rules`-adjacent `InjuryCatalogLookup` (extrait de la logique de
  correspondance par jet déjà dans `GetOrCreateInjuryAsync`, désormais partagée) permet à
  `WarriorOutcomeRow.InjuryBranchSpecialRules` de prévisualiser EN DIRECT, avant même l'enregistrement,
  quelle(s) `SpecialRule`(s) la branche résolue va attacher - le catalogue Injury (déjà résolu avec ses
  SpecialRules) est chargé une fois à l'ouverture du wizard et transmis à chaque `WarriorOutcomeRow`.
  `EndOfGameDialog.xaml` affiche désormais un `ChipView` tapable (nouvelle commande
  `ShowInjuryBranchSpecialRuleDetail`, même popup que la fiche guerrier) à la place du texte dès qu'il y
  a une règle à montrer - 23/25 (aucune `SpecialRule`) gardent le texte brut inchangé. (2) "On a la même
  chose à faire pour la blessure au bras" - la branche grave de Blessure au bras (23, sous-jet 1,
  "amputé") gagne une nouvelle `SpecialRule` **"Armes à une main uniquement"** (purement informative,
  toujours pas de blocage actif à l'équipement - cohérent avec la doctrine "pas de moteur de règles").
  **Nouveau backfill `BackfillInjurySpecialRulesAsync`** : contrairement à Folie (ligne catalogue
  toute neuve, ses règles s'attachaient déjà à l'insertion via `BackfillBranchedInjuriesAsync`),
  "Blessure au bras : amputé" EXISTAIT déjà en base (créée par le tout premier backfill de la session,
  avant que Folie n'introduise `Injury.SpecialRules`) - `BackfillBranchedInjuriesAsync` ne revisite
  jamais une ligne déjà là, donc la nouvelle règle ne se serait jamais attachée sur une base déjà
  migrée sans ce backfill dédié (correspond par (Catégorie, RollRange, BranchRange), n'agit que si la
  ligne trouvée a zéro `InjurySpecialRuleEntity` - jamais de doublon si la liste de règles du seed
  change encore après coup). 299/299 tests passent, build clean (Core + tête MAUI, tests compilés et
  exécutés avec succès).
- **2026-08-26 (texte purement descriptif - Peur/Frénésie/Stupidité/Haine)** — L'utilisateur a fourni
  le texte officiel complet des règles de Psychologie (Peur, Frénésie, Stupidité, Haine) en précisant
  explicitement "c'est purement textuel" - aucune nouvelle mécanique demandée, seulement enrichir les
  descriptions déjà affichées dans les popups de règle. `SpecialRules.json` : descriptions de
  "Causes Fear"/"Frenzy" remplacées par le texte complet du livre ; nouvelle entrée "Hatred"/"Haine"
  (catalogue seulement, pas encore branchée sur aucune UI - prépare le terrain si l'utilisateur demande
  un jour de rendre les chips Haine tapables, cf. Journal 2026-08-25). `Injuries.json` : la
  `SpecialRule` "Stupidity" nichée sous Folie:Stupidité reçoit le même texte complet. Ces 3 fichiers
  seed ne suffisant pas seuls sur une base déjà peuplée (`SeedTranslationAsync`/
  `FindOrCreateSpecialRuleAsync` ne revisitent jamais une ligne existante - même limite que les
  backfills précédents), nouveau `BackfillSpecialRuleDescriptionsAsync` (enregistré juste après
  `BackfillInjurySpecialRulesAsync`) met à jour en place la traduction de description des 3 règles déjà
  seedées, uniquement si `ContentSource.Official` (jamais une ligne éditée par le joueur). Réutilise le
  helper partagé `TranslationResolver.SetAsync` (déjà utilisé par `LibraryService.SetTranslationAsync`)
  plutôt qu'un upsert dupliqué à la main. **Bug trouvé par les tests** (pas en relecture manuelle) :
  la première version indexait les traductions anglaises par `.ToDictionary(t => t.Value, t => t.Key)`
  pour retrouver la clé de traduction d'un nom de règle par son texte anglais - `Value` n'est pas
  unique (plusieurs objets/règles sans rapport partagent un texte de description identique, ex. deux
  armes utilisant la même description de masse) donc `ToDictionary` levait `ArgumentException` dès
  l'`InitializeAsync` suivant, cassant 29 tests qui touchent la base. Corrigé en indexant par `Key`
  (unique) et en comparant la valeur ensuite. 299/299 tests passent, build Core clean.
- **2026-08-26 (retour en arrière : plus de fusion dans les Règles spéciales du guerrier)** — "On
  ajoute pas la regle special stupidité/frenzy/amputed arm au guerrier directement. L'idée c'est
  d'avoir la chip de blessure 'Folie : Stupidité'. Quand on clique dessus, on a la chip de la regle
  Stupidité" - la fusion `warrior.Injuries.SelectMany(i => i.Item.SpecialRules)` dans
  `WarbandDetailViewModel.ToRow` (ajoutée lors de la passe Folie, 2026-08-25) créait une chip en double :
  une fois via la puce Blessure elle-même ("Folie : Stupidité" dans Blessures), une fois via la règle
  fusionnée dans Règles spéciales ("Stupidité"). Retirée - `mergedRules` ne concatène plus que
  `_bandWideSpecialRules`/`archetypeRules`/`equipmentRules` (équipement inchangé, seule la fusion Injury
  est revenue en arrière). À la place, `InjuryDetailDialog`/`InjuryDetailDialogViewModel` gagnent le
  même bloc `ChipListView` "Règles spéciales" qu'`EquipmentItemDetailDialog` (nouvelle commande
  `ShowSpecialRuleDetail`, `IDetailDialogService` injecté dans le ViewModel - `DetailDialogService.
  ShowInjuryDetailDialogAsync` passe désormais `this`) : la puce Blessure ("Folie : Stupidité") reste le
  seul point d'entrée sur la fiche guerrier, et c'est en l'ouvrant qu'on trouve la puce imbriquée
  "Stupidité" (tapable, même popup détail que partout ailleurs). Compilation confirmée (0 erreur C#) -
  copie finale de l'exécutable bloquée par l'instance de l'appli ouverte en parallèle (PID 33316), même
  limite connue que les fois précédentes ; aucun test xUnit concerné (le changement touche uniquement
  des ViewModels de la tête MAUI, hors périmètre de `MordheimLedgerApp.Tests`).
- **2026-08-26 (Jambe écrasée grave rejoint le mécanisme - même principe que Bras amputé)** — "On passe
  sur la jambe écrasé. On a le même principe que pour le bras blessé : soit une partie sautée soit une
  blessure avec une règle spéciale." Le sous-jet 1D6 de 25 existait déjà (branché depuis la passe
  22/23/25/26/..., 2026-08-25) - seule "Jambe écrasée : grave" (branche 1, "ne peut plus courir mais
  peut charger") n'avait encore aucune `SpecialRule` attachée (listée Palier 3 jusqu'ici). Comme pour
  Bras amputé (23, branche 1), nouvelle règle **"Ne peut plus courir"/"Cannot Run"** (purement
  informative, aucun blocage actif des déplacements) ajoutée au `specialRules` de l'entrée existante dans
  `Injuries.json` - **aucun nouveau code**, le mécanisme générique posé pour Bras amputé/Folie couvre
  déjà ce cas tel quel : `BackfillInjurySpecialRulesAsync` (matche par Catégorie/RollRange/BranchRange,
  n'attache que si zéro règle déjà présente) attache la règle sur une base déjà migrée, et
  `InjuryDetailDialog` affiche automatiquement la nouvelle puce imbriquée tapable dès que
  `Item.SpecialRules` contient quelque chose - conforme au "petit ajustement" du dessus (pas de fusion
  dans les Règles spéciales du guerrier). **Bug de doc annexe corrigé au passage** : le nom de la
  `SpecialRule` de Bras amputé avait été renommé "One-Handed Weapons Only" → "Amputated arm"/"Bras
  amputé" à un moment non documenté de la session précédente (repéré en relisant `Injuries.json` pour ce
  travail) - `DataServiceTests.cs` référençait encore l'ancien nom, jamais mis à jour ; corrigé, plus
  Jambe écrasée : grave sort du Palier 3 pour rejoindre le Palier 2 (reste seulement Endurci/Horribles
  balafres). 299/299 tests passent, build Core clean.
- **2026-08-26 (second Œil crevé - premier résultat dont l'effet dépend de l'état déjà porté par le
  guerrier)** — "Pour le 31 œil crevé, il faut qu'on gère mécaniquement le cas du double œil crevé."
  Contrairement à tout ce qui précède dans ce fichier, l'effet de ce résultat dépend de ce que le
  guerrier porte DÉJÀ (une Injury "Œil crevé" antérieure), pas seulement du jet - premier cas de ce genre
  (voir `SeriousInjuryEffectTable`, jusqu'ici une fonction pure du jet seul). Nouveau
  `SeriousInjuryEffectKind.ForcedRetirement` + surcharge `TryGetOutcome(int roll, bool
  alreadyBlindedInOneEye, out outcome)` - l'ancienne surcharge à 2 arguments délègue à la nouvelle avec
  `false`, donc tous les appelants existants (RulesTests.cs, la table Homme de main qui n'atteint jamais
  31) restent inchangés. `WarbandDetailViewModel.EndOfGame.ApplyWarriorOutcomesAsync` calcule
  `alreadyBlindedInOneEye` par guerrier (`warrior.Injuries.Any(...)` via `InjuryCatalogLookup.
  RollRangeMatches`) et la met à jour après CHAQUE jet à 31 (jet principal ET chaque sous-jet
  "Blessures multiples") - couvre même le cas rare d'un double 31 dans la même Fin de Partie.
  **Nouveau `WarriorStatus.Retired`** (3) : mécaniquement identique à Mort partout où le roster exclut
  les guerriers inactifs - nouveau groupe "Retraités" dans `WarbandDetailPage.xaml`/
  `WarbandDetailViewModel.cs` (mirror exact du groupe Morts : `RetiredWarriors`/`RetiredExpanded`/
  `ToggleRetiredCommand`), `WarriorRow.IsRetired` + nouveau `WarriorRow.IsEditable` (`!IsDead &&
  !IsRetired`, remplace le binding direct sur `IsDead` du bouton Éditer de la carte guerrier) et exclusion
  des créneaux de guerrier existant dans `WarbandEditDialogViewModel` (même filtre que
  `Status != WarriorStatus.Dead`). Contrairement à Mort, jamais sélectionnable manuellement dans le
  Picker de statut du wizard (`WarriorOutcomeRow._statusByLabel` ne liste toujours que Active/Dead) -
  uniquement atteint via `ApplySeriousInjuryEffectAsync`'s nouveau cas `ForcedRetirement`, après le bloc
  `row.Status != warrior.Status` (même position que MissNextGame/MissGamesRollD3, qui court-circuitent
  déjà le Picker de la même façon). Nouvelle phrase d'Historique dédiée (`HistoryForcedRetirementSentence`)
  plutôt que de réutiliser `HistoryDeathSentence` - le guerrier n'est pas mort. 10 nouveaux tests
  `RulesTests.cs` (dont un vérifiant que le flag n'affecte aucun autre jet que 31). 309/309 tests passent,
  build Core + tête MAUI clean (compilation confirmée, copie finale de l'exécutable réussie cette fois -
  pas d'instance de l'appli verrouillant le DLL).
- **2026-08-26 (Vieille blessure - nouvel écran "Lancer la partie")** — Dernier résultat mécanisable du
  Palier 3 : contrairement à tout le reste de la table, son jet se rejoue "avant CHAQUE partie future",
  pas une seule fois à la Fin de Partie où il est obtenu - aucun moment "avant bataille" n'existait dans
  l'appli jusqu'ici (seulement Fin de Partie), d'où son classement Palier 3 initial. L'utilisateur a
  suggéré de le résoudre via un bouton "Lancer la partie" qui remplacerait "Fin de partie" sur
  `WarbandDetailPage` ("Start a game" -> "End of game", un seul bouton visible à la fois), doublé d'un
  "wizard informatif" à un seul écran plutôt qu'un vrai wizard multi-étapes comme `EndOfGameDialog` -
  confirmé via un mock (`mcp__visualize`) avant tout code. Nouveau `Warband.GameInProgress` (bool,
  `WarbandEntity`/`EntityMapping` mirroré) bascule quel bouton s'affiche (`WarbandDetailViewModel.
  HasGameInProgress`) - **aucun verrouillage réel** : rien n'empêche d'éditer le roster/l'inventaire
  pendant qu'une partie est "en cours", décision explicite de l'utilisateur, ce flag ne fait que piloter
  l'affichage du bouton. `EndOfGame` le repasse à `false` en fin de traitement. Nouveau
  `Features/Warbands/StartGame/` (`StartGameDialog`/`StartGameDialogViewModel`, `OldWoundRollEntry`,
  `UnavailableWarriorRow`) : 3 sections, toutes masquées si vides (`HasNothingToShow` si les 3 le sont) -
  guerriers indisponibles (Malade/Retraité/Mort, simple rappel non-actionnable), jets de Vieille blessure
  (1D6 par guerrier Actif portant l'Injury roll "32", même idiome de saisie manuelle + bouton dé optionnel
  que `WarriorOutcomeRow.ManualRoll`/`EndOfGameDialogViewModel.Injury.AutoRoll`, échec sur 1), et
  `Warband.NextGameNote` en simple rappel textuel. Seuls les échecs sont journalisés dans l'Historique
  (nouveau `HistoryOldWoundFailSentence`) - un guerrier qui passe le test n'a rien de notable à
  consigner. **Nouveau `WarriorStatusRetired`** (resx) : nécessaire pour afficher la raison d'un guerrier
  Retraité dans la liste des indisponibles, jusqu'ici jamais affiché en toutes lettres nulle part (voir
  Journal du 2026-08-26 sur `WarriorStatus.Retired`). **Discussion annexe, tranchée sans changement de
  code** : l'utilisateur a demandé si plusieurs `NextGameNote` simultanés s'accumulent - non, c'est un
  simple `string?` remplacé par écrasement (`Warband.NextGameNote = nextGameNote`), aucune accumulation ;
  laissé tel quel pour l'instant (aucun second résultat Exploration ne pose encore de NextGameNote en
  plus de Cimetière - "Une Faveur Rendue" reste à construire), à revisiter si un futur résultat cause une
  vraie collision. Aucune nouvelle logique `Core.Rules` (pas de nouveau test `RulesTests.cs` - le
  matching par jet réutilise `InjuryCatalogLookup.RollRangeMatches` déjà couvert), 309/309 tests passent
  (compte inchangé), build Core + tête MAUI clean.
- **2026-08-26 (correctif : un jet par Vieille blessure PORTÉE, pas par guerrier)** — Retour utilisateur
  juste après la passe ci-dessus : "si on a plusieurs oldwound, on doit tirer le nombre de old wound...
  2 old wound = 2 jets... ça augmente les chances de ne pas jouer" - un guerrier peut accumuler plusieurs
  Vieilles blessures distinctes au fil des parties (2 résultats Serious Injury séparés tombés sur 32), et
  chacune se teste indépendamment - la première implémentation ne générait qu'UNE `OldWoundRollEntry` par
  guerrier concerné (`Where(...Injuries.Any(...))`), quel que soit le nombre réel de Vieilles blessures
  portées. Corrigé en `SelectMany` sur le COMPTE d'instances (`Injuries.Count(...)`) plutôt que sur une
  simple présence - un guerrier à 2 Vieilles blessures obtient maintenant 2 lignes de jet indépendantes.
  `OldWoundRollEntry` gagne un `Subtitle` résolu par l'appelant (plutôt qu'un `{loc:Loc ...}` fixe en
  XAML) pour pouvoir afficher "(1/2)"/"(2/2)" quand le guerrier en porte plusieurs, texte de base inchangé
  sinon. Historique dédupliqué par guerrier (`.Select(r => r.Warrior).Distinct()`) avant de générer les
  phrases d'échec : un guerrier qui rate 2 jets sur 2 ne doit apparaître qu'une fois dans l'Historique,
  pas une phrase par jet raté. Aucun changement Core (seuls des fichiers tête MAUI touchés) - suite de
  tests non concernée, non relancée ; build Core + tête MAUI clean.
- **2026-08-26 (même bug pour Blessure profonde - correctif par analogie)** — L'utilisateur a repéré que
  le correctif Vieille blessure ci-dessus s'applique aussi à Blessure profonde (35) : "ces changements
  vont nous permettre de traiter plus efficacement la blessure profonde". Vérification faite,
  `ApplySeriousInjuryEffectAsync` (`MissNextGame`/`MissGamesRollD3`) écrasait `Warrior.
  SickGamesRemaining` (`=`) au lieu de le cumuler (`+=`) - inoffensif pour un jet isolé, mais "Blessures
  multiples" (16/21) peut produire PLUSIEURS sous-résultats accordant chacun du temps Malade pour le
  MÊME guerrier dans la MÊME résolution (deux Blessures profondes, ou Blessure au bras légère + Blessure
  profonde) : le texte du livre est explicite ("cumulez tous les effets obtenus"), un remplacement
  perdait silencieusement les parties déjà accumulées par un sous-résultat précédent. Même famille de
  bug que Vieille blessure ("plusieurs occurrences du même effet doivent se cumuler, pas s'écraser") -
  corrigé en changeant les deux affectations en `+=`. Un guerrier déjà Malade avant cette Fin de Partie
  (`previouslySickWarriors`) ne peut pas être concerné : il est exclu d'`activeWarriorRows` et ne repasse
  donc jamais par cette méthode dans la même session - seul le cas "plusieurs sous-résultats Blessures
  multiples pour le même guerrier, même résolution" était concerné. Aucun changement Core (fichier tête
  MAUI uniquement) - compilation confirmée (0 `error CS`), copie finale de l'exécutable bloquée par
  l'instance de l'appli ouverte en parallèle (limite connue).
- **2026-08-26 (Lancer la partie - affichage groupé + conséquence réelle de l'échec)** — Deux retours
  utilisateur sur "Lancer la partie". (1) Affichage : "au lieu d'avoir 2 card, c'est d'avoir une card
  avec 2 roll à l'intérieur" - un guerrier à plusieurs Vieilles blessures affichait jusqu'ici une carte
  par jet ; nouveau `OldWoundWarriorEntry` (une carte par guerrier, `List<OldWoundRollEntry>` à
  l'intérieur) remplace le `List<OldWoundRollEntry>` plat de `StartGameDialogViewModel`.
  `OldWoundRollEntry` perd son `Warrior`/`Subtitle` (redondants une fois regroupés par carte) au profit
  d'un simple `Label` ("Jet 1"/"Jet 2", vide et masqué si une seule Vieille blessure). (2) Conséquence
  du jet : "quand on clique sur commencer la partie, et que le personnage ne peut pas la jouer, il n'est
  pas flagué pour la partie et est bien présent dans le End of Game" - avant ce correctif, un échec
  n'était que journalisé dans l'Historique, sans toucher `Warrior.Status` : le guerrier restait Actif et
  se retrouvait à tort dans `activeWarriorRows` si "Fin de partie" était ouvert avant sa prochaine
  partie, alors qu'il n'avait pas participé à celle-ci. Corrigé : un guerrier avec au moins un jet raté
  (`OldWoundWarriorEntry.HasFailure`, un seul suffit parmi ses jets) est marqué `WarriorStatus.Sick` +
  `SickGamesRemaining += 1` à la confirmation - même mécanisme que le Puits de l'Exploration, donc
  correctement exclu du prochain `EndOfGame()` et automatiquement rebasculé Actif par
  `ApplySicknessLifecycleAsync` une fois cette partie (qu'il vient de manquer) enregistrée. Nouvelle clé
  resx `StartGameOldWoundRollLabel` ("Jet"/"Roll"). Aucun changement Core - compilation confirmée (0
  `error CS`), copie finale bloquée par l'instance de l'appli ouverte en parallèle (limite connue).
- **2026-08-26 (Blessure profonde : le 1D3 devient un jet visible + indicateur de parties restantes)** —
  "Il nous faut le tirage du nombre de partie manquée (avec un indicateur du nombre d'indisponibilité)."
  Deux lacunes réelles corrigées. (1) Le 1D3 de Blessure profonde (35) était tiré silencieusement par
  `SeriousInjuryEffectTable.RollD3()` DANS `ApplySeriousInjuryEffectAsync`, à l'enregistrement - le
  joueur ne le voyait ni ne le saisissait jamais, contrairement à tous les autres jets de l'appli (jet
  manuel + bouton dé optionnel). Nouveau `SeriousInjuryOutcome.Value` (Core, `int?`, toujours null tel
  que renvoyé par `TryGetOutcome` - une fonction pure du jet seul, sans accès à un sous-jet de wizard) :
  rempli par l'appelant via `outcome with { Value = ... }` juste avant `ApplySeriousInjuryEffectAsync`,
  depuis un nouveau sous-jet visible - `WarriorOutcomeRow.DeepWoundSubRoll`/`ShowDeepWoundSubRoll`/
  `DeepWoundConfirmationText`/`DeepWoundRollError` (miroir exact d'`InjuryBranchSubRoll`, 1D3 au lieu de
  1D6), plus la même chose sur `InjurySubRollEntry` pour un sous-jet "Blessures multiples" qui tombe
  lui-même sur 35 - nouvelles commandes `AutoRollDeepWound`/`AutoRollSubDeepWound`. `MissGamesRollD3`
  lit maintenant `outcome.Value` (repli défensif sur `RollD3()` si absent, ne devrait plus arriver -
  `ValidateInjuryStep` exige ce sous-jet avant de continuer). (2) `Warrior.SickGamesRemaining` n'était
  affiché NULLE PART dans l'UI malgré son existence depuis le Palier 1 - nouveau
  `WarriorRow.SickChipText` ("Indisponible (2)", nouvelle clé resx `WarriorStatusSickCount`) remplace le
  texte fixe "Indisponible" sur la puce du roster ET dans la liste "Guerriers indisponibles" de "Lancer
  la partie" (réutilisé tel quel, une seule source de vérité pour ce texte). 309/309 tests passent
  (aucun nouveau test Core - `SeriousInjuryOutcome.Value` est un champ additif, `TryGetOutcome` ne le
  renseigne jamais lui-même donc la couverture existante reste valide telle quelle), build Core + tête
  MAUI clean (compilation confirmée, copie finale bloquée par l'instance de l'appli ouverte en
  parallèle - limite connue).
- **2026-08-27 (Capturé - choix du joueur plutôt qu'un jet, nouvelle branche `feature/injuries-cleanup`)**
  — L'utilisateur a fourni le texte officiel exact (61) : 5 issues nommées (Rançon, Échange, Vendu aux
  esclavagistes, Tué par les Morts-Vivants avec Zombie gagné par le CAPTEUR, Sacrifié par les Possédés
  avec +1 XP pour le chef du CAPTEUR) - toutes racontées du point de vue du capteur (l'or/le Zombie/l'XP
  vont TOUJOURS à la bande adverse, jamais à la nôtre). Contrairement à Rancune (portée par un sous-jet
  D6), aucune de ces 5 issues n'est déterminée par un dé - c'est une négociation/décision du capteur,
  donc un simple **choix du joueur** (Picker, nouveau `EndOfGameCapturedChoicePh`) plutôt qu'un
  mécanisme de jet. Nouveau `Core.Rules.CapturedOutcomeTable`/`CapturedOutcome` (5 valeurs) avec deux
  prédicats purs : `ReturnsToWarband` (Rançon/Échange - le guerrier revient avec tout son équipement,
  aucune mutation) et `CausesDeath` (Tué par les Morts-Vivants/Sacrifié par les Possédés ->
  `WarriorStatus.Dead`) - la 5e issue (Vendu aux esclavagistes) ne renvoie ni l'un ni l'autre : perdu
  pour de bon mais jamais confirmé mort, mappé sur `WarriorStatus.Retired` plutôt que `Dead` (2e usage
  de ce statut après le second Œil crevé - doc mise à jour). Les 3 issues "perdues" perdent tout
  l'équipement du guerrier (même mécanisme que Dépouillé, 36). Aucun jet de gold formule 1D6×5 CO
  appliqué à NOTRE trésorerie : cet or va au capteur, pas à nous - vérifié dans le texte, aucune
  mutation de `Warband.Treasury` pour ce résultat. Câblage wizard en miroir exact du reste : `WarriorOutcomeRow.
  ShowCapturedChoice`/`SelectedCapturedOutcome` (+ mêmes propriétés sur `InjurySubRollEntry` pour un
  sous-jet "Blessures multiples" tombant lui-même sur 61), validé par `ValidateInjuryStep`, appliqué
  dans `ApplyWarriorOutcomesAsync` (jet principal et sous-jet) avec une phrase d'Historique dédiée par
  issue (`HistoryCaptured{Outcome}Sentence`, cinq nouvelles clés resx). Capturé reste caché du chip
  roster (déjà couvert par `SeriousInjuryTable.HidesRosterChip`, aucun changement nécessaire - un
  guerrier revenu par Rançon/Échange n'a de toute façon aucune condition durable à rappeler, et un
  guerrier Mort/Retraité ne montre plus aucune chip Blessure puisqu'il change de groupe roster). Nouveaux
  tests `RulesTests.cs` pour les 2 prédicats (5 cas). Aucun changement `DataServiceTests.cs` (pas de
  nouveau seed de catalogue - Capturé n'attache aucune `SpecialRule`, contrairement à Folie/Endurci).
  Build Core + tête MAUI clean (0 `error CS`), suite de tests en cours de vérification.
- **2026-08-27 (Capturé - portée revue à la baisse : le capteur est une vraie bande adverse)** —
  L'utilisateur, en relisant le résultat ci-dessus, a fait remarquer le vrai problème de fond : les 5
  issues de Capturé racontent toutes une décision du CAPTEUR, c'est-à-dire une bande adverse - or
  l'appli n'a aujourd'hui aucune notion de "l'autre bande" comme donnée structurée (elle vit sur un seul
  appareil, pas de lobby/réseau entre deux joueurs). D'où l'idée, notée en mémoire pour plus tard sans
  être lancée maintenant : un futur système de connexion entre appareils (lobby sur un réseau local)
  pour que deux joueurs puissent réellement échanger ces données - "même si ça, c'est pour plus tard,
  juste ne pas l'oublier". Dans l'immédiat, portée revue à la baisse : supprimé entièrement
  `Core.Rules.CapturedOutcomeTable`/`CapturedOutcome` (5 valeurs, Picker) au profit d'un simple choix
  binaire - une case à cocher "Racheté contre rançon" (`WarriorOutcomeRow.IsRansomed`/`RansomAmount`,
  miroir sur `InjurySubRollEntry` pour le sous-jet imbriqué) : cochée, un montant en CO est saisi et
  déduit de `Warband.Treasury` (négocié entre les deux joueurs à la table, aucune formule dans le
  livre), le guerrier revient avec son équipement ; décochée (par défaut), le guerrier est
  **considéré mort** (`WarriorStatus.Dead`, équipement perdu comme Dépouillé) - les 4 autres issues du
  livre (Échange/Vendu aux esclavagistes/Tué par les Morts-Vivants/Sacrifié par les Possédés) ne sont
  plus distinguées : toutes reviennent au même de notre point de vue (le guerrier ne revient jamais,
  rien à gagner pour notre bande). `WarriorStatus.Retired` perd son 2e cas d'usage (Vendu aux
  esclavagistes) - doc revenue à son unique cas d'origine (second Œil crevé). Tests `RulesTests.cs`
  `CapturedOutcomeTable_*` supprimés (plus de table Core - la logique restante est trop simple pour
  justifier une abstraction testée séparément). Build Core + tête MAUI clean (0 `error CS`), suite de
  tests en cours de vérification.
- **2026-08-27 (Prisonniers ennemis - contrepartie de Capturé, notre propre bande capture l'adversaire)**
  — Suite logique de la simplification ci-dessus, mockée puis confirmée par l'utilisateur ("tu peux
  implémenter le mock ... pour le zombie, oui ça devient un vrai zombie") : Capturé (61) ne garde que
  Rançon/Mort côté "un DE NOS guerriers capturé PAR l'adversaire" (bande adverse non modélisée), mais
  l'INVERSE - notre bande capture des héros adverses - ne dépend que de NOTRE type de bande, une donnée
  bien réelle (`WarbandArchetype`). Nouvelle étape de wizard `StepKind.Captives` (une seule fois pour
  toute la bande, pas par guerrier, juste après les étapes Blessure) : case à cocher "des héros ennemis
  ont été capturés", stepper pour le nombre, puis un `Picker` de destin par prisonnier
  (`CapturedEnemyEntry`, nouveau fichier) - Rançon/Vendu aux esclavagistes toujours proposés (montant
  saisi, gagné pour NOTRE trésorerie cette fois - Vendu a un bouton dé pour la formule du livre 1D6×5
  CO) ; Tué (devient un vrai Zombie recruté dans la bande, fusionné dans un groupe de Zombies existant -
  même mécanisme que `GrantsFreeHenchmanArchetypeName`) uniquement si la bande jouée est "Undead" ;
  Sacrifié (+1 XP au chef, `Warrior.IsLeader`) uniquement pour "Cult of the Possessed" - match par nom
  anglais d'archétype de bande (`EndOfGameDialogViewModel.IsUndeadWarband`/`IsPossessedWarband`), pas par
  Race (le Culte des Possédés partage la race "Chaos Human" avec la Kermesse du Chaos, donc gater sur la
  Race aurait mélangé les deux bandes). Nouvelle méthode `WarbandDetailViewModel.EndOfGame.
  ApplyCapturedEnemiesAsync`, appelée une fois par Fin de Partie (pas par guerrier). Aucune nouvelle
  logique Core.Rules (même décision que la simplification de Capturé - trop simple pour justifier une
  abstraction). Build Core + tête MAUI clean (0 `error CS`), suite de tests en cours de vérification.
- **2026-08-27 (Vendu aux Fosses - dernier résultat du Palier 3, nouveau catalogue Bibliothèque
  Franc-Tireur)** — Demande explicite de l'utilisateur avec le profil officiel complet du Franc-Tireur
  (Pit Fighter) : deux profils `StatRowView` côte à côte (notre Héros vs. le Franc-Tireur) pour visualiser
  le combat de gladiateur de 65, plus le CRUD Bibliothèque complet pour ce nouveau type de catalogue
  ("bien sûr on aura besoin du crud et de la section librairie pour les francs tireur"). Passé par
  `EnterPlanMode` vu l'ampleur (nouveau modèle Core + CRUD + onglet Codex + mécanique wizard) ; deux
  décisions confirmées via `AskUserQuestion` avant d'implémenter : (1) l'équipement de départ du
  Franc-Tireur référence de vrais `EquipmentItem` (Morgenstern/Casque déjà existants, Gantelet à pointes
  nouveau) plutôt qu'un texte libre ; (2) cette passe se limite au catalogue + CRUD + l'affichage
  comparatif - **pas de vrai flux d'engagement** (le Franc-Tireur de Vendu aux Fosses est un adversaire
  éphémère qui ne rejoint jamais notre bande, c'est NOTRE guerrier qui revient s'il gagne - engagement
  réel = chantier séparé, déjà noté bloquant pour "Une Faveur Rendue").
  - **Core** : nouveau modèle `HiredSword` (profil complet M/CC/CT/F/E/PV/I/A/Cd, `HireCost`/`Upkeep`/
    `BaseRating`, `AllowedSkillCategories`, `StartingEquipmentIds`, `RestrictedToWarbandArchetypeIds` -
    même sémantique Include/Exclude que Skill/Mutation/EquipmentItem) + `HiredSwordEntity` +
    2 tables de jointure (`HiredSwordEquipmentEntity` pour l'équipement de départ,
    `WarbandArchetypeHiredSwordEntity` pour la restriction de bande, mêmes formes que les jointures
    Skill/EquipmentItem existantes).
  - **Seed** : nouveau `Data/SeedData/HiredSwords.json` (demande explicite de l'utilisateur) avec
    l'entrée Franc-Tireur (30 CO d'engagement/15 CO d'entretien, +22 de Valeur de bande, profil M4/CC4/
    CT3/F4/E4/PV1/I4/A2/Cd7, Combat/Vitesse/Force, Morgenstern/Casque/Gantelet à pointes) restreinte à
    13 des 15 bandes seedées (toutes sauf Undead et Skavens - `WarbandRestrictionEditor` gère déjà
    nativement l'affichage "Toutes sauf X, Y" pour ce cas, aucun nouveau mécanisme d'exclusion
    nécessaire). Nouvelle entrée "Spiked gauntlet"/"Gantelet à pointes" dans `Equipment.json` (seul
    objet des 3 qui n'existait pas déjà). `SeedHiredSwordsAsync` suit le patron `SeedSkillsAsync` +
    résout `StartingEquipmentNames` contre `_equipmentIdsByEnglishName` (déjà peuplé, seedé après
    Equipment.json) + nouveau `SharedRestrictionKind.HiredSword` pour la résolution différée de la
    restriction de bande (même mécanisme que Equipment/Skill/Mutation).
  - **CRUD Bibliothèque** : nouveau dossier `Features/Library/HiredSwords/` (patron le plus proche :
    `Mutations/`, liste plate groupée Commun/bande - pas `Skills/`, qui groupe par `SkillCategory`, axe
    inexistant ici ; pas de mode picker, aucun recrutement en jeu). `HiredSwordEditDialog` réutilise
    `StatRowView` en édition (même usage que `WarriorArchetypeEditDialog`), un nouveau bloc de 6
    cases à cocher "Compétences autorisées" (aucun éditeur existant pour ce champ, même sur
    `WarriorArchetypeEditDialog` qui porte pourtant le même champ modèle) et `IEquipmentPickerService.
    PickEquipmentAsync(warbandArchetypeId: 0)` pour choisir l'équipement de départ (même patron que
    `EquipmentListEditDialogViewModel.Items`). Nouvel onglet Codex "Francs-Tireurs" (8e onglet,
    `LibraryPage.xaml`/`LibraryViewModel.cs`).
  - **Mécanique wizard (65)** : `WarriorOutcomeRow.ShowSoldToThePits` mirrore exactement
    `ShowCapturedChoice` (jet == 65, Héros uniquement). `WonPitFight` (case à cocher) pilote deux
    branches : victoire = +50 CO/+2 PX/équipement intact ; défaite = un unique sous-jet D66 sur la même
    table de Blessures Graves, réutilisant `InjurySubRollEntry` tel quel (même patron que
    `MultipleInjuryRolls`, y compris ses propres sous-cas imbriqués Blessure profonde/Capturé) plutôt
    qu'un nouveau type - le livre dit de ne garder que 11-35 pour cette relance mais c'est resté une
    consigne indicative (`Label` d'aide), jamais validée : aucun jet n'est bloqué ailleurs dans l'appli.
    Le profil du Franc-Tireur est résolu une seule fois à l'ouverture du wizard (recherche par Nom
    anglais exact "Pit Fighter", même idiome que la résolution "Zombie" existante) et transmis à chaque
    `WarriorOutcomeRow` - jamais recruté, uniquement affiché pour comparaison. `ApplyWarriorOutcomesAsync`
    applique la relance de défaite avec exactement le même traitement Palier 1 que les sous-jets
    "Blessures multiples" (réutilise `GetOrCreateInjuryAsync`/`ApplySeriousInjuryEffectAsync`), puis
    vide l'équipement du guerrier sans condition s'il survit (sauf s'il retombe sur Capturé-Rançon, seule
    exception où l'équipement reste intact, cohérent avec le traitement normal de Capturé).
  - Build Core + tête MAUI clean (0 `error CS`), nouveau test `HiredSwords_SeedPitFighterFromRulebook`
    (`DataServiceTests.cs`) vérifiant le profil/l'équipement de départ/la restriction 13-bandes, suite
    complète 317/317 verte.
- **2026-08-27 (retouches demandées : nom FR du Gladiateur, onglet Codex non groupé, "fiche perso"
  complète dans la comparaison)** — Trois corrections après relecture de l'utilisateur :
  1. **Précision terminologique** : "Franc-Tireur" est le nom FR de la CATÉGORIE (Hired Sword),
     "Gladiateur" est le nom FR propre au Pit Fighter (traduction officielle fournie par l'utilisateur,
     avec le texte d'ambiance complet) - `HiredSwords.json` corrigé (`name.fr`), description
     remplacée par le texte d'ambiance officiel (pas une redite des champs mécaniques déjà structurés),
     nouveau test `HiredSwords_SeedPitFighter_InFrenchToo`. Quelques commentaires de code corrigés pour
     ne plus laisser croire que "Franc-Tireur" désignait ce Gladiateur en particulier.
  2. **Onglet Codex "Francs-Tireurs" dégroupé** (retiré par l'utilisateur) : `HiredSwordViewModel`/
     `HiredSwordView.xaml` simplifiés en liste plate (`HiredSwordRows`), tout le mécanisme de
     regroupement Commun/bande (`HiredSwordGroup`, filtre, `ShowGroupHeaders`...) supprimé - trop peu
     d'entrées pour le justifier pour l'instant.
  3. **Comparaison de Vendu aux Fosses enrichie** : les deux profils passent du simple `StatRowView`
     nu au même gabarit de "fiche perso" que la carte guerrier de `WarbandDetailPage`
     (`SectionCardBorderStyle`, nom+rôle, `StatRowView` resserré 10/13, chips Équipement/Compétences
     tapables) - demande explicite de l'utilisateur avec le XAML exact de la fiche à reprendre comme
     patron. Côté notre guerrier : équipement/compétences RÉELLEMENT portés (`Warrior.Equipment`/
     `Skills`, nouvelles commandes `ShowInjuryEquipmentDetail`/`ShowInjurySkillDetail`). Côté
     Gladiateur : équipement de départ FIXE résolu en vrais `EquipmentItem` (`WarriorOutcomeRow.
     PitFighterEquipment`, nouvelle commande `ShowPitFighterEquipmentDetail`) plutôt qu'une liste de
     `WarriorEquipment` (il n'est jamais recruté, pas d'instance portée à modéliser) ; compétences
     autorisées en simple texte (`PitFighterAllowedSkillCategoriesText`) plutôt qu'une ChipListView de
     compétences apprises, puisqu'il n'en a par définition aucune. Build Core + tête MAUI clean, suite
     complète relancée.
- **2026-08-27 (roster complet des Francs-Tireurs Grade 1A, 12 nouvelles entrées + Règles Spéciales
  propres)** — L'utilisateur a fourni le texte officiel complet des 13 Francs-Tireurs de Grade 1A
  (Arabian Merchant, Beast Hunter, Dwarf Troll Slayer, Elf Ranger, Freelancer, Halfling Scout,
  Highwayman, Imperial Assassin, Ogre Bodyguard, Pit Fighter déjà seedé, Roadwarden, Tilean Marksman,
  Warlock) pour peupler le catalogue en une passe.
  - **Nouveau : `HiredSword.SpecialRules`** (+ `HiredSwordSpecialRuleEntity`, jointure identique à
    `WarriorArchetypeSpecialRuleEntity`) - jusque-là le modèle n'avait aucune règle spéciale, seul un
    profil statique. Réservé aux règles propres à UN seul effet clair et non conditionnel (ex. Tueur de
    Troll : Deathwish/Hard to Kill/Hard Head, nouvelles ; Ogre/Sorcier : réutilisent les entrées
    partagées existantes "Causes Fear"/"Large Target"/"Wizard" par nom anglais, comme n'importe quel
    autre find-or-create de ce catalogue) - même patron `SpecialRuleSeedData` inline que
    `EquipmentSeedData.SpecialRules`. Éditeur/dialog récap étendus avec un `ChipListView` (picker non
    filtré, `SpecialRuleFilterKind` n'a que Warband/Warrior, aucune valeur HiredSword - une règle propre
    à un Franc-Tireur ne serait de toute façon jamais retrouvée par un filtre qui ne regarde que les
    jointures Warband/Warrior).
  - **Collision de nom repérée et résolue** : Highwayman et Roadwarden ont chacun une règle nommée
    "Expert Rider" dans le texte officiel, mais avec un effet mécanique différent - le find-or-create
    par nom anglais aurait fait hériter le second de la description du premier. Désambiguïsé en
    "Expert Rider (Highwayman)"/"Expert Rider (Roadwarden)", même convention que les variantes d'arme
    ("Heavy (Morning star)" vs "Heavy (Flail)"). Couvert par un test dédié
    (`HiredSwords_SeedFullGrade1ARoster`) qui vérifie explicitement que les deux lignes restent
    distinctes (Id et Description différents), pas fusionnées par erreur.
  - **Systèmes trop complexes pour un chip** (aucune modélisation, texte de référence dans Description
    uniquement, doctrine "pas de moteur de règles" déjà en place) : les 3 tables de marché du Marchand
    d'Arabie (Marché Noir/Marchandises Étrangères/Recéleur) + son garde du corps compagnon (profil
    séparé) ; les compétences uniques de plusieurs Francs-Tireurs (Tueur de Troll, Rôdeur Elfe, Assassin
    Impérial) puisqu'aucune recrue réelle n'existe pour les "apprendre" via un Advance ; la monture
    optionnelle du Chevalier Errant/Bandit de Grand Chemin/Gardien des Routes (Destrier/Cheval, profil
    séparé) ; l'upkeep conditionnel (Tueur de Troll/Rôdeur Elfe payés plus cher par une bande Elfe -
    aucune bande Elfe dans les 15 seedées, exception sans objet ; mentionné en Description quand même
    pour Rôdeur Elfe puisque Chasseurs de Trésors Nains, notre "bande Naine", EST concernée).
  - **5 nouveaux `EquipmentItem`** (`Equipment.json`) pour l'équipement de départ sans équivalent
    existant : Cimeterre (compte comme Épée, réutilise sa règle "Parry (Sword)"), Hachette de Lancer
    (compte comme Couteau de Lancer +1 Force), Rapière, Bâton (arme de Sorcier). Une "Brace of Pistols"
    envisagée puis abandonnée : le catalogue traite déjà ce cas via une simple note de Description sur
    l'objet "Pistol"/"Duelling Pistol" existant plutôt qu'une entrée séparée pour la paire (même
    précédent que "Duelling Pistol" qui mentionne déjà "50 gc for a brace" dans son propre texte) -
    suivi pour Bandit de Grand Chemin/Assassin Impérial.
  - **Équipement à choix ou en plusieurs exemplaires** (Deux Haches du Tueur de Troll/Chasseur de Bêtes,
    armes au choix de l'Ogre, monture optionnelle...) : un seul exemplaire listé dans
    `StartingEquipmentIds` (le modèle n'a pas de quantité ni de "choix multiple"), le reste noté dans
    Description - décision délibérée plutôt qu'ajouter une notion de quantité pour ce cas seul.
  - Traductions FR : fournies par l'utilisateur pour le Gantelet à pointes (passe précédente) ; pour
    cette passe (texte source uniquement en anglais), traductions FR faites par l'assistant - à
    vérifier par l'utilisateur si un nom/une règle mérite d'être corrigé (ex. "Chevalier Errant" pour
    "Freelancer", pas un terme officiel FR retrouvé).
  - Build Core clean, tête MAUI compile sans erreur CS (seul l'exécutable local est resté verrouillé par
    l'instance de l'utilisateur en cours d'exécution - pas un problème de code). Nouveau test
    `HiredSwords_SeedFullGrade1ARoster` (`DataServiceTests.cs`), suite complète relancée.
- **2026-08-27 (Magie Mineure - table de sorts du Sorcier)** — L'utilisateur a fourni la table complète
  (6 sorts, D6) que le Sorcier génère aléatoirement à l'engagement. Ajoutée comme 8e `MagicSchool` dans
  `MagicSchools.json` ("Lesser Magic"/"Magie Mineure", même format `MagicSchoolWithSpellsSeedData` que
  les 7 écoles existantes) plutôt qu'en texte libre dans la fiche du Sorcier - contenu suffisamment
  structuré (jet/difficulté/effet par sort) pour mériter d'être un vrai catalogue, browsable dans
  l'onglet Sorts du Codex comme n'importe quelle autre école. Pas de lien structurel Sorcier ->
  Magie Mineure : `HiredSword` n'a pas de champ `MagicSchools` (seul `WarbandArchetype` en a un),
  cohérent avec la décision de ne pas construire de vrai flux de recrutement pour cette passe - noté
  dans la Description du Sorcier à la place. Traductions FR par l'assistant (source fournie en anglais
  uniquement). `DataServiceTests.CommonCatalogs_DedupCollisions_AndSeedSkills` mis à jour (42 -> 48
  sorts total, +1 assertion pour "Lesser Magic"). Build Core + tête MAUI clean, suite complète relancée.
