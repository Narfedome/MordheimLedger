# Tests de bout en bout (Android, Appium)

Parcours complets sur l'appli Android, pilotée par Appium (pilote UiAutomator2). Chaque test part d'une
installation neuve : les données de l'appli sont effacées au démarrage de la session, et les visites guidées sont
coupées (extra d'Intent `mordheim_e2e`, voir `MordheimLedgerApp/Services/E2EMode.cs`).

Scénarios (`WarbandScenarios.cs`) :

- **Création d'une bande** : accueil « Partir de zéro », assistant de création (Reiklanders, Capitaine, Champion,
  Guerrier), bande présente dans la liste.
- **Partie complète** : bande d'exemple chargée à l'accueil, partie lancée, Fin de partie jouée jusqu'au bout (dés
  d'Exploration sans double, autres jets lancés par l'appli), entrée « Victoire » dans l'Historique.

Ils ne tournent pas dans la CI et sont **ignorés** sans `MORDHEIM_E2E=1` : un `dotnet test` ordinaire ne les lance pas.

## Une fois par machine

```bash
winget install OpenJS.NodeJS.LTS
npm install -g appium
appium driver install uiautomator2
```

Il faut aussi le SDK Android (`ANDROID_HOME`) et Java 17, déjà là pour builder l'appli.

## Lancer

1. Démarrer un émulateur (par défaut `emulator-5554` ; sinon `MORDHEIM_E2E_DEVICE=<id adb>`).
2. Installer l'appli **avec ses assemblies dans l'APK**. En Debug, MAUI les pousse sinon dans les données de
   l'appli (Fast Deployment), que chaque test efface : l'appli ne démarrerait plus.

   ```bash
   dotnet build MordheimLedgerApp/MordheimLedgerApp.csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true -t:Install
   ```

3. Lancer les tests (le serveur Appium est démarré tout seul s'il ne tourne pas déjà) :

   ```bash
   MORDHEIM_E2E=1 dotnet test MordheimLedgerApp.UITests
   ```

Compter environ 2 minutes. En cas d'échec, une capture d'écran est enregistrée dans
`MordheimLedgerApp.UITests/bin/Debug/net10.0/TestResults/e2e/`.

## Écrire un test

Repérer les éléments par leur `AutomationId` (MAUI l'expose comme identifiant de ressource Android, indépendant de la
langue) avec `AppSession.Tap/Type/WaitFor/All`. En ajouter dans le XAML au besoin : un même identifiant peut servir à
plusieurs contrôles du même rôle (`AutoRollButton` sur tous les boutons de dé, `RecruitIncrementButton` sur chaque
ligne de recrutement). Pour un texte (nom de bande...), `WaitForText` accepte la version française et anglaise.
