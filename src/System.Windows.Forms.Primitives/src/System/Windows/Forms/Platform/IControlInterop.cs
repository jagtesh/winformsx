// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Common Controls abstraction — ImageList, menu, toolbar, status bar, etc.
/// In the Impeller backend, all controls are owner-drawn.
/// </summary>
internal unsafe interface IControlInterop
{
    // ─── ImageList ──────────────────────────────────────────────────────

    HIMAGELIST ImageList_Create(int cx, int cy, IMAGELIST_CREATION_FLAGS flags, int cInitial, int cGrow);
    bool ImageList_Destroy(HIMAGELIST himl);
    int ImageList_Add(HIMAGELIST himl, HBITMAP hbmImage, HBITMAP hbmMask);
    int ImageList_ReplaceIcon(HIMAGELIST himl, int i, HICON hicon);
    bool ImageList_Remove(HIMAGELIST himl, int i);
    int ImageList_GetImageCount(HIMAGELIST himl);
    bool ImageList_GetIconSize(HIMAGELIST himl, out int cx, out int cy);
    bool ImageList_SetIconSize(HIMAGELIST himl, int cx, int cy);
    bool ImageList_Draw(HIMAGELIST himl, int i, HDC hdcDst, int x, int y, IMAGE_LIST_DRAW_STYLE fStyle);

    // ─── Menu ───────────────────────────────────────────────────────────

    HMENU CreateMenu();
    HMENU CreatePopupMenu();
    bool DestroyMenu(HMENU hMenu);
    bool AppendMenu(HMENU hMenu, MENU_ITEM_FLAGS uFlags, nuint uIDNewItem, string? lpNewItem);
    bool InsertMenuItem(HMENU hmenu, uint item, bool fByPosition, in MENUITEMINFOW lpmi);
    bool SetMenuItemInfo(HMENU hmenu, uint item, bool fByPosition, in MENUITEMINFOW lpmii);
    bool GetMenuItemInfo(HMENU hmenu, uint item, bool fByPosition, ref MENUITEMINFOW lpmii);
    int GetMenuItemCount(HMENU hMenu);
    bool RemoveMenu(HMENU hMenu, uint uPosition, MENU_ITEM_FLAGS uFlags);
    bool TrackPopupMenuEx(HMENU hMenu, uint uFlags, int x, int y, HWND hwnd, RECT* excludeRect);
    bool EnableMenuItem(HMENU hMenu, uint uIDEnableItem, MENU_ITEM_FLAGS uEnable);
    bool DrawMenuBar(HWND hWnd);

    // ─── Accelerator ────────────────────────────────────────────────────

    HACCEL CreateAcceleratorTable(ACCEL* paccel, int cAccel);
    bool DestroyAcceleratorTable(HACCEL hAccel);
    int TranslateAccelerator(HWND hWnd, HACCEL hAccTable, ref MSG lpMsg);

    // ─── Icon ───────────────────────────────────────────────────────────

    HICON LoadIcon(HINSTANCE hInstance, PCWSTR lpIconName);
    bool DestroyIcon(HICON hIcon);
    HICON CreateIconIndirect(in ICONINFO piconinfo);
    bool GetIconInfo(HICON hIcon, out ICONINFO piconinfo);
    int DrawIconEx(HDC hdc, int xLeft, int yTop, HICON hIcon, int cxWidth, int cyWidth,
        uint istepIfAniCur, HBRUSH hbrFlickerFreeDraw, DI_FLAGS diFlags);

    // ─── ScrollBar ──────────────────────────────────────────────────────

    bool GetScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS nBar, ref SCROLLINFO lpsi);
    int SetScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS nBar, in SCROLLINFO lpsi, bool redraw);
    bool ShowScrollBar(HWND hWnd, SCROLLBAR_CONSTANTS wBar, bool bShow);
    bool EnableScrollBar(HWND hWnd, uint wSBflags, ENABLE_SCROLL_BAR_ARROWS wArrows);

    // ─── Common Controls Init ───────────────────────────────────────────

    bool InitCommonControlsEx(in INITCOMMONCONTROLSEX picce);
}
