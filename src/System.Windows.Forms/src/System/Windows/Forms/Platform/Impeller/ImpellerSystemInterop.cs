// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Accessibility;
using System.Runtime.CompilerServices;
using Windows.Win32.System.ApplicationInstallationAndServicing;

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller system interop — timers, metrics, DPI, clipboard, shell, memory.
/// </summary>
internal sealed unsafe class ImpellerSystemInterop : ISystemInterop
{
    private const int DefaultWorkAreaWidth = 1920;
    private const int DefaultWorkAreaHeight = 1080;

    [ThreadStatic]
    private static uint s_lastError;

    [ThreadStatic]
    private static nint s_currentActivationContext;

    [ThreadStatic]
    private static Dictionary<nuint, nint>? s_activationCookiePrevious;

    private long _nextTimerId = 1;
    private long _nextActivationContextHandle = 0x600000;
    private long _nextActivationCookie;
    private readonly Dictionary<nint, System.Threading.Timer> _timers = [];
    private readonly HashSet<nint> _activationContexts = [];
    private readonly Dictionary<string, uint> _clipboardFormats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _clipboardFormatNames = [];
    private readonly Dictionary<uint, HANDLE> _clipboardData = [];
    private readonly Dictionary<nint, nuint> _globalMemorySizes = [];
    private DPI_AWARENESS_CONTEXT _processDpiAwarenessContext = DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2;
    private DPI_AWARENESS_CONTEXT _threadDpiAwarenessContext = DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2;
    private DPI_HOSTING_BEHAVIOR _threadDpiHostingBehavior = DPI_HOSTING_BEHAVIOR.DPI_HOSTING_BEHAVIOR_MIXED;
    private uint _nextClipboardFormat = 0xC000;
    private bool _clientAreaAnimation = true;
    private bool _dragFullWindows = true;
    private int _wheelScrollLines = 3;
    private int _mouseHoverWidth = 4;
    private int _mouseHoverHeight = 4;
    private int _mouseHoverTime = 400;
    private int _mouseSpeed = 10;
    private int _keyBoardDelay = 1;
    private int _menuDropAlignment;
    private int _menuShowDelay = 400;
    private bool _menuFade;
    private bool _menuAnimation;
    private bool _fontSmoothing = true;
    private bool _iconTitleWrap;
    private bool _keyboardCues = true;
    private bool _keyboardPref = true;
    private bool _listBoxSmoothScrolling = true;
    private bool _comboBoxAnimation = true;
    private bool _gradientCaptions = true;
    private bool _hotTracking = true;
    private bool _selectionFade = true;
    private bool _toolTipAnimation = true;
    private bool _uiEffects = true;
    private bool _activeWindowTracking;
    private int _activeWindowTrackingTimeout = 1000;
    private bool _animation = true;
    private int _caretWidth = 1;
    private int _caretBlinkTime = 500;
    private int _border = 4;
    private int _snapToDefButton = 1;
    private int _mouseTrails = 0;
    private uint _highContrastFlags = 0;
    private nint _defaultInputLanguage = unchecked((nint)0x04090409);
    private RECT _workArea = new(0, 0, DefaultWorkAreaWidth, DefaultWorkAreaHeight);

    // --- Timers ---------------------------------------------------------

    public nint SetTimer(HWND hWnd, nint id, uint elapse, nint proc)
    {
        var timerId = Interlocked.Increment(ref _nextTimerId);
        return (nint)timerId;
    }

    public bool KillTimer(HWND hWnd, nint id)
    {
        if (_timers.Remove(id, out var t))
        { t.Dispose(); }
        return true;
    }

    // --- System Metrics / Info ------------------------------------------

