using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Library.EquipmentItems;
using MordheimLedgerApp.Features.Library.WarbandArchetypes;
using MordheimLedgerApp.Features.Library.WarriorArchetypes;
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
            builder.Services.AddTransient<WarbandArchetypeListPage>();
            builder.Services.AddTransient<WarriorArchetypeViewModel>();
            builder.Services.AddTransient<WarriorArchetypeListPage>();
            builder.Services.AddTransient<EquipmentItemViewModel>();
            builder.Services.AddTransient<EquipmentItemListPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
