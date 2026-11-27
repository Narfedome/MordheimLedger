# Plan de chantier Clean Code / SOLID — Mordheim Ledger

Préparé le 2026-11-27 sur `master` @ `19b2016`. Chaque lot est autonome (une branche `feature/<nom>` par lot),
ordonné du plus rentable au plus lourd. Les numéros de ligne datent de ce relevé : refaire le `grep` indiqué avant
d'éditer si des features sont passées entre-temps.

Validation de chaque lot : `dotnet build MordheimLedgerApp.csproj -f net10.0-windows10.0.19041.0` +
`dotnet test MordheimLedgerApp.Tests` (+ `dotnet stryker --since:master` pour les lots 1 et 6, qui touchent les
services mutés).

## Correctifs du premier bilan (rien à faire)

- **`async void`** : les 27 de l'appli sont des gestionnaires de cycle de vie/d'événement, et leur travail passe
  par `Services/UiErrors.RunAsync`. Le seul dans un ViewModel (`WarbandEditDialogViewModel.ShowExistingWarbandHint`,
  l. 394) est lui aussi gardé. Conforme.
- **`catch` génériques** : tous sont à une frontière (UI, journal, transaction avec `throw;`, préchauffage) et
  commentés. Conforme. Seule retouche possible : `WarbandListPage.xaml.cs:59` écrit dans `Debug.WriteLine` alors
  que tout le reste passe par `CrashLogger.LogException`.

---

## Lot 1 — Services de Fin de partie injectés (DIP) · ~1 h · priorité 1

Les 6 services Core de la Fin de partie sont créés par `new` dans le ViewModel, alors que leurs tests les
reçoivent déjà par constructeur.

**Core (`MordheimLedgerApp.Core/Services/`)**
1. Ajouter une interface par service, dans le fichier du service ou à côté (même convention que
   `IWarbandService.cs`) : `IAdvancementService`, `IRosterChangeService`, `IPostBattleSequenceService`,
   `IPostBattleTradingService`, `IExplorationRewardService`, `IInjuryService`. Membres = les méthodes `public`
   d'instance actuelles. Les `static` restent sur la classe (`InjuryService.Kill`, `InjuryService.RemoveDeadFigures`).
