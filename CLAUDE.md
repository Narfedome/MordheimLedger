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

**Import bande par bande (2e lot, en cours)** : l'utilisateur a fourni 13 fichiers texte (extraits FR
bruts GW/GLM, mise en page dégradée par l'extraction PDF→texte — colonnes de tableaux mélangées,
accents cassés sur certains fichiers) pour Kermesse du Chaos, Chasseurs de Trésors Nains (déjà
intégrée, fichier de relecture), Culte des Possédés, Horde Orque, Pillards Hommes-Bêtes,
Répurgateurs, Skavens (Clan Eshin — confirmé par l'équipement signature griffes de combat/lames
suintantes/Magie du Rat Cornu, pas Pestilens), Sœurs de Sigmar, Kislévites, Mercenaires Averlanders/
Ostlanders/Morts-Vivants (déjà intégrées, fichiers de relecture) et Mercenaires Marienburgers/
Middenheimers/Reiklanders (un seul fichier pour les 3 variantes — même roster de base, règles
spéciales et trésorerie de départ différentes par ville). Méthode : croiser ces textes avec
mordheimer.net (EN) pour combler les trous de mise en page/vérifier les chiffres avant d'écrire
chaque JSON, bande par bande, comme pour le premier lot.

**Fait** : Reiklander a migré vers le pipeline JSON (`Reiklanders.json`), `OfficialContentSeed.cs` ne
contient plus que l'équipement commun (`CoreEquipment`). Les 3 variantes Mercenaires (Reiklander/
Middenheim/Marienburg) partagent un même roster de base (Capitaine/Champion/Jeune Loup/Guerrier/
Tireur/Bretteur, cf. mordheimer.net) mais divergent sur les règles spéciales : Reiklander a
"Discipline Militaire" (12ps au lieu de Chef 6ps) sur son Capitaine + le bonus +1 CT des Tireurs cuit
directement dans leur profil ; Middenheim démarre Capitaine/Champion à Force 4 ; Marienburg démarre à
600 CO + a la règle "Négociants Fortunés" (bonus objets rares + budget recrutement). Plusieurs armes/
armures communes manquantes (Masse, Hallebarde, Arbalète, Pistolet, Armure Lourde, etc.) ont été
ajoutées non-restreintes via `Reiklanders.json` plutôt que dans `OfficialContentSeed.CoreEquipment`
(pas de mécanisme de dédup par nom pour l'Équipement comme il en existe pour SpecialRule/Mutation/
MagicSchool — attention à ne pas re-déclarer un nom déjà présent dans un futur import, ça créerait un
doublon).

**Fait (suite)** : Kermesse du Chaos et Culte des Possédés intégrées. Mutation a reçu le même
mécanisme de restriction par bande qu'Équipement/Compétence/Monture
(`RestrictedToWarbandArchetypeIds` + `WarbandArchetypeMutationEntity`), utilisé pour la première fois
sur les Bénédictions de Nurgle de Kermesse (exclusives aux Impurs) en contraste avec le pool de
Mutations génériques du Culte des Possédés (non-restreint, partagé avec les Pillards Hommes-Bêtes à
venir). Les deux bandes ont chacune leur propre `MagicSchool` distincte (Rituels de Nurgle vs Rituels
du Chaos) bien que thématiquement proches — confirmé sur mordheimer.net, ne pas les fusionner. La
Roulotte de la Peste de Kermesse (véhicule à 4 profils combinés) reste hors périmètre V1.

**Limite connue (Équipement)** : pas de dédup find-or-create par nom pour `EquipmentItem` (contrairement
à SpecialRule/Mutation/MagicSchool) — donc impossible d'étendre la restriction d'un objet déjà seedé
par une autre bande dans un JSON importé plus tard. L'Arme Obsidienne et l'Armure du Chaos du Culte
des Possédés (censées être partagées par 6 bandes selon le livre de règles, dont les Pillards
Hommes-Bêtes) sont pour l'instant restreintes à la seule bande Culte des Possédés en attendant un vrai
mécanisme de partage multi-bandes pour l'Équipement — à généraliser si/quand ça bloque un futur import
(probablement aux Pillards Hommes-Bêtes).

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
