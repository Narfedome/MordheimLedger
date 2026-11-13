<#
.SYNOPSIS
    Refait les captures d'ecran du site et des fiches Google Play / Microsoft Store depuis le code actuel, puis
    regenere les elements des fiches (Build-StoreListing.ps1).

.DESCRIPTION
    L'appli demarre sur une base de captures toute prete, toujours la meme, rangee dans screenshot-data\ (jamais
    embarquee dans l'appli) : la bande d'exemple + une bande de Soeurs de Sigmar, francais, theme sombre, accueil deja
    passe. Les scenarios ne font que parcourir les ecrans, dans l'ordre des legendes du site (Website\index.html,
    en\index.html) : 1 liste des bandes, 2 fiche de bande, 3 Codex, 4 modification d'un objet, 5 recrutement (assistant,
    rien n'est enregistre), 6 parametres.

    - Android (site + Google Play) : APK Debug (assemblies embarquees) installe sur l'emulateur, base deposee par
      run-as, puis scenario Appium MordheimLedgerApp.UITests/StoreScreenshots.cs -> Website\assets\screenshots\1..6.png
      (1080x2400). Il faut un emulateur demarre (par defaut emulator-5554, sinon MORDHEIM_E2E_DEVICE) et Appium (voir
      MordheimLedgerApp.UITests\README.md).
    - Windows (Microsoft Store) : appli Windows Debug lancee avec --mordheim-e2e (visites guidees coupees), pilotee
      par UI Automation et toucher simule (WindowsUi.ps1 : la souris ne bouge jamais, mais la fenetre reste au-dessus
      des autres pendant ~30 s), capturee par PrintWindow -> windows-screenshots\1..6.png. L'appli Windows n'ayant
      qu'un dossier de donnees (%LOCALAPPDATA%\Narfedome\Narfdme.MordheimLedger), celui de ce PC est sauvegarde avant,
      puis restaure et verifie par empreinte SHA-256 ; en cas d'ecart, la sauvegarde est gardee et son chemin affiche.
    - Puis Build-StoreListing.ps1 recadre le tout pour les stores (output\phone-N.png, output\microsoft-store\screenshot-N.png).

    Pour changer le contenu des captures : modifier la base dans l'appli (Android Debug), puis la recuperer avec
      adb exec-out run-as com.narfedome.mordheimledger cat files/mordheimledger.db3 > screenshot-data\mordheimledger.db3

.PARAMETER SkipAndroid
    Ne refait pas les captures Android.

.PARAMETER SkipWindows
    Ne refait pas les captures Windows.

.PARAMETER SkipBuild
    Reutilise l'APK deja installe et l'appli Windows deja compilee (bin\Debug).

.PARAMETER NoPause
    Pas de pause en cas d'echec (appel depuis Build-Release.ps1 ou un terminal).
#>

param(
    [switch]$SkipAndroid,
    [switch]$SkipWindows,
    [switch]$SkipBuild,
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"

$listingDir = $PSScriptRoot
$repoRoot   = Split-Path -Parent (Split-Path -Parent $listingDir)
$csproj     = Join-Path $repoRoot "MordheimLedgerApp\MordheimLedgerApp.csproj"
$dataDir    = Join-Path $listingDir "screenshot-data"

function Invoke-AndroidScreenshots {
    Write-Host "=== Android : captures du site et de Google Play ===" -ForegroundColor Cyan
    $device = if ($env:MORDHEIM_E2E_DEVICE) { $env:MORDHEIM_E2E_DEVICE } else { "emulator-5554" }
    # Via cmd : le message de demarrage du serveur adb, ecrit sur stderr, ferait sinon echouer PowerShell 5.1.
    if (-not ((cmd /c "adb devices 2>nul") -match "^$([regex]::Escape($device))\s+device")) {
        throw "Emulateur $device introuvable (adb devices) : demarrer l'AVD avant, ou definir MORDHEIM_E2E_DEVICE."
    }
    if (-not $SkipBuild) {
        dotnet build $csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true -p:AndroidPackageFormat=apk -t:Install "-p:AdbTarget=-s $device" -v:q -nologo
        if ($LASTEXITCODE -ne 0) { throw "Build/installation de l'APK echoue (code $LASTEXITCODE)." }
    }
    $shotsDir = Join-Path $repoRoot "Website\assets\screenshots"
    $env:MORDHEIM_E2E = "1"
    $env:MORDHEIM_SCREENSHOTS_DIR = $shotsDir
    $env:MORDHEIM_SCREENSHOTS_DATA = $dataDir
    try {
        dotnet test (Join-Path $repoRoot "MordheimLedgerApp.UITests") --filter "FullyQualifiedName~StoreScreenshots" -nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw "Scenario Appium des captures echoue (code $LASTEXITCODE) : voir MordheimLedgerApp.UITests\bin\Debug\net10.0\TestResults\e2e\." }
    }
    finally {
        Remove-Item Env:MORDHEIM_SCREENSHOTS_DIR, Env:MORDHEIM_SCREENSHOTS_DATA, Env:MORDHEIM_E2E -ErrorAction SilentlyContinue
    }
    Get-ChildItem $shotsDir -Filter *.png | ForEach-Object { Write-Host "  $($_.FullName)" -ForegroundColor Green }
}