2. `InjuryService` : retirer `string languageCode` du constructeur (l. 27). Seul `ResolveInjuryAsync` (l. 36)
   l'utilise (l. 38 et 46) → `ResolveInjuryAsync(int roll, bool isHero, string fallbackText, string languageCode,
   int? branchSubRoll = null)`. Le cache `_catalog` doit alors retenir sa langue (`_catalogLanguage`) et se recharger
   si elle change.

**Appli**
3. `MauiProgram.cs` (après l. 146, `IWarbandService`) : `AddTransient<IXxxService, XxxService>()` ×6. Transient,
   parce que le ViewModel est lui-même transient (l. 154) et qu'`InjuryService` garde un cache par Fin de partie.
4. `EndOfGamePageViewModel.cs:648` : ajouter les 6 interfaces au constructeur + champs `_advancement`, `_roster`,
   `_sequence`, `_trading`, `_explorationRewards`, `_injuries`. Le constructeur passe de 9 à 15 paramètres : ce
   symptôme du ViewModel trop gros se résorbe au lot 6, où chaque ViewModel d'étape ne reçoit que ses services.
5. Remplacer les créations (`grep -rnE "new (PostBattle|Exploration|Injury|Advancement|RosterChange)\w*\(|=> new\(_warbandService\)" MordheimLedgerApp`) :

   | Fichier | Lignes |
   |---|---|
   | `EndOfGamePageViewModel.Apply.PostBattleSequence.cs` | 57, 71, 172, 220, 241, 316, 331 |
   | `EndOfGamePageViewModel.Apply.Exploration.cs` | 206 |
   | `EndOfGamePageViewModel.Apply.WarriorOutcomes.cs` | 23 (`injuries` → `_injuries`, langue passée à l. 25), 402 (supprimer la propriété `Advancement`), 423 |
   | `EndOfGamePageViewModel.Apply.Recruitment.cs` | 24 (supprimer la propriété `Roster`) |
   | `EndOfGamePageViewModel.Reserve.cs` | 206 |
   | `EndOfGamePageViewModel.ScenarioRewards.cs` | 96 |

**Tests** : `InjuryServiceTests.cs` l. 27 (constructeur sans `"en"`) + 13 appels de `ResolveInjuryAsync` à
compléter avec `"en"`. Les 5 autres fichiers de tests sont inchangés. `stryker-config.json` reste inchangé.

---

## Lot 2 — `ILibraryService` séparé en lecture / écriture (ISP) · ~1 h 30

50 méthodes = 16 entités × Get/Save/Delete. La majorité des consommateurs ne fait que lire. Découper par entité
(16 interfaces) serait excessif ; on sépare la lecture de l'écriture.

1. `Core/Services/ILibraryService.cs` : extraire `ILibraryReader` (`DataVersion` + les 22 `Get*`) et
   `ILibraryWriter` (les 14 `Save*` + 14 `Delete*`). `ILibraryService : ILibraryReader, ILibraryWriter` reste, vide.
2. `MauiProgram.cs:144` : enregistrer la même instance sous les 3 types :
   `AddSingleton<LibraryService>()`, puis `AddSingleton<ILibraryService>(sp => sp.GetRequiredService<LibraryService>())`,
   idem pour `ILibraryReader` et `ILibraryWriter`.
3. Passer à `ILibraryReader` les consommateurs en lecture seule (relevé des appels réels) :
   - Core : `WarbandService` (+ `InjuryService` garde `ILibraryService`, il crée une blessure hors table)
   - Services : `DetailDialogService`, `EquipmentPickerService`
   - ViewModels : `WarbandListViewModel`, `WarbandDetailViewModel`, `EndOfGamePageViewModel`,
     `WarbandEditDialogViewModel`, `WarriorEditDialogViewModel`, `SpellRollDialogViewModel`,
     `WarriorArchetypeSelectorViewModel`, `WarbandArchetypeDetailDialogViewModel`,
     `DramatisPersonaEditDialogViewModel`, `HiredSwordEditDialogViewModel`, `WarriorArchetypeEditDialogViewModel`
   - Restent sur `ILibraryService` : les ViewModels du Codex qui écrivent (`XxxViewModel`, `XxxEditDialogViewModel`
     avec `Save*`/`Delete*`).
4. Tests : aucun changement (ils instancient `LibraryService`).

`IWarbandService` (39 méthodes) : même idée possible (`IWarbandReader`, `IWarriorAttachments` pour les
Add/Remove de compétence/blessure/sort/mutation/équipement, `IWarbandHistory`), **optionnel**. Ses consommateurs
utilisent presque tous lecture et écriture, donc le gain est faible.

---

## Lot 3 — Localisateur de services résiduel (DIP) · ~15 min

Un seul endroit à corriger :
- `Features/Settings/SettingsViewModel.cs:175` résout `OnboardingPage` via `IPlatformApplication.Current`. Le
  ViewModel est créé par la DI (`MauiProgram.cs:156`) → injecter `Func<OnboardingPage>` dans son constructeur
  (+ `AddTransient<Func<OnboardingPage>>(sp => () => sp.GetRequiredService<OnboardingPage>())`).

Les 3 autres sont des exceptions justifiées, à documenter d'une ligne de commentaire plutôt qu'à corriger :
- `BaseViewModel.cs:17` (`LoadingService`) : hérité par ~60 ViewModels, dont tous les dialogs construits avec `new`.
- `Components/Dialogs/ChipDetailDialogViewModel.cs:33` (`IDetailDialogService`) : créé avec `new` à 5 endroits
  (`DialogViewModel.cs:101`, `WarbandDetailViewModel.cs:508/548`, `RaceViewModel.cs:74`, `MagicSchoolViewModel.cs:108`),
  et le service n'y sert que pour le cas des sorts liés (déjà commenté).
- `Features/Warbands/CreateEdit/WarbandEditForm.xaml.cs:34` (`AppTourService`) : la forme est créée avec `new`
  (`WarbandListViewModel.cs:127/167`), frontière UI.

---

## Lot 4 — Découpage d'`AppDatabase` (SRP) · ~2 h

`Core/Data/AppDatabase.cs` (1 417 l.) mélange connexion/cache/transaction, schéma, réparations des parties jouées
et seed. `ContentSync` et `OfficialIdBridge` sont déjà en `partial`.

| Nouveau fichier | Contenu (lignes actuelles) |
|---|---|
| `AppDatabase.cs` | constructeur, cache de tables, transaction, `InitializeAsync`, `ResetAsync` (27–150, 517–543) |
| `AppDatabase.Schema.cs` | `CreateAllTablesAsync`, `DropAllTablesAsync` (402–516) |
| `AppDatabase.Repairs.cs` | `RepairPlayedDataAsync` + `Remove*` + `Backfill*` + `ResyncExplorationResultsAsync` (164–401) |
| `OfficialContentSeeder.cs` (classe à part, `internal`) | tout `Seed*`, `FindOrCreate*`, `LoadWarbandSeedDataAsync`, `PendingSharedRestriction`, empreinte (544–1417) |

- Phase A, mécanique : déplacer en `partial` (zéro risque, aucune signature ne change).
- Phase B, optionnelle : sortir le seed dans `OfficialContentSeeder` (reçoit la connexion + `_warbandArchetypeIdsByFileStem`).
  C'est le vrai gain SRP, mais il touche un code sans tests unitaires directs : s'appuyer sur `DataServiceTests` (seed complet).
- Garder `// Stryker disable all` en tête de chaque nouveau fichier `AppDatabase*.cs`/seeder (cf. CLAUDE.md).

