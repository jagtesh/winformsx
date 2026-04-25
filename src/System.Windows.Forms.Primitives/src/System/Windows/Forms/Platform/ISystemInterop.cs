// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// System services abstraction — timers, clipboard, DPI, system metrics,
/// module loading, process/thread info.
/// </summary>
internal unsafe interface ISystemInterop
{
    // ─── Timers ─────────────────────────────────────────────────────────

    nint SetTimer(HWND hWnd, nint nIDEvent, uint uElapse, nint lpTimerFunc);
    bool KillTimer(HWND hWnd, nint uIDEvent);

    // ─── System Metrics / Info ──────────────────────────────────────────

    int GetSystemMetrics(SYSTEM_METRICS_INDEX nIndex);
    bool SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION uiAction, uint uiParam, void* pvParam, uint fWinIni);
    COLORREF GetSysColor(SYS_COLOR_INDEX nIndex);
    bool GetMonitorInfo(HMONITOR hMonitor, ref MONITORINFO lpmi);
    HMONITOR MonitorFromWindow(HWND hwnd, MONITOR_FROM_FLAGS dwFlags);
    HMONITOR MonitorFromPoint(System.Drawing.Point pt, MONITOR_FROM_FLAGS dwFlags);
    HMONITOR MonitorFromRect(in RECT lprc, MONITOR_FROM_FLAGS dwFlags);

    // ─── DPI ────────────────────────────────────────────────────────────

    uint GetDpiForWindow(HWND hwnd);
    uint GetDpiForSystem();
    DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext();
    DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);
    bool AdjustWindowRectExForDpi(ref RECT lpRect, WINDOW_STYLE dwStyle, bool bMenu, WINDOW_EX_STYLE dwExStyle, uint dpi);
    DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(HWND hwnd);

    // ─── Module / Process ───────────────────────────────────────────────

    HMODULE GetModuleHandle(string? lpModuleName);
    nint GetProcAddress(HMODULE hModule, PCSTR lpProcName);
    uint GetCurrentThreadId();
    uint GetCurrentProcessId();
    uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId);

    // ─── Clipboard ──────────────────────────────────────────────────────

    bool OpenClipboard(HWND hWndNewOwner);
    bool CloseClipboard();
    bool EmptyClipboard();
    HANDLE SetClipboardData(uint uFormat, HANDLE hMem);
    HANDLE GetClipboardData(uint uFormat);
    bool IsClipboardFormatAvailable(uint format);
    uint RegisterClipboardFormat(string lpszFormat);
    int GetClipboardFormatName(uint format, Span<char> lpszFormatName);

    // ─── Shell ──────────────────────────────────────────────────────────

    nint SHBrowseForFolder(ref BROWSEINFOW lpbi);
    bool SHGetPathFromIDList(nint pidl, Span<char> pszPath);
    void DragAcceptFiles(HWND hWnd, bool fAccept);
    uint DragQueryFile(HDROP hDrop, uint iFile, Span<char> lpszFile);

    // ─── Memory ─────────────────────────────────────────────────────────

    HGLOBAL GlobalAlloc(uint uFlags, nuint dwBytes);
    HGLOBAL GlobalFree(HGLOBAL hMem);
    void* GlobalLock(HGLOBAL hMem);
    bool GlobalUnlock(HGLOBAL hMem);
}
