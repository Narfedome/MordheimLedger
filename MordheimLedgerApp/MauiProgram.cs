using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.Injuries;
using MordheimLedgerApp.Features.Library.Skills;
using MordheimLedgerApp.Features.Library.Spells;
using MordheimLedgerApp.Features.Library.SpecialRules;
using MordheimLedgerApp.Features.Library.Mutations;
using MordheimLedgerApp.Features.Library.Mounts;
using MordheimLedgerApp.Features.Library.MagicSchools;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
using MordheimLedgerApp.Features.Onboarding;
using MordheimLedgerApp.Features.Settings;
using MordheimLedgerApp.Features.Warbands;
using MordheimLedgerApp.Services;
using Microsoft.Extensions.Logging;

namespace MordheimLedgerApp
{
    public static class MauiProgram
    {
        static readonly string dbPath = Path.Combine(FileSystem.AppDataDirectory, "mordheimledger.db3");

        public static MauiApp CreateMauiApp()
        {
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
            builder.Services.AddTransient<WarbandArchetypeSelectorPage>();
            builder.Services.AddSingleton<IWarbandArchetypePickerNavigationService, WarbandArchetypePickerNavigationService>();
            builder.Services.AddSingleton<IWarbandArchetypePickerService, WarbandArchetypePickerService>();
            builder.Services.AddTransient<WarriorArchetypeViewModel>();
            builder.Services.AddTransient<WarriorArchetypeListPage>();
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
            builder.Services.AddTransient<MountViewModel>();
            builder.Services.AddTransient<MountSelectorPage>();
            builder.Services.AddSingleton<IMountPickerNavigationService, MountPickerNavigationService>();
            builder.Services.AddSingleton<IMountPickerService, MountPickerService>();
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
    }
}