---

## Lot 5 — `WarbandEditDialogViewModel` par étape (SRP) · ~½ journée

1 558 lignes, 35 commandes, un assistant à 6 onglets. Les frontières sont déjà visibles dans le code :

| Classe extraite (même dossier `CreateEdit/`) | Contenu actuel |
|---|---|
| `WarbandGeneralStep` | archétype, nom, mode bande existante (470–515, `ValidateGeneralStep` 1136) |
| `WarbandRecruitStep` | archétypes recrutables, +/−, groupes, Francs-Tireurs, `UpdateRecruitability` (516–790, `ValidateWarriorsStep` 1146) |
| `WarbandOutfitStep` | équipement/compétences/mutations/blessures/sorts par recrue (792–1135, `ValidateEquipmentStep` 1171) |
| `WarbandNamesStep` | noms des guerriers et des Francs-Tireurs, noms suggérés (1207–1275) |
| reste dans le ViewModel | navigation Next/Back/onglets, en-tête/pied, `Save`/`SaveToDatabaseAsync` (1276–fin) |

Chaque étape = `ObservableObject` exposé en propriété (`General`, `Recruit`…) ; le XAML de `WarbandEditForm` passe
de `{Binding X}` à `{Binding Recruit.X}`. Faire une étape par commit, en commençant par `WarbandNamesStep` (la plus
isolée). La logique de règles est déjà dans `Core/Rules/RecruitmentRules` et `EquipmentPricing` : rien à y ajouter.

---

## Lot 6 — `EndOfGamePageViewModel` en ViewModels d'étape (SRP) · plusieurs jours, par petits morceaux

28 fichiers `partial`, ~7 900 lignes, 29 vues d'étape (`Steps/*.xaml`), toutes en `x:DataType="local:EndOfGamePageViewModel"`.
Le point dur est l'état partagé :
- `Warband`, `WarriorRows`/`_allActiveWarriorRows`, promus (`_promotedHeroRows`) ;
- **la trésorerie** : `TreasuryBeforeFistfulOfCrowns` (`EndOfGamePageViewModel.cs:266`) additionne 11 postes venant de
  8 étapes différentes.

