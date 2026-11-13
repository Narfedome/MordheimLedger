<#
.SYNOPSIS
    Outils de pilotage de l'appli Windows pour les captures du Microsoft Store, sans jamais toucher a la
    souris ni au clavier reels : UI Automation (recherche par AutomationId ou par nom, motif Invoke),
    toucher simule (InjectTouchInput, le curseur ne bouge pas) et capture de la fenetre seule (PrintWindow).
    A charger par dot-sourcing : . .\WindowsUi.ps1
#>

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing

if (-not ("MordheimUi.Native" -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace MordheimUi {
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct POINTER_INFO {
        public int pointerType; public uint pointerId; public uint frameId; public int pointerFlags;
        public IntPtr sourceDevice; public IntPtr hwndTarget; public POINT ptPixelLocation; public POINT ptHimetricLocation;
        public POINT ptPixelLocationRaw; public POINT ptHimetricLocationRaw; public uint dwTime; public uint historyCount;
        public int InputData; public uint dwKeyStates; public ulong PerformanceCount; public int ButtonChangeType; }
    [StructLayout(LayoutKind.Sequential)] struct POINTER_TOUCH_INFO {
        public POINTER_INFO pointerInfo; public int touchFlags; public int touchMask;
        public RECT rcContact; public RECT rcContactRaw; public uint orientation; public uint pressure; }

    [DllImport("user32.dll")] static extern bool InitializeTouchInjection(uint maxCount, int dwMode);
    [DllImport("user32.dll")] static extern bool InjectTouchInput(uint count, [In] POINTER_TOUCH_INFO[] contacts);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    public static IntPtr Target = IntPtr.Zero;

    /// Refuse de toucher un point qui n'appartient pas a la fenetre de l'appli (autre fenetre par-dessus).
    static void CheckTarget(int x, int y) {
        if (Target == IntPtr.Zero) return;
        IntPtr root = GetAncestor(WindowFromPoint(new POINT { X = x, Y = y }), 2); // GA_ROOT
        if (root != Target) throw new Exception("Toucher annule : le point (" + x + "," + y + ") n'est pas sur la fenetre de l'appli.");
    }
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

    static bool _touchReady;

    static POINTER_TOUCH_INFO Contact(int x, int y, int flags) {
        var c = new POINTER_TOUCH_INFO();
        c.pointerInfo.pointerType = 2; // PT_TOUCH
        c.pointerInfo.pointerId = 0;
        c.pointerInfo.ptPixelLocation = new POINT { X = x, Y = y };
        c.pointerInfo.pointerFlags = flags;
        c.touchFlags = 0; c.touchMask = 0x7; c.orientation = 90; c.pressure = 32000;
        c.rcContact = new RECT { Left = x - 2, Top = y - 2, Right = x + 2, Bottom = y + 2 };
        return c;
    }

    /// Toucher simule aux coordonnees ecran : le curseur de la souris ne bouge pas.
    public static void Tap(int x, int y) {
        CheckTarget(x, y);
        if (!_touchReady) { _touchReady = InitializeTouchInjection(1, 1); if (!_touchReady) throw new Exception("InitializeTouchInjection : " + Marshal.GetLastWin32Error()); }
        const int DOWN = 0x10000 | 0x2 | 0x4, UP = 0x40000, UPDATE = 0x20000 | 0x2 | 0x4;
        if (!InjectTouchInput(1, new[] { Contact(x, y, DOWN) })) throw new Exception("InjectTouchInput (down) : " + Marshal.GetLastWin32Error());
        Thread.Sleep(40);
        InjectTouchInput(1, new[] { Contact(x, y, UPDATE) });
        Thread.Sleep(40);
        InjectTouchInput(1, new[] { Contact(x, y, UP) });
    }

    /// Glissement simule (defilement) de (x1,y1) a (x2,y2).
    public static void Swipe(int x1, int y1, int x2, int y2, int steps) {
        CheckTarget(x1, y1);
        if (!_touchReady) { _touchReady = InitializeTouchInjection(1, 1); }
        const int DOWN = 0x10000 | 0x2 | 0x4, UP = 0x40000, UPDATE = 0x20000 | 0x2 | 0x4;
        InjectTouchInput(1, new[] { Contact(x1, y1, DOWN) });
        for (int i = 1; i <= steps; i++) {
            Thread.Sleep(16);
            InjectTouchInput(1, new[] { Contact(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps, UPDATE) });
        }
        Thread.Sleep(300);
        InjectTouchInput(1, new[] { Contact(x2, y2, UP) });
    }

    /// Rectangle visible de la fenetre (sans l'ombre invisible de Windows 11).
    public static RECT FrameRect(IntPtr hwnd) {
        RECT r;
        if (DwmGetWindowAttribute(hwnd, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(hwnd, out r);
        return r;
    }

    /// Capture de la fenetre seule (PrintWindow, PW_RENDERFULLCONTENT), rognee a son cadre visible.
    public static void Capture(IntPtr hwnd, string path) {
        RECT w; GetWindowRect(hwnd, out w);
        RECT f = FrameRect(hwnd);
        int ww = w.Right - w.Left, wh = w.Bottom - w.Top;
        using (var bmp = new Bitmap(ww, wh, PixelFormat.Format32bppArgb)) {
            using (var g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            var crop = new Rectangle(f.Left - w.Left, f.Top - w.Top, f.Right - f.Left, f.Bottom - f.Top);
            using (var outBmp = bmp.Clone(crop, PixelFormat.Format24bppRgb)) outBmp.Save(path, ImageFormat.Png);
        }
    }
}
}
"@
}

$script:Uia = [System.Windows.Automation.AutomationElement]
$script:Tree = [System.Windows.Automation.TreeScope]

function Get-AppWindow([System.Diagnostics.Process]$Process, [int]$TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) { throw "L'appli s'est fermee au demarrage." }
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) { $script:AppHwnd = $Process.MainWindowHandle; [MordheimUi.Native]::Target = $script:AppHwnd; return $Process.MainWindowHandle }
        Start-Sleep -Milliseconds 500
    }
    throw "Fenetre de l'appli introuvable apres $TimeoutSeconds s."
}

