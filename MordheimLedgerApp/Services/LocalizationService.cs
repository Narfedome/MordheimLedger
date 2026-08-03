using CommunityToolkit.Mvvm.Messaging;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace MordheimLedgerApp.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        public static readonly Dictionary<string, string> SupportedLanguages = new()
        {
            { "fr", "Français" },
            { "en", "English" },
        };

        private static readonly ResourceManager _rm = new(
            "MordheimLedgerApp.Strings.AppStrings",
            typeof(LocalizationService).Assembly);

        private CultureInfo _culture;

        public static readonly LocalizationService Instance = new();

        private LocalizationService()
        {
            var deviceLang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var defaultLang = SupportedLanguages.ContainsKey(deviceLang) ? deviceLang : "fr";
            _culture = new CultureInfo(Preferences.Default.Get("app_lang", defaultLang));
        }

        public string this[string key] => _rm.GetString(key, _culture) ?? key;

        public static string GetString(string key, string languageCode) =>
            _rm.GetString(key, new CultureInfo(languageCode)) ?? key;

        public string Language
        {
            get => _culture.Name;
            set
            {
                if (_culture.Name == value) return;
                _culture = new CultureInfo(value);
                Preferences.Default.Set("app_lang", value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
                WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