# preferences.json -> preferences.dat de MAUI sous Windows (dictionnaire JSON, valeurs en texte).
function Write-WindowsPreferences([string]$Path) {
    $prefs = Get-Content (Join-Path $dataDir "preferences.json") -Raw | ConvertFrom-Json
    $values = [ordered]@{}
    foreach ($p in $prefs.PSObject.Properties) {
        $values[$p.Name] = if ($p.Value -is [bool]) { if ($p.Value) { "True" } else { "False" } } else { [string]$p.Value }
    }
    $json = @{ "" = $values } | ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Invoke-WindowsScreenshots {
    Write-Host "=== Windows : captures du Microsoft Store ===" -ForegroundColor Cyan
    . (Join-Path $listingDir "WindowsUi.ps1")

    if (Get-Process MordheimLedgerApp -ErrorAction SilentlyContinue) {
        throw "Mordheim Ledger est ouvert sur ce PC : le fermer avant (ses donnees vont etre mises de cote le temps des captures)."
    }
    $exe = Join-Path $repoRoot "MordheimLedgerApp\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MordheimLedgerApp.exe"
    if (-not $SkipBuild) {
        dotnet build $csproj -f net10.0-windows10.0.19041.0 -c Debug -v:q -nologo
        if ($LASTEXITCODE -ne 0) { throw "Build Windows echoue (code $LASTEXITCODE)." }
    }
    if (-not (Test-Path $exe)) { throw "Appli Windows introuvable : $exe" }

    # --- Donnees de l'appli de ce PC : sauvegarde + empreintes, puis base de captures ---
    $appData = Join-Path $env:LOCALAPPDATA "Narfédôme\Narfdme.MordheimLedger"
    $backup  = Join-Path $env:TEMP "mordheim-screenshots-backup-$([guid]::NewGuid().ToString('N'))"
    $hashes  = @{}
    if (Test-Path $appData) {
        Copy-Item $appData $backup -Recurse
        foreach ($file in Get-ChildItem $appData -Recurse -File) {
            $relative = $file.FullName.Substring($appData.Length)
            $hashes[$relative] = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
            if ((Get-FileHash (Join-Path $backup $relative) -Algorithm SHA256).Hash -ne $hashes[$relative]) { throw "Sauvegarde incomplete : $relative" }
        }
        Write-Host "  Donnees de ce PC sauvegardees ($($hashes.Count) fichiers) : $backup"
        Get-ChildItem $appData -Recurse -File | Remove-Item -Force
    }

    $outDir = Join-Path $listingDir "windows-screenshots"
    $process = $null
    $hwnd = $null
    try {
        New-Item -ItemType Directory -Force -Path (Join-Path $appData "Data"), (Join-Path $appData "Settings") | Out-Null
        Copy-Item (Join-Path $dataDir "mordheimledger.db3") (Join-Path $appData "Data\mordheimledger.db3")
        Write-WindowsPreferences (Join-Path $appData "Settings\preferences.dat")

        $process = Start-Process $exe -ArgumentList "--mordheim-e2e" -PassThru
        $hwnd = Get-AppWindow $process 90
        Set-AppWindowSize $hwnd 640 900
        $button = [System.Windows.Automation.ControlType]::Button
        $tab    = [System.Windows.Automation.ControlType]::TabItem

        # 1. Liste des bandes.
        Find-UiElement $hwnd -Name "Le Marteau de Sigmar" -TimeoutSeconds 90 | Out-Null
        Start-Sleep 2
        Save-AppWindow $hwnd (Join-Path $outDir "1.png")

        # 2. Fiche de la bande d'exemple : zone « Jouer » de sa ligne.
        $sampleY = (Find-UiElement $hwnd -Name "Les Roses Sanglantes").Current.BoundingRectangle.Y
        $jouerCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Jouer")
        $jouer = (Get-UiRoot $hwnd).FindAll([System.Windows.Automation.TreeScope]::Descendants, $jouerCond) |
            Sort-Object { [math]::Abs($_.Current.BoundingRectangle.Y - $sampleY) } | Select-Object -First 1
        Invoke-UiTap $jouer
        Find-UiElement $hwnd -Name "Lancer la partie" -TimeoutSeconds 60 | Out-Null
        Start-Sleep 3
        Save-AppWindow $hwnd (Join-Path $outDir "2.png")
        Invoke-UiElement (Find-UiElement $hwnd -Name ([string][char]0x3008) -ControlType $button) # retour a la liste
        Find-UiElement $hwnd -Name "Le Marteau de Sigmar" -TimeoutSeconds 30 | Out-Null

        # 3. Codex, section Bandes.
        Invoke-UiTap (Find-UiElement $hwnd -Name "Codex" -ControlType $tab)
        Find-UiElement $hwnd -Name "Livre de base" -TimeoutSeconds 60 | Out-Null
        Start-Sleep 3
        Save-AppWindow $hwnd (Join-Path $outDir "3.png")

        # 4. Modification d'un objet de la Place du Marche.
        Invoke-UiElement (Find-UiElement $hwnd -Name "Bandes" -ControlType $button) # titre de section, pas l'onglet
        Invoke-UiElement (Find-UiElement $hwnd -Name "Place du Marché" -ControlType $button)
        $sword = Find-UiElement $hwnd -Name "Épée" -TimeoutSeconds 30
        Start-Sleep 2
        # Toucher sur l'icone de la tuile, juste au-dessus du nom : le nom seul ne selectionne pas la tuile.
        $r = $sword.Current.BoundingRectangle
        [MordheimUi.Native]::SetForegroundWindow($hwnd) | Out-Null
        [MordheimUi.Native]::Tap([int]($r.X + $r.Width / 2), [int]($r.Y - 25))
        $pencil = Find-UiElement $hwnd -Name ([string][char]0xf304) -ControlType $button
        for ($i = 0; $i -lt 20 -and -not $pencil.Current.IsEnabled; $i++) { Start-Sleep -Milliseconds 250 }
        Invoke-UiElement $pencil # crayon
        Find-UiElement $hwnd -AutomationId FormPrimaryButton -TimeoutSeconds 30 | Out-Null
        Start-Sleep 2
        Save-AppWindow $hwnd (Join-Path $outDir "4.png")
        Invoke-UiElement (Find-UiElement $hwnd -AutomationId FormSecondaryButton)
        Start-Sleep 2

        # 5. Recrutement : assistant de creation, etape Guerriers (rien n'est enregistre).
        Invoke-UiTap (Find-UiElement $hwnd -Name "Bandes" -ControlType $tab)
        Invoke-UiTap (Find-UiElement $hwnd -AutomationId AddWarbandButton)
        $name = Find-UiElement $hwnd -AutomationId WarbandNameEntry
        $name.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Les Filles de Sigmar")
        $chip = Find-UiElement $hwnd -AutomationId WarbandTypeChip
        $plusCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "+")
        Invoke-UiElement ($chip.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $plusCond))
        Invoke-UiTap (Find-UiElement $hwnd -Name "Sœurs de Sigmar")
        Start-Sleep 1
        Invoke-UiTap (Find-UiElement $hwnd -AutomationId PickerConfirmButton)
        Start-Sleep 2
        Invoke-UiElement (Find-UiElement $hwnd -AutomationId FormPrimaryButton)
        Find-UiElement $hwnd -AutomationId RecruitIncrementButton | Out-Null
        Start-Sleep 1
        # Boutons releves une seule fois : le « + » d'une ligne au maximum disparait.
        $plus = Find-UiElements $hwnd RecruitIncrementButton
        foreach ($row in 0, 1, 2, 2, 3, 3, 4, 4) { Invoke-UiTap $plus[$row]; Start-Sleep -Milliseconds 400 }
        Start-Sleep 1
        Save-AppWindow $hwnd (Join-Path $outDir "5.png")

        # 6. Parametres.
        Invoke-UiTap (Find-UiElement $hwnd -Name "Paramètres" -ControlType $tab)
        Find-UiElement $hwnd -Name "Palette" -TimeoutSeconds 30 | Out-Null
        Start-Sleep 3
        Save-AppWindow $hwnd (Join-Path $outDir "6.png")
    }
    catch {
        # Etat de l'appli au moment de l'echec, pour comprendre sans relancer.
        if ($hwnd) {
            $failure = Join-Path $env:TEMP "mordheim-screenshots-failure"
            try {
                Save-AppWindow $hwnd "$failure.png"
                Write-UiDump $hwnd | Out-File -Encoding utf8 "$failure.txt"
                Write-Host "  Etat a l'echec : $failure.png / .txt"
            } catch { }
        }
        throw
    }
    finally {
        if ($hwnd) { Clear-AppWindowTopmost $hwnd }
        if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(10000) | Out-Null }
        Start-Sleep 1
        Get-ChildItem $appData -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force
        if (Test-Path $backup) {
            Copy-Item (Join-Path $backup "*") $appData -Recurse -Force
            $mismatch = @($hashes.Keys | Where-Object { -not (Test-Path (Join-Path $appData $_)) -or (Get-FileHash (Join-Path $appData $_) -Algorithm SHA256).Hash -ne $hashes[$_] })
            if ($mismatch.Count -gt 0) {
                Write-Warning "Restauration incomplete ($($mismatch -join ', ')) : la sauvegarde reste dans $backup."
            } else {
                Remove-Item $backup -Recurse -Force
                Write-Host "  Donnees de ce PC restaurees et verifiees ($($hashes.Count) fichiers)." -ForegroundColor Green
            }
        }
    }
}

