using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MordheimLedgerApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Tests de bout en bout (Appium, extra d'Intent « mordheim_e2e ») : voir Services.E2EMode.
            if (Intent?.GetBooleanExtra(MordheimLedgerApp.Services.E2EMode.IntentExtra, false) == true)
                MordheimLedgerApp.Services.E2EMode.IsEnabled = true;
            base.OnCreate(savedInstanceState);
        }
    }
}
