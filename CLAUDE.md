# Mordheim Ledger — Notes de contexte pour Claude

Application MAUI (.NET 10) de suivi de bandes et de campagnes Mordheim. Contexte produit et
avancement détaillé : [ROADMAP.md](ROADMAP.md). Ce fichier est là pour reprendre le fil d'une
machine/session à l'autre (l'utilisateur développe depuis plusieurs PC).

## Architecture

Reprend délibérément les patterns du projet sœur **DmTools** (`D:\Dev\DmTools` sur cette machine —
le chemin peut différer sur les autres PC de l'utilisateur, même auteur, appli MAUI mature) plutôt
que de réinventer :

- `MordheimLedgerApp.Core` (net10.0, **sans dépendance MAUI**) : `Models/` (modèles purs),
  `Data/` (`Entities/` SQLite + `AppDatabase.cs` + `EntityMapping.cs`), `Services/` (CRUD)
- `MordheimLedgerApp` (tête MAUI) : `Features/<Domaine>/` (Page + Page.xaml.cs + ViewModel),
  `Components/Dialogs/` (Confirm/Prompt/ActionSheet réutilisables, portés de DmTools),
  `Services/` (LocalizationService/ThemeService/LoadingService), `BaseViewModel`,
  `Resources/Styles/` (Colors/Sizes/Styles = design tokens)
- `MordheimLedgerApp.Tests` (xUnit) : tests de mapping Entity↔Modèle + tests d'intégration des
  services sur une base SQLite temporaire (voir `DataServiceTests.cs`)

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

## Sources de contenu officiel (voir ROADMAP.md § Sources)

- Livre des Règles PDF fourni par l'utilisateur — **trop volumineux** pour l'outil Read (>100 Mo,
  y compris en ciblant des pages)
- Grande Librairie de Mordheim (FR, sites.google.com) — contenu principal en images sur le site,
  mais des PDF par bande plus réduits existent et sont probablement lisibles directement
- Broheim.net (EN) — terminologie officielle + règles core découpées en 3 parties plus petites
- Mordheimer.net (EN) — catalogue de bandes le plus complet, jusqu'à Grand Army 2a

**Important** : les stats saisies dans `MordheimLedgerApp.Core/Data/OfficialContentSeed.cs`
(Reiklander Mercenaries) viennent de la mémoire générale du modèle, pas d'une extraction fiable
d'une des sources ci-dessus. À vérifier contre le livre de règles et corriger via l'UI (le flux
Official → Modified existe précisément pour ça) si un chiffre est faux.

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
