// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <string.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef int16_t SHORT;
typedef uint32_t UINT;
typedef int32_t INT;
typedef uint8_t UINT8;
typedef intptr_t HWND;
typedef intptr_t HHOOK;
typedef intptr_t HMENU;
typedef intptr_t HICON;
typedef intptr_t HCURSOR;
typedef intptr_t HANDLE;
typedef intptr_t HDC;
typedef intptr_t HBRUSH;
typedef intptr_t WPARAM;
typedef intptr_t LPARAM;
typedef uintptr_t HKL;
typedef uint16_t WCHAR;

typedef struct WinFormsXRect
{
    int32_t left;
    int32_t top;
    int32_t right;
    int32_t bottom;
} WinFormsXRect;

typedef struct WinFormsXPoint
{
    int32_t x;
    int32_t y;
} WinFormsXPoint;

typedef struct WinFormsXWindowPlacement
{
    UINT length;
    UINT flags;
    UINT showCmd;
    WinFormsXPoint ptMinPosition;
    WinFormsXPoint ptMaxPosition;
    WinFormsXRect rcNormalPosition;
} WinFormsXWindowPlacement;

typedef struct WinFormsXIconInfo
{
    BOOL fIcon;
    UINT xHotspot;
    UINT yHotspot;
    HANDLE hbmMask;
    HANDLE hbmColor;
} WinFormsXIconInfo;

typedef struct WinFormsXUser32Dispatch
{
    uint32_t version;
    uint32_t size;
    BOOL (*get_cursor_pos)(WinFormsXPoint* point);
    BOOL (*set_cursor_pos)(INT x, INT y);
    SHORT (*get_async_key_state)(INT vkey);
    UINT (*map_virtual_key)(UINT code, UINT map_type);
    UINT (*send_input)(UINT count, const void* inputs, INT cb_size);
    HWND (*get_focus)(void);
    HWND (*set_focus)(HWND hwnd);
    HWND (*get_desktop_window)(void);
    HWND (*get_active_window)(void);
    HWND (*set_active_window)(HWND hwnd);
    HWND (*get_foreground_window)(void);
    BOOL (*set_foreground_window)(HWND hwnd);
    INT (*get_system_metrics)(INT index);
    BOOL (*is_window)(HWND hwnd);
    BOOL (*is_window_visible)(HWND hwnd);
    BOOL (*is_window_enabled)(HWND hwnd);
    BOOL (*enable_window)(HWND hwnd, BOOL enable);
    BOOL (*get_window_rect)(HWND hwnd, WinFormsXRect* lpRect);
    BOOL (*get_client_rect)(HWND hwnd, WinFormsXRect* lpRect);
    BOOL (*get_window_placement)(HWND hwnd, WinFormsXWindowPlacement* placement);
    BOOL (*set_window_placement)(HWND hwnd, const WinFormsXWindowPlacement* placement);
    INT (*map_window_points)(HWND hwnd_from, HWND hwnd_to, const WinFormsXPoint* points, UINT c_points);
    INT (*client_to_screen)(HWND hwnd, WinFormsXPoint* point);
    INT (*screen_to_client)(HWND hwnd, WinFormsXPoint* point);
    HWND (*get_parent)(HWND hwnd);
    HWND (*set_parent)(HWND child, HWND new_parent);
    HWND (*get_window)(HWND hwnd, UINT u_cmd);
    HWND (*get_ancestor)(HWND hwnd, UINT flags);
    BOOL (*is_child)(HWND hwnd_parent, HWND hwnd);
    HWND (*window_from_point)(WinFormsXPoint point);
    HWND (*child_window_from_point_ex)(HWND parent, WinFormsXPoint point, UINT flags);
    BOOL (*set_menu)(HWND hwnd, HMENU hmenu);
    HMENU (*get_menu)(HWND hwnd);
    HMENU (*get_system_menu)(HWND hwnd, BOOL b_revert);
    BOOL (*enable_menu_item)(HMENU hmenu, UINT u_id_enable_item, UINT u_enable);
    INT (*get_menu_item_count)(HMENU hmenu);
    BOOL (*get_menu_item_info)(HMENU hmenu, UINT item, BOOL f_by_position, void* lpmii);
    BOOL (*draw_menu_bar)(HWND hwnd);
    HWND (*get_capture)(void);
    HWND (*set_capture)(HWND hwnd);
    BOOL (*release_capture)(void);
    SHORT (*get_key_state)(INT vkey);
    BOOL (*get_keyboard_state)(UINT8* lp_key_state);
    HKL (*get_keyboard_layout)(UINT idThread);
    INT (*get_keyboard_layout_list)(INT nBuff, HKL* lpList);
    HKL (*activate_keyboard_layout)(HKL hkl, UINT flags);
    BOOL (*system_parameters_info)(UINT ui_action, UINT ui_param, void* pv_param, UINT flags);
    BOOL (*system_parameters_info_for_dpi)(UINT ui_action, UINT ui_param, void* pv_param, UINT flags, UINT dpi);
    UINT (*get_dpi_for_window)(HWND hwnd);
    UINT (*get_dpi_for_system)(void);
    BOOL (*update_window)(HWND hwnd);
    BOOL (*invalidate_rect)(HWND hwnd, const WinFormsXRect* rect, INT erase);
    BOOL (*validate_rect)(HWND hwnd, const WinFormsXRect* rect);
    UINT (*msg_wait_for_multiple_objects_ex)(UINT n_count, const intptr_t* handles, UINT milliseconds, UINT wake_mask, UINT flags);
    BOOL (*open_clipboard)(HWND hwnd_new_owner);
    BOOL (*close_clipboard)(void);
    BOOL (*empty_clipboard)(void);
    intptr_t (*set_clipboard_data)(UINT format, intptr_t data);
    intptr_t (*get_clipboard_data)(UINT format);
    BOOL (*is_clipboard_format_available)(UINT format);
    UINT (*register_clipboard_format)(const WCHAR* format);
    INT (*get_clipboard_format_name)(UINT format, WCHAR* buffer, INT max_count);
    HICON (*load_icon)(intptr_t instance, const WCHAR* icon_name);
    BOOL (*destroy_icon)(HICON icon);
    HCURSOR (*load_cursor)(intptr_t instance, const WCHAR* cursor_name);
    HCURSOR (*destroy_cursor)(HCURSOR cursor);
    BOOL (*get_icon_info)(HICON icon, WinFormsXIconInfo* icon_info);
    INT (*draw_icon_ex)(HDC hdc, INT x, INT y, HICON icon, INT width, INT height, UINT step, HBRUSH brush, UINT flags);
} WinFormsXUser32Dispatch;

