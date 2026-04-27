// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller system interop — timers, metrics, DPI, clipboard, shell, memory.
/// </summary>
internal sealed unsafe class ImpellerSystemInterop : ISystemInterop
{
    private long _nextTimerId = 1;
    private readonly Dictionary<nint, System.Threading.Timer> _timers = [];
    private readonly Dictionary<string, uint> _clipboardFormats = [];
    private uint _nextClipboardFormat = 0xC000;

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
        SYSTEM_METRICS_INDEX.SM_CXVSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYHSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYCAPTION => 30,
        SYSTEM_METRICS_INDEX.SM_CXBORDER => 1,
        SYSTEM_METRICS_INDEX.SM_CYBORDER => 1,
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
        SYSTEM_METRICS_INDEX.SM_CYMENU => 20,
        SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK => 4,
        SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK => 4,
        SYSTEM_METRICS_INDEX.SM_CXMINTRACK => 136,
        SYSTEM_METRICS_INDEX.SM_CYMINTRACK => 39,
        SYSTEM_METRICS_INDEX.SM_CMONITORS => 1,
        SYSTEM_METRICS_INDEX.SM_SWAPBUTTON => 0,
        SYSTEM_METRICS_INDEX.SM_MOUSEWHEELPRESENT => 1,
        _ => 0,
    };

    public bool SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION action, uint param, void* pvParam, uint flags)
        => true;

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

    public uint GetDpiForWindow(HWND hwnd) => 96;
    public uint GetDpiForSystem() => 96;
    public DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext() => DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2;
    public DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT ctx) => ctx;
    public bool AdjustWindowRectExForDpi(ref RECT rect, WINDOW_STYLE style, bool menu, WINDOW_EX_STYLE exStyle, uint dpi) => true;
    public DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(HWND hwnd) => DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2;

    // --- Module / Process -----------------------------------------------

    public HMODULE GetModuleHandle(string? name) => (HMODULE)(nint)0x400000;
    public nint GetProcAddress(HMODULE hModule, PCSTR name) => 0;
    public uint GetCurrentThreadId() => (uint)Environment.CurrentManagedThreadId;
    public uint GetCurrentProcessId() => (uint)Environment.ProcessId;
    public uint GetWindowThreadProcessId(HWND hWnd, out uint processId)
    {
        processId = (uint)Environment.ProcessId;
        return (uint)Environment.CurrentManagedThreadId;
    }

    // --- Clipboard ------------------------------------------------------

    public bool OpenClipboard(HWND hWnd) => true;
    public bool CloseClipboard() => true;
    public bool EmptyClipboard() => true;
    public HANDLE SetClipboardData(uint format, HANDLE hMem) => hMem;
    public HANDLE GetClipboardData(uint format) => HANDLE.Null;
    public bool IsClipboardFormatAvailable(uint format) => false;
    public uint RegisterClipboardFormat(string name)
    {
        if (!_clipboardFormats.TryGetValue(name, out var id))
        {
            id = _nextClipboardFormat++;
            _clipboardFormats[name] = id;
        }

        return id;
    }

    public int GetClipboardFormatName(uint format, Span<char> buf) => 0;

    // --- Shell ----------------------------------------------------------

    public nint SHBrowseForFolder(ref BROWSEINFOW bi) => 0;
    public bool SHGetPathFromIDList(nint pidl, Span<char> path) => false;
    public void DragAcceptFiles(HWND hWnd, bool accept) { }
    public uint DragQueryFile(HDROP hDrop, uint iFile, Span<char> buf) => 0;

    // --- Memory ---------------------------------------------------------

    public HGLOBAL GlobalAlloc(uint flags, nuint bytes)
        => (HGLOBAL)Runtime.InteropServices.Marshal.AllocHGlobal((int)bytes);
    public HGLOBAL GlobalFree(HGLOBAL hMem)
    {
        Runtime.InteropServices.Marshal.FreeHGlobal(hMem);
        return HGLOBAL.Null;
    }

    public void* GlobalLock(HGLOBAL hMem) => (void*)(nint)hMem;
    public bool GlobalUnlock(HGLOBAL hMem) => true;
}
