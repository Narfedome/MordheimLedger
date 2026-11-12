using System.Diagnostics;
using System.Net.Http;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MordheimLedgerApp.UITests;

/// <summary>Une session Appium sur l'appli Android, données effacées au démarrage (comme une installation neuve) et
/// visites guidées coupées (extra d'Intent « mordheim_e2e », voir Services.E2EMode dans l'appli). Démarre le serveur
/// Appium s'il ne tourne pas déjà. Les éléments sont repérés par leur AutomationId, que MAUI expose comme
/// identifiant de ressource Android - indépendant de la langue de l'appli.</summary>
public sealed class AppSession : IDisposable
{
    public const string Package = "com.narfedome.mordheimledger";
    private const string Activity = "crc647ee7434c7d932e23.MainActivity";
    private static readonly Uri ServerUri = new("http://127.0.0.1:4723/");

    private static Process? _server;
    private static readonly object ServerLock = new();

    public AndroidDriver Driver { get; }

    public AppSession()
    {
        EnsureServer();
        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2",
            DeviceName = Environment.GetEnvironmentVariable("MORDHEIM_E2E_DEVICE") ?? "emulator-5554",
        };
        options.AddAdditionalAppiumOption("appPackage", Package);
        options.AddAdditionalAppiumOption("appActivity", Activity);
        options.AddAdditionalAppiumOption("noReset", false); // données effacées : appli comme neuve
        options.AddAdditionalAppiumOption("optionalIntentArguments", "--ez mordheim_e2e true");
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        options.AddAdditionalAppiumOption("appWaitDuration", 60000);
        Driver = new AndroidDriver(ServerUri, options, TimeSpan.FromMinutes(3));
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
    }

    // ---- Repérage -------------------------------------------------------------------------------------------

    public static By Id(string automationId) => MobileBy.Id($"{Package}:id/{automationId}");

    public IReadOnlyCollection<IWebElement> All(string automationId) => Driver.FindElements(Id(automationId));

    public bool Exists(string automationId) => All(automationId).Count > 0;

    public IWebElement WaitFor(string automationId, int seconds = 20) =>
        new WebDriverWait(Driver, TimeSpan.FromSeconds(seconds)).Until(d => d.FindElements(Id(automationId)).FirstOrDefault(e => e.Displayed))!;

    /// <summary>Premier élément dont le texte contient l'un des textes donnés (français ou anglais).</summary>
    public IWebElement WaitForText(int seconds, params string[] texts) =>
        new WebDriverWait(Driver, TimeSpan.FromSeconds(seconds)).Until(d => texts.Select(t => d.FindElements(
            MobileBy.AndroidUIAutomator($"new UiSelector().textContains(\"{t}\")")).FirstOrDefault()).FirstOrDefault(e => e is not null))!;

    public bool HasText(params string[] texts) => texts.Any(t =>
        Driver.FindElements(MobileBy.AndroidUIAutomator($"new UiSelector().textContains(\"{t}\")")).Count > 0);

    // ---- Actions --------------------------------------------------------------------------------------------

    public void Tap(string automationId) => WaitFor(automationId).Click();

    public void Type(string automationId, string text)
    {
        var field = WaitFor(automationId);
        field.Click();
        field.Clear();
        field.SendKeys(text);
        HideKeyboard();
    }

    public void HideKeyboard()
    {
        try { if (Driver.IsKeyboardShown()) Driver.HideKeyboard(); } catch (WebDriverException) { /* déjà caché */ }
    }

    /// <summary>Laisse l'appli finir une navigation ou un chargement.</summary>
    public static void Pause(int milliseconds = 800) => Thread.Sleep(milliseconds);

    /// <summary>Capture d'écran dans TestResults/e2e, pour comprendre un échec.</summary>
    public string Screenshot(string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestResults", "e2e");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{DateTime.Now:HHmmss}-{name}.png");
        Driver.GetScreenshot().SaveAsFile(path);
        return path;
    }

    public void Dispose()
    {
        try { Driver.Quit(); } catch (WebDriverException) { }
        Driver.Dispose();
    }

    // ---- Serveur Appium -------------------------------------------------------------------------------------

    private static void EnsureServer()
    {
        lock (ServerLock)
        {
            if (IsServerUp()) return;
            _server ??= Process.Start(new ProcessStartInfo("cmd.exe", "/c appium --port 4723 --log-level error")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { _server?.Kill(entireProcessTree: true); } catch { } };
            for (var i = 0; i < 60 && !IsServerUp(); i++) Thread.Sleep(500);
            if (!IsServerUp()) throw new InvalidOperationException("Le serveur Appium n'a pas démarré (npm install -g appium, voir README.md).");
        }
    }

    private static bool IsServerUp()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            return http.GetAsync(new Uri(ServerUri, "status")).GetAwaiter().GetResult().IsSuccessStatusCode;
        }
        catch (Exception) { return false; }
    }
}
