using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;

namespace MordheimLedgerApp.Services
{
    public enum AppThemePreference
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    // Single entry for now (ash/wyrdstone-green) — same enum + switch-based token lookup as DmTools'
    // ThemeService so more palettes slot in later without touching Settings or this service's shape.
    public enum AppPalette
    {
        CendreEtWyrdstone = 0
    }

    public class ThemeService : INotifyPropertyChanged
    {
        public static readonly ThemeService Instance = new();

        private const string PaletteKey = "app_palette";
        private const string ThemePrefKey = "app_theme";

        private AppPalette _palette;
        private AppThemePreference _themePref;

        public event Action? ThemeChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private ThemeService()
        {
            _palette = (AppPalette)Preferences.Default.Get(PaletteKey, (int)AppPalette.CendreEtWyrdstone);
            _themePref = (AppThemePreference)Preferences.Default.Get(ThemePrefKey, (int)AppThemePreference.System);
        }

        // No embedded watermark art yet for any palette (single entry, "Cendre & Wyrdstone") - returns
        // null until a resource exists, cf. DmTools' ImageSource.FromResource per-palette lookup for
        // the pattern to extend once art is available (WatermarkedLayout.xaml already wired for it).
        public ImageSource? WatermarkImageSource => null;

        public AppPalette Palette
        {
            get => _palette;
            set
            {
                if (_palette == value) return;
                _palette = value;
                Preferences.Default.Set(PaletteKey, (int)value);
                Apply();
                ThemeChanged?.Invoke();
                OnPropertyChanged(nameof(WatermarkImageSource));
            }
        }

        public AppThemePreference ThemePreference
        {
            get => _themePref;
            set
            {
                if (_themePref == value) return;
                _themePref = value;
                Preferences.Default.Set(ThemePrefKey, (int)value);
                ApplyThemePreference();
                Apply();
                ThemeChanged?.Invoke();
            }
        }

        public void Initialize()
        {
            if (Application.Current is not null)
                Application.Current.RequestedThemeChanged += (_, _) =>
                {
                    if (_themePref == AppThemePreference.System) Apply();
                };

            ApplyThemePreference();
            Apply();
        }

        private void ApplyThemePreference()
        {
            if (Application.Current is null) return;
            Application.Current.UserAppTheme = _themePref switch
            {
                AppThemePreference.Light => AppTheme.Light,
                AppThemePreference.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        public bool IsDark() =>
            _themePref == AppThemePreference.Dark ||
            (_themePref == AppThemePreference.System && Application.Current?.RequestedTheme == AppTheme.Dark);

        public void Apply()
        {
            if (Application.Current?.Resources is null) return;
            var tokens = GetPaletteTokens(_palette, IsDark());
            tokens["AppSurfaceTranslucent"] = tokens["AppSurface"].WithAlpha(0.80f);
            tokens["AppDanger"] = Color.FromArgb("#8B3A3A");
            var res = Application.Current.Resources;
            foreach (var kv in tokens)
                res[kv.Key] = kv.Value;
        }

        public Color CurrentAccent => GetPaletteTokens(_palette, IsDark()).GetValueOrDefault("AppAccent", Colors.Gray);
        public Color CurrentAccentSecondary => GetPaletteTokens(_palette, IsDark()).GetValueOrDefault("AppAccentSecondary", Colors.Gray);

        private static Dictionary<string, Color> GetPaletteTokens(AppPalette palette, bool dark) => palette switch
        {
            AppPalette.CendreEtWyrdstone => dark
                ? new()
                {
                    ["AppBackground"]      = Color.FromArgb("#17151A"),
                    ["AppSurface"]         = Color.FromArgb("#241F26"),
                    ["AppAccent"]          = Color.FromArgb("#7FA34F"),
                    ["AppAccentSecondary"] = Color.FromArgb("#5C6B73"),
                    ["AppText"]            = Color.FromArgb("#E8E4D8"),
                    ["AppTextMuted"]       = Color.FromArgb("#948C7E"),
                    ["AppBorder"]          = Color.FromArgb("#35303A"),
                }
                : new()
                {
                    ["AppBackground"]      = Color.FromArgb("#EDE7D9"),
                    ["AppSurface"]         = Color.FromArgb("#F5F1E6"),
                    ["AppAccent"]          = Color.FromArgb("#4F7A34"),
                    ["AppAccentSecondary"] = Color.FromArgb("#3D4A50"),
                    ["AppText"]            = Color.FromArgb("#201C1F"),
                    ["AppTextMuted"]       = Color.FromArgb("#6B6258"),
                    ["AppBorder"]          = Color.FromArgb("#D8D0BC"),
                },

            _ => new()
        };

        public static (Color dark, Color light, Color accent) GetPaletteSwatchColors(AppPalette p) => p switch
        {
            AppPalette.CendreEtWyrdstone => (Color.FromArgb("#17151A"), Color.FromArgb("#EDE7D9"), Color.FromArgb("#7FA34F")),
            _                             => (Colors.Black, Colors.White, Colors.Gray)
        };
    }
}
