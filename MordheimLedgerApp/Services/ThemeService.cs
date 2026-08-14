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

    // Deux entrées (grim/cendre + wyrdstone-vert, ténèbre + or) — même enum + switch-based token lookup
    // que DmTools' ThemeService, plus de palettes pourront s'ajouter plus tard sans retoucher Settings
    // ni la forme de ce service.
    public enum AppPalette
    {
        AshAndWarpstone = 0,
        ShadowAndGold
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
            // Ténèbre & Or = palette par défaut (demande explicite de l'utilisateur, préférée à Cendre &
            // Wyrdstone après comparaison des deux).
            _palette = (AppPalette)Preferences.Default.Get(PaletteKey, (int)AppPalette.ShadowAndGold);
            _themePref = (AppThemePreference)Preferences.Default.Get(ThemePrefKey, (int)AppThemePreference.System);
        }

        // Même pattern que DmTools' ImageSource.FromResource per-palette lookup - le nom passé ici doit
        // correspondre au LogicalName déclaré sur l'EmbeddedResource (voir MordheimLedgerApp.csproj),
        // pas au chemin complet du fichier.
        public ImageSource? WatermarkImageSource => ImageSource.FromResource(_palette switch
        {
            AppPalette.AshAndWarpstone => "green.png",
            AppPalette.ShadowAndGold => "gold.png",
            _ => "gold.png",
        }, typeof(ThemeService).Assembly);

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
            AppPalette.AshAndWarpstone => dark
                ? new()
                {
                    ["AppBackground"]      = Color.FromArgb("#17151A"),
                    ["AppSurface"]         = Color.FromArgb("#241F26"),
                    ["AppAccent"]          = Color.FromArgb("#7FA34F"),
                    ["AppAccentSecondary"] = Color.FromArgb("#5C6B73"),
                    ["AppText"]            = Color.FromArgb("#E8E4D8"),
                    ["AppTextMuted"]       = Color.FromArgb("#948C7E"),
                    ["AppBorder"]          = Color.FromArgb("#35303A"),
                    ["AppMilestone"]       = Color.FromArgb("#C9A227"),
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
                    ["AppMilestone"]       = Color.FromArgb("#C9A227"),
                },

            // Noir & or proche de la couverture du livre de règles (fond quasi-noir, lettrage or antique,
            // accent rouge sombre repris de la bannière derrière "MORDHEIM") plutôt que le violet/indigo
            // de DmTools' NuitEtOr - demande explicite de l'utilisateur après comparaison avec les
            // références (couverture GW + sites fan), teintes propres à cette palette.
            AppPalette.ShadowAndGold => dark
                ? new()
                {
                    ["AppBackground"]      = Color.FromArgb("#0D0B0A"),
                    ["AppSurface"]         = Color.FromArgb("#1A1512"),
                    ["AppAccent"]          = Color.FromArgb("#C9A66B"),
                    ["AppAccentSecondary"] = Color.FromArgb("#5C1A1E"),
                    ["AppText"]            = Color.FromArgb("#EDE0C0"),
                    ["AppTextMuted"]       = Color.FromArgb("#9C8F72"),
                    ["AppBorder"]          = Color.FromArgb("#332B22"),
                    ["AppMilestone"]       = Color.FromArgb("#8B2331"),
                }
                : new()
                {
                    ["AppBackground"]      = Color.FromArgb("#E8DBB8"),
                    ["AppSurface"]         = Color.FromArgb("#F2E8CC"),
                    ["AppAccent"]          = Color.FromArgb("#8B6F1F"),
                    ["AppAccentSecondary"] = Color.FromArgb("#6B1F22"),
                    ["AppText"]            = Color.FromArgb("#241F16"),
                    ["AppTextMuted"]       = Color.FromArgb("#6B5D42"),
                    ["AppBorder"]          = Color.FromArgb("#D4C79E"),
                    ["AppMilestone"]       = Color.FromArgb("#8B2331"),
                },

            _ => new()
        };

        public static (Color dark, Color light, Color accent) GetPaletteSwatchColors(AppPalette p) => p switch
        {
            AppPalette.AshAndWarpstone => (Color.FromArgb("#17151A"), Color.FromArgb("#EDE7D9"), Color.FromArgb("#7FA34F")),
            AppPalette.ShadowAndGold       => (Color.FromArgb("#0D0B0A"), Color.FromArgb("#E8DBB8"), Color.FromArgb("#C9A66B")),
            _                             => (Colors.Black, Colors.White, Colors.Gray)
        };
    }
}
