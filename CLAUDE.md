# Mordheim Ledger — Notes de contexte pour Claude

Application MAUI (.NET 10) de suivi de bandes et de campagnes Mordheim. Contexte produit et
avancement détaillé : [ROADMAP.md](ROADMAP.md). Ce fichier est là pour reprendre le fil d'une
machine/session à l'autre (l'utilisateur développe depuis plusieurs PC).

## Architecture

Reprend délibérément les patterns du projet sœur **DmTools** (`D:\Dev\perso\DmTools`, même
auteur, appli MAUI mature) plutôt que de réinventer :

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
Une seule palette pour l'instant (pas de sélecteur multi-thèmes comme dans DmTools) — grim/cendre
+ vert wyrdstone en accent, à affiner une fois qu'il y a de vrais écrans à regarder. Icônes et
identité visuelle définitive : pas encore tranchées.

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