# Deux executions ne donnent pas des PNG identiques a l'octet pres (anticrenelage, indicateur de defilement :
# quelques dizaines de pixels). Une capture qui differe de sa version commitee de moins de 0,5 % de ses pixels
# reprend la version commitee : seul un vrai changement d'ecran modifie les fichiers (et arrete Build-Release
# -Screenshots).
function Restore-UnchangedScreenshots {
    if (-not ("MordheimShots.PixelDiff" -as [type])) {
        Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Runtime.InteropServices;
namespace MordheimShots { public static class PixelDiff {
    /// Part des pixels differents (0..1), ou 1 si les tailles different.
    public static double Ratio(byte[] a, string b) {
        using (var ba = new Bitmap(new MemoryStream(a))) using (var bb = new Bitmap(b)) {
            if (ba.Width != bb.Width || ba.Height != bb.Height) return 1;
            var rect = new Rectangle(0, 0, ba.Width, ba.Height);
            var da = ba.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var db = bb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int n = da.Stride * ba.Height; var pa = new byte[n]; var pb = new byte[n];
            Marshal.Copy(da.Scan0, pa, 0, n); Marshal.Copy(db.Scan0, pb, 0, n);
            ba.UnlockBits(da); bb.UnlockBits(db);
            long diff = 0;
            for (int i = 0; i < n; i += 4)
                if (pa[i] != pb[i] || pa[i + 1] != pb[i + 1] || pa[i + 2] != pb[i + 2] || pa[i + 3] != pb[i + 3]) diff++;
            return diff / (double)(ba.Width * ba.Height);
        }
    }
} }
"@
    }
    $dirs = @("Website/assets/screenshots", "Deploy/StoreListing/windows-screenshots")
    foreach ($relative in @(git -C $repoRoot diff --name-only -- $dirs)) {
        if ($relative -notlike "*.png") { continue }
        $tmp = [System.IO.Path]::GetTempFileName()
        try {
            # git show sans conversion : passer par cmd pour garder les octets du PNG intacts.
            cmd /c "git -C `"$repoRoot`" show HEAD:$relative > `"$tmp`" 2>nul"
            if ($LASTEXITCODE -ne 0) { continue }
            $ratio = [MordheimShots.PixelDiff]::Ratio([System.IO.File]::ReadAllBytes($tmp), (Join-Path $repoRoot $relative))
            if ($ratio -lt 0.005) {
                git -C $repoRoot checkout -- $relative
                Write-Host ("  {0} : inchangee ({1:P2} de pixels differents), version commitee gardee" -f $relative, $ratio)
            } else {
                Write-Host ("  {0} : modifiee ({1:P1} de pixels differents)" -f $relative, $ratio) -ForegroundColor Yellow
            }
        } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
    }
}

try {
    if (-not $SkipAndroid) { Invoke-AndroidScreenshots }
    if (-not $SkipWindows) { Invoke-WindowsScreenshots }
    Write-Host "=== Comparaison avec les captures commitees ===" -ForegroundColor Cyan
    Restore-UnchangedScreenshots
    & (Join-Path $listingDir "Build-StoreListing.ps1")
}
catch {
    Write-Host "`nECHEC : $($_.Exception.Message)" -ForegroundColor Red
    if (-not $NoPause) { Read-Host "`nAppuie sur Entree pour fermer" }
    exit 1
}