**Cible**
1. `EndOfGameSession` (appli, `ObservableObject`) : bande, lignes guerriers, instantanés pré-bataille, `Loc`.
2. Trésorerie dans Core : `Core/Rules/EndOfGameTreasury` qui reçoit une liste de postes `(source, montant)` et
   renvoie solde/blocage. Chaque étape expose son poste (`ITreasuryContribution { int TreasuryDelta }`), la session
   fait la somme. Cette règle devient testable dans `RulesTests.cs`, alors qu'aujourd'hui elle n'est couverte par
   aucun test.
3. Un ViewModel par groupe d'étapes, qui reçoit la session + ses services (lot 1 fait avant) :

   | ViewModel | partials actuels | Vues |
   |---|---|---|
   | `ResultStepVm` | `.cs` (résultat), `ScenarioRewards`, `Gains` | Result, ScenarioRewards |
   | `InjuryStepsVm` | `Injury`, `InjuryRecap`, `Captives`, `IndependentOutcomes` | OutOfAction, Injury, PitFight, InjuryRecap, Captives |
   | `AdvanceStepsVm` | `Advance`, `Promotion`, `Effects` (partie progression) | Experience, Advance |
   | `ExplorationStepsVm` | `Exploration` (1 546 l.), `Apply.Exploration` | ExplorationRoll, ExplorationResult, WyrdstoneSale |
   | `TradingStepsVm` | `RareItems`, `EquipmentTrading`, `Reserve`, `Reallocation`, `Effects` (Marchandage) | RareItems, RareItemPurchase, EquipmentTrading, EquipmentReallocation |
   | `MercenaryStepsVm` | `HiredSwords`, `DramatisPersonae`, `PairEngagement`, `PairDuel` | HiredSwords, DramatisPersonae, PairEngagement, PairDuel |
   | `RecruitmentStepsVm` | `Recruitment` (720 l.), `Roster`, `Dismissal`, `Apply.Recruitment` | AvailableVeterans, DismissWarriors, Recruit* (6 vues) |
   | `RecapVm` | `Recap`, `PostBattle` | Recap |
   | reste dans `EndOfGamePageViewModel` | navigation (`StepKind`, `WizardStep`), orchestration de `Apply*` dans l'ordre | — |

**Méthode (une branche par ligne du tableau)**
- Commencer par une étape feuille avec peu d'état partagé : `MercenaryStepsVm` (`PairDuel`/`PairEngagement` sont
  déjà quasi autonomes), puis `ResultStepVm`.
- À chaque extraction, la vue concernée passe à `x:DataType="local:XxxStepVm"` et `BindingContext="{Binding Xxx}"` ;
  les autres vues ne bougent pas, donc l'appli reste fonctionnelle entre deux morceaux.
- Garder le test E2E « partie complète sur la bande d'exemple » (`MordheimLedgerApp.UITests`) comme filet après chaque
  morceau. C'est le seul test qui couvre le câblage du wizard.
- `WarriorOutcomeRow.cs` (1 125 l.) suit `InjuryStepsVm`/`AdvanceStepsVm` : sortir ses calculs de paliers et de
  blessures vers `Core/Rules` au passage (ce qui est déjà fait pour les tables).

---

## Accepté tel quel (décision à confirmer, pas de lot)

- **`LocalizationService.Instance`** (157 usages, surtout via `BaseViewModel.Loc` et `{Binding Loc[...]}` en XAML) :
  motif repris de DmTools, cohérent dans les deux applis. Passer par une interface injectée coûterait cher pour peu
  de gain tant qu'aucun test de ViewModel n'en a besoin.
- **`DialogStack.Instance`** (22 fichiers, code-behind) : frontière UI, même raisonnement.
- **Dialogs créés dans les ViewModels** (59 `new XxxDialog(...)` dans 26 ViewModels) : un `IDialogService` serait plus
  « MVVM pur », mais n'a d'intérêt que si on se met à tester des ViewModels unitairement. À reconsidérer seulement
  dans ce cas.
- **Core = domaine + infrastructure SQLite dans un même projet** : pas de projet `Infrastructure` séparé. Une appli
  locale, une seule base : le découpage ajouterait des projets sans bénéfice concret.
