# Blessures Graves — état d'implémentation

Suivi de l'assistant Fin de Partie, étape Blessure (voir CLAUDE.md § Règles dans Core, plan de
séquencement "Mécaniser les Blessures Graves — Palier 1"). Mis à jour à chaque avancée — dernière
mise à jour : **2026-08-26**.

Légende : ✅ Fait (jouable de bout en bout, y compris la sauvegarde) · 🔧 En cours · ⏳ À faire

Reprise "règle par règle" (comme pour `EXPLORATION_CHART_STATUS.md`) plutôt qu'un gros palier d'un
coup - trop de résultats pour avancer autrement sans perdre le fil.

## Vue d'ensemble

| Palier | Mécanisme | Statut |
|---|---|---|
| 1 — effet direct sur stat/statut/équipement/XP déjà modélisé | Pénalité de caractéristique permanente, perte d'équipement, +1 XP, Indisponible (1 ou D3 parties) | **10/10 ✅** |
| 2 — règle spéciale permanente attachée à l'Injury | Frénésie/Stupidité (Folie, 24) + Armes à une main uniquement (23 grave) via `Injury.SpecialRules` - pas de nouvelle table `WarriorSpecialRule`, voir Journal ; Endurci/Horribles balafres au même mécanisme | **Folie + Bras amputé ✅, 2 restants (Endurci, Horribles balafres)** |
| 3 — branches complexes / jet récurrent | Capturé, Vendu aux Fosses, Vieille blessure ("avant chaque bataille"), suivi "second œil → retraite" | **0/4 ⏳** |
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
| 23 (sous-jet 1) | Blessure au bras : amputé | 2 | ✅ | — | Chip catalogue dédiée portant une vraie `SpecialRule` "Armes à une main uniquement" (nouvelle, purement informative - pas de blocage actif à l'équipement) - même traitement que Folie, y compris la non-fusion dans les Règles spéciales (2026-08-26) |
| 24 (sous-jet 1-3) | Folie : Stupidité | 2 | ✅ | ✅ | Chip catalogue dédiée portant une vraie `SpecialRule` "Stupidité" (trouvée/réutilisée depuis le catalogue commun) - PAS fusionnée dans les Règles spéciales du guerrier (revenu dessus le 2026-08-26), surfacée uniquement en puce imbriquée tapable à l'intérieur du dialog récap de la Blessure |
| 24 (sous-jet 4-6) | Folie : Frénésie | 2 | ✅ | ✅ | Idem avec la `SpecialRule` "Frénésie" (déjà dans le catalogue commun, réutilisée telle quelle) |
| 25 (sous-jet 2-6) | Jambe écrasée : légère | 1 | ✅ | ✅ | Statut Indisponible, 1 partie ratée ; chip catalogue dédiée, temporaire - supprimée dès le retour Actif |
| 25 (sous-jet 1) | Jambe écrasée : grave | 3 | ⏳ | ❌ | "Ne peut plus courir" - pas de notion de course en combat, reste texte seul (aucun effet mécanisé) ; chip catalogue dédiée, permanente |
| 26 | Blessure au torse | 1 | ✅ | — | Endurance -1 permanent |
| 31 | Œil crevé | 1 | ✅ | — | Tir -1 permanent (le suivi "second œil crevé → retrait obligatoire" reste Palier 3) |
| 32 | Vieille blessure | 3 | ⏳ | ❌ | Jet 1D6 avant CHAQUE partie future - aucun moment "avant bataille" dans l'appli aujourd'hui |
| 33 | Traumatisme nerveux | 1 | ✅ | — | Initiative -1 permanent |
| 34 | Blessure à la main | 1 | ✅ | — | Capacité de Combat -1 permanent |
| 35 | Blessure profonde | 1 | ✅ | — | Malade, D3 parties (`Warrior.SickGamesRemaining`) |
| 36 | Dépouillé | 1 | ✅ | — | Perd tout l'équipement porté, sans remboursement |
| 41-55 | Récupération totale | — | ✅ | — | No-op, déjà correct |
| 56 | Rancune | — | ✅ | — | Cible D6 + `WarriorHatred`, fait avant ce chantier |
| 61 | Capturé | 3 | ⏳ | ❌ | Rançon/échange/vente comme esclave - branches multiples mutuellement exclusives |
| 62-63 | Endurci | 2 | ⏳ | ❌ | Immunisé à la Peur permanent - `WarriorSpecialRule` |
| 64 | Horribles balafres | 2 | ⏳ | ❌ | Provoque la Peur permanent - `WarriorSpecialRule` |
| 65 | Vendu aux arènes | 3 | ⏳ | ❌ | Combat un gladiateur (Francs-Tireurs) - branches victoire/défaite |
| 66 | Survie miraculeuse | 1 | ✅ | — | +1 Expérience |

## Détail — Hommes de main (D6)

| Dé | Résultat | Statut | Testé | Note |
|---|---|---|---|---|
| 1-2 | Perdu | ✅ | — | `HeadCount -= 1` / suppression du groupe, fait avant ce chantier |
| 3-6 | Récupération totale | ✅ | — | No-op, déjà correct |

## Hors périmètre pour l'instant

- **Palier 2, restants** : Endurci (62-63, Immunisé à la Peur) et Horribles balafres (64, Provoque la
  Peur) au même mécanisme que Folie/Bras amputé (`Injury.SpecialRules`, voir Journal 2026-08-25 - pas de
  nouvelle table de jointure `WarriorSpecialRule` finalement nécessaire).
- **Palier 3 (4 résultats)** : Capturé (61) et Vendu aux Fosses (65) ont des branches multiples
  mutuellement exclusives sans patron existant dans le wizard (le patron "Groupe D jets indépendants"
  de l'Exploration ne convient pas, voir Journal) ; Vieille blessure (32) exige un jet "avant chaque
  bataille future" - aucun moment de ce type n'existe dans l'appli (seulement Fin de Partie) ; le
  suivi "second œil crevé → retrait obligatoire" (31) n'est pas encore construit.

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
