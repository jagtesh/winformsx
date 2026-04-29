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
typedef intptr_t HWND;

typedef struct WinFormsXPoint
{
    int32_t x;
    int32_t y;
} WinFormsXPoint;

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
} WinFormsXUser32Dispatch;

static WinFormsXUser32Dispatch g_dispatch;

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

WF_EXPORT INT GetSystemMetrics(INT index)
{
    return g_dispatch.get_system_metrics != 0 ? g_dispatch.get_system_metrics(index) : 0;
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
