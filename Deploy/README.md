# Publier une version de Mordheim Ledger

Tout ce qui sert à construire, signer et publier une version vit dans ce dossier `Deploy\`. Ce document
décrit chaque script, ce qu'il faut sur un PC pour les lancer, et l'ordre d'une release complète.
Les détails d'implémentation restent dans l'en-tête de chaque script (`Get-Help .\Build-Release.ps1 -Full`).
L'obscurcissement R8 d'Android a sa propre note : [R8-Obfuscation.md](R8-Obfuscation.md).

## Vue d'ensemble

Une release se lance d'un clic droit sur l'un des lanceurs `Release-*.ps1` > *Exécuter avec PowerShell*.
Tous appellent `Build-Release.ps1`, qui enchaîne :

1. **Version** : `AppVersionMajor.AppVersionMinor` du csproj + nombre de commits git
   (`git rev-list --count HEAD`), comme la cible `SetVersionFromGit` du csproj. L'appli, l'installeur,
   le MSIX et l'AAB portent donc le même numéro.
2. **Windows** : installeur Inno Setup + MSIX, soumis au Microsoft Store via `msstore`.
3. **Android** : AAB signé, publié sur Google Play via `PlayPublisher.cs`.
4. **GitHub Release** `v<version>` avec l'installeur Windows en pièce jointe.
5. **Site** : appel du Build Hook Netlify, qui redéploie le site ; au build, le site télécharge
   l'installeur de la dernière GitHub Release.

Les envois aux stores et le Build Hook ne bloquent pas la suite : en cas d'échec, un avertissement
s'affiche et le fichier reste dans `Deploy\` pour un envoi à la main. Un échec de build ou de
`gh release create` arrête tout.

| Lanceur | Appelle | Ce qu'il publie |
|---|---|---|
| `Build-Release.ps1` (sans option) | — | Windows + Android, GitHub Release, site. Images des fiches inchangées. |
| `Release-Complete.ps1` | `-Screenshots -UploadListingImages` | Refait les captures, puis tout publier avec les nouvelles images des fiches. |
| `Release-AvecImages.ps1` | `-UploadListingImages` | Tout, et remplace les images des fiches par celles de `StoreListing\output\`, sans refaire les captures. |
| `Release-WindowsSeul.ps1` | `-SkipAndroid` | Installeur, Microsoft Store, GitHub Release, site. |
| `Release-AndroidSeul.ps1` | `-SkipWindows` | Google Play seulement (ni GitHub Release ni site). |
| `Build-Test.ps1` | — | Rien : build de test en local (voir plus bas). |

## Installer un nouveau PC

À faire une fois par machine.

### Outils

| Outil | Pour | Installation |
|---|---|---|
| SDK .NET 10 + workload MAUI | tous les builds, `PlayPublisher.cs` (fichier C# lancé par `dotnet run`) | `dotnet workload install maui` |
| Inno Setup 6 | installeur Windows | attendu à `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` |
| GitHub CLI (`gh`) | GitHub Release | `winget install GitHub.cli`, puis `gh auth login` |
| Microsoft Store Developer CLI (`msstore`) | soumission au Microsoft Store | `winget install "Microsoft Store Developer CLI"` |
| Inkscape, Google Chrome | images des fiches (`Build-StoreListing.ps1`) | chemins par défaut sous `C:\Program Files\` |
| Émulateur Android + `adb` + Appium | captures d'écran (`Capture-Screenshots.ps1`) | voir [MordheimLedgerApp.UITests/README.md](../MordheimLedgerApp.UITests/README.md) |

### Fichiers secrets (jamais commités)

Les trois fichiers ci-dessous sont dans le `.gitignore`. Ils se récupèrent sur le **Drive partagé** et se
déposent dans `Deploy\`, à côté de `Build-Release.ps1`.

| Fichier | Rôle | Si absent |
|---|---|---|
| `mordheimledger-release.keystore` | clé d'upload Android (Google re-signe avec Play App Signing) | signature avec le `debug.keystore` du PC : différente d'une machine à l'autre, à ne pas publier |
| `Build-Release.local.ps1` | identifiants (variables d'environnement ci-dessous), chargé automatiquement | pas de signature de release, pas de Store, pas de redéploiement du site |
| clé JSON du compte de service Google (`*.json` de type `service_account`) | publication Google Play | AAB à envoyer à la main dans la Play Console |

`Build-Release.local.ps1` se crée à partir de [Build-Release.local.ps1.example](Build-Release.local.ps1.example),
qui explique où trouver chaque valeur. Le plus simple est de copier celui du Drive. Variables qu'il définit :

| Variable | Utilisée par |
|---|---|
| `MORDHEIMLEDGER_KEYSTORE_ALIAS`, `_STOREPASS`, `_KEYPASS` | signature de l'AAB |
| `MORDHEIMLEDGER_NETLIFY_BUILD_HOOK` | redéploiement du site (optionnel) |
| `MORDHEIMLEDGER_MSSTORE_PRODUCT_ID`, `_SELLER_ID`, `_TENANT_ID`, `_CLIENT_ID`, `_CLIENT_SECRET` | soumission Microsoft Store (optionnel : sans `PRODUCT_ID`, MSIX à envoyer à la main) |
| `MORDHEIMLEDGER_PLAY_SERVICE_ACCOUNT_JSON` | chemin de la clé Google si elle n'est pas dans `Deploy\` (optionnel) |
| `MORDHEIMLEDGER_PLAY_TRACK` | piste Google Play : `production` par défaut, sinon `beta`, `alpha`, `internal` (optionnel) |

Côté Netlify (pas sur le PC) : la variable d'environnement `MORDHEIMLEDGER_GITHUB_TOKEN`, un jeton GitHub
en lecture seule sur ce dépôt, permet à `Website/scripts/fetch-downloads.js` de lire les GitHub Releases.

## Checklist avant une release

- [ ] Tout est fusionné sur `master` ; lancer depuis `master` à jour, arbre de travail propre. Le numéro de
      version dépend du nombre de commits : une release lancée d'une branche aurait un numéro qui ne
      correspond à rien sur `master`.
- [ ] Nouvelle version majeure ou mineure : `AppVersionMajor` / `AppVersionMinor` mis à jour dans
      `MordheimLedgerApp/MordheimLedgerApp.csproj`.
- [ ] **Section de la version écrite dans les deux changelogs du site** : un bloc
      `<div class="changelog-entry"><h2>majeure.mineure</h2>…</div>` dans
      [Website/changelog.html](../Website/changelog.html) (français) et
      [Website/en/changelog.html](../Website/en/changelog.html) (anglais). Cette même section sert à trois endroits :
      la page Changelog du site, les notes « Nouveautés » de Google Play, et le dialog « Nouveautés » de l'appli
      (les deux fichiers sont embarqués dans l'appli). Sans elle, Google Play part sans notes et l'appli
      n'affiche rien après la mise à jour. Pour vérifier le texte extrait sans rien publier :
      `dotnet run Deploy\PlayPublisher.cs -- --changelog Website --changelog-version 1.5 --notes-only`
      (depuis la racine du dépôt). Google Play limite les notes à 500 caractères par langue.
- [ ] Changement visible dans les captures (écran, charte, icône) : prévoir `Release-Complete.ps1`
      (voir « Captures et images des fiches »).
- [ ] Les trois fichiers secrets sont dans `Deploy\` et `gh auth status` répond.
- [ ] En cas de doute sur Android : `Build-Test.ps1 -SkipWindows`, puis installer l'APK sur l'émulateur.

## Déroulé d'une release

1. Cocher la checklist ci-dessus.
2. Si les captures doivent changer, lancer `Release-Complete.ps1` (émulateur démarré, Appium lancé,
   Mordheim Ledger fermé sur le PC). Si une capture a changé, le script s'arrête **avant de construire quoi que
   ce soit** : faire passer `Website/assets/screenshots/`, `Deploy/StoreListing/windows-screenshots/` et
   `Deploy/StoreListing/output/` par une PR, la fusionner, puis lancer `Release-AvecImages.ps1`
   (inutile de refaire les captures).
3. Sinon, lancer `Build-Release.ps1` (ou un des lanceurs du tableau).
4. Lire le bilan dans la fenêtre, qui reste ouverte jusqu'à Entrée. Chercher les lignes jaunes
   (avertissements) : un store non soumis laisse son fichier dans `Deploy\MicrosoftStore\` ou
   `Deploy\PlayStore\`.
5. **Partner Center, à la main** : quelques minutes après l'envoi, la soumission peut s'arrêter sur la
   question de la fonctionnalité restreinte `runFullTrust` (voir « Justification de `runFullTrust` »).
   Ouvrir la soumission dans Partner Center, coller la justification et valider : sans ça, elle ne passe
   jamais en certification.
6. Suivre l'examen dans la Play Console et Partner Center. Les deux stores mettent en ligne après leur
   propre validation.

Les commentaires du bot Netlify « deploy preview canceled » sur les PR sont normaux : `netlify.toml`
ignore les builds déclenchés par git (`ignore = "exit 0"`), le site ne se redéploie que par le Build Hook
ou à la main.

## Les scripts en détail

### `Build-Release.ps1`

Le script de release, décrit dans « Vue d'ensemble ». Options : `-SkipWindows`, `-SkipAndroid`,
`-Screenshots`, `-UploadListingImages`, `-StoreDraft`, `-NoPause` (pas d'attente d'Entrée à la fin,
pour un lancement depuis un terminal).

Produit :

| Fichier | Contenu |
|---|---|
| `Website\downloads\MordheimLedgerInstaller.exe` | installeur de la version, nom fixe pour le lien du site (gitignoré) |
| `Deploy\Installer\MordheimLedgerInstaller-<version>.exe` | archive de chaque installeur |
| `Deploy\MicrosoftStore\MordheimLedger-<version>.msix` | MSIX non signé (Partner Center le signe) |
| `Deploy\PlayStore\MordheimLedger-<version>.aab` | AAB signé |
| GitHub Release `v<version>` | installeur Windows |

Points à connaître :

- **Windows** : deux `dotnet publish` autonomes (runtime .NET et Windows App SDK embarqués). Les dossiers
  `obj` Windows sont vidés entre l'installeur et le MSIX, sinon l'appli plante au démarrage
  (`REGDB_E_CLASSNOTREG`). L'installeur est compilé par `ISCC.exe` avec [Installer.iss](Installer.iss).
- **Microsoft Store** : avant `msstore publish`, le script demande lui-même un jeton et interroge l'API de
  soumission, pour distinguer un secret refusé, un 403 ou un Store ID inconnu. **Un 403** veut dire que l'appli
  Entra n'a pas de rôle valide dans Partner Center (Paramètres du compte > Gestion des utilisateurs >
  Applications Microsoft Entra, rôle Manager). Déjà rencontré : la ligne existait sans rôle, corrigé en la
  supprimant puis en rajoutant l'appli. `msstore publish` reçoit un **dossier** (`-i`) qui ne contient que le
  MSIX de cette version. `-StoreDraft` crée la soumission en brouillon, pour tester les identifiants : la
  supprimer ensuite dans Partner Center.
- **Justification de `runFullTrust`** : Partner Center détecte cette fonctionnalité restreinte dans le MSIX
  et demande pourquoi l'appli en a besoin. Elle est indispensable (une appli MAUI sous Windows est une appli
  de bureau WinUI 3 empaquetée, qui ne démarre pas sans elle) : ne pas la retirer du manifeste. L'API ne
  permet pas de répondre, d'où l'étape à la main du déroulé. Demandée pour la 1.5 (novembre 2026) ;
  vérifier à la release suivante si elle revient à chaque MSIX. Texte à coller :

  > Mordheim Ledger is a .NET MAUI application. On Windows, .NET MAUI apps are packaged WinUI 3 desktop
  > (Win32) applications, and the runFullTrust capability is required for any packaged desktop app to
  > launch its executable. The app does not use it to access anything beyond what a standard desktop
  > application needs: it stores the user's warband data in a local SQLite database in its own app data
  > folder and reads image files the user explicitly picks (warrior photos). It does not install drivers
  > or services, does not modify system settings, and does not access other applications' data.
- **Android** : build toujours reparti de zéro (un `obj` laissé par un build Debug a déjà donné un AAB sans
  polices d'icônes). Le script vérifie ensuite que chaque police de `Resources\Fonts` est dans l'AAB et
  refuse de publier sinon. R8 est actif en Release : le `mapping.txt` est envoyé à Google Play avec l'AAB.
- **Site** : seul l'installeur Windows est sur le site. Android n'est distribué que par Google Play.

### `Release-*.ps1`

Lanceurs d'une ligne, à exécuter d'un clic droit, qui appellent `Build-Release.ps1` avec les options du
tableau de la vue d'ensemble.

### `PlayPublisher.cs`

Fichier C# unique lancé par `dotnet run` (application « fichier unique » de .NET 10, le package NuGet
Google.Apis.AndroidPublisher se restaure au premier lancement). Appelé par `Build-Release.ps1`, il ouvre un
« edit » de la Play Console, envoie l'AAB et `mapping.txt`, affecte la version à la piste avec les notes
extraites des changelogs, puis valide l'edit (envoi en examen). Avec `--listing-images`, il remplace aussi
les captures téléphone et l'image de présentation. Notes et images vont à chaque langue qui existe déjà
sur la fiche (aujourd'hui en-GB, la langue par défaut, et fr-FR) : contenu français pour `fr-*`, anglais
pour les autres. Le script ne crée jamais de nouvelle langue.

Lancement à la main, depuis la racine du dépôt :

```bash
dotnet run Deploy/PlayPublisher.cs -- --changelog Website --changelog-version 1.5 --notes-only
```

Options utiles hors release : `--notes-only` (affiche les notes extraites, n'envoie rien), `--dry-run`
(fait tout sauf valider l'edit : rien n'est publié). La liste complète est en tête du fichier.

Compte de service : un compte Google Cloud dont le projet a l'API « Google Play Android Developer API »,
invité dans Play Console > Utilisateurs et autorisations avec « Afficher les informations sur
l'application » et « Mettre les applications à disposition de tous les utilisateurs », plus le droit de
modifier la fiche Play Store pour `--listing-images` (sans lui, la validation de l'edit renvoie probablement un 403). La même clé sert pour
toutes les applis du compte.

### `Build-Test.ps1`

Même build que la release (même version, même signature), sans rien publier : ni stores, ni GitHub
Release, ni site. Produit en plus un APK de test dans `Deploy\Telephone\MordheimLedger-<version>.apk`.
Attention : l'installeur écrase celui de `Website\downloads\`. Options `-SkipWindows` et `-SkipAndroid`.

### Captures et images des fiches (`StoreListing\`)

- **`Capture-Screenshots.ps1`** refait les 6 captures depuis le code actuel, sur une base de démonstration
  fixe (`screenshot-data\`). Android : APK Debug sur l'émulateur (`emulator-5554` ou
  `MORDHEIM_E2E_DEVICE`), piloté par Appium (`MordheimLedgerApp.UITests/StoreScreenshots.cs`), vers
  `Website\assets\screenshots\`. Windows : appli Debug pilotée par UI Automation et toucher simulé
  (`WindowsUi.ps1`, la souris ne bouge pas), vers `windows-screenshots\`. Les données Windows du PC sont
  sauvegardées avant et restaurées après. Enchaîne sur `Build-StoreListing.ps1`. Options `-SkipAndroid`,
  `-SkipWindows`, `-SkipBuild`, `-NoPause`.
- **`Build-StoreListing.ps1`** génère les images des fiches dans `output\` : icône 512, images de
  présentation FR/EN (`feature-graphic.html` via Chrome headless), captures recadrées au format de chaque
  store, affiche, logo et tuiles du Microsoft Store (`microsoft-store-art.html`). À relancer après un
  changement d'icône, de charte ou de captures.
- **`WindowsUi.ps1`** : fonctions de pilotage de l'appli Windows, chargées par `Capture-Screenshots.ps1`.

Les images partent vers les stores seulement avec `-UploadListingImages`. Côté Microsoft Store, `msstore`
ne sait pas envoyer d'images : le script crée la soumission en brouillon, y remplace images et MSIX par
l'API de Partner Center, puis la valide. En cas d'échec, la soumission reste en brouillon dans Partner Center.

### Site (`Website\`)

- `netlify.toml` : publie le dossier tel quel après `node scripts/fetch-downloads.js`, qui télécharge
  l'installeur de la dernière GitHub Release (avec `MORDHEIMLEDGER_GITHUB_TOKEN`). Les builds
  déclenchés par git sont ignorés.
- `serve.ps1` : serveur local pour relire le site (`.\Website\serve.ps1`, puis http://localhost:8532/).

## En cas de problème

| Symptôme | Cause probable |
|---|---|
| « signature avec le debug.keystore local » | keystore ou `Build-Release.local.ps1` absent de `Deploy\` |
| Microsoft Store : identifiants refusés | `CLIENT_SECRET` expiré, ou ID du secret copié à la place de sa valeur |
| Microsoft Store : 403 | appli Entra sans rôle Manager dans Partner Center (voir plus haut) |
| Google Play : 403 « The caller does not have permission » à la validation | cause probable : droit de modifier la fiche manquant au compte de service (avec `-UploadListingImages`) |
| Google Play : version refusée | versionCode déjà utilisé : la release a été lancée sans nouveau commit |
| Google Play : pas de notes de version | section `<h2>majeure.mineure</h2>` absente d'un des changelogs |
| « Polices absentes dans l'AAB » | relancer : le build repart de zéro |
| Plantage au démarrage en Release Android | règle R8 manquante, voir [R8-Obfuscation.md](R8-Obfuscation.md) |
| Microsoft Store : soumission bloquée avant la certification | justification de `runFullTrust` à donner dans Partner Center (voir plus haut) |
| Microsoft Store : aucune soumission récente dans Partner Center | lire l'avertissement jaune « Echec de la soumission Microsoft Store » de la release ; le MSIX reste dans `Deploy\MicrosoftStore\` |
| Le site garde l'ancien installeur | Build Hook non défini ou en échec : relancer le déploiement depuis Netlify |
