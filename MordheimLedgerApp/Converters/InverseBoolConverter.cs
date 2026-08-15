using MordheimLedgerApp.Resources.Icons;
using System.Globalization;

namespace MordheimLedgerApp.Converters
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : value!;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : value!;
    }

    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value != null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Contrairement à IsNotNullConverter, collapse aussi sur chaîne vide (ex.
    /// RecruitSlot.EquipmentSummary, toujours non-null mais vide tant qu'aucun objet n'est acheté).</summary>
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Opacity 0/1 plutôt qu'IsVisible=False : MAUI n'a pas de vrai "Hidden" (IsVisible=False
    /// retire l'élément du layout, l'espace se referme) - utilisé quand un bouton doit rester
    /// invisible tout en gardant sa place (ex. stepper +/- de WarriorRecruitListView, pour que le
    /// compteur ne saute pas latéralement quand + ou - disparaît). À coupler avec InputTransparent
    /// (via InverseBoolConverter) pour qu'un bouton à Opacity 0 ne reste pas cliquable.</summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && b ? 1d : 0d;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToChevronConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool isExpanded && isExpanded ? SolidFont.ChevronUp : SolidFont.ChevronDown;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
