// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller control interop — ImageList, menus, scrollbars, icons, accelerators.
/// All controls are owner-drawn via Impeller; no comctl32.
/// </summary>
internal sealed unsafe class ImpellerControlInterop : IControlInterop
{
    private static long s_nextHandle = 0x30000;

    private nint NextHandle() => (nint)System.Threading.Interlocked.Increment(ref s_nextHandle);

    // --- ImageList ------------------------------------------------------

    public HIMAGELIST ImageList_Create(int cx, int cy, IMAGELIST_CREATION_FLAGS flags, int initial, int grow)
        => (HIMAGELIST)(nint)NextHandle();
    public bool ImageList_Destroy(HIMAGELIST himl) => true;
    public int ImageList_Add(HIMAGELIST himl, HBITMAP img, HBITMAP mask) => 0;
    public int ImageList_ReplaceIcon(HIMAGELIST himl, int i, HICON icon) => i < 0 ? 0 : i;
    public bool ImageList_Remove(HIMAGELIST himl, int i) => true;
    public int ImageList_GetImageCount(HIMAGELIST himl) => 0;
    public bool ImageList_GetIconSize(HIMAGELIST himl, out int cx, out int cy) { cx = 16; cy = 16; return true; }
    public bool ImageList_SetIconSize(HIMAGELIST himl, int cx, int cy) => true;
    public bool ImageList_Draw(HIMAGELIST himl, int i, HDC hdc, int x, int y, IMAGE_LIST_DRAW_STYLE style) => true;

    // --- Menu -----------------------------------------------------------

    public HMENU CreateMenu() => (HMENU)(nint)NextHandle();
    public HMENU CreatePopupMenu() => (HMENU)(nint)NextHandle();
    public bool DestroyMenu(HMENU hMenu) => true;
    public bool AppendMenu(HMENU hMenu, MENU_ITEM_FLAGS flags, nuint id, string? text) => true;
    public bool InsertMenuItem(HMENU hmenu, uint item, bool byPos, in MENUITEMINFOW mi) => true;
    public bool SetMenuItemInfo(HMENU hmenu, uint item, bool byPos, in MENUITEMINFOW mi) => true;
    public bool GetMenuItemInfo(HMENU hmenu, uint item, bool byPos, ref MENUITEMINFOW mi) => true;
    public int GetMenuItemCount(HMENU hMenu) => 0;
    public bool RemoveMenu(HMENU hMenu, uint pos, MENU_ITEM_FLAGS flags) => true;
    public bool TrackPopupMenuEx(HMENU hMenu, uint flags, int x, int y, HWND hwnd, RECT* excludeRect) => false;
    public bool EnableMenuItem(HMENU hMenu, uint id, MENU_ITEM_FLAGS enable) => true;
    public bool DrawMenuBar(HWND hWnd) => true;

    // --- Accelerator ----------------------------------------------------

    public HACCEL CreateAcceleratorTable(ACCEL* accel, int count) => (HACCEL)(nint)NextHandle();
    public bool DestroyAcceleratorTable(HACCEL hAccel) => true;
    public int TranslateAccelerator(HWND hWnd, HACCEL hAccTable, ref MSG msg) => 0;

    // --- Icon -----------------------------------------------------------

    public HICON LoadIcon(HINSTANCE hInstance, PCWSTR name) => (HICON)(nint)NextHandle();
    public bool DestroyIcon(HICON hIcon) => true;
    public HICON CreateIconIndirect(in ICONINFO info) => (HICON)(nint)NextHandle();
    public bool GetIconInfo(HICON hIcon, out ICONINFO info) { info = default; return true; }
    public int DrawIconEx(HDC hdc, int x, int y, HICON hIcon, int cx, int cy,
        uint istep, HBRUSH hbrFlicker, DI_FLAGS flags) => 1;

    // --- ScrollBar ------------------------------------------------------

    public bool GetScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS bar, ref SCROLLINFO si) => true;
    public int SetScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS bar, in SCROLLINFO si, bool redraw) => 0;
    public bool ShowScrollBar(HWND hWnd, SCROLLBAR_CONSTANTS bar, bool show) => true;
    public bool EnableScrollBar(HWND hWnd, uint flags, ENABLE_SCROLL_BAR_ARROWS arrows) => true;

    // --- Common Controls Init -------------------------------------------

    public bool InitCommonControlsEx(in INITCOMMONCONTROLSEX icc) => true; // No-op — no comctl32
}
