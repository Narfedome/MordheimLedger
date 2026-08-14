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
bande, avec ses propres SpecialRules "Charge Furieuse"/"Peau Épaisse"). Cette bande a aussi fait
apparaître un cas non couvert par le modèle : le Mouvement des Squigs des cavernes n'est pas une
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
