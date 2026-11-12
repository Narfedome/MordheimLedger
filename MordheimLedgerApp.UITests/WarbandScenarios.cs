using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace MordheimLedgerApp.UITests;

/// <summary>Parcours complets sur l'appli Android, chacun depuis une installation neuve (voir AppSession).</summary>
public class WarbandScenarios
{
    /// <summary>Accueil passé en « Partir de zéro », bande de Reiklanders créée par l'assistant (Capitaine,
    /// Champion, Guerrier), puis présente dans la liste.</summary>
    [E2EFact]
    public void CreateWarband_FromAnEmptyInstall()
    {
        using var app = new AppSession();
        try
        {
            FinishOnboarding(app, loadSample: false);

            app.Tap("AddWarbandButton");
            app.Type("WarbandNameEntry", "Bande E2E");

            // « + » de la puce Type de bande : dernier élément tapable de la puce.
            var typeChip = app.WaitFor("WarbandTypeChip");
            typeChip.FindElements(By.XPath(".//*[@clickable='true']")).Last().Click();
            app.WaitForText(20, "Reiklander").Click();
            app.Tap("PickerConfirmButton");
            AppSession.Pause();

            app.Tap("FormPrimaryButton"); // → Guerriers
            AppSession.Pause();
            app.WaitFor("RecruitIncrementButton");
            var plus = app.All("RecruitIncrementButton").ToList();
            Assert.True(plus.Count >= 4, "Recrues Reiklander introuvables");
            plus[0].Click(); // Capitaine
            plus[1].Click(); // Champion
            plus[3].Click(); // Guerrier

            // Équipement, noms, Francs-Tireurs, récap : Suivant jusqu'à Créer.
            for (var i = 0; i < 10 && app.Exists("FormPrimaryButton"); i++)
            {
                app.Tap("FormPrimaryButton");
                AppSession.Pause(1200);
            }

            Assert.False(app.Exists("FormPrimaryButton"), "L'assistant de création ne s'est pas fermé");
            app.WaitForText(20, "Bande E2E");
        }
        catch
        {
            app.Screenshot(nameof(CreateWarband_FromAnEmptyInstall));
            throw;
        }
    }

    /// <summary>Bande d'exemple chargée à l'accueil, partie lancée puis Fin de partie jouée jusqu'au bout (dés
    /// d'Exploration sans double, autres jets lancés par l'appli), et une entrée « Victoire » dans l'Historique.</summary>
    [E2EFact]
    public void PlayAGame_OnTheSampleWarband()
    {
        using var app = new AppSession();
        try
        {
            FinishOnboarding(app, loadSample: true);

            // Ligne de la bande d'exemple : zone « Jouer ».
            app.WaitForText(30, "Jouer", "Play").Click();
            app.Tap("StartGameButton");
            app.Tap("StartGameConfirmButton");
            AppSession.Pause();
            app.Tap("EndOfGameButton");
            AppSession.Pause(1500);

            var steps = 0;
            while (app.Exists("FormPrimaryButton"))
            {
                Assert.True(++steps < 60, "La Fin de partie ne se termine pas");
                FillRolls(app);
                app.Tap("FormPrimaryButton");
                AppSession.Pause(1200);
            }

            app.Tap("HistoryTabButton");
            app.WaitForText(20, "Victoire", "Victory");
        }
        catch
        {
            app.Screenshot(nameof(PlayAGame_OnTheSampleWarband));
            throw;
        }
    }

    /// <summary>Dés d'Exploration : 1, 2, 3... (jamais de double, donc pas d'issue à choisir) ; les autres jets de
    /// l'étape sont lancés par leur bouton dé.</summary>
    private static void FillRolls(AppSession app)
    {
        var dice = app.All("ExplorationDieEntry").ToList();
        for (var i = 0; i < dice.Count; i++)
        {
            if (!string.IsNullOrEmpty(dice[i].Text) && int.TryParse(dice[i].Text, out _)) continue;
            dice[i].Click();
            dice[i].SendKeys(((i % 6) + 1).ToString());
        }
        if (dice.Count > 0)
        {
            app.HideKeyboard();
            return;
        }

        foreach (var roll in app.All("AutoRollButton"))
        {
            try { roll.Click(); } catch (StaleElementReferenceException) { /* l'étape s'est redessinée */ }
        }
    }

    private static void FinishOnboarding(AppSession app, bool loadSample)
    {
        var target = loadSample ? "OnboardingLoadSampleButton" : "OnboardingStartEmptyButton";
        app.WaitFor("OnboardingNextButton", 60);
        for (var i = 0; i < 8 && !app.Exists(target); i++)
        {
            app.Tap("OnboardingNextButton");
            AppSession.Pause(600);
        }
        app.Tap(target);
        AppSession.Pause(2000);
    }
}
