using System.Diagnostics;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace MordheimLedgerApp.UITests;

/// <summary>Captures du site et de la fiche Google Play (Website/assets/screenshots/1..6.png, 1080x2400). L'appli démarre
/// sur une base toute prête, toujours la même (Deploy/StoreListing/screenshot-data : bande d'exemple + une bande de Sœurs
/// de Sigmar, français, thème sombre, accueil déjà passé), puis le scénario ne fait que parcourir les écrans, barre
/// d'état figée par le mode démo d'Android. Lancé par Deploy/StoreListing/Capture-Screenshots.ps1, qui renseigne
/// MORDHEIM_SCREENSHOTS_DIR ; ignoré sinon. Les légendes du site (Website/index.html et en/index.html) suivent l'ordre
/// des captures.</summary>
public class StoreScreenshots
{
    private static readonly string? OutputDir = Environment.GetEnvironmentVariable("MORDHEIM_SCREENSHOTS_DIR");

    private static readonly string DataDir = Environment.GetEnvironmentVariable("MORDHEIM_SCREENSHOTS_DATA")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Deploy", "StoreListing", "screenshot-data"));

    [ScreenshotFact]
    public void CaptureStoreScreenshots()
    {
        PrepareDevice();
        using var app = new AppSession(clearData: false);
        try
        {
            // 1. Liste des bandes.
            var sample = app.WaitForText(60, "Les Roses Sanglantes");
            app.WaitForText(20, "Le Marteau de Sigmar");
            AppSession.Pause(1500);
            Save(app, 1);

            // 2. Fiche de la bande d'exemple : zone « Jouer » de sa ligne.
            app.Driver.FindElements(MobileBy.AndroidUIAutomator("new UiSelector().text(\"Jouer\")"))
                .OrderBy(e => Math.Abs(e.Location.Y - sample.Location.Y)).First().Click();
            app.WaitForText(30, "Héros");
            AppSession.Pause(2000);
            Save(app, 2);
            app.Driver.Navigate().Back();
            AppSession.Pause(1500);

            // 3. Codex, section Bandes.
            OpenTab(app, "Codex");
            app.WaitForText(30, "Livre de base");
            AppSession.Pause(1500);
            Save(app, 3);

            // 4. Formulaire de modification d'un objet de la Place du Marché.
            Topmost(app, "Bandes").Click(); // titre de section, pas l'onglet du même nom
            app.WaitForText(20, "Place du Marché").Click();
            app.WaitForText(30, "Épée").Click();
            AppSession.Pause(800);
            Topmost(app, "", lowest: true).Click(); // crayon de la barre d'actions
            app.WaitFor("FormPrimaryButton", 30);
            AppSession.Pause(1500);
            Save(app, 4);
            app.Tap("FormSecondaryButton");
            AppSession.Pause(1500);

            // 5. Recrutement : assistant de création, étape Guerriers (rien n'est enregistré).
            OpenTab(app, "Bandes");
            app.Tap("AddWarbandButton");
            app.Type("WarbandNameEntry", "Les Filles de Sigmar");
            app.WaitFor("WarbandTypeChip").FindElements(By.XPath(".//*[@clickable='true']")).Last().Click();
            app.WaitForText(20, "Sœurs de Sigmar").Click();
            app.Tap("PickerConfirmButton");
            AppSession.Pause();
            app.Tap("FormPrimaryButton");
            app.WaitFor("RecruitIncrementButton");
            // Boutons relevés une seule fois : le « + » d'une ligne au maximum disparaît et décalerait les suivants.
            var plus = app.All("RecruitIncrementButton").ToList();
            foreach (var row in new[] { 0, 1, 2, 2, 3, 3, 4, 4 })
            {
                plus[row].Click();
                AppSession.Pause(300);
            }
            AppSession.Pause(800);
            Save(app, 5);

            // 6. Paramètres.
            OpenTab(app, "Paramètres");
            app.WaitForText(30, "Palette");
            AppSession.Pause(2500); // laisse disparaître l'indicateur de défilement
            Save(app, 6);
        }
        catch
        {
            app.Screenshot(nameof(CaptureStoreScreenshots));
            throw;
        }
        finally
        {
            Adb("shell am broadcast -a com.android.systemui.demo -e command exit");
        }
    }

