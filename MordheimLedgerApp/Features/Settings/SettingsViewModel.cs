using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Core.Data;
using MordheimLedgerApp.Core.Services;
using MordheimLedgerApp.Features.Onboarding;
using MordheimLedgerApp.Features.Settings.ContentConflicts;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Settings
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly ThemeService _theme = ThemeService.Instance;
        private readonly AppDatabase _db;

        // Windows exige 4 segments (Major.Minor.Build.Revision) pour l'identité de package - le 4e
        // (Revision) vaut toujours 0 (exigence du Microsoft Store), sans intérêt pour l'utilisateur, on l'aligne sur
        // le format 3 segments affiché nativement sur les autres plateformes.
        public string AppVersion
        {
            get
            {
                var raw = AppInfo.Current.VersionString;
                var parts = raw.Split('.');
                return parts.Length > 3 ? string.Join('.', parts.Take(3)) : raw;
            }
        }

        [RelayCommand]
        public async Task SelectLanguage()
        {
            var labels = LanguageLabels.Values.ToArray();
            var result = await ShowActionSheetAsync(Loc["SettingsLanguage"], labels);
            if (result == null) return;
            SelectedLanguage = LanguageLabels.First(kv => kv.Value == result).Key;
        }

        [RelayCommand]
        public async Task SelectTheme()
        {
            var result = await ShowActionSheetAsync(Loc["SettingsTheme"], ThemeOptions.ToArray());
            if (result != null) SelectedThemeOption = result;
        }

        [RelayCommand]
        public async Task OpenCoffeeLink() =>
            await Launcher.OpenAsync(new Uri("https://buymeacoffee.com/narfedome"));

        [RelayCommand]
        public async Task ReportBug() =>
            await Launcher.OpenAsync(new Uri("https://docs.google.com/forms/d/e/1FAIpQLSdg2q1o01eGZsFvd0qIwOqYEVKDwBikQ0g7FWLSWHenKqeW0g/viewform?usp=dialog"));

        // Sources & remerciements - lien Discord repris tel quel de la page d'accueil du site de la
        // Grande Librairie (invitation publique).
        [RelayCommand]
        public async Task OpenGrandeLibrairieSite() =>
            await Launcher.OpenAsync(new Uri("https://sites.google.com/view/grande-librairie-de-mordheim/accueil"));

        [RelayCommand]
        public async Task OpenGrandeLibrairieDiscord() =>
            await Launcher.OpenAsync(new Uri("https://discord.gg/Fbn4CrCCA6"));

        [RelayCommand]
        public async Task OpenMordheimer() =>
            await Launcher.OpenAsync(new Uri("https://mordheimer.net"));

        /// <summary>Wipes every table (all campaigns/warbands AND any Library edits/custom content) and
        /// re-seeds from the bundled JSON (see AppDatabase.ResetAsync) - the point is to let dev/beta
        /// iteration re-run the seed after a Core schema/data change without manually deleting the db
        /// file, now that the schema is roughly stable. Double confirmation (destructive, irreversible)
        /// then a restart prompt rather than force-closing the app ourselves - every already-loaded
        /// page/ViewModel in the running Shell still holds now-stale in-memory data.</summary>
        [RelayCommand]
        public async Task ResetDatabase()
        {
            if (!await ConfirmAsync(Loc["SettingsResetTitle"], Loc["SettingsResetConfirm"])) return;
            if (!await ConfirmAsync(Loc["SettingsResetTitle"], Loc["SettingsResetConfirmAgain"])) return;

            await Loading.RunAsync(() => _db.ResetAsync());
            // Plus aucun guerrier après la réinitialisation : leurs photos (Warrior.PhotoPath) non plus.
            _warriorPhotos.DeleteAll();

            await ShowInfoAsync(Loc["SettingsResetTitle"], Loc["SettingsResetDone"]);
        }

        // --- Sauvegarde (v2, lot 1) --------------------------------------------------------------------------
        // Fichier .mordheimbackup = base + photos + réglages (BackupService). Choix de l'utilisateur : « Enregistrer
        // sous… » du système (FileSaver) ; avant chaque restauration, copie de secours des données remplacées, que
        // « Annuler la dernière restauration » remet en place. Même avertissement de redémarrage que ResetDatabase.

        private readonly BackupService _backup;

        /// <summary>Réglages repris par la sauvegarde, avec leur type de préférence (les repères « accueil vu »,
        /// « nouveautés vues », visites restent propres à l'appareil). Clés de LocalizationService/ThemeService.</summary>
        private static readonly (string Key, bool IsInt)[] BackedUpSettings = [("app_lang", false), ("app_theme", true), ("app_palette", true)];

        public bool CanUndoRestore => _backup.CanUndoRestore;

        public string UndoRestoreDescription => _backup.UndoCreatedAtUtc is { } date
            ? string.Format(Loc["BackupUndoDesc"], date.ToLocalTime().ToString("g", Loc.Culture))
            : string.Empty;

        private static Dictionary<string, string> ReadBackedUpSettings()
        {
            var settings = new Dictionary<string, string>();
            foreach (var (key, isInt) in BackedUpSettings)
                if (Preferences.Default.ContainsKey(key))
                    settings[key] = isInt
                        ? Preferences.Default.Get(key, 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : Preferences.Default.Get(key, string.Empty);
            return settings;
        }

        private static void ApplyBackedUpSettings(IReadOnlyDictionary<string, string> settings)
        {
            foreach (var (key, isInt) in BackedUpSettings)
            {
                if (!settings.TryGetValue(key, out var value)) continue;
                if (!isInt) Preferences.Default.Set(key, value);
                else if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number))
                    Preferences.Default.Set(key, number);
            }
        }

        private static readonly FilePickerFileType BackupFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            // Android ne connaît pas l'extension : tout fichier proposé, la lecture refuse ce qui n'est pas une sauvegarde.
            [DevicePlatform.Android] = new[] { "*/*" },
            [DevicePlatform.WinUI] = new[] { BackupService.FileExtension },
        });

        private string BackupProblemMessage(BackupFormatException ex) => Loc[ex.Problem switch
        {
            BackupProblem.FromNewerApp => "BackupFromNewerApp",
            BackupProblem.Damaged => "BackupDamaged",
            _ => "BackupNotABackup",
        }];

        private void RefreshUndoRestore()
        {
            OnPropertyChanged(nameof(CanUndoRestore));
            OnPropertyChanged(nameof(UndoRestoreDescription));
        }

        [RelayCommand]
        public async Task ExportBackup()
        {
            var fileName = $"MordheimLedger-{DateTime.Now:yyyy-MM-dd}{BackupService.FileExtension}";
            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            try
            {
                await Loading.RunAsync(async () =>
                {
                    await using var file = File.Create(tempPath);
                    await _backup.WriteAsync(file, AppVersion, ReadBackedUpSettings());
                });
                CommunityToolkit.Maui.Storage.FileSaverResult result;
                await using (var stream = File.OpenRead(tempPath))
                    result = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
                // Android : le fichier est bien écrit, mais le Toolkit ne sait pas retrouver le chemin absolu d'un
                // document de certains fournisseurs (Téléchargements) et renvoie un échec - vu en recette le 2026-11-15.
                // D'où le nom du fichier dans le message plutôt que son chemin, et ce cas compté comme réussi.
                if (result.IsSuccessful || result.Exception?.Message.Contains("absolute path", StringComparison.OrdinalIgnoreCase) == true)
                    await ShowInfoAsync(Loc["BackupTitle"], string.Format(Loc["BackupExportDone"], fileName));
                else if (result.Exception is { } error && error is not OperationCanceledException
                         && !error.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase))
                    await ShowErrorAsync(error);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [RelayCommand]
        public async Task RestoreBackup()
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Loc["BackupRestorePick"], FileTypes = BackupFileType });
            if (picked is null) return;

            try
            {
                BackupManifest manifest;
                await using (var stream = await picked.OpenReadAsync())
                    manifest = await BackupService.ReadManifestAsync(stream, AppVersion);
                var message = string.Format(Loc["BackupRestoreConfirm"],
                    manifest.CreatedAtUtc.ToLocalTime().ToString("g", Loc.Culture), manifest.WarbandCount);
                if (!await ConfirmAsync(Loc["BackupRestoreTitle"], message, Loc["BackupRestoreAction"], Loc["BtnCancel"], destructive: true)) return;

                BackupManifest restored = null!;
                await Loading.RunAsync(async () =>
                {
                    await using var stream = await picked.OpenReadAsync();
                    restored = await _backup.RestoreAsync(stream, AppVersion, ReadBackedUpSettings());
                });
                ApplyBackedUpSettings(restored.Settings);
                RefreshUndoRestore();
                await RefreshOfficialContentStatusAsync();
                await ShowInfoAsync(Loc["BackupRestoreTitle"], Loc["BackupRestoreDone"]);
            }
            catch (BackupFormatException ex)
            {
                await ShowInfoAsync(Loc["BackupRestoreTitle"], BackupProblemMessage(ex));
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex);
            }
        }

        [RelayCommand]
        public async Task UndoRestore()
        {
            if (!_backup.CanUndoRestore) return;
            if (!await ConfirmAsync(Loc["BackupUndoTitle"], Loc["BackupUndoConfirm"], Loc["BackupUndoAction"], Loc["BtnCancel"], destructive: true)) return;
            try
            {
                BackupManifest restored = null!;
                await Loading.RunAsync(async () => restored = await _backup.UndoRestoreAsync(AppVersion));
                ApplyBackedUpSettings(restored.Settings);
                RefreshUndoRestore();
                await RefreshOfficialContentStatusAsync();
                await ShowInfoAsync(Loc["BackupUndoTitle"], Loc["BackupUndoDone"]);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex);
            }
        }

        /// <summary>Synchro manuelle du contenu officiel depuis la seed.db3 embarquée - même si la version est
        /// déjà à jour (répare un catalogue qui aurait dérivé). Même avertissement de redémarrage que
        /// ResetDatabase si quelque chose a changé.</summary>
        [RelayCommand]
        public async Task SyncOfficialContent()
        {
            ContentSyncReport? report = null;
            try
            {
                await Loading.RunAsync(async () => report = await _officialContent.SyncNowAsync());
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex);
                return;
            }
            await RefreshOfficialContentStatusAsync();

            var message = OfficialContentService.Describe(report!);
            if (report!.HasChanges) message += "\n\n" + Loc["OfficialContentRestartHint"];
            await ShowInfoAsync(Loc["OfficialContentTitle"], message);
            if (report.Conflicts > 0) await ReviewContentConflicts();
        }

        /// <summary>Choix garder sa version / prendre l'officielle pour chaque entrée en attente.</summary>
        [RelayCommand]
        public async Task ReviewContentConflicts()
        {
            List<ContentConflict> conflicts = [];
            await Loading.RunAsync(async () =>
                conflicts = await _db.GetContentConflictsAsync(OfficialContentService.OpenEmbeddedSeedAsync, Loc.Language));
            if (conflicts.Count > 0
                && await ShowDialogAsync(new ContentConflictsDialog(new ContentConflictsDialogViewModel(_db, conflicts))))
                await ShowInfoAsync(Loc["OfficialContentTitle"], Loc["OfficialContentRestartHint"]);
            await RefreshOfficialContentStatusAsync();
        }

        [RelayCommand]
        public async Task LoadSampleWarband()
        {
            // Le chargement crée toujours une nouvelle bande : un second chargement en ferait une copie.
            if (_sampleWarbandService.HasImportedSample &&
                !await ConfirmAsync(Loc["OnboardingLoadSample"], Loc["SettingsSampleAlreadyImported"]))
                return;

            try
            {
                await Loading.RunAsync(() => _sampleWarbandService.ImportAsync());
                await ShowInfoAsync(Loc["OnboardingLoadSample"], Loc["SampleImportDone"]);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex);
            }
        }

        // Fait revenir les visites de chaque écran ; celle des Bandes démarre tout de suite (cf.
        // WarbandListPage), les autres à la prochaine arrivée sur leur écran.
        [RelayCommand]
        public async Task StartTour()
        {
            _tourService.ResetAll();
            await Shell.Current.GoToAsync("//WarbandListPage");
        }

        // Nouvelle instance de l'accueil (Transient) : repart de la première étape. Son dernier bouton
        // ramène sur l'AppShell (singleton), resté tel quel entre-temps.
        /// <summary>Puces de la version installée (Découvrir › Nouveautés), chargées à l'ouverture des Paramètres.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasWhatsNew))]
        private IReadOnlyList<string> whatsNewItems = [];

        public bool HasWhatsNew => WhatsNewItems.Count > 0;

        public string WhatsNewDescription => string.Format(Loc["SettingsWhatsNewDesc"], WhatsNewService.CurrentVersion);

        public async Task LoadWhatsNewAsync()
        {
            WhatsNewItems = await WhatsNewService.LoadCurrentItemsAsync();
            OnPropertyChanged(nameof(WhatsNewDescription));
        }

        [RelayCommand]
        public Task ShowWhatsNew() => ShowWhatsNewAsync(WhatsNewItems);

        [RelayCommand]
        public void ReplayWelcome() =>
            Application.Current!.Windows[0].Page = IPlatformApplication.Current!.Services.GetRequiredService<OnboardingPage>();

        [ObservableProperty]
        private string officialContentStatus = string.Empty;

        [ObservableProperty]
        private bool hasPendingContentConflicts;

        private async Task RefreshOfficialContentStatusAsync()
        {
            var upToDate = await _db.IsOfficialContentUpToDateAsync();
            var pending = await _db.GetPendingContentConflictCountAsync();
            HasPendingContentConflicts = pending > 0;
            OfficialContentStatus = Loc[upToDate ? "OfficialContentUpToDate" : "OfficialContentNotUpToDate"]
                + (pending > 0 ? " · " + string.Format(Loc["OfficialContentPending"], pending) : string.Empty);
        }

        public static Dictionary<string, string> LanguageLabels => LocalizationService.SupportedLanguages;

        public ObservableCollection<string> ThemeOptions { get; } = new();

        [ObservableProperty]
        private string selectedLanguage;

        public string? SelectedLanguageLabel =>
            LanguageLabels.TryGetValue(SelectedLanguage ?? "", out var label) ? label : SelectedLanguage;

        [ObservableProperty]
        private string selectedThemeOption;

        [ObservableProperty]
        private AppPalette selectedPalette;

        private readonly OfficialContentService _officialContent;
        private readonly SampleWarbandService _sampleWarbandService;
        private readonly AppTourService _tourService;
        private readonly WarriorPhotoStore _warriorPhotos;

        public SettingsViewModel(AppDatabase db, OfficialContentService officialContent,
            SampleWarbandService sampleWarbandService, AppTourService tourService, WarriorPhotoStore warriorPhotos,
            BackupService backup)
        {
            _db = db;
            _backup = backup;
            _warriorPhotos = warriorPhotos;
            _officialContent = officialContent;
            _sampleWarbandService = sampleWarbandService;
            _tourService = tourService;
            _ = Services.UiErrors.RunAsync(RefreshOfficialContentStatusAsync, nameof(SettingsViewModel), showMessage: false);
            selectedLanguage = Loc.Language;
            selectedPalette = _theme.Palette;
            RebuildThemeOptions();
            selectedThemeOption = ThemeOptions[(int)_theme.ThemePreference];

            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this,
                (r, m) => ((SettingsViewModel)r).RebuildThemeOptions());
        }

        private void RebuildThemeOptions()
        {
            var current = _theme.ThemePreference;

            string[] labels = [Loc["SettingsThemeSystem"], Loc["SettingsThemeLight"], Loc["SettingsThemeDark"]];
            for (int i = 0; i < labels.Length; i++)
            {
                if (i < ThemeOptions.Count) ThemeOptions[i] = labels[i];
                else ThemeOptions.Add(labels[i]);
            }

            _rebuildingOptions = true;
            SelectedThemeOption = ThemeOptions[(int)current];
            _rebuildingOptions = false;
        }

        private bool _rebuildingOptions;

        partial void OnSelectedLanguageChanged(string value)
        {
            if (value != null) Loc.Language = value;
            OnPropertyChanged(nameof(SelectedLanguageLabel));
        }

        partial void OnSelectedThemeOptionChanged(string value)
        {
            if (value is null || _rebuildingOptions) return;
            int idx = ThemeOptions.IndexOf(value);
            if (idx >= 0) _theme.ThemePreference = (AppThemePreference)idx;
        }

        partial void OnSelectedPaletteChanged(AppPalette value)
        {
            _theme.Palette = value;
        }
    }
}