function Get-UiRoot([IntPtr]$Hwnd) { $script:Uia::FromHandle($Hwnd) }

function Find-UiElement([IntPtr]$Hwnd, [string]$AutomationId, [string]$Name, [System.Windows.Automation.ControlType]$ControlType,
                        [int]$TimeoutSeconds = 20, [switch]$Optional) {
    $prop = if ($AutomationId) { $script:Uia::AutomationIdProperty } else { $script:Uia::NameProperty }
    $value = if ($AutomationId) { $AutomationId } else { $Name }
    $cond = New-Object System.Windows.Automation.PropertyCondition($prop, $value)
    if ($ControlType) {
        $typeCond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::ControlTypeProperty, $ControlType)
        $cond = New-Object System.Windows.Automation.AndCondition($cond, $typeCond)
    }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        # Premier element visible : une page Shell precedente peut garder une copie hors ecran du meme texte.
        $el = (Get-UiRoot $Hwnd).FindAll($script:Tree::Descendants, $cond) | Where-Object { -not $_.Current.IsOffscreen } | Select-Object -First 1
        if ($el) { return $el }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
    if ($Optional) { return $null }
    throw "Element introuvable : $value"
}

function Find-UiElements([IntPtr]$Hwnd, [string]$AutomationId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:Uia::AutomationIdProperty, $AutomationId)
    @((Get-UiRoot $Hwnd).FindAll($script:Tree::Descendants, $cond) | Where-Object { -not $_.Current.IsOffscreen })
}

# Toucher simule au centre de l'element (fonctionne pour les zones tapables MAUI, qui n'exposent pas toutes Invoke).
# Le toucher simule vise des coordonnees ecran : la fenetre de l'appli doit etre au premier plan.
function Invoke-UiTap($Element) {
    if ($script:AppHwnd) { [MordheimUi.Native]::SetForegroundWindow($script:AppHwnd) | Out-Null; Start-Sleep -Milliseconds 200 }
    $r = $Element.Current.BoundingRectangle
    [MordheimUi.Native]::Tap([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
}

function Invoke-UiElement($Element) {
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke() }
    else { Invoke-UiTap $Element }
}

# Toucher aux coordonnees relatives a la fenetre (pour le contenu des dialogs, invisible pour UI Automation).
function Invoke-WindowTap([IntPtr]$Hwnd, [int]$X, [int]$Y) {
    [MordheimUi.Native]::SetForegroundWindow($Hwnd) | Out-Null; Start-Sleep -Milliseconds 200
    $f = [MordheimUi.Native]::FrameRect($Hwnd)
    [MordheimUi.Native]::Tap($f.Left + $X, $f.Top + $Y)
}

function Invoke-WindowSwipe([IntPtr]$Hwnd, [int]$X, [int]$FromY, [int]$ToY) {
    $f = [MordheimUi.Native]::FrameRect($Hwnd)
    [MordheimUi.Native]::Swipe($f.Left + $X, $f.Top + $FromY, $f.Left + $X, $f.Top + $ToY, 25)
}

function Set-AppWindowSize([IntPtr]$Hwnd, [int]$Width, [int]$Height) {
    [MordheimUi.Native]::ShowWindow($Hwnd, 9) | Out-Null # SW_RESTORE
    # Toujours au-dessus le temps des captures : le toucher simule vise l'ecran, et Windows refuse souvent le premier
    # plan a un processus d'arriere-plan. Voir Clear-AppWindowTopmost.
    [MordheimUi.Native]::SetWindowPos($Hwnd, [IntPtr]-1, 40, 40, $Width, $Height, 0x0040) | Out-Null # HWND_TOPMOST, SWP_SHOWWINDOW
    [MordheimUi.Native]::SetForegroundWindow($Hwnd) | Out-Null
}

function Clear-AppWindowTopmost([IntPtr]$Hwnd) {
    [MordheimUi.Native]::SetWindowPos($Hwnd, [IntPtr]-2, 0, 0, 0, 0, 0x0003) | Out-Null # HWND_NOTOPMOST, SWP_NOSIZE|SWP_NOMOVE
}

function Save-AppWindow([IntPtr]$Hwnd, [string]$Path) {
    [MordheimUi.Native]::Capture($Hwnd, $Path)
    Write-Host "  $Path" -ForegroundColor Green
}

function Write-UiDump([IntPtr]$Hwnd) {
    $all = (Get-UiRoot $Hwnd).FindAll($script:Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $c = $e.Current
        if ($c.IsOffscreen) { continue }
        $r = $c.BoundingRectangle
        "{0,-14} id={1,-28} name={2,-40} [{3},{4} {5}x{6}]" -f $c.ControlType.ProgrammaticName.Replace("ControlType.", ""), $c.AutomationId, ($c.Name -replace "\s+", " "), [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height
    }
}
