using Microsoft.Maui.Devices;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>
/// A fixed desktop fraction of screen height would badly overshoot the space left above the
/// keyboard on phone (the keyboard + its suggestion bar easily take 35-45% of the screen): compute a
/// fraction of the device's actual height instead.
/// </summary>
public static class DialogSizing
{
    /// <summary>Zone défilante d'un dialog : 70 % de la hauteur de l'écran (50 % avant la refonte de
    /// novembre 2026), le reste pour le titre, les boutons et la marge - centré, le dialog remonte avec le
    /// clavier plutôt que d'être coupé par lui.</summary>
    public static double MaxContentHeight()
    {
        // Mobile : 70 % de la zone réellement visible sous le dialog (barre d'onglets du Shell décomptée),
        // pour que le dialog centré au-dessus de cette barre (voir DialogPage) ne déborde pas dessus.
        if (BottomBarInset() > 0 && HostPage() is { Height: > 0 } host)
            return host.Height * 0.7;
        var display = DeviceDisplay.Current.MainDisplayInfo;
        return display.Height / display.Density * 0.7;
    }

    /// <summary>Page Shell visible sous les dialogs : haut de la pile de l'onglet courant (fiche, formulaire
    /// plein écran...) ou sa page racine - jamais un DialogPage, qui vit dans la pile modale.</summary>
    private static Page? HostPage()
    {
        var section = Shell.Current?.CurrentItem?.CurrentItem;
        if (section is null) return null;
        return section.Stack.LastOrDefault(p => p is not null)
            ?? (section.CurrentItem as IShellContentController)?.Page;
    }

    /// <summary>Hauteur de la barre d'onglets du Shell en bas d'écran (Android/iOS), à décompter pour centrer
    /// un dialog dans la zone utile plutôt que sur tout l'écran (refonte de novembre 2026). 0 sur Windows (la
    /// barre n'y est pas en bas) ou si les tailles ne sont pas encore connues.</summary>
    public static double BottomBarInset()
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI) return 0;
        // Shell.Height n'est pas renseignée sur Android (-1) : la fenêtre sert de référence. Les pages de
        // l'appli masquent toutes la barre de navigation native, l'écart est donc la seule barre d'onglets
        // (80 dp mesurés sur l'émulateur 1080 × 2400) - 0 sur une page poussée sans barre d'onglets.
        var windowHeight = Application.Current?.Windows.FirstOrDefault()?.Height ?? 0;
        var host = HostPage();
        if (host is null || windowHeight <= 0 || host.Height <= 0) return 0;
        return Math.Max(0, windowHeight - host.Height);
    }

    /// <summary>Même plafond pour les listes à choix (ActionSheetDialog), au lieu d'un 320 px fixe qui cachait
    /// leurs dernières options.</summary>
    public static double MaxListHeight => MaxContentHeight();

    /// <summary>Largeur d'un dialog : l'écran moins 2 × 16 px, plafonnée à 480 px (téléphone/tablette) ou
    /// 560 px (Windows) - au lieu des largeurs fixes 320/340/360.</summary>
    public static double CardWidth(double availableWidth) =>
        Math.Max(0, Math.Min(availableWidth - 32, DeviceInfo.Idiom == DeviceIdiom.Desktop ? 560 : 480));
}
