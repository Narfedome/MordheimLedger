# Mordheim Ledger — Notes de contexte pour Claude

Application MAUI (.NET 10) de suivi de bandes et de campagnes Mordheim. Contexte produit et
avancement détaillé : [ROADMAP.md](ROADMAP.md). Ce fichier est là pour reprendre le fil d'une
machine/session à l'autre (l'utilisateur développe depuis plusieurs PC).

## Architecture

Reprend délibérément les patterns du projet sœur **DmTools** (`D:\Dev\DmTools` sur cette machine —
le chemin peut différer sur les autres PC de l'utilisateur, même auteur, appli MAUI mature) plutôt
que de réinventer :

- `MordheimLedgerApp.Core` (net10.0, **sans dépendance MAUI**) : `Models/` (modèles purs),
  `Data/` (`Entities/` SQLite + `AppDatabase.cs` + `EntityMapping.cs`), `Services/` (CRUD),
  `Rules/` (décisions de règles pures - voir § Règles dans Core ci-dessous)
- `MordheimLedgerApp` (tête MAUI) : `Features/<Domaine>/` (Page + Page.xaml.cs + ViewModel),
  `Components/Dialogs/` (Confirm/Prompt/ActionSheet réutilisables, portés de DmTools),
  `Services/` (LocalizationService/ThemeService/LoadingService), `BaseViewModel`,
  `Resources/Styles/` (Colors/Sizes/Styles = design tokens)
- `MordheimLedgerApp.Tests` (xUnit) : tests de mapping Entity↔Modèle + tests d'intégration des
  services sur une base SQLite temporaire (voir `DataServiceTests.cs`) + tests de règles pures sur
  `Core.Rules` (`RulesTests.cs`, aucune base de données)

**Règles dans Core (branche `feature/rules-to-core`, 2026-08-15)** : suite à l'audit de fidélité aux
mécaniques (§ ci-dessous n'existe que dans l'historique de conversation, pas ce fichier - retenir la
conclusion), une partie du code de règles vivait dans la tête MAUI (`MordheimLedgerApp/Services/`,
`Features/Warbands/CreateEdit/`) plutôt que dans Core, donc hors de portée de
`MordheimLedgerApp.Tests` qui ne référence que Core. Déplacé vers `MordheimLedgerApp.Core/Rules/` :
`WeaponLimits`, `ExperienceMilestones`, `SeriousInjuryTable`, `HenchmanInjuryTable`,
`HeroAdvanceTable`, `HenchmanAdvanceTable`, et une nouvelle `RecruitmentRules` (extraite de la
logique jusque-là inline dans `WarbandEditDialogViewModel.IncrementWarrior`/`UpdateRecruitability`/
`ValidateWarriorsStep` - MaxCount/MaxWarriors/trésorerie/MinWarriors/MinCount). Les 4 tables de jets
(D66 Blessures Graves Héros, D6 Blessures Hommes de main, 2D6 Progression Héros/Hommes de main)
résolvaient jusque-là le texte affiché directement via `LocalizationService.Instance[...]` — c'est
précisément pourquoi elles vivaient dans la tête MAUI (Core doit rester sans dépendance de
localisation). Scindé : Core garde le jet de dés + la classification pure (`IsDeath`/`IsSkill`/
validité du jet) et expose une **clé de ressource** (`TryGetTextKey`, ex. `"InjurySerious34"`) plutôt
que le texte résolu ; `EndOfGameDialogViewModel` (seul consommateur) résout cette clé via
`LocalizationService` lui-même. Effet de bord positif : ce découpage rend explicite, pour n'importe
quelle règle du livre, si elle est déjà appliquée (testée dans `RulesTests.cs`) ou seulement décrite
en texte libre pour le joueur.

Deuxième passe la même session : la formule de prix des matériaux (Gromril/Ithilmar, multiplicateur
sur le coût de base) et l'éligibilité de la première dague gratuite étaient dupliquées telles quelles
dans 4-5 endroits (`EquipmentPick.Cost`, `MaterialChoice`, `WarbandEditDialogViewModel.AddEquipment`,
`WarriorEditDialogViewModel.AddEquipment`) - consolidées dans `Core.Rules.EquipmentPricing`
(`IsFreeDaggerEligible`/`CalculateCost`). `RecruitmentRules.CalculateRemainingTreasury` couvre la
formule de trésorerie restante (StartingTreasury - dépenses, sauf en mode Bande existante où c'est la
saisie libre `TreasuryOverride` qui prime).

**`Core/Rules/` est désormais le point d'entrée pour toute nouvelle règle du livre** (ex. compléter le
wizard Fin de Partie avec de nouvelles mécaniques) plutôt que de la coder inline dans un ViewModel -
décision explicite de l'utilisateur, pas juste un refactor ponctuel.

