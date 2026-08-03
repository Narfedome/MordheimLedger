using Microsoft.Maui.Devices;

namespace MordheimLedgerApp.Components.Dialogs;

/// <summary>
/// A fixed desktop fraction of screen height would badly overshoot the space left above the
/// keyboard on phone (the keyboard + its suggestion bar easily take 35-45% of the screen): compute a
/// fraction of the device's actual height instead.
/// </summary>
internal static class DialogSizing
{
    public static double MaxContentHeight()
    {
        var display = DeviceDisplay.Current.MainDisplayInfo;
        return display.Height / display.Density * 0.5;
    }
}
