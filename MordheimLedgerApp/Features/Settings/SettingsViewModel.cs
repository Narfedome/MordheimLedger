using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MordheimLedgerApp.Services;
using System.Collections.ObjectModel;

namespace MordheimLedgerApp.Features.Settings
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly ThemeService _theme = ThemeService.Instance;

        // Windows exige 4 segments (Major.Minor.Build.Revision) pour l'identité de package - le 4e
        // (Revision) est une valeur fixe du csproj sans intérêt pour l'utilisateur, on l'aligne sur
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

        public SettingsViewModel()
        {
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