    public int GetSystemMetrics(SYSTEM_METRICS_INDEX idx) => idx switch
    {
        SYSTEM_METRICS_INDEX.SM_CXSCREEN => 1920,
        SYSTEM_METRICS_INDEX.SM_CYSCREEN => 1080,
        SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN => 0,
        SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN => 0,
        SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN => 1920,
        SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN => 1080,
        SYSTEM_METRICS_INDEX.SM_CXVSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYVSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CXHSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYHSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYVTHUMB => 17,
        SYSTEM_METRICS_INDEX.SM_CXHTHUMB => 17,
        SYSTEM_METRICS_INDEX.SM_CYCAPTION => 30,
        SYSTEM_METRICS_INDEX.SM_CYSMCAPTION => 23,
        SYSTEM_METRICS_INDEX.SM_CXBORDER => 1,
        SYSTEM_METRICS_INDEX.SM_CYBORDER => 1,
        SYSTEM_METRICS_INDEX.SM_CXFIXEDFRAME => 4,
        SYSTEM_METRICS_INDEX.SM_CYFIXEDFRAME => 4,
        SYSTEM_METRICS_INDEX.SM_CXFRAME => 4,
        SYSTEM_METRICS_INDEX.SM_CYFRAME => 4,
        SYSTEM_METRICS_INDEX.SM_CXEDGE => 2,
        SYSTEM_METRICS_INDEX.SM_CYEDGE => 2,
        SYSTEM_METRICS_INDEX.SM_CXSMICON => 16,
        SYSTEM_METRICS_INDEX.SM_CYSMICON => 16,
        SYSTEM_METRICS_INDEX.SM_CXICON => 32,
        SYSTEM_METRICS_INDEX.SM_CYICON => 32,
        SYSTEM_METRICS_INDEX.SM_CXCURSOR => 32,
        SYSTEM_METRICS_INDEX.SM_CYCURSOR => 32,
        SYSTEM_METRICS_INDEX.SM_CXMENUCHECK => 16,
        SYSTEM_METRICS_INDEX.SM_CYMENUCHECK => 16,
        SYSTEM_METRICS_INDEX.SM_CXMENUSIZE => 18,
        SYSTEM_METRICS_INDEX.SM_CYMENUSIZE => 18,
        SYSTEM_METRICS_INDEX.SM_CXSIZE => 30,
        SYSTEM_METRICS_INDEX.SM_CYSIZE => 30,
        SYSTEM_METRICS_INDEX.SM_CXSMSIZE => 16,
        SYSTEM_METRICS_INDEX.SM_CYSMSIZE => 16,
        SYSTEM_METRICS_INDEX.SM_CYMENU => 20,
        SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK => 4,
        SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK => 4,
        SYSTEM_METRICS_INDEX.SM_CXMIN => 112,
        SYSTEM_METRICS_INDEX.SM_CYMIN => 27,
        SYSTEM_METRICS_INDEX.SM_CXMINTRACK => 136,
        SYSTEM_METRICS_INDEX.SM_CYMINTRACK => 39,
        SYSTEM_METRICS_INDEX.SM_CXMAXTRACK => 1920,
        SYSTEM_METRICS_INDEX.SM_CYMAXTRACK => 1080,
        SYSTEM_METRICS_INDEX.SM_CXMAXIMIZED => 1920,
        SYSTEM_METRICS_INDEX.SM_CYMAXIMIZED => 1040,
        SYSTEM_METRICS_INDEX.SM_CXMINIMIZED => 160,
        SYSTEM_METRICS_INDEX.SM_CYMINIMIZED => 27,
        SYSTEM_METRICS_INDEX.SM_CXMINSPACING => 160,
        SYSTEM_METRICS_INDEX.SM_CYMINSPACING => 27,
        SYSTEM_METRICS_INDEX.SM_CXICONSPACING => 75,
        SYSTEM_METRICS_INDEX.SM_CYICONSPACING => 75,
        SYSTEM_METRICS_INDEX.SM_CXFOCUSBORDER => 1,
        SYSTEM_METRICS_INDEX.SM_CYFOCUSBORDER => 1,
        SYSTEM_METRICS_INDEX.SM_CXDRAG => 4,
        SYSTEM_METRICS_INDEX.SM_CYDRAG => 4,
        SYSTEM_METRICS_INDEX.SM_CYKANJIWINDOW => 0,
        SYSTEM_METRICS_INDEX.SM_CMONITORS => 1,
        SYSTEM_METRICS_INDEX.SM_SAMEDISPLAYFORMAT => 1,
        SYSTEM_METRICS_INDEX.SM_CMOUSEBUTTONS => 3,
        SYSTEM_METRICS_INDEX.SM_MOUSEPRESENT => 1,
        SYSTEM_METRICS_INDEX.SM_SWAPBUTTON => 0,
        SYSTEM_METRICS_INDEX.SM_MOUSEWHEELPRESENT => 1,
        SYSTEM_METRICS_INDEX.SM_MENUDROPALIGNMENT => 0,
        SYSTEM_METRICS_INDEX.SM_ARRANGE => 0,
        SYSTEM_METRICS_INDEX.SM_NETWORK => 0,
        SYSTEM_METRICS_INDEX.SM_REMOTESESSION => 0,
        SYSTEM_METRICS_INDEX.SM_CLEANBOOT => 0,
        SYSTEM_METRICS_INDEX.SM_SHOWSOUNDS => 0,
        SYSTEM_METRICS_INDEX.SM_MIDEASTENABLED => 0,
        SYSTEM_METRICS_INDEX.SM_PENWINDOWS => 0,
        SYSTEM_METRICS_INDEX.SM_DBCSENABLED => 0,
        SYSTEM_METRICS_INDEX.SM_SECURE => 0,
        SYSTEM_METRICS_INDEX.SM_DEBUG => 0,
        _ => 0,
    };