static WinFormsXUser32Dispatch g_dispatch;
static intptr_t g_next_hook_handle = 1;
static intptr_t g_next_resource_handle = 0x550000;

WF_EXPORT BOOL DrawIconEx(HDC hdc, INT x, INT y, HICON icon, INT width, INT height, UINT step, HBRUSH brush, UINT flags);

WF_EXPORT BOOL WinFormsXUser32RegisterDispatch(const WinFormsXUser32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXUser32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT BOOL GetCursorPos(WinFormsXPoint* point)
{
    if (g_dispatch.get_cursor_pos != 0)
    {
        return g_dispatch.get_cursor_pos(point);
    }

    if (point != 0)
    {
        point->x = 0;
        point->y = 0;
    }

    return 0;
}

WF_EXPORT BOOL SetCursorPos(INT x, INT y)
{
    return g_dispatch.set_cursor_pos != 0 ? g_dispatch.set_cursor_pos(x, y) : 0;
}

WF_EXPORT UINT GetMessagePos(void)
{
    WinFormsXPoint point = { 0, 0 };
    if (g_dispatch.get_cursor_pos == 0 || !g_dispatch.get_cursor_pos(&point))
    {
        return 0;
    }

    return ((UINT)(uint16_t)point.x) | (((UINT)(uint16_t)point.y) << 16);
}

WF_EXPORT SHORT GetAsyncKeyState(INT vkey)
{
    return g_dispatch.get_async_key_state != 0 ? g_dispatch.get_async_key_state(vkey) : 0;
}

WF_EXPORT UINT MapVirtualKey(UINT code, UINT map_type)
{
    return g_dispatch.map_virtual_key != 0 ? g_dispatch.map_virtual_key(code, map_type) : 0;
}

WF_EXPORT UINT SendInput(UINT count, const void* inputs, INT cb_size)
{
    return g_dispatch.send_input != 0 ? g_dispatch.send_input(count, inputs, cb_size) : 0;
}

WF_EXPORT HWND GetFocus(void)
{
    return g_dispatch.get_focus != 0 ? g_dispatch.get_focus() : 0;
}

WF_EXPORT HWND SetFocus(HWND hwnd)
{
    return g_dispatch.set_focus != 0 ? g_dispatch.set_focus(hwnd) : 0;
}

WF_EXPORT HWND GetDesktopWindow(void)
{
    return g_dispatch.get_desktop_window != 0 ? g_dispatch.get_desktop_window() : 0;
}

WF_EXPORT HWND GetActiveWindow(void)
{
    return g_dispatch.get_active_window != 0 ? g_dispatch.get_active_window() : 0;
}

WF_EXPORT HWND SetActiveWindow(HWND hwnd)
{
    return g_dispatch.set_active_window != 0 ? g_dispatch.set_active_window(hwnd) : 0;
}

WF_EXPORT HWND GetForegroundWindow(void)
{
    return g_dispatch.get_foreground_window != 0 ? g_dispatch.get_foreground_window() : 0;
}

WF_EXPORT BOOL SetForegroundWindow(HWND hwnd)
{
    return g_dispatch.set_foreground_window != 0 ? g_dispatch.set_foreground_window(hwnd) : 0;
}

WF_EXPORT HWND GetCapture(void)
{
    return g_dispatch.get_capture != 0 ? g_dispatch.get_capture() : 0;
}

WF_EXPORT HWND SetCapture(HWND hwnd)
{
    return g_dispatch.set_capture != 0 ? g_dispatch.set_capture(hwnd) : 0;
}

WF_EXPORT BOOL ReleaseCapture(void)
{
    return g_dispatch.release_capture != 0 ? g_dispatch.release_capture() : 0;
}

WF_EXPORT SHORT GetKeyState(INT vkey)
{
    return g_dispatch.get_key_state != 0 ? g_dispatch.get_key_state(vkey) : 0;
}

WF_EXPORT BOOL GetKeyboardState(UINT8* lp_key_state)
{
    return g_dispatch.get_keyboard_state != 0 ? g_dispatch.get_keyboard_state(lp_key_state) : 0;
}

WF_EXPORT HKL GetKeyboardLayout(UINT idThread)
{
    return g_dispatch.get_keyboard_layout != 0 ? g_dispatch.get_keyboard_layout(idThread) : 0;
}

WF_EXPORT INT GetKeyboardLayoutList(INT nBuff, HKL* lpList)
{
    return g_dispatch.get_keyboard_layout_list != 0 ? g_dispatch.get_keyboard_layout_list(nBuff, lpList) : 0;
}

WF_EXPORT HKL ActivateKeyboardLayout(HKL hkl, UINT flags)
{
    return g_dispatch.activate_keyboard_layout != 0 ? g_dispatch.activate_keyboard_layout(hkl, flags) : 0;
}

WF_EXPORT UINT GetDpiForWindow(HWND hwnd)
{
    return g_dispatch.get_dpi_for_window != 0 ? g_dispatch.get_dpi_for_window(hwnd) : 96;
}

WF_EXPORT UINT GetDpiForSystem(void)
{
    return g_dispatch.get_dpi_for_system != 0 ? g_dispatch.get_dpi_for_system() : 96;
}

WF_EXPORT BOOL SystemParametersInfo(UINT ui_action, UINT ui_param, void* pv_param, UINT flags)
{
    return g_dispatch.system_parameters_info != 0
        ? g_dispatch.system_parameters_info(ui_action, ui_param, pv_param, flags)
        : 0;
}

WF_EXPORT BOOL SystemParametersInfoForDpi(UINT ui_action, UINT ui_param, void* pv_param, UINT flags, UINT dpi)
{
    return g_dispatch.system_parameters_info_for_dpi != 0
        ? g_dispatch.system_parameters_info_for_dpi(ui_action, ui_param, pv_param, flags, dpi)
        : 0;
}

WF_EXPORT INT GetSystemMetrics(INT index)
{
    return g_dispatch.get_system_metrics != 0 ? g_dispatch.get_system_metrics(index) : 0;
}

WF_EXPORT UINT GetGuiResources(intptr_t h_process, UINT ui_flags)
{
    (void)h_process;
    (void)ui_flags;
    return 0;
}

WF_EXPORT BOOL IsWindow(HWND hwnd)
{
    return g_dispatch.is_window != 0 ? g_dispatch.is_window(hwnd) : 0;
}

WF_EXPORT BOOL IsWindowVisible(HWND hwnd)
{
    return g_dispatch.is_window_visible != 0 ? g_dispatch.is_window_visible(hwnd) : 0;
}

WF_EXPORT BOOL IsWindowEnabled(HWND hwnd)
{
    return g_dispatch.is_window_enabled != 0 ? g_dispatch.is_window_enabled(hwnd) : 0;
}

WF_EXPORT BOOL EnableWindow(HWND hwnd, BOOL enable)
{
    return g_dispatch.enable_window != 0 ? g_dispatch.enable_window(hwnd, enable) : 0;
}

WF_EXPORT BOOL GetWindowRect(HWND hwnd, WinFormsXRect* lp_rect)
{
    return g_dispatch.get_window_rect != 0 ? g_dispatch.get_window_rect(hwnd, lp_rect) : 0;
}

WF_EXPORT BOOL GetClientRect(HWND hwnd, WinFormsXRect* lp_rect)
{
    return g_dispatch.get_client_rect != 0 ? g_dispatch.get_client_rect(hwnd, lp_rect) : 0;
}

WF_EXPORT BOOL GetWindowPlacement(HWND hwnd, WinFormsXWindowPlacement* placement)
{
    return g_dispatch.get_window_placement != 0 ? g_dispatch.get_window_placement(hwnd, placement) : 0;
}

WF_EXPORT BOOL SetWindowPlacement(HWND hwnd, const WinFormsXWindowPlacement* placement)
{
    return g_dispatch.set_window_placement != 0 ? g_dispatch.set_window_placement(hwnd, placement) : 0;
}

WF_EXPORT INT MapWindowPoints(HWND hwnd_from, HWND hwnd_to, WinFormsXPoint* points, UINT c_points)
{
    return g_dispatch.map_window_points != 0 ? g_dispatch.map_window_points(hwnd_from, hwnd_to, points, c_points) : 0;
}

WF_EXPORT INT ClientToScreen(HWND hwnd, WinFormsXPoint* point)
{
    return g_dispatch.client_to_screen != 0 ? g_dispatch.client_to_screen(hwnd, point) : 0;
}

WF_EXPORT INT ScreenToClient(HWND hwnd, WinFormsXPoint* point)
{
    return g_dispatch.screen_to_client != 0 ? g_dispatch.screen_to_client(hwnd, point) : 0;
}

WF_EXPORT HWND GetParent(HWND hwnd)
{
    return g_dispatch.get_parent != 0 ? g_dispatch.get_parent(hwnd) : 0;
}

WF_EXPORT HWND SetParent(HWND child, HWND new_parent)
{
    return g_dispatch.set_parent != 0 ? g_dispatch.set_parent(child, new_parent) : 0;
}

WF_EXPORT HWND GetWindow(HWND hwnd, UINT u_cmd)
{
    return g_dispatch.get_window != 0 ? g_dispatch.get_window(hwnd, u_cmd) : 0;
}

WF_EXPORT HWND GetAncestor(HWND hwnd, UINT flags)
{
    return g_dispatch.get_ancestor != 0 ? g_dispatch.get_ancestor(hwnd, flags) : 0;
}

WF_EXPORT BOOL IsChild(HWND hwnd_parent, HWND hwnd)
{
    return g_dispatch.is_child != 0 ? g_dispatch.is_child(hwnd_parent, hwnd) : 0;
}

WF_EXPORT HWND WindowFromPoint(WinFormsXPoint point)
{
    return g_dispatch.window_from_point != 0 ? g_dispatch.window_from_point(point) : 0;
}

WF_EXPORT HWND ChildWindowFromPointEx(HWND parent, WinFormsXPoint point, UINT flags)
{
    return g_dispatch.child_window_from_point_ex != 0 ? g_dispatch.child_window_from_point_ex(parent, point, flags) : 0;
}

WF_EXPORT BOOL SetMenu(HWND hwnd, HMENU hmenu)
{
    return g_dispatch.set_menu != 0 ? g_dispatch.set_menu(hwnd, hmenu) : 0;
}

WF_EXPORT HMENU GetMenu(HWND hwnd)
{
    return g_dispatch.get_menu != 0 ? g_dispatch.get_menu(hwnd) : 0;
}

WF_EXPORT HMENU GetSystemMenu(HWND hwnd, BOOL b_revert)
{
    return g_dispatch.get_system_menu != 0 ? g_dispatch.get_system_menu(hwnd, b_revert) : 0;
}

WF_EXPORT BOOL EnableMenuItem(HMENU hmenu, UINT u_id_enable_item, UINT u_enable)
{
    return g_dispatch.enable_menu_item != 0
        ? g_dispatch.enable_menu_item(hmenu, u_id_enable_item, u_enable)
        : 0;
}

WF_EXPORT INT GetMenuItemCount(HMENU hmenu)
{
    return g_dispatch.get_menu_item_count != 0 ? g_dispatch.get_menu_item_count(hmenu) : 0;
}

WF_EXPORT BOOL GetMenuItemInfo(HMENU hmenu, UINT item, BOOL f_by_position, void* lpmii)
{
    return g_dispatch.get_menu_item_info != 0 ? g_dispatch.get_menu_item_info(hmenu, item, f_by_position, lpmii) : 0;
}

WF_EXPORT BOOL GetMenuItemInfoA(HMENU hmenu, UINT item, BOOL f_by_position, void* lpmii)
{
    return GetMenuItemInfo(hmenu, item, f_by_position, lpmii);
}

WF_EXPORT BOOL GetMenuItemInfoW(HMENU hmenu, UINT item, BOOL f_by_position, void* lpmii)
{
    return GetMenuItemInfo(hmenu, item, f_by_position, lpmii);
}

WF_EXPORT BOOL DrawMenuBar(HWND hwnd)
{
    return g_dispatch.draw_menu_bar != 0 ? g_dispatch.draw_menu_bar(hwnd) : 0;
}

WF_EXPORT BOOL UpdateWindow(HWND hwnd)
{
    return g_dispatch.update_window != 0 ? g_dispatch.update_window(hwnd) : 0;
}

WF_EXPORT BOOL InvalidateRect(HWND hwnd, const WinFormsXRect* rect, BOOL erase)
{
    return g_dispatch.invalidate_rect != 0 ? g_dispatch.invalidate_rect(hwnd, rect, erase) : 0;
}

WF_EXPORT BOOL ValidateRect(HWND hwnd, const WinFormsXRect* rect)
{
    return g_dispatch.validate_rect != 0 ? g_dispatch.validate_rect(hwnd, rect) : 0;
}

WF_EXPORT UINT MsgWaitForMultipleObjectsEx(UINT n_count, const intptr_t* handles, UINT milliseconds, UINT wake_mask, UINT flags)
{
    return g_dispatch.msg_wait_for_multiple_objects_ex != 0
        ? g_dispatch.msg_wait_for_multiple_objects_ex(n_count, handles, milliseconds, wake_mask, flags)
        : 0x00000102;
}

WF_EXPORT BOOL OpenClipboard(HWND hwnd_new_owner)
{
    return g_dispatch.open_clipboard != 0 ? g_dispatch.open_clipboard(hwnd_new_owner) : 0;
}

WF_EXPORT BOOL CloseClipboard(void)
{
    return g_dispatch.close_clipboard != 0 ? g_dispatch.close_clipboard() : 0;
}

WF_EXPORT BOOL EmptyClipboard(void)
{
    return g_dispatch.empty_clipboard != 0 ? g_dispatch.empty_clipboard() : 0;
}

WF_EXPORT intptr_t SetClipboardData(UINT format, intptr_t data)
{
    return g_dispatch.set_clipboard_data != 0 ? g_dispatch.set_clipboard_data(format, data) : 0;
}

WF_EXPORT intptr_t GetClipboardData(UINT format)
{
    return g_dispatch.get_clipboard_data != 0 ? g_dispatch.get_clipboard_data(format) : 0;
}

WF_EXPORT BOOL IsClipboardFormatAvailable(UINT format)
{
    return g_dispatch.is_clipboard_format_available != 0 ? g_dispatch.is_clipboard_format_available(format) : 0;
}

WF_EXPORT UINT RegisterClipboardFormatW(const WCHAR* format)
{
    return g_dispatch.register_clipboard_format != 0 ? g_dispatch.register_clipboard_format(format) : 0;
}

WF_EXPORT UINT RegisterClipboardFormatA(const char* format)
{
    if (format == 0 || g_dispatch.register_clipboard_format == 0)
    {
        return 0;
    }

    WCHAR buffer[512];
    int i = 0;
    for (; i < 511 && format[i] != 0; i++)
    {
        buffer[i] = (WCHAR)(unsigned char)format[i];
    }

    buffer[i] = 0;
    return g_dispatch.register_clipboard_format(buffer);
}

WF_EXPORT UINT RegisterClipboardFormat(const WCHAR* format)
{
    return RegisterClipboardFormatW(format);
}

WF_EXPORT INT GetClipboardFormatNameW(UINT format, WCHAR* buffer, INT max_count)
{
    return g_dispatch.get_clipboard_format_name != 0
        ? g_dispatch.get_clipboard_format_name(format, buffer, max_count)
        : 0;
}

WF_EXPORT INT GetClipboardFormatNameA(UINT format, char* buffer, INT max_count)
{
    if (buffer == 0 || max_count <= 0 || g_dispatch.get_clipboard_format_name == 0)
    {
        return 0;
    }

    WCHAR wide[512];
    INT wide_count = g_dispatch.get_clipboard_format_name(format, wide, 512);
    if (wide_count <= 0)
    {
        buffer[0] = 0;
        return 0;
    }

    INT count = wide_count < (max_count - 1) ? wide_count : (max_count - 1);
    for (INT i = 0; i < count; i++)
    {
        buffer[i] = wide[i] <= 0x7f ? (char)wide[i] : '?';
    }

    buffer[count] = 0;
    return count;
}

WF_EXPORT INT GetClipboardFormatName(UINT format, WCHAR* buffer, INT max_count)
{
    return GetClipboardFormatNameW(format, buffer, max_count);
}

WF_EXPORT HICON LoadIconW(intptr_t instance, const WCHAR* icon_name)
{
    if (g_dispatch.load_icon != 0)
    {
        HICON result = g_dispatch.load_icon(instance, icon_name);
        if (result != 0)
        {
            return result;
        }
    }

    (void)instance;
    (void)icon_name;
    return (HICON)(g_next_resource_handle++);
}

WF_EXPORT HICON LoadIconA(intptr_t instance, const char* icon_name)
{
    (void)icon_name;
    return LoadIconW(instance, 0);
}

WF_EXPORT HICON LoadIcon(intptr_t instance, const WCHAR* icon_name)
{
    return LoadIconW(instance, icon_name);
}

WF_EXPORT HCURSOR LoadCursorW(intptr_t instance, const WCHAR* cursor_name)
{
    if (g_dispatch.load_cursor != 0)
    {
        HCURSOR result = g_dispatch.load_cursor(instance, cursor_name);
        if (result != 0)
        {
            return result;
        }
    }

    (void)instance;
    (void)cursor_name;
    return (HCURSOR)(g_next_resource_handle++);
}

WF_EXPORT HCURSOR LoadCursorA(intptr_t instance, const char* cursor_name)
{
    (void)cursor_name;
    return LoadCursorW(instance, 0);
}

WF_EXPORT HCURSOR LoadCursor(intptr_t instance, const WCHAR* cursor_name)
{
    return LoadCursorW(instance, cursor_name);
}

WF_EXPORT BOOL DestroyIcon(HICON icon)
{
    if (g_dispatch.destroy_icon != 0)
    {
        return g_dispatch.destroy_icon(icon);
    }

    return icon != 0 ? 1 : 0;
}

WF_EXPORT BOOL DestroyCursor(HCURSOR cursor)
{
    if (g_dispatch.destroy_cursor != 0)
    {
        return g_dispatch.destroy_cursor(cursor) != 0 ? 1 : 0;
    }

    return cursor != 0 ? 1 : 0;
}

WF_EXPORT HICON CopyIcon(HICON icon)
{
    return icon != 0 ? (HICON)(g_next_resource_handle++) : 0;
}

WF_EXPORT HCURSOR CopyCursor(HCURSOR cursor)
{
    return cursor != 0 ? (HCURSOR)(g_next_resource_handle++) : 0;
}

WF_EXPORT HANDLE CopyImage(HANDLE image, UINT image_type, INT width, INT height, UINT flags)
{
    (void)image_type;
    (void)width;
    (void)height;
    (void)flags;
    return image != 0 ? (HANDLE)(g_next_resource_handle++) : 0;
}

WF_EXPORT BOOL GetIconInfo(HICON icon, WinFormsXIconInfo* icon_info)
{
    if (icon_info == 0)
    {
        return 0;
    }

    if (g_dispatch.get_icon_info != 0 && g_dispatch.get_icon_info(icon, icon_info))
    {
        return 1;
    }

    icon_info->fIcon = icon != 0 ? 1 : 0;
    icon_info->xHotspot = 0;
    icon_info->yHotspot = 0;
    icon_info->hbmMask = 0;
    icon_info->hbmColor = 0;
    return icon != 0 ? 1 : 0;
}

WF_EXPORT BOOL DrawIcon(HDC hdc, INT x, INT y, HICON icon)
{
    return DrawIconEx(hdc, x, y, icon, 0, 0, 0, 0, 3) != 0 ? 1 : 0;
}

WF_EXPORT BOOL DrawIconEx(HDC hdc, INT x, INT y, HICON icon, INT width, INT height, UINT step, HBRUSH brush, UINT flags)
{
    if (g_dispatch.draw_icon_ex != 0)
    {
        return g_dispatch.draw_icon_ex(hdc, x, y, icon, width, height, step, brush, flags) != 0 ? 1 : 0;
    }

    (void)hdc;
    (void)x;
    (void)y;
    (void)width;
    (void)height;
    (void)step;
    (void)brush;
    (void)flags;
    return icon != 0 ? 1 : 0;
}

WF_EXPORT HICON CreateIconFromResourceEx(UINT8* bits, UINT size, BOOL icon, UINT version, INT width, INT height, UINT flags)
{
    (void)bits;
    (void)size;
    (void)icon;
    (void)version;
    (void)width;
    (void)height;
    (void)flags;
    return bits != 0 || size == 0 ? (HICON)(g_next_resource_handle++) : 0;
}

WF_EXPORT HHOOK SetWindowsHookEx(INT id_hook, intptr_t hook_proc, intptr_t hmod, UINT thread_id)
{
    (void)id_hook;
    (void)hook_proc;
    (void)hmod;
    (void)thread_id;
    return (HHOOK)(g_next_hook_handle++);
}

WF_EXPORT HHOOK SetWindowsHookExA(INT id_hook, intptr_t hook_proc, intptr_t hmod, UINT thread_id)
{
    return SetWindowsHookEx(id_hook, hook_proc, hmod, thread_id);
}

WF_EXPORT HHOOK SetWindowsHookExW(INT id_hook, intptr_t hook_proc, intptr_t hmod, UINT thread_id)
{
    return SetWindowsHookEx(id_hook, hook_proc, hmod, thread_id);
}

WF_EXPORT BOOL UnhookWindowsHookEx(HHOOK hook)
{
    (void)hook;
    return 1;
}

WF_EXPORT intptr_t CallNextHookEx(HHOOK hook, INT code, WPARAM wparam, LPARAM lparam)
{
    (void)hook;
    (void)code;
    (void)wparam;
    (void)lparam;
    return 0;
}
