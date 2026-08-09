using MordheimLedgerApp.Services;

namespace MordheimLedgerApp.Components.PalettePicker
{
    public partial class PalettePickerView : ContentView
    {
        public static readonly BindableProperty SelectedPaletteProperty =
            BindableProperty.Create(nameof(SelectedPalette), typeof(AppPalette), typeof(PalettePickerView),
                AppPalette.ShadowAndGold, BindingMode.TwoWay, propertyChanged: (b, _, n) =>
                    ((PalettePickerView)b).UpdateSelection((AppPalette)n));

        public AppPalette SelectedPalette
        {
            get => (AppPalette)GetValue(SelectedPaletteProperty);
            set => SetValue(SelectedPaletteProperty, value);
        }

        public PalettePickerView()
        {
            InitializeComponent();
            UpdateSelection(SelectedPalette);
        }

        private void SelectA(object? sender, TappedEventArgs e) => SelectedPalette = AppPalette.ShadowAndGold;
        private void SelectB(object? sender, TappedEventArgs e) => SelectedPalette = AppPalette.AshAndWarpstone;

        private void UpdateSelection(AppPalette palette)
        {
            SetBorder(BorderA, palette == AppPalette.ShadowAndGold, "#C9A66B");
            SetBorder(BorderB, palette == AppPalette.AshAndWarpstone, "#7FA34F");
        }

        private static void SetBorder(Border border, bool selected, string accentHex)
        {
            border.StrokeThickness = selected ? 3 : 1;
            border.Stroke = selected
                ? new SolidColorBrush(Color.FromArgb(accentHex))
                : new SolidColorBrush(Colors.Transparent);
        }
    }
}