    /// <summary>Base et préférences de captures déposées dans l'appli (APK Debug : run-as), thème sombre et barre d'état
    /// figée (12:00, Wi-Fi et batterie pleins, pas de notification).</summary>
    private static void PrepareDevice()
    {
        const string pkg = AppSession.Package;
        Adb($"shell am force-stop {pkg}");
        Adb($"shell pm clear {pkg}");
        Adb($"push \"{Path.Combine(DataDir, "mordheimledger.db3")}\" /data/local/tmp/mordheimledger.db3");
        Adb($"shell run-as {pkg} mkdir -p files shared_prefs");
        Adb($"shell run-as {pkg} cp /data/local/tmp/mordheimledger.db3 files/mordheimledger.db3");

        var prefsXml = Path.Combine(Path.GetTempPath(), "mordheim-screenshots-prefs.xml");
        File.WriteAllText(prefsXml, AndroidPreferences(Path.Combine(DataDir, "preferences.json")));
        Adb($"push \"{prefsXml}\" /data/local/tmp/mordheim-prefs.xml");
        Adb($"shell run-as {pkg} cp /data/local/tmp/mordheim-prefs.xml shared_prefs/{pkg}_preferences.xml");
        Adb("shell rm /data/local/tmp/mordheimledger.db3 /data/local/tmp/mordheim-prefs.xml");

        Adb("shell cmd uimode night yes");
        Adb("shell settings put global sysui_demo_allowed 1");
        foreach (var demo in new[]
                 {
                     "-e command enter",
                     "-e command clock -e hhmm 1200",
                     "-e command battery -e level 100 -e plugged false",
                     "-e command network -e wifi show -e level 4 -e fully true",
                     "-e command network -e mobile hide",
                     "-e command notifications -e visible false",
                 })
            Adb($"shell am broadcast -a com.android.systemui.demo {demo}");
    }

    /// <summary>preferences.json (clé → booléen, entier ou texte) au format SharedPreferences de MAUI.</summary>
    private static string AndroidPreferences(string jsonPath)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var lines = json.RootElement.EnumerateObject().Select(p => p.Value.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => $"    <boolean name=\"{p.Name}\" value=\"{p.Value.GetBoolean().ToString().ToLowerInvariant()}\" />",
            JsonValueKind.Number => $"    <int name=\"{p.Name}\" value=\"{p.Value.GetInt32()}\" />",
            _ => $"    <string name=\"{p.Name}\">{System.Security.SecurityElement.Escape(p.Value.GetString())}</string>",
        });
        return "<?xml version='1.0' encoding='utf-8' standalone='yes' ?>\n<map>\n" + string.Join("\n", lines) + "\n</map>\n";
    }

    private static void OpenTab(AppSession app, string name)
    {
        app.Driver.FindElement(MobileBy.AccessibilityId(name)).Click();
        AppSession.Pause(1500);
    }

    /// <summary>Élément au texte exact le plus haut (ou le plus bas) de l'écran.</summary>
    private static IWebElement Topmost(AppSession app, string text, bool lowest = false)
    {
        var all = new OpenQA.Selenium.Support.UI.WebDriverWait(app.Driver, TimeSpan.FromSeconds(20)).Until(d =>
        {
            var found = d.FindElements(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{text}\")"));
            return found.Count > 0 ? found : null;
        })!;
        return lowest ? all.OrderByDescending(e => e.Location.Y).First() : all.OrderBy(e => e.Location.Y).First();
    }

    private static void Save(AppSession app, int index)
    {
        Directory.CreateDirectory(OutputDir!);
        app.Driver.GetScreenshot().SaveAsFile(Path.Combine(OutputDir!, $"{index}.png"));
    }

    private static void Adb(string arguments)
    {
        var device = Environment.GetEnvironmentVariable("MORDHEIM_E2E_DEVICE") ?? "emulator-5554";
        using var process = Process.Start(new ProcessStartInfo("adb", $"-s {device} {arguments}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        })!;
        process.WaitForExit(60000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"adb {arguments} : {process.StandardError.ReadToEnd()}");
    }
}

/// <summary>Ne tourne que si MORDHEIM_SCREENSHOTS_DIR est défini (dossier de sortie des captures).</summary>
public sealed class ScreenshotFactAttribute : FactAttribute
{
    public ScreenshotFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MORDHEIM_SCREENSHOTS_DIR")))
            Skip = "Captures des stores : lancer Deploy/StoreListing/Capture-Screenshots.ps1 (définit MORDHEIM_SCREENSHOTS_DIR).";
    }
}