MVVM via **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`)
partout, y compris dans les ViewModels de dialogues. Navigation Shell classique avec routes
enregistrées dans `AppShell.xaml.cs` et paramètres passés en query string (`[QueryProperty]`).

## Séparation Library vs Campagne

C'est la distinction structurante du modèle de données, demandée explicitement par l'utilisateur :

- **`Models/Library/`** (+ `Data/Entities/Library/`) : le contenu **éditable** — `WarbandArchetype`,
  `WarriorArchetype`, `EquipmentItem`. Chacun porte un `ContentSource` (`Official` / `Modified` /
  `Custom`) affiché comme badge dans l'UI. Éditer une ligne `Official` la bascule en `Modified`
  (jamais de retour silencieux à `Official`) — logique centralisée dans `LibraryService`.
- **`Models/`** (racine) : les **instances jouées** — `Campaign`, `Warband`, `Warrior`,
  `WarriorEquipment`. Recruter un guerrier (`WarriorArchetype.ToWarrior(name)` dans
  `EntityMapping.cs`) copie les stats/coût de l'archétype à cet instant précis ; le `Warrior`
  évolue ensuite indépendamment (XP, blessures) — modifier l'archétype après coup n'affecte pas
  les guerriers déjà recrutés.

**Rangement vérifié (2026-08-15)** : les 12 tables de jointure entre deux concepts Library (ex.
`WarbandArchetypeSkillEntity`, `EquipmentItemSpecialRuleEntity`) vivaient à la racine de
`Data/Entities/` au lieu de `Data/Entities/Library/`, malgré la règle ci-dessus — repéré lors d'un
audit d'architecture puis déplacé (`git mv` + namespace `MordheimLedgerApp.Core.Data.Entities.Library`).
Aucune de ces 12 ne référence de `Warband`/`Warrior` joué, seulement des paires d'archétypes/catalogue.

## Terminologie

Le code utilise les **termes anglais officiels** du jeu (Warband, Warrior, Hero/Henchman, Hired
Sword, Wyrdstone, Trading Post, etc.) même si les échanges avec l'utilisateur sont en français —
conformité avec le matériel officiel plutôt que traduction.

## Style / UI

Même esprit visuel et mêmes interactions que **DmTools** (déjà repris : `BaseViewModel`,
dialogues, `LocalizationService`/`ThemeService`/`LoadingService`, tokens `Resources/Styles/*`).
Pour tout ce qui touche à l'UI/au style (layouts, structure des composants, tailles `OnIdiom`
Windows/Android), reprendre DmTools tel quel plutôt que trimmer ou réinventer — voir l'écran
Settings (`Features/Settings/`) qui reprend la disposition de DmTools intégralement, y compris le
sélecteur de palette (`Components/PalettePicker/`, `ThemeService.AppPalette`) même s'il n'expose
qu'une seule entrée pour l'instant (grim/cendre + vert wyrdstone en accent) — d'autres palettes
pourront s'ajouter plus tard sans retoucher l'écran.

**Icônes** : polices Font Awesome 7 (Solid/Regular/Brands, `Resources/Fonts/`) + RPG Awesome
(`rpgawesome-webfont.ttf`) portées depuis DmTools telles quelles, avec leurs classes de glyphes
générées (`Resources/Icons/{Solid,Regular,Brand,Rpg}Font.cs`, ne pas éditer à la main — regénérer
via IconFont2Code si la police change). Aucune icône gothique/Mordheim-thématique disponible en
police toute faite : RPG Awesome (armes, crânes, dés...) est le meilleur compromis trouvé. Boutons
icône via `Components/FaIconButton/FaIconButtonView` (`GhostIconButtonStyle` = fond transparent,
style implicite = fond accent plein) et `Components/IconTextButton/IconTextButton` (icône + texte
en une seule zone tappable), portés tels quels depuis DmTools.

**Passe cohérence icônes (2026-08-15)** : audit de toutes les icônes de l'app (`SolidFont`/`RpgFont`)
demandé par l'utilisateur, qui a repéré des glyphes utilisés dans le mauvais contexte. Règle
dégagée : **un glyphe = un concept dans toute l'app**, jamais deux sens différents pour le même
glyphe même dans des écrans éloignés. Mordheim se déroule dans le Vieux Monde (Warhammer Fantasy,
pas 40k) : proscrire tout glyphe RPG Awesome à connotation moderne/militaire (ex. `RaAmmoBag`,
écarté au profit de `RaArrowCluster` pour la catégorie Munitions - repéré visuellement comme "sacoche
de chargeur" plutôt que carquois).
- `EquipmentCategoryIconConverter` (puces objet) et `EquipmentItemRow.CategoryIcon`/`CategoryIconFont`
  (tuiles Place du Marché) utilisaient deux mappings différents pour le même `EquipmentCategory` -
  désormais alignés : Armure = `RaVest`, Munitions = `RaArrowCluster`, Divers = `RaPotion`.
- `SolidFont.Shield` n'est plus utilisé nulle part : remplacé par `SolidFont.Users` partout où il
  servait à autre chose que "Bandes" à proprement parler (chips "réservé à ces bandes" dans les
  dialogs Équipement/Animal/Mutation/Compétence, chip "listes d'équipement" et tuile par défaut de
  `WarbandArchetypeView`) - `Users` couvre maintenant tout ce qui touche à la notion de bande.
- Domaine magie : `RaBurningBook` = Sorts uniquement (chips + tuile par défaut de `SpellView`) ;
  `RaCrystalBall` = École de magie (tuile `MagicSchoolView` + chip dans `WarbandArchetypeEditDialog`/
  `DetailDialog`, y compris le bouton "gérer les écoles de magie" en en-tête de `SpellView` - son
  action cible les écoles, pas les sorts). `RaBook` réservé à la catégorie de compétence Érudition ;
  l'onglet Codex garde `SolidFont.BookOpen` (testé avec `RaBook` puis revert, pas de double emploi).
- `SkillCategoryIconConverter` (Combat/Tir/Érudition/Force/Vitesse/Spécial) n'était câblé que sur les
  puces (fiche guerrier, dialogs de recrutement) - la grille de tuiles Compétences du Codex
  (`SkillView.xaml`) affichait un glyphe `Brain` fixe pour toutes les catégories. Corrigé pour utiliser
  le même converter partout ; `SolidFont.Brain` n'est plus utilisé.
- **Bug de fond trouvé en cours de route** : `Components/LibraryItemImage/LibraryItemImageView`
  (icône par défaut des tuiles Codex quand pas d'image) rendait son glyphe via `<Image><FontImageSource>`
  plutôt qu'un `<Label>`. `FontImageSource` rastérise le glyphe dans un bitmap **carré** de taille fixe
  et rogne l'encre qui dépasse ce carré - invisible pour les glyphes compacts, visible pour ceux dont
  l'empreinte est naturellement plus large qu'haute (`RaFootprint`, `RaMuscleUp` repérés coupés à
  l'écran). Remplacé par un `<Label>` (même mécanisme que les icônes de puce ailleurs dans l'app, qui
  n'ont jamais eu ce problème) - corrige les 10 usages de `LibraryItemImageView` d'un coup, pas
  seulement Compétences.

**Navigation** : pages Shell séparées (`Shell.GoToAsync`) pour la navigation entre écrans — pas
d'accordéon façon `CampaignPage` de DmTools pour `WarbandListPage` (essayé le 2026-08-04 avec un
système de favoris à 2 groupes "Favoris"/"My Warbands", finalement abandonné le même jour : pas assez
de valeur pour la complexité ajoutée). La liste reste plate (`ObservableCollection<WarbandRow>`, pas
de groupes/imbrication), mais garde le style "Chapitre" (`SessionTemplate`) de DmTools plutôt que
celui de "Scène" pour chaque ligne : pas de card bordée, simple trait de séparation
(`ChapterDividerStyle`), padding `AppChapterRowPadding`, titre en italique — seule la zone "Jouer" en
bout de ligne (largeur fixe façon `Launch` de `SceneTemplate`) est reprise du niveau Scène, aux côtés
du split sélection (corps de ligne) / ouverture (zone "Jouer") de `SceneTemplate`. Comme
`CategoryListPage` de DmTools, les
pages "détail" poussées via une route masquent la barre de navigation native
(`Shell.NavBarIsVisible="False"` + `Shell.BackButtonBehavior IsVisible="False"`) et affichent leur
propre en-tête via le composant `Components/DetailPageHeader/DetailPageHeaderView` (Title +
BackCommand) — le rendu natif de la barre/flèche retour diverge trop entre plateformes (surtout
Windows) pour rester cohérent avec le reste du thème.

**Roster de `WarbandDetailPage`** : conçu d'après une vraie feuille de bande papier ("Feuille de
Bande Mordheim v3.4" + fiches Héros/Hommes de main originales 1999, fournies par l'utilisateur) plutôt
que deviné :
- Guerriers groupés Héros/Hommes de main (dépliable/repliable, `HeroesExpanded`/`HenchmenExpanded`),
  le nom du rôle (`WarriorRow.RoleName`, résolu depuis `WarriorArchetype.Name`) affiché à la place
  d'un simple badge "Hero"/"Henchman".
- Pas de coût en or affiché sur la carte guerrier : absent de la fiche officielle (qui ne le montre
  qu'au recrutement et dans le calcul de Valeur de Bande), retiré pour coller à l'original.
- `Components/ExperienceTrack/ExperienceTrackView` : piste de cases à cocher fixe (comme sur papier —
  toutes les cases sont dessinées d'avance, remplies ou non), avec des cases-paliers à bordure dorée
  plus épaisse — **purement un repère visuel copié de la fiche, pas une règle que l'app interprète**.
  L'espacement n'est pas régulier : Héros = paliers d'écart 1/2/3 (×4 chacun) puis 4/5/6 (×3 chacun)
  → 90 cases ; Hommes de main = écart qui augmente de 1 à chaque palier (2, 5, 9, 14, 20...). Voir le
  code (`HeroMilestones`/`HenchmanMilestones`) pour la formule exacte.
- `WarriorArchetype.StartingExperience` : XP de départ d'un type de guerrier (ex. un Capitaine
  Chasseur de Sorcières démarre à 20 XP, déjà reflété dans son profil) — copié sur le `Warrior` par
  `ToWarrior()` au recrutement, à renseigner par archétype (0 par défaut, correct pour la plupart des
  types génériques).

**Refonte carte guerrier (2026-08-16)** : trois correctifs sur `WarbandDetailPage`.
- `StatRowView` en lecture seule affiche maintenant une vraie grille tabulaire (filet horizontal sous
  les abréviations + filets verticaux entre colonnes, valeurs en gras) — comparé à 3 pistes visuelles,
  celle-ci retenue. Le mode édition (Entry) n'est pas concerné.
- Puces de la carte guerrier (Règles spéciales/Blessures/Sorts/Mutations/Monture) migrées vers le
  composant partagé `ChipListView` (déjà utilisé côté Bibliothèque, header+FlexLayout+chip en un seul
  tag) au lieu d'un `FlexLayout`+`DataTemplate`+`Border` dupliqué à la main par section. Équipement et
  Compétences (icône par item - catégorie d'équipement/de compétence) restent dans un `FlexLayout`
  manuel **mais réutilisent `ChipView`** (la puce elle-même, sans le header/`FlexLayout` que
  `ChipListView` embarque) à l'intérieur : `IconGlyph="{Binding Item.Category, Converter=
  {StaticResource EquipmentCategoryIconConverter}}"` fonctionne tel quel car le contexte du
  `DataTemplate` est déjà l'item porté (`WarriorEquipment`/`WarriorSkill`) - même motif exact que
  `WarbandEditDialogViewModel`'s Equipment step (`EquipmentPick`/`ChipView` avec le même converter). Pas
  besoin d'étendre `ChipListView` avec un converter dynamique - fausse piste explorée puis abandonnée,
  `ChipListView.IconGlyph` reste un simple glyphe fixe pour toute la liste, adapté aux 5 autres puces
  (une seule icône par notion) mais pas à Équipement/Compétences. A nécessité d'ajouter un passe-plat
  `Name` sur `WarriorInjury`/`WarriorSpell`/`WarriorMutation`/`WarriorSkill` (`=> Item.Name`) et
  `WarriorEquipment` (`=> NameDisplay`) - `ChipView` lie son Label directement sur `Name`.
  `ChipListView`/`ChipItemView` ont chacun gagné un `HeaderFontSize` bindable (défauts 14/12 -
  comportement inchangé pour les ~15 usages existants côté Bibliothèque qui ne le précisent pas) et
  `ChipListView` un `HeaderTextColor` (défaut null, appliqué seulement si renseigné via un
  `DataTrigger`) pour que les headers de la carte guerrier soient homogènes entre eux (10/AppTextMuted
  partout, y compris Équipement/Compétences qui gardaient déjà ce style en dur).
- Tap sur une puce de la carte guerrier ouvre le même dialog récap en lecture seule complet que la
  Bibliothèque/le recrutement (`XxxDetailDialog`/`XxxDetailDialogViewModel` par type - Équipement/
  Compétence/Sort/Mutation/Blessure/Règle spéciale/Monture), pas le popup générique `ChipDetailDialog`
  (Nom+Description seuls, gardé pour son usage d'origine côté Bibliothèque). Précédent direct :
  `WarriorEditDialogViewModel.ShowEquipmentDetail`. Répète volontairement la même logique de
  résolution des restrictions (bandes/guerriers autorisés) déjà dupliquée 2-3 fois ailleurs
  (WarriorEditDialogViewModel/WarbandEditDialogViewModel/les `XxxViewModel.ShowDetails` de la
  Bibliothèque) plutôt que de la centraliser - pas demandé, hors périmètre de cette passe.
- **Bug trouvé en branchant ces dialogs** : `WarbandService.GetWarriorsAsync` résolvait chaque
  EquipmentItem/Skill/Mutation/Animal porté par un guerrier via un simple `FindAsync` + `ToModel
  (translations)` minimal - `SpecialRules`/`RestrictedToWarbandArchetypeIds`/
  `RestrictedToWarriorArchetypeIds` restaient donc vides pour tout guerrier déjà recruté (invisible
  jusqu'à ce que ces dialogs récap se mettent à les afficher). Corrigé en injectant `ILibraryService`
  dans `WarbandService` et en réutilisant ses méthodes déjà pleinement résolues
  (`GetEquipmentItemsAsync`/`GetSkillsAsync`/`GetMutationsAsync`/`GetAnimalsAsync`, chargées une fois
  par appel à `GetWarriorsAsync` plutôt qu'un `FindAsync` par ligne portée). Test de non-régression :
  `WarbandMutationTests.RecruitedWarrior_CarriedEquipment_HasSpecialRulesResolved`.
- `ExperienceTrackView` sur mobile : le nombre de cases par ligne (30 pour un Héros, comme sur la
  feuille imprimée en 3 lignes) reste fixe à dessein — ne pas le recalculer depuis la largeur
  disponible (essayé puis annulé, ça casse la mise en page "3 lignes" voulue). C'est la case
  elle-même (`BoxSize`, bindable property) qui rétrécit sur un écran étroit, entre `DefaultBoxSize`
  (12) et `MinBoxSize` (7). Nécessite que l'appelant (`WarbandDetailPage.xaml`) NE pose PAS
  `HorizontalOptions="Center"` sur l'instance : ça la ferait se réduire à son propre contenu, rendant
  la mesure de largeur circulaire — le centrage visuel se fait déjà à l'intérieur du composant.

**Tuiles du Codex + dialogs récap en lecture seule** (branche `feature/codex-tile-recap-ui`, 2 commits,
pas encore mergée sur `master`) : passe de polish sur les 8 onglets Codex (Bandes, Trading Post,
Compétences, Règles Spéciales, Mutations, Montures, Sorts, Blessures) + 3 grilles de tuiles annexes
(`EquipmentListView`, `WarriorArchetypeView`, `WarriorArchetypeSelectorView` — mêmes styles partagés,
mais sans bouton info/dialog récap, hors périmètre des 8 types catalogués).
- Tuiles carrées à taille fixe (130×130, `CodexTileFrameStyle` — `ResponsiveGridSpanBehavior` est posé
  tel quel par fichier, sans `TileWidth` explicite, et retombe donc sur son défaut de classe (140) pour
  le calcul du nombre de colonnes ; **pas** centralisé en constante malgré la tentation, décision
  explicite après essai : cf. règles de collaboration ci-dessous), nom enroulé sur 3 lignes
  (`CodexTileNameLabelStyle`) plutôt que tronqué à 1 (`TruncatedLabelStyle`, toujours utilisé tel quel par
  `WarbandListPage` et `MagicSchoolView` — liste à plat, pas une grille de tuiles, hors périmètre).
  Icône+nom(+ligne secondaire) centrés en groupe compact (`VerticalStackLayout`) plutôt qu'en lignes de
  `Grid` élastiques — l'ancienne disposition en lignes laissait l'icône flotter loin du nom. Icône à
  taille fixe (26px tuiles simples, 20px si ligne secondaire) au lieu de `Padding` élastique qui la
  laissait gonfler pour remplir la tuile.
- Icône de tuile par `EquipmentCategory` pour la Place du Marché (`EquipmentItemRow.CategoryIcon`/
  `CategoryIconFont`) plutôt qu'un glyphe "Coins" unique pour tout le catalogue, peu pertinent pour une
  arme/armure.
- Ligne d'info secondaire (`CodexTileSecondaryLabelStyle`) : Trading Post affiche coût+rareté, Sorts
  affiche jet+difficulté — abréviations en toutes lettres (`LibGoldCrownsAbbr` = "CO"/"GC",
  `LibRollAbbr` = "Jet"/"Roll", `LibDifficultyAbbr` = "Diff.") plutôt qu'une icône, trop peu distinguable
  à la taille d'une tuile (testé puis abandonné).
- Bouton info (cercle fantôme, coin haut-droit) sur les 8 types catalogués, ouvrant un nouveau dialog
  récap en lecture seule par type (`Features/Library/*/CreateEdit/XxxDetailDialog.xaml(.cs)` +
  `XxxDetailDialogViewModel.cs`) qui reprend le layout de son dialog Edit mais en `Label` plutôt
  qu'`Entry`/`Editor`/`Picker` ("plus mignon" que des champs désactivés) — sans l'icône du dialog Edit
  (retirée sur demande explicite, readonly uniquement). Base commune `Components/Dialogs/
  ReadOnlyDialogViewModel.cs` (mirror de `ConfirmDialogViewModel`, juste un bouton Fermer).
- Chips (règles spéciales/écoles de magie/restrictions bande-guerrier) à l'intérieur de ces dialogs
  récap : tapotables, ouvrent un mini-popup partagé (`Components/Dialogs/ChipDetailDialog.xaml` +
  `ChipDetailDialogViewModel`) affichant juste Nom+Description de l'élément taponné — un seul dialog
  générique réutilisé par tous les types de chip plutôt qu'un par type.

En-têtes de groupe (les 8 onglets catalogués) resserrés via un style partagé
(`CodexGroupHeaderStyle` dans `Resources/Styles/Styles.xaml`) : taille réduite (`AppFontSizeBase` au
lieu d'`AppFontSizeSectionTitle`, repris par erreur du titre de page) et marge réduite, plus marges
resserrées sur le picker de section (`LibraryPage`) et le picker de catégorie de chaque onglet.
**Limite connue (WinUI)** : `CollectionView IsGrouped="True"` délègue le rendu du `GroupHeaderTemplate`
à un conteneur natif dont le padding/chrome autour du header n'est pas contrôlable depuis le `Label`
qu'on y place — `Margin`/`HeightRequest`/`VerticalTextAlignment` sur le Label n'ont aucun effet sur la
hauteur totale du header (testé et confirmé sans effet le 2026-08-07).

**Essayé puis abandonné** : un contrôle maison, `Components/CodexGroupedGrid/CodexGroupedGridView` (+
`ICodexGroup`, header inline + tuiles en `FlexLayout Wrap`, header épinglé au scroll via
`ScrollView.Scrolled`), avait été construit pour contourner la limite WinUI ci-dessus et généralisé aux
8 onglets Codex. **Revert complet vers le `CollectionView` natif** décidé par l'utilisateur : le contrôle
maison faisait perdre la flexibilité (sélection, virtualisation, comportements) qu'offre `CollectionView`
prête à l'emploi, pour un gain qui ne concernait que l'espacement du tout premier header de groupe. La
marge du tout premier header (`IsFirst` sur chaque `XxxGroup` + `DataTrigger` sur `CodexGroupHeaderStyle`)
reste donc sans effet visible (annulée par le padding natif non maîtrisable, cf. limite ci-dessus) -
`IsFirst` est toujours positionné correctement côté code, juste sans effet visuel avec `CollectionView`.

En attente/reporté (pas dans cette passe) :
- Espacement du premier header de groupe (`IsFirst`) — reporté, pas de solution retenue pour l'instant
  avec `CollectionView` natif.
- Regroupement de la liste à plat de Règles Spéciales (77 entrées, pas de groupe) — pas encore tranché.
- Icônes par objet (URIs du site FR de Mordheim) — encore en évaluation par l'utilisateur.

## Sources de contenu officiel (voir ROADMAP.md § Sources)

- Livre des Règles PDF fourni par l'utilisateur — **trop volumineux** pour l'outil Read (>100 Mo,
  y compris en ciblant des pages)
- Grande Librairie de Mordheim (FR, sites.google.com) — contenu principal en images sur le site,
  mais des PDF par bande plus réduits existent et sont probablement lisibles directement
- Broheim.net (EN) — terminologie officielle + règles core découpées en 3 parties plus petites
- Mordheimer.net (EN) — catalogue de bandes le plus complet, jusqu'à Grand Army 2a ; **seule source de
  référence retenue** pour vérifier/compléter les données (décision explicite de l'utilisateur -
  BSData/mordheim, utilisé ponctuellement comme piste de vérification croisée lors du 2e lot d'import,
  est abandonné, y compris pour la classification par Grade des bandes).

**Important** : les stats saisies à l'origine pour Reiklander Mercenaries (aujourd'hui dans
`Reiklanders.json` + `Data/SeedData/Equipment.json`, `OfficialContentSeed.cs` a été retiré, voir plus
bas) venaient de la mémoire générale du modèle, pas d'une extraction fiable d'une des sources
ci-dessus. À vérifier contre le livre de règles et corriger via l'UI (le flux Official → Modified
existe précisément pour ça) si un chiffre est faux.

**Import bande par bande (2e lot, terminé)** : l'utilisateur a fourni 13 fichiers texte (extraits FR
bruts GW/GLM, mise en page dégradée par l'extraction PDF→texte — colonnes de tableaux mélangées,
accents cassés sur certains fichiers) pour Kermesse du Chaos, Chasseurs de Trésors Nains, Culte des
Possédés, Horde Orque, Pillards Hommes-Bêtes, Répurgateurs, Skavens (Clan Eshin — confirmé par
l'équipement signature griffes de combat/lames suintantes/Magie du Rat Cornu, pas Pestilens), Sœurs de
Sigmar, Kislévites, Mercenaires Averlanders/Ostlanders/Morts-Vivants et Mercenaires Marienburgers/
Middenheimers/Reiklanders (un seul fichier pour les 3 variantes — même roster de base, règles
spéciales et trésorerie de départ différentes par ville). Les 13 bandes sont maintenant toutes
intégrées (15 bandes au total avec Reiklander + les 2 pilotes du premier lot). Méthode : croiser ces
textes avec mordheimer.net (EN) pour combler les trous de mise en page/vérifier les chiffres/compléter
les tables de sorts absentes du texte FR fourni (Prières de Sigmar pour Répurgateurs/Sœurs de Sigmar,
Magie du Rat Cornu pour Skavens — sections "Magie" vides à l'extraction) avant d'écrire chaque JSON,
bande par bande, comme pour le premier lot. Tout jeteur de sorts, quelle que soit la bande, réutilise
la même règle spéciale générique « Wizard »/« Sorcier » plutôt qu'une règle dédiée par bande —
l'affiliation à une école de magie passe uniquement par `WarbandSeedData.MagicSchools`/
`WarriorSeedData.IsSpellcaster` (voir ROADMAP.md § V1 pour le détail bande par bande et les limites
connues).

**Fait** : Reiklander a migré vers le pipeline JSON (`Reiklanders.json`). Les 3 variantes Mercenaires
(Reiklander/Middenheim/Marienburg) partagent un même roster de base (Capitaine/Champion/Jeune Loup/
Guerrier/Tireur/Bretteur, cf. mordheimer.net) mais divergent sur les règles spéciales : Reiklander a
"Discipline Militaire" (12ps au lieu de Chef 6ps, confirmé texte exact sur mordheimer.net) sur son
Capitaine + le bonus +1 CT des Tireurs cuit directement dans leur profil ; Middenheim démarre Capitaine/
Champion à Force 4 ; Marienburg démarre à 600 CO + a la règle "Négociants Fortunés" (bonus objets rares
+ budget recrutement).

**Fait (données communes centralisées)** : `Data/SeedData/SpecialRules.json`/`Equipment.json`/
`Mutations.json`/`Skills.json`/`MagicSchools.json` — un fichier par catalogue vraiment partagé (au lieu
d'un fichier "Common" fourre-tout), seedés en premier dans `AppDatabase.SeedOfficialContentAsync` avant
les 15 fichiers de bande. `OfficialContentSeed.cs` (l'ancien pool d'équipement commun écrit à la main,
`CoreEquipment`/`CoreEquipmentFr` par position de tableau) est **retiré**, entièrement absorbé par
`Equipment.json`. Un fichier de bande ne déclare plus que ce qui lui est vraiment propre ; pour une
règle/mutation/école commune (ex. "Leader"), il porte juste un stub `{name: {en, fr}}` **sans
description** — nécessaire pour que la ligne de jointure par guerrier/bande (`WarriorArchetype
SpecialRuleEntity` etc.) se crée quand même, la description venant du fichier commun via le cache
find-or-create déjà existant (`FindOrCreateSpecialRuleAsync`/`FindOrCreateMutationAsync`/
`FindOrCreateMagicSchoolAsync`, inchangés). **Attention si un futur import ajoute une règle/mutation/
école qu'on pense déjà commune** : vérifier si son nom anglais existe déjà dans `SpecialRules.json`/
`Mutations.json`/`MagicSchools.json` avant de la redéclarer en toutes lettres dans le fichier de bande —
sinon ça marche quand même (le texte du fichier de bande est juste silencieusement ignoré au profit du
fichier commun s'il seed après), mais autant utiliser le stub directement. `Equipment.json` n'a **pas**
de mécanisme de dédup à l'exécution (contrairement aux 3 autres) : c'est un fichier unique écrit à la
main sans doublon interne, et les fichiers de bande ne déclarent plus aucun des noms qu'il contient — ce
refactor a d'ailleurs corrigé 3 vrais doublons à coût différent qui existaient avant lui (Arc court,
Fléau, Livre Saint, chacun réparti sur 2-3 bandes avec des prix contradictoires) en les fusionnant en une
seule entrée, prix vérifié contre mordheimer.net. `Skills.json` seede pour la première fois les ~34
compétences core du livre (Combat/Tir/Érudition/Force/Vitesse) — l'onglet Compétences du Codex était vide
jusque-là ; les tableaux de compétences spéciales par bande restent en texte libre dans une
`SpecialRule`, pas de vraies entrées `Skill` (décision explicite, restriction par héros à traiter plus
tard).

**Fait (vérification croisée post-import)** : les 15 bandes ont été vérifiées une seconde fois contre
BSData/mordheim (dépôt GitHub communautaire au format BattleScribe, `.cat` par bande + `data.cat`
partagé) en croisant chaque divergence trouvée contre mordheimer.net pour trancher (seule source de
confiance retenue, sur demande explicite de l'utilisateur — BSData sert de piste à vérifier, pas de
vérité). Sur 14 signalements, seuls 4 étaient de vraies erreurs (corrigées : règle du Capitaine
Reiklander, rareté du Long Fusil du Hochland, tableau de compétences du Tueur de Troll nain, exclusion
Personæ Dramatis des Rancuniers) ; le reste était soit déjà correct (BSData incomplet/silencieux sur
l'objet, ex. Sanglier de guerre/Arme Obsidienne absents de leur dépôt), soit une confusion de leur part.

**Fait (suite)** : Kermesse du Chaos et Culte des Possédés intégrées. Mutation a reçu le même
mécanisme de restriction par bande qu'Équipement/Compétence/Monture
(`RestrictedToWarbandArchetypeIds` + `WarbandArchetypeMutationEntity`), utilisé pour la première fois
sur les Bénédictions de Nurgle de Kermesse (exclusives aux Impurs) en contraste avec le pool de
Mutations génériques du Culte des Possédés (non-restreint, partagé avec les Pillards Hommes-Bêtes à
venir). Les deux bandes ont chacune leur propre `MagicSchool` distincte (Rituels de Nurgle vs Rituels
du Chaos) bien que thématiquement proches — confirmé sur mordheimer.net, ne pas les fusionner. La
Roulotte de la Peste de Kermesse (véhicule à 4 profils combinés) reste hors périmètre V1. Horde Orque
intégrée ensuite (1re bande à peupler `Mounts` dans son JSON — Sanglier de guerre, restreint à cette
bande, avec ses propres SpecialRules "Charge Furieuse"/"Peau Épaisse" — **retiré depuis, voir la note
sur la fusion Animal/Equipment plus bas : c'est du contenu Blazing Saddles 1b, pas le roster core d'Orc
Mob**). Cette bande a aussi fait apparaître un cas non couvert par le modèle : le Mouvement des Squigs
des cavernes n'est pas une
caractéristique fixe (2D6ps à chaque déplacement, comme sur la fiche officielle et mordheimer.net) alors
que `WarriorArchetype.Movement`/`Warrior.Movement` sont des `int`. Décision avec l'utilisateur (plutôt que
d'improviser un 0 ou une moyenne) : ajout de `MovementOverride` (`string?`, nullable) sur `WarriorArchetype`
et `Warrior` + `MovementDisplay` (= `MovementOverride ?? Movement.ToString()`), copié à la volée
(`ToWarrior`) et affiché partout où le Mouvement apparaît (fiche guerrier, dialogs d'édition) — `Movement`
reste un `int` de repli, `MovementOverride` prend le dessus dès qu'il est renseigné. À réutiliser si
Skavens ou une autre bande à venir présente le même genre de caractéristique non-fixe.

**Limite connue (Équipement)** : pas de dédup find-or-create par nom pour `EquipmentItem` (contrairement
à SpecialRule/Mutation/MagicSchool) — donc impossible d'étendre la restriction d'un objet déjà seedé
par une autre bande dans un JSON importé plus tard. L'Arme Obsidienne et l'Armure du Chaos du Culte
des Possédés (censées être partagées par 6 bandes selon le livre de règles, dont les Pillards
Hommes-Bêtes) sont pour l'instant restreintes à la seule bande Culte des Possédés en attendant un vrai
mécanisme de partage multi-bandes pour l'Équipement — à généraliser si/quand ça bloque un futur import
(probablement aux Pillards Hommes-Bêtes). **Toujours vrai après le refactor ci-dessous** : le nouveau
mécanisme `RestrictedToWarbandNames` ne couvre que les 3 catalogues **communs** (Equipment.json/
Skills.json/Mutations.json), pas un objet déclaré dans le fichier propre à une bande (comme l'Arme
Obsidienne, déclarée dans `CultOfThePossessed.json`) — la même résolution différée pourrait s'étendre à
ce cas si besoin, pas fait faute de cas d'usage réel pour l'instant.

**Fait (branche `feature/detail-dialog-service`, 2026-08-16) — restrictions multi-bandes génériques +
fusion Animal dans Equipment** : en complétant les restrictions des montures (Cheval/Destrier réservés
aux bandes humaines, Chien de guerre à toutes sauf les Skavens), une première passe avait dupliqué
l'entrée dans chaque fichier de bande concerné (8 copies de Cheval/Destrier, 14 de Chien de guerre) car
`RestrictedToThisWarband: bool` ne sait exprimer qu'une seule bande par entrée — décision reprise après
coup par l'utilisateur ("on a de la duplication de partout, c'est pas bon") en faveur d'un vrai
mécanisme. Deux volets :
1. **Restriction multi-bandes dans l'import JSON** : nouveau champ optionnel `RestrictedToWarbandNames`
   (liste de noms de fichier, ex. `["Reiklanders", "Kislevites"]`) sur `EquipmentSeedData`/
   `SkillSeedData`/`MutationSeedData` (`WarbandSeedData.cs`), à côté de `RestrictedToThisWarband` qui
   reste pertinent pour une entrée propre à une seule bande. Comme les 6 catalogues communs seedent
   avant les 15 fichiers de bande (`AppDatabase.SeedOfficialContentAsync`), aucune `WarbandArchetypeId`
   n'existe encore au moment de lire ce champ — résolution différée via `_warbandArchetypeIdsByFileStem`
   (peuplé dans `SeedWarbandFromJsonAsync`) + `_pendingSharedRestrictions`
   (`PendingSharedRestriction`/`SharedRestrictionKind`), résolus en une passe finale après le dernier
   `SeedWarbandFromJsonAsync("Kislevites.json")`.
2. **Animal devient une catégorie d'`EquipmentItem`** (`EquipmentCategory.Animal`), sur demande explicite
   de l'utilisateur ("les animaux sont considérés comme équipement dans les règles ... on devrait faire
   en sorte que ce soit un héritier des équipements") : `Animal.cs`/`AnimalEntity.cs`/
   `WarbandArchetypeAnimalEntity.cs`/`AnimalSpecialRuleEntity.cs`/`AnimalSeedData`/`Animals.json`
   supprimés entièrement, `EquipmentItem`/`EquipmentItemEntity` gagnent les 9 champs de profil (nullable,
   seulement renseignés pour la catégorie Animal). `Warrior.Animal` est retypé `EquipmentItem?` (toujours
   un champ 0..1, pas une table de jointure). Toute l'UI dédiée (`Features/Library/Animals/*`,
   `AnimalPickerService`/`AnimalPickerNavigationService`, l'onglet Codex "Montures") est supprimée : les
   montures apparaissent désormais comme une catégorie filtrable dans l'onglet Trading Post existant.
   `IEquipmentPickerService.PickEquipmentAsync` gagne un paramètre optionnel `lockedCategory` (verrouille
   le picker sur une catégorie et désactive son bouton de changement) réutilisé par
   `WarriorEditDialogViewModel.SelectAnimal` pour ouvrir le même picker d'équipement que le reste,
   pré-filtré sur Animaux, plutôt qu'un picker dédié. Cheval/Destrier (8 bandes humaines) et Chien de
   guerre (toutes sauf Skavens, 14 bandes) rejoignent `Equipment.json` avec `restrictedToWarbandNames`.
   **Le Sanglier de guerre est retiré entièrement** plutôt que migré : vérifié sur mordheimer.net, c'est
   du contenu *Blazing Saddles* (Mordheim Annual 2002, Grade **1b**), pas le roster core d'Orc Mob
   (Grade **1a**, Town Cryer #6) — repéré par l'utilisateur, confirmé par la fiche mordheimer.net de
   l'objet ("WAR BOAR — Blazing Saddles (1b)") qui liste bien Cheval/Destrier/Chien de guerre comme
   `core`. Pas d'équivalent pour l'instant : aucune bande importée ne contient de contenu Blazing
   Saddles, donc rien à réintroduire ailleurs tant qu'un import futur ne l'exige pas explicitement.
   **Petit bug corrigé au passage** : `EquipmentItemViewModel.Edit()` recopiait l'`EquipmentItem`
   sélectionné dans une copie défensive sans les 9 champs de profil (Mouvement/CC/CT/F/E/PV/I/A/Cd) —
   la catégorie Animal ouvrait donc `EquipmentItemEditDialog` avec un profil vide alors que l'objet en
   base avait bien ses stats. Corrigé en complétant l'initialiseur de la copie.

**Fait (2026-08-16, même session) — helper d'édition Inclure/Exclure pour les restrictions par bande** :
signalé par l'utilisateur juste après le refactor ci-dessus - éditer une restriction "quasi-universelle"
(Chien de guerre : toutes les bandes sauf Skavens) dans `EquipmentItemEditDialog` affichait une douzaine
de chips à la fois, dialog ingérable. Priorité donnée explicitement à l'UI plutôt qu'au JSON ("pour moi
c'est surtout l'ui... ça fait une longueur ingérable dans le dialogue") - `RestrictedToWarbandNames`
(JSON, voir ci-dessus) reste une simple liste d'inclusion, inchangée.
- Nouveau `Components/WarbandRestrictionEditor.cs` : petit `ObservableObject` réutilisable (au sens
  propre du mot "helper" employé par l'utilisateur) qui factorise le bloc "chips + picker de bandes +
  Add/Remove" jusque-là dupliqué à l'identique dans `EquipmentItemEditDialogViewModel`/
  `SkillEditDialogViewModel`/`MutationEditDialogViewModel`. Ajoute un mode **Exclure**, purement une
  commodité d'édition/affichage - aucun changement de modèle/schéma, `RestrictedToWarbandArchetypeIds`
  reste une simple liste d'inclusion.
  **Révisé dans la foulée** (l'utilisateur a signalé juste après : en repassant "toutes sauf X" à zéro
  exclusion il fallait que ça revienne à "commun à toutes" — corrigé une première fois par calcul du
  complément, puis l'utilisateur a demandé plus simple : *"dans une liste ou dans une autre il faut
  laisser la liste vide et elle doit n'être alimentée que par l'utilisateur quand on appuie sur le +...
  l'excluse ou la restriction en soit c'est 2 listes, si un élément est dans l'une, elle n'est pas dans
  l'autre et inversement"* — confirmé via `AskUserQuestion` : **jamais de recalcul automatique**, ni à
  l'ouverture ni au bascule). Design : **deux listes littérales indépendantes** `_included`/`_excluded`,
  jamais dérivées l'une de l'autre par complément *pendant l'édition*. `_included` est initialisée
  depuis `RestrictedToWarbandArchetypeIds` (données réelles) ; basculer de mode en cours d'édition
  n'alimente/ne recalcule jamais rien, chaque liste ne grandit que via le "+". Les deux restent
  mutuellement exclusives (ajouter une bande à la liste active la retire de l'autre) mais rien ne les
  garde synchronisées au-delà. `SelectedIds` calcule le complément contre `AllWarbandArchetypes`
  **seulement en mode Exclure et seulement au moment de lire la valeur à persister** (jamais pour
  l'affichage des chips) - une liste exclue vide se persiste bien comme liste vide ("commun à toutes"),
  pas comme "toutes les bandes listées explicitement".
  **Revenu dessus une 3e fois** (l'utilisateur, en reprenant le contrôle : *"si on exclue un bande et
  qu'on réouvre l'edit, on a toute les bande en restrinct pour"* - conséquence directe de "jamais de
  recalcul même à l'ouverture" : rouvrir Chien de guerre montrait ses 14 bandes en mode Inclure).
  Distinction retenue après confirmation `AskUserQuestion` : le risque initial (bug du 2026-08-16)
  venait de recalculer le complément à **chaque bascule pendant l'édition**, y compris depuis un item
  vraiment non-restreint (0 incluse) où "le complément" n'a aucun sens (ça note "tout exclu"). Calculer
  le complément **une seule fois, à la construction du dialog**, à partir des données déjà sauvegardées,
  n'a pas ce problème (lecture fidèle de l'existant, rien à corrompre) - seulement si la restriction est
  **réellement partielle** (`_included.Count` strictement entre 0 et le total de bandes). Dans ce cas
  précis, `_excluded` est peuplée une fois par complément et le mode de départ choisi pour afficher le
  moins de chips possible (`_included.Count > total/2` ⇒ Exclure) ; un item non-restreint (0 incluse) ou
  explicitement listé en entier démarre toujours en mode Inclure, `_excluded` vide - pas de changement
  là. Toucher ensuite au bouton bascule (`IconTextButton`, glyphe `RightLeft`) en cours d'édition ne
  recalcule toujours rien, comme avant.
- `SkillEditDialogViewModel` est le seul cas à deux niveaux (restriction bande + restriction guerrier
  narrowée aux bandes restreintes) - `WarbandRestrictionEditor.Changed` (event) notifie quand l'ensemble
  inclus change (peu importe si via le mode Inclure ou Exclure) pour purger les guerriers dont la bande
  n'est plus incluse, et le picker de guerriers (`AddRestrictedWarriorCommand`) narrowe toujours sur
  `WarbandRestriction.SelectedIds` (l'ensemble inclus réel), jamais sur les chips affichés qui peuvent
  être les bandes exclues.
- Nouvelles clés resx : `LibRestrictedToAllExceptPh`/`LibRestrictionSwitchToExcludePh`/
  `LibRestrictionSwitchToIncludePh` (fr/en).
- **Extension aux dialogs récap en lecture seule : retirée puis réintroduite dans la même session.**
  Première passe : `Components/WarbandRestrictionDisplay.cs` (contrepartie statique de l'éditeur, sans
  picker) calculait le complément pour `EquipmentItemDetailDialogViewModel`/`SkillDetailDialogViewModel`/
  `MutationDetailDialogViewModel`. Retirée entièrement quand le design de l'éditeur est passé aux deux
  listes jamais recalculées (ci-dessus) - par excès de prudence, en assimilant à tort la règle de
  l'éditeur ("jamais de recalcul, pour ne pas corrompre une sauvegarde") à la lecture seule, qui n'a
  pourtant aucun chemin de sauvegarde à corrompre. L'utilisateur a fait remarquer la conséquence
  concrète : exclure une seule bande (ex. Skavens) affiche ensuite "Réservé à" 14 bandes dans le
  readonly - exactement le mur de chips que toute cette fonctionnalité visait à éviter. Confirmé via
  `AskUserQuestion` : le calcul du complément est **sûr en lecture seule** (recalculé à chaque ouverture
  depuis les données réelles, jamais persisté) contrairement à l'éditeur (où togglé sans y toucher puis
  sauvegarder écrirait la mauvaise chose). `WarbandRestrictionDisplay.cs` réintroduit tel quel, reconnecté
  aux 3 `XxxDetailDialogViewModel` + aux 3 méthodes de `DetailDialogService` (qui gardent désormais la
  liste complète des bandes récupérée pour `restrictedWarbands`, au lieu de la jeter après le filtre).
  **Bug distinct corrigé juste avant, dans `WarbandRestrictionEditor.SelectedIds`** (éditeur, pas la
  recap) : en mode Exclure avec `_excluded` vide (bascule en Exclure puis sauvegarde sans rien exclure),
  `Where(...All(...))` sur une liste vide est vacuously vrai pour toute bande, donc ça persistait le
  catalogue entier explicitement au lieu d'une liste vide ("commun à toutes"). `_excluded.Count == 0` est
  maintenant spécial-cassé vers la liste vide, cohérent avec ce que `HeaderText` affichait déjà. Signalé
  par l'utilisateur en deux temps : d'abord ce cas à zéro exclusion (corrigé), puis séparément le cas à
  une exclusion réelle (Chien de guerre en montre bien 14 en readonly, correct côté données - c'est le
  retrait de `WarbandRestrictionDisplay` qui en faisait un mur de chips, résolu par sa réintroduction
  ci-dessus).
- **Contrôle unique** (suite à la remarque de l'utilisateur "on ne le ferait pas en control unique...
  pour centraliser la gestion") : le bloc `ChipListView` + `IconTextButton` de bascule, jusque-là
  dupliqué à l'identique dans les 3 dialogs d'édition, est remplacé par `Components/WarbandRestriction/
  WarbandRestrictionEditorView.xaml(.cs)` (une seule `BindableProperty Editor`, type
  `WarbandRestrictionEditor`) - chaque dialog se réduit à
  `<components:WarbandRestrictionEditorView Editor="{Binding WarbandRestriction}"/>`.

mordheimer.net bloque WebFetch direct (403) — passer par le Browser pane (`preview_start` +
`get_page_text`) fonctionne.

## Règles de collaboration

- **Ne jamais committer sans relecture explicite de l'utilisateur** — toujours demander avant,
  même quand tout compile et que les tests passent.
- **Messages de commit courts et précis** — une seule phrase suffit généralement (sujet + portée),
  pas de corps explicatif multi-paragraphes. Le detail vit dans le code/les tests, pas le message.
- Pas de moteur de règles / calculateur de combat en V1 : les règles spéciales restent du texte
  libre (`Description`/`Notes`), appliqué manuellement par le joueur. Voir ROADMAP.md § V3.
- Build de référence pour valider une modification : `dotnet build MordheimLedgerApp.csproj -f
  net10.0-windows10.0.19041.0` (le seul target testable sur cette machine Windows) +
  `dotnet test MordheimLedgerApp.Tests`.
- **Depuis la passe tuiles du Codex : dev par branche de fonctionnalité** (`feature/<nom>`), plus
  directement sur `master` — la base est maintenant stable, à protéger.
- La taille des tuiles du Codex (`ResponsiveGridSpanBehavior.TileWidth`, calcul du span de colonnes) a
  été temporairement centralisée en constante C# (`DefaultTileWidth`) référencée par XAML via
  `x:Static`, puis explicitement annulée par l'utilisateur ("on retourne en arrière") en faveur du
  défaut de classe posé tel quel par fichier — **ne pas retenter cette centralisation sans redemander**,
  la décision était volontaire, pas un oubli de nettoyage. Contrairement à une version antérieure de
  cette note, aucun fichier XAML ne déclare plus `TileWidth="130"` explicitement aujourd'hui : le span se
  base sur le défaut de classe (140) plutôt que sur la taille visuelle réelle de la tuile (130,
  `CodexTileFrameStyle`) — vérifié fonctionnel à l'usage (2026-08-15), donc pas une régression à corriger
  sans y être invité.
