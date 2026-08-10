using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.EquipmentLists;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.Spells;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Features.Library.Animals;
using MordheimLedgerApp.Features.Library.MagicSchools;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Features.Onboarding;
using MordheimLedgerApp.Features.Settings;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;
using Microsoft.Extensions.Logging;
using MordheimLedgerApp.Features.Warbands.CreateEdit;

namespace MordheimLedgerApp
{
    public static class MauiProgram
    {
        static readonly string dbPath = Path.Combine(FileSystem.AppDataDirectory, "mordheimledger.db3");

        public static MauiApp CreateMauiApp()
        {
            // Diagnostic du crash natif intermittent au sélecteur de règles spéciales (voir CrashLogger) -
            // capte tout ce qui échapperait normalement au débogueur (exception sur un thread pool/Task
            // non observée, thread hors dispatcher UI). Actif en Debug ET Release, contrairement à
            // builder.Logging.AddDebug() plus bas.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                CrashLogger.Log($"AppDomain.UnhandledException (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                CrashLogger.LogException("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
            CrashLogger.Log("CreateMauiApp start");

            EnsureDatabaseFileExists();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit(options =>
                {
                    // Every popup in the app (themed dialogs) draws its own card (Border): disable
                    // the toolkit's default shape/shadow to avoid a double outline (known white-border
                    // bug on Windows).
                    options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings
                    {
                        Shape = null,
                        Shadow = null
                    });
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Font Awesome 7 Free-Regular-400.otf", "FontRegular");
                    fonts.AddFont("Font Awesome 7 Brands-Regular-400.otf", "FontBrands");
                    fonts.AddFont("Font Awesome 7 Free-Solid-900.otf", "FontSolid");
                    fonts.AddFont("rpgawesome-webfont.ttf", "RpgAwesome");
                });

            builder.Services.AddSingleton(new AppDatabase(dbPath));
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddTransient<OnboardingViewModel>();
            builder.Services.AddTransient<OnboardingPage>();
            builder.Services.AddTransient<LoadingService>();
            builder.Services.AddSingleton<ILibraryService, LibraryService>();
            builder.Services.AddSingleton<IWarbandService, WarbandService>();
            builder.Services.AddTransient<WarbandListViewModel>();
            builder.Services.AddTransient<WarbandListPage>();
            builder.Services.AddTransient<WarbandDetailViewModel>();
            builder.Services.AddTransient<WarbandDetailPage>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<WarbandArchetypeViewModel>();
            builder.Services.AddTransient<WarbandEditDialogViewModel>();
            builder.Services.AddTransient<WarbandArchetypeSelectorPage>();
            builder.Services.AddSingleton<IWarbandArchetypePickerNavigationService, WarbandArchetypePickerNavigationService>();
            builder.Services.AddSingleton<IWarbandArchetypePickerService, WarbandArchetypePickerService>();
            builder.Services.AddTransient<WarriorArchetypeSelectorViewModel>();
            builder.Services.AddTransient<WarriorArchetypeSelectorPage>();
            builder.Services.AddSingleton<IWarriorArchetypePickerNavigationService, WarriorArchetypePickerNavigationService>();
            builder.Services.AddSingleton<IWarriorArchetypePickerService, WarriorArchetypePickerService>();
            builder.Services.AddTransient<EquipmentItemViewModel>();
            builder.Services.AddTransient<EquipmentItemSelectorPage>();
            builder.Services.AddSingleton<IEquipmentPickerNavigationService, EquipmentPickerNavigationService>();
            builder.Services.AddSingleton<IEquipmentPickerService, EquipmentPickerService>();
            builder.Services.AddTransient<SkillViewModel>();
            builder.Services.AddTransient<SkillSelectorPage>();
            builder.Services.AddSingleton<ISkillPickerNavigationService, SkillPickerNavigationService>();
            builder.Services.AddSingleton<ISkillPickerService, SkillPickerService>();
            builder.Services.AddTransient<InjuryViewModel>();
            builder.Services.AddTransient<InjurySelectorPage>();
            builder.Services.AddSingleton<IInjuryPickerNavigationService, InjuryPickerNavigationService>();
            builder.Services.AddSingleton<IInjuryPickerService, InjuryPickerService>();
            builder.Services.AddTransient<SpellViewModel>();
            builder.Services.AddTransient<SpellSelectorPage>();
            builder.Services.AddSingleton<ISpellPickerNavigationService, SpellPickerNavigationService>();
            builder.Services.AddSingleton<ISpellPickerService, SpellPickerService>();
            builder.Services.AddTransient<SpecialRuleViewModel>();
            builder.Services.AddTransient<SpecialRuleSelectorPage>();
            builder.Services.AddSingleton<ISpecialRulePickerNavigationService, SpecialRulePickerNavigationService>();
            builder.Services.AddSingleton<ISpecialRulePickerService, SpecialRulePickerService>();
            builder.Services.AddTransient<MutationViewModel>();
            builder.Services.AddTransient<MutationSelectorPage>();
            builder.Services.AddSingleton<IMutationPickerNavigationService, MutationPickerNavigationService>();
            builder.Services.AddSingleton<IMutationPickerService, MutationPickerService>();
            builder.Services.AddTransient<AnimalViewModel>();
            builder.Services.AddTransient<AnimalSelectorPage>();
            builder.Services.AddSingleton<IAnimalPickerNavigationService, AnimalPickerNavigationService>();
            builder.Services.AddSingleton<IAnimalPickerService, AnimalPickerService>();
            builder.Services.AddTransient<MagicSchoolViewModel>();
            builder.Services.AddTransient<MagicSchoolSelectorPage>();
            builder.Services.AddTransient<MagicSchoolListPage>();
            builder.Services.AddSingleton<IMagicSchoolPickerNavigationService, MagicSchoolPickerNavigationService>();
            builder.Services.AddSingleton<IMagicSchoolPickerService, MagicSchoolPickerService>();
            builder.Services.AddTransient<LibraryViewModel>();
            builder.Services.AddTransient<LibraryPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        /// <summary>Premier lancement (dbPath n'existe pas encore) : copie la base déjà entièrement
        /// seedée embarquée comme asset (Resources/Raw/seed.db3, régénérée à chaque build depuis
        /// Data/SeedData/*.json tant qu'un JSON a changé - voir MordheimLedgerApp.csproj/
        /// GenerateSeedDatabase et Tools/DbSeedGenerator) plutôt que de laisser AppDatabase rejouer les
        /// ~22 passes de seed JSON->SQLite à froid sur l'appareil. AppDatabase garde son garde-fou "table
        /// vide -> seed" (InitializeAsync) totalement inchangé : la base copiée n'étant plus vide, il ne
        /// se déclenche simplement jamais après un premier lancement. Lancements suivants (dbPath existe
        /// déjà, campagnes/contenu personnalisé de l'utilisateur dedans) : no-op immédiat.
        /// GetAwaiter().GetResult() volontaire ici (avant tout dispatcher UI, un seul blocage au tout
        /// premier lancement) - CreateMauiApp() elle-même n'est pas async côté framework.</summary>
        private static void EnsureDatabaseFileExists()
        {
            if (File.Exists(dbPath)) return;

            using var source = FileSystem.OpenAppPackageFileAsync("seed.db3").GetAwaiter().GetResult();
            using var destination = File.Create(dbPath);
            source.CopyTo(destination);
        }
    }
}
