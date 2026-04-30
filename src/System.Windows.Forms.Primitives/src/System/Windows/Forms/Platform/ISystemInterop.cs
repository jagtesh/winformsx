// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.ApplicationInstallationAndServicing;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.Threading;

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
    uint GetCaretBlinkTime();
    bool SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION uiAction, uint uiParam, void* pvParam, uint fWinIni);
    COLORREF GetSysColor(SYS_COLOR_INDEX nIndex);
    bool GetMonitorInfo(HMONITOR hMonitor, ref MONITORINFO lpmi);
    HMONITOR MonitorFromWindow(HWND hwnd, MONITOR_FROM_FLAGS dwFlags);
    HMONITOR MonitorFromPoint(System.Drawing.Point pt, MONITOR_FROM_FLAGS dwFlags);
    HMONITOR MonitorFromRect(in RECT lprc, MONITOR_FROM_FLAGS dwFlags);

    // ─── DPI ────────────────────────────────────────────────────────────

    uint GetDpiForWindow(HWND hwnd);
    uint GetDpiForSystem();
    bool AreDpiAwarenessContextsEqual(DPI_AWARENESS_CONTEXT dpiContextA, DPI_AWARENESS_CONTEXT dpiContextB);
    DPI_AWARENESS GetAwarenessFromDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);
    HRESULT GetProcessDpiAwareness(HANDLE process, out PROCESS_DPI_AWARENESS dpiAwareness);
    DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext();
    DPI_AWARENESS_CONTEXT SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);
    DPI_HOSTING_BEHAVIOR GetThreadDpiHostingBehavior();
    DPI_HOSTING_BEHAVIOR SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR value);
    bool IsValidDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);
    bool SetProcessDPIAware();
    HRESULT SetProcessDpiAwareness(PROCESS_DPI_AWARENESS dpiAwareness);
    bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext);
    bool AdjustWindowRectExForDpi(ref RECT lpRect, WINDOW_STYLE dwStyle, bool bMenu, WINDOW_EX_STYLE dwExStyle, uint dpi);
    DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(HWND hwnd);

    // ─── Module / Process ───────────────────────────────────────────────

    HMODULE GetModuleHandle(string? lpModuleName);
    uint GetModuleFileName(HMODULE hModule, Span<char> lpFilename);
    HINSTANCE LoadLibraryEx(string? lpLibFileName, uint dwFlags);
    bool FreeLibrary(HINSTANCE hLibModule);
    nint GetProcAddress(HMODULE hModule, PCSTR lpProcName);
    HRSRC FindResource(HMODULE hModule, PCWSTR lpName, PCWSTR lpType);
    HRSRC FindResourceEx(HMODULE hModule, PCWSTR lpType, PCWSTR lpName, ushort wLanguage);
    HGLOBAL LoadResource(HMODULE hModule, HRSRC hResInfo);
    void* LockResource(HGLOBAL hResData);
    uint SizeofResource(HMODULE hModule, HRSRC hResInfo);
    bool FreeResource(HGLOBAL hResData);
    uint GetCurrentThreadId();
    uint GetCurrentProcessId();
    uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId);
    uint GetLastError();
    void SetLastError(uint dwErrCode);
    bool CloseHandle(HANDLE hObject);
    bool DuplicateHandle(
        HANDLE hSourceProcessHandle,
        HANDLE hSourceHandle,
        HANDLE hTargetProcessHandle,
        HANDLE* lpTargetHandle,
        uint dwDesiredAccess,
        BOOL bInheritHandle,
        uint dwOptions);
    uint FormatMessage(FORMAT_MESSAGE_OPTIONS dwFlags, void* lpSource, uint dwMessageId, uint dwLanguageId, char* lpBuffer, uint nSize, void* arguments);
    bool GetExitCodeThread(HANDLE hThread, uint* lpExitCode);
    int GetLocaleInfoEx(string lpLocaleName, uint lcType, char* lpLCData, int cchData);
    void GetStartupInfo(out STARTUPINFOW lpStartupInfo);
    uint GetThreadLocale();
    uint GetTickCount();
    HANDLE CreateActCtx(ACTCTXW* pActCtx);
    bool ActivateActCtx(HANDLE hActCtx, nuint* lpCookie);
    bool DeactivateActCtx(uint dwFlags, nuint ulCookie);
    bool GetCurrentActCtx(HANDLE* lphActCtx);

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
    HGLOBAL GlobalReAlloc(HGLOBAL hMem, nuint dwBytes, uint uFlags);
    HGLOBAL GlobalFree(HGLOBAL hMem);
    void* GlobalLock(HGLOBAL hMem);
    bool GlobalUnlock(HGLOBAL hMem);
    nuint GlobalSize(HGLOBAL hMem);
    nint LocalAlloc(uint uFlags, nuint uBytes);
    nint LocalReAlloc(nint hMem, nuint uBytes, uint uFlags);
    nint LocalFree(nint hMem);
    void* LocalLock(nint hMem);
    bool LocalUnlock(nint hMem);
    nuint LocalSize(nint hMem);
}