    public uint GetCaretBlinkTime() => (uint)_caretBlinkTime;

    public unsafe bool SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION action, uint param, void* pvParam, uint flags)
    {
        return action switch
        {
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION => ReadBoolean(pvParam, _clientAreaAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDRAGFULLWINDOWS => ReadBoolean(pvParam, _dragFullWindows),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFONTSMOOTHING => ReadBoolean(pvParam, _fontSmoothing),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETICONTITLEWRAP => ReadBoolean(pvParam, _iconTitleWrap),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDCUES => ReadBoolean(pvParam, _keyboardCues),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDPREF => ReadBoolean(pvParam, _keyboardPref),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETLISTBOXSMOOTHSCROLLING => ReadBoolean(pvParam, _listBoxSmoothScrolling),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETTOOLTIPANIMATION => ReadBoolean(pvParam, _toolTipAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETUIEFFECTS => ReadBoolean(pvParam, _uiEffects),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMENUFADE => ReadBoolean(pvParam, _menuFade),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETACTIVEWINDOWTRACKING => ReadBoolean(pvParam, _activeWindowTracking),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETANIMATION => ReadBoolean(pvParam, _animation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCOMBOBOXANIMATION => ReadBoolean(pvParam, _comboBoxAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETGRADIENTCAPTIONS => ReadBoolean(pvParam, _gradientCaptions),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHOTTRACKING => ReadBoolean(pvParam, _hotTracking),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSELECTIONFADE => ReadBoolean(pvParam, _selectionFade),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDROPSHADOW => ReadBoolean(pvParam, true),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFLATMENU => ReadBoolean(pvParam, true),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSNAPTODEFBUTTON => ReadBoolean(pvParam, _snapToDefButton == 1),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMENUSHOWDELAY => ReadInt(pvParam, _menuShowDelay),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMOUSEHOVERWIDTH => ReadInt(pvParam, _mouseHoverWidth),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMOUSEHOVERHEIGHT => ReadInt(pvParam, _mouseHoverHeight),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMOUSEHOVERTIME => ReadInt(pvParam, _mouseHoverTime),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMENUANIMATION => ReadInt(pvParam, _menuAnimation ? 1 : 0),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDSPEED => ReadInt(pvParam, _mouseSpeed),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDDELAY => ReadInt(pvParam, _keyBoardDelay),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETBORDER => ReadInt(pvParam, _border),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_ICONHORIZONTALSPACING => ReadInt(pvParam, 75),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_ICONVERTICALSPACING => ReadInt(pvParam, 75),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMENUDROPALIGNMENT => ReadInt(pvParam, _menuDropAlignment),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFONTSMOOTHINGCONTRAST => ReadInt(pvParam, 0),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFONTSMOOTHINGTYPE => ReadInt(pvParam, 2),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCARETWIDTH => ReadInt(pvParam, _caretWidth),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWHEELSCROLLLINES => ReadInt(pvParam, _wheelScrollLines),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETACTIVEWNDTRKTIMEOUT => ReadInt(pvParam, _activeWindowTrackingTimeout),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSCREENSAVERRUNNING => ReadInt(pvParam, 0),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETSCREENREADER => ReadInt(pvParam, 0),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETMOUSESPEED => ReadInt(pvParam, _mouseSpeed),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA => ReadRect(pvParam, in _workArea),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST => ReadHighContrast(pvParam, (int)param, _highContrastFlags),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDEFAULTINPUTLANG => ReadNint(pvParam, _defaultInputLanguage),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETNONCLIENTMETRICS => ReadNonClientMetrics(pvParam, (int)param),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION => SetBoolean(pvParam, ref _clientAreaAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDRAGFULLWINDOWS => SetBoolean(pvParam, ref _dragFullWindows),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETFONTSMOOTHING => SetBoolean(pvParam, ref _fontSmoothing),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETICONTITLEWRAP => SetBoolean(pvParam, ref _iconTitleWrap),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETKEYBOARDCUES => SetBoolean(pvParam, ref _keyboardCues),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETKEYBOARDPREF => SetBoolean(pvParam, ref _keyboardPref),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETLISTBOXSMOOTHSCROLLING => SetBoolean(pvParam, ref _listBoxSmoothScrolling),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETTOOLTIPANIMATION => SetBoolean(pvParam, ref _toolTipAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETUIEFFECTS => SetBoolean(pvParam, ref _uiEffects),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMENUFADE => SetBoolean(pvParam, ref _menuFade),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETBEEP => true,
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETACTIVEWINDOWTRACKING => SetBoolean(pvParam, ref _activeWindowTracking),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDOUBLECLICKTIME => SetIntFromParam(param, ref _caretBlinkTime),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWHEELSCROLLLINES => SetIntFromParam(param, ref _wheelScrollLines),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMOUSESPEED => SetIntFromParam(param, ref _mouseSpeed),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA => StoreRectAndWrite(pvParam, ref _workArea),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMOUSEHOVERWIDTH => SetIntFromParam(param, ref _mouseHoverWidth),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMOUSEHOVERHEIGHT => SetIntFromParam(param, ref _mouseHoverHeight),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMOUSETRAILS => SetIntFromParam(param, ref _mouseTrails),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMENUANIMATION => SetBoolean(pvParam, ref _menuAnimation),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETSCREENSAVETIMEOUT => true,
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMENUDROPALIGNMENT => SetIntFromParam(param, ref _menuDropAlignment),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETMENUSHOWDELAY => SetIntFromParam(param, ref _menuShowDelay),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETKEYBOARDDELAY => SetIntFromParam(param, ref _keyBoardDelay),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCARETWIDTH => SetIntFromParam(param, ref _caretWidth),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETACTIVEWNDTRKTIMEOUT => SetIntFromParam(param, ref _activeWindowTrackingTimeout),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETHIGHCONTRAST => SetHighContrast(pvParam),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETNONCLIENTMETRICS => SetNonClientMetrics(pvParam),
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETICONTITLELOGFONT => true,
            SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETDESKWALLPAPER => true,
            _ => true,
        };
    }

    private static unsafe bool SetBoolean(void* pvParam, ref bool destination)
    {
        if (pvParam is not null)
        {
            destination = *(BOOL*)pvParam != 0;
        }

        return true;
    }

    private static unsafe bool ReadBoolean(void* pvParam, bool value)
    {
        if (pvParam is null)
        {
            return true;
        }

        *((uint*)pvParam) = value ? 1u : 0u;
        return true;
    }

    private static unsafe bool SetIntFromParam(uint param, ref int destination)
    {
        destination = (int)param;
        return true;
    }

    private static unsafe bool ReadInt(void* pvParam, int value)
    {
        if (pvParam is null)
        {
            return true;
        }

        *((int*)pvParam) = value;
        return true;
    }

    private static unsafe bool ReadRect(void* pvParam, in RECT value)
    {
        if (pvParam is null)
        {
            return true;
        }

        *(RECT*)pvParam = value;
        return true;
    }

    private static unsafe bool ReadNint(void* pvParam, nint value)
    {
        if (pvParam is null)
        {
            return true;
        }

        *(nint*)pvParam = value;
        return true;
    }

    private static unsafe bool StoreRectAndWrite(void* pvParam, ref RECT destination)
    {
        if (pvParam is null)
        {
            return true;
        }

        destination = *(RECT*)pvParam;
        return true;
    }

    private static unsafe bool ReadHighContrast(void* pvParam, int uiParam, uint flags)
    {
        if (pvParam is null)
        {
            return true;
        }

        var highContrast = (HIGHCONTRASTW*)pvParam;
        highContrast->cbSize = (uint)(uiParam == 0
            ? (uint)sizeof(HIGHCONTRASTW)
            : (uint)uiParam);
        highContrast->dwFlags = (HIGHCONTRASTW_FLAGS)flags;
        highContrast->lpszDefaultScheme = null;
        return true;
    }

    private unsafe bool SetHighContrast(void* pvParam)
    {
        if (pvParam is null)
        {
            _highContrastFlags = 0;
            return true;
        }

        _highContrastFlags = (uint)((HIGHCONTRASTW*)pvParam)->dwFlags;
        return true;
    }

    private static unsafe bool ReadNonClientMetrics(void* pvParam, int uiParam)
    {
        if (pvParam is null)
        {
            return true;
        }

        var metrics = (NONCLIENTMETRICSW*)pvParam;
        metrics->cbSize = (uint)(uiParam == 0
            ? (uint)sizeof(NONCLIENTMETRICSW)
            : (uint)uiParam);
        metrics->iBorderWidth = (int)1;
        metrics->iScrollWidth = (int)17;
        metrics->iScrollHeight = (int)17;
        metrics->iCaptionHeight = (int)30;
        metrics->iCaptionWidth = (int)136;
        metrics->iPaddedBorderWidth = (int)4;
        metrics->lfCaptionFont = ToLogicalFont(System.Drawing.SystemFonts.CaptionFont);
        metrics->lfMenuFont = ToLogicalFont(System.Drawing.SystemFonts.MenuFont);
        metrics->lfMessageFont = ToLogicalFont(System.Drawing.SystemFonts.MessageBoxFont);
        metrics->lfStatusFont = ToLogicalFont(System.Drawing.SystemFonts.StatusFont);
        metrics->lfSmCaptionFont = ToLogicalFont(System.Drawing.SystemFonts.SmallCaptionFont);
        metrics->iMenuHeight = 20;
        metrics->iMenuWidth = 0;
        return true;
    }

    private bool SetNonClientMetrics(void* pvParam)
    {
        if (pvParam is null)
        {
            return true;
        }

        NONCLIENTMETRICSW* metrics = (NONCLIENTMETRICSW*)pvParam;
        _border = metrics->iBorderWidth;
        return true;
    }

    private static unsafe global::Windows.Win32.Graphics.Gdi.LOGFONTW ToLogicalFont(System.Drawing.Font? font)
    {
        if (font is null)
        {
            return default;
        }

        global::System.Drawing.Interop.LOGFONT logicalFont = default;
        font.ToLogFont(out logicalFont);
        return Unsafe.As<global::System.Drawing.Interop.LOGFONT, global::Windows.Win32.Graphics.Gdi.LOGFONTW>(ref logicalFont);
    }

    public COLORREF GetSysColor(SYS_COLOR_INDEX idx) => idx switch
    {
        SYS_COLOR_INDEX.COLOR_WINDOW => new COLORREF(0x00FFFFFF),
        SYS_COLOR_INDEX.COLOR_WINDOWTEXT => new COLORREF(0x00000000),
        SYS_COLOR_INDEX.COLOR_BTNFACE => new COLORREF(0x00F0F0F0),
        SYS_COLOR_INDEX.COLOR_BTNTEXT => new COLORREF(0x00000000),
        SYS_COLOR_INDEX.COLOR_HIGHLIGHT => new COLORREF(0x00FF8C00),
        SYS_COLOR_INDEX.COLOR_HIGHLIGHTTEXT => new COLORREF(0x00FFFFFF),
        SYS_COLOR_INDEX.COLOR_GRAYTEXT => new COLORREF(0x006D6D6D),
        SYS_COLOR_INDEX.COLOR_SCROLLBAR => new COLORREF(0x00C8C8C8),
        SYS_COLOR_INDEX.COLOR_MENU => new COLORREF(0x00F0F0F0),
        SYS_COLOR_INDEX.COLOR_MENUTEXT => new COLORREF(0x00000000),
        SYS_COLOR_INDEX.COLOR_ACTIVECAPTION => new COLORREF(0x00D1B499),
        SYS_COLOR_INDEX.COLOR_INACTIVECAPTION => new COLORREF(0x00BFCDDB),
        _ => new COLORREF(0x00C0C0C0),
    };

    public bool GetMonitorInfo(HMONITOR hMonitor, ref MONITORINFO lpmi)
    {
        lpmi.rcMonitor = new RECT(0, 0, 1920, 1080);
        lpmi.rcWork = new RECT(0, 0, 1920, 1040);
        lpmi.dwFlags = 1; // MONITORINFOF_PRIMARY
        return true;
    }

    public HMONITOR MonitorFromWindow(HWND hwnd, MONITOR_FROM_FLAGS flags) => (HMONITOR)(nint)1;
    public HMONITOR MonitorFromPoint(System.Drawing.Point pt, MONITOR_FROM_FLAGS flags) => (HMONITOR)(nint)1;
    public HMONITOR MonitorFromRect(in RECT rc, MONITOR_FROM_FLAGS flags) => (HMONITOR)(nint)1;

    // --- DPI ------------------------------------------------------------

    public uint GetDpiForWindow(HWND hwnd)
    {
        if (hwnd == HWND.Null)
        {
            return GetDpiForSystem();
        }

        if (Control.FromHandle(hwnd) is { DeviceDpi: > 0 } control)
        {
            return (uint)control.DeviceDpi;
        }

        return GetDpiForSystem();
    }

    public uint GetDpiForSystem()
    {
        foreach (Form form in Application.OpenForms)
        {
            if (form.DeviceDpi > 0)
            {
                return (uint)form.DeviceDpi;
            }
        }

        return 96;
    }

    public bool AreDpiAwarenessContextsEqual(DPI_AWARENESS_CONTEXT dpiContextA, DPI_AWARENESS_CONTEXT dpiContextB)
        => dpiContextA.IsEquivalent(dpiContextB);

    public DPI_AWARENESS GetAwarenessFromDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext)
    {
        if (dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_UNAWARE)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED))
        {
            return DPI_AWARENESS.DPI_AWARENESS_UNAWARE;
        }

        if (dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE))
        {
            return DPI_AWARENESS.DPI_AWARENESS_SYSTEM_AWARE;
        }

        if (dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        {
            return DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE;
        }

        return DPI_AWARENESS.DPI_AWARENESS_INVALID;
    }

    public HRESULT GetProcessDpiAwareness(HANDLE process, out PROCESS_DPI_AWARENESS dpiAwareness)
    {
        dpiAwareness = GetAwarenessFromDpiAwarenessContext(_processDpiAwarenessContext) switch
        {
            DPI_AWARENESS.DPI_AWARENESS_UNAWARE => PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE,
            DPI_AWARENESS.DPI_AWARENESS_SYSTEM_AWARE => PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE,
            DPI_AWARENESS.DPI_AWARENESS_PER_MONITOR_AWARE => PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE,
            _ => PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE
        };

        return HRESULT.S_OK;
    }

    public DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext() => _threadDpiAwarenessContext;

    public DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT ctx)
    {
        DPI_AWARENESS_CONTEXT previous = _threadDpiAwarenessContext;
        _threadDpiAwarenessContext = ctx;
        return previous;
    }

    public DPI_HOSTING_BEHAVIOR GetThreadDpiHostingBehavior() => _threadDpiHostingBehavior;

    public DPI_HOSTING_BEHAVIOR SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR value)
    {
        DPI_HOSTING_BEHAVIOR previous = _threadDpiHostingBehavior;
        _threadDpiHostingBehavior = value;
        return previous;
    }

    public bool IsValidDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext)
        => dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_UNAWARE)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)
            || dpiContext.IsEquivalent(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED);

    public bool SetProcessDPIAware()
    {
        _processDpiAwarenessContext = DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE;
        return true;
    }

    public HRESULT SetProcessDpiAwareness(PROCESS_DPI_AWARENESS dpiAwareness)
    {
        _processDpiAwarenessContext = dpiAwareness switch
        {
            PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE => DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_UNAWARE,
            PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE => DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_SYSTEM_AWARE,
            PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE => DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE,
            _ => _processDpiAwarenessContext
        };

        return HRESULT.S_OK;
    }

    public bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext)
    {
        _processDpiAwarenessContext = dpiContext;
        _threadDpiAwarenessContext = dpiContext;
        return true;
    }

    public bool AdjustWindowRectExForDpi(ref RECT rect, WINDOW_STYLE style, bool menu, WINDOW_EX_STYLE exStyle, uint dpi) => true;
    public DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(HWND hwnd)
    {
        if (PlatformApi.Window is ImpellerWindowInterop windowInterop
            && windowInterop.GetWindowState(hwnd) is { } state)
        {
            return state.DpiAwarenessContext;
        }

        return _threadDpiAwarenessContext;
    }

    // --- Module / Process -----------------------------------------------

    public HMODULE GetModuleHandle(string? name) => (HMODULE)(nint)0x400000;
    public uint GetModuleFileName(HMODULE hModule, Span<char> lpFilename)
    {
        if (lpFilename.IsEmpty)
        {
            return 0;
        }

        string? path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        int length = Math.Min(path.Length, lpFilename.Length);
        path.AsSpan(0, length).CopyTo(lpFilename);
        if (length < lpFilename.Length)
        {
            lpFilename[length] = '\0';
        }

        return (uint)length;
    }

    public nint GetProcAddress(HMODULE hModule, PCSTR name) => 0;
    public uint GetCurrentThreadId() => (uint)Environment.CurrentManagedThreadId;
    public uint GetCurrentProcessId() => (uint)Environment.ProcessId;
    public uint GetWindowThreadProcessId(HWND hWnd, out uint processId)
    {
        processId = (uint)Environment.ProcessId;
        return (uint)Environment.CurrentManagedThreadId;
    }

    public uint GetLastError() => s_lastError;

    public void SetLastError(uint dwErrCode) => s_lastError = dwErrCode;

    public HANDLE CreateActCtx(ACTCTXW* pActCtx)
    {
        if (pActCtx is null || pActCtx->cbSize < sizeof(ACTCTXW))
        {
            return (HANDLE)(-1);
        }

        nint handle = (nint)Interlocked.Increment(ref _nextActivationContextHandle);
        _activationContexts.Add(handle);
        return (HANDLE)handle;
    }

    public bool ActivateActCtx(HANDLE hActCtx, nuint* lpCookie)
    {
        nint handle = (nint)hActCtx;
        if (handle == 0 || !_activationContexts.Contains(handle) || lpCookie is null)
        {
            return false;
        }

        nuint cookie = (nuint)Interlocked.Increment(ref _nextActivationCookie);
        s_activationCookiePrevious ??= [];
        s_activationCookiePrevious[cookie] = s_currentActivationContext;
        s_currentActivationContext = handle;
        *lpCookie = cookie;
        return true;
    }

    public bool DeactivateActCtx(uint dwFlags, nuint ulCookie)
    {
        _ = dwFlags;
        if (ulCookie == 0 || s_activationCookiePrevious is null || !s_activationCookiePrevious.Remove(ulCookie, out nint previous))
        {
            return false;
        }

        s_currentActivationContext = previous;
        return true;
    }

    public bool GetCurrentActCtx(HANDLE* lphActCtx)
    {
        if (lphActCtx is null)
        {
            return false;
        }

        *lphActCtx = (HANDLE)s_currentActivationContext;
        return s_currentActivationContext != 0;
    }

    // --- Clipboard ------------------------------------------------------

    public bool OpenClipboard(HWND hWnd) => true;
    public bool CloseClipboard() => true;
    public bool EmptyClipboard()
    {
        _clipboardData.Clear();
        return true;
    }

    public HANDLE SetClipboardData(uint format, HANDLE hMem)
    {
        if (format == 0 || hMem == HANDLE.Null)
        {
            return HANDLE.Null;
        }

        _clipboardData[format] = hMem;
        return hMem;
    }

    public HANDLE GetClipboardData(uint format)
        => _clipboardData.TryGetValue(format, out HANDLE handle) ? handle : HANDLE.Null;

    public bool IsClipboardFormatAvailable(uint format)
        => _clipboardData.ContainsKey(format);

    public uint RegisterClipboardFormat(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (!_clipboardFormats.TryGetValue(name, out var id))
        {
            id = _nextClipboardFormat++;
            _clipboardFormats[name] = id;
            _clipboardFormatNames[id] = name;
        }

        return id;
    }

    public int GetClipboardFormatName(uint format, Span<char> buf)
    {
        if (buf.IsEmpty || !_clipboardFormatNames.TryGetValue(format, out string? name))
        {
            return 0;
        }

        int count = Math.Min(name.Length, buf.Length - 1);
        name.AsSpan(0, count).CopyTo(buf);
        buf[count] = '\0';
        return count;
    }

    // --- Shell ----------------------------------------------------------

    public nint SHBrowseForFolder(ref BROWSEINFOW bi) => 0;
    public bool SHGetPathFromIDList(nint pidl, Span<char> path) => false;
    public void DragAcceptFiles(HWND hWnd, bool accept) { }
    public uint DragQueryFile(HDROP hDrop, uint iFile, Span<char> buf) => 0;

    // --- Memory ---------------------------------------------------------

    public HGLOBAL GlobalAlloc(uint flags, nuint bytes)
    {
        nint handle = AllocateMemory(flags, bytes);
        return handle == 0 ? HGLOBAL.Null : (HGLOBAL)handle;
    }

    public HGLOBAL GlobalReAlloc(HGLOBAL hMem, nuint bytes, uint flags)
    {
        nint handle = ReAllocMemory((nint)hMem.Value, bytes, flags);
        return handle == 0 ? HGLOBAL.Null : (HGLOBAL)handle;
    }

    public HGLOBAL GlobalFree(HGLOBAL hMem)
    {
        FreeMemory((nint)hMem.Value);
        return HGLOBAL.Null;
    }

    public void* GlobalLock(HGLOBAL hMem) => (void*)(nint)hMem;
    public bool GlobalUnlock(HGLOBAL hMem)
    {
        _ = hMem;
        return false;
    }

    public nuint GlobalSize(HGLOBAL hMem) => SizeOfMemory((nint)hMem.Value);

    public nint LocalAlloc(uint flags, nuint bytes) => AllocateMemory(flags, bytes);

    public nint LocalReAlloc(nint hMem, nuint bytes, uint flags) => ReAllocMemory(hMem, bytes, flags);

    public nint LocalFree(nint hMem)
    {
        FreeMemory(hMem);
        return 0;
    }

    public void* LocalLock(nint hMem) => (void*)hMem;

    public bool LocalUnlock(nint hMem)
    {
        _ = hMem;
        return false;
    }

    public nuint LocalSize(nint hMem) => SizeOfMemory(hMem);

    private nint AllocateMemory(uint flags, nuint bytes)
    {
        if (bytes == 0)
        {
            return 0;
        }

        nint handle = Runtime.InteropServices.Marshal.AllocHGlobal(checked((nint)bytes));
        if (handle == 0)
        {
            return 0;
        }

        _globalMemorySizes[handle] = bytes;

        const uint ZeroInit = 0x40;
        if ((flags & ZeroInit) != 0)
        {
            new Span<byte>((void*)handle, checked((int)bytes)).Clear();
        }

        return handle;
    }

    private nint ReAllocMemory(nint hMem, nuint bytes, uint flags)
    {
        if (hMem == 0)
        {
            return AllocateMemory(flags, bytes);
        }

        if (bytes == 0)
        {
            FreeMemory(hMem);
            return 0;
        }

        _globalMemorySizes.TryGetValue(hMem, out nuint oldSize);
        nint handle = Runtime.InteropServices.Marshal.ReAllocHGlobal(hMem, checked((nint)bytes));
        if (handle == 0)
        {
            return 0;
        }

        _globalMemorySizes.Remove(hMem);
        _globalMemorySizes[handle] = bytes;

        const uint ZeroInit = 0x40;
        if ((flags & ZeroInit) != 0 && bytes > oldSize)
        {
            byte* start = (byte*)handle + checked((nint)oldSize);
            new Span<byte>(start, checked((int)(bytes - oldSize))).Clear();
        }

        return handle;
    }

    private void FreeMemory(nint hMem)
    {
        if (hMem == 0)
        {
            return;
        }

        _globalMemorySizes.Remove(hMem);
        Runtime.InteropServices.Marshal.FreeHGlobal(hMem);
    }

    private nuint SizeOfMemory(nint hMem)
    {
        return hMem != 0 && _globalMemorySizes.TryGetValue(hMem, out nuint size) ? size : 0;
    }
}
