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
typedef int32_t HRESULT;
typedef uint32_t DWORD;

typedef struct WinFormsXComDlg32Dispatch
{
    uint32_t version;
    uint32_t size;
    BOOL (*get_open_file_name)(void* ofn);
    BOOL (*get_save_file_name)(void* ofn);
    BOOL (*choose_color)(void* choose_color);
    BOOL (*choose_font)(void* choose_font);
    BOOL (*print_dlg)(void* print_dlg);
    HRESULT (*print_dlg_ex)(void* print_dlg_ex);
    BOOL (*page_setup_dlg)(void* page_setup);
    DWORD (*comm_dlg_extended_error)(void);
} WinFormsXComDlg32Dispatch;

static WinFormsXComDlg32Dispatch g_dispatch;

WF_EXPORT BOOL WinFormsXComDlg32RegisterDispatch(const WinFormsXComDlg32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXComDlg32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT BOOL GetOpenFileNameW(void* ofn)
{
    return g_dispatch.get_open_file_name != 0 ? g_dispatch.get_open_file_name(ofn) : 0;
}

WF_EXPORT BOOL GetOpenFileNameA(void* ofn)
{
    return GetOpenFileNameW(ofn);
}

WF_EXPORT BOOL GetOpenFileName(void* ofn)
{
    return GetOpenFileNameW(ofn);
}

WF_EXPORT BOOL GetSaveFileNameW(void* ofn)
{
    return g_dispatch.get_save_file_name != 0 ? g_dispatch.get_save_file_name(ofn) : 0;
}

WF_EXPORT BOOL GetSaveFileNameA(void* ofn)
{
    return GetSaveFileNameW(ofn);
}

WF_EXPORT BOOL GetSaveFileName(void* ofn)
{
    return GetSaveFileNameW(ofn);
}

WF_EXPORT BOOL ChooseColorW(void* choose_color)
{
    return g_dispatch.choose_color != 0 ? g_dispatch.choose_color(choose_color) : 0;
}

WF_EXPORT BOOL ChooseColorA(void* choose_color)
{
    return ChooseColorW(choose_color);
}

WF_EXPORT BOOL ChooseColor(void* choose_color)
{
    return ChooseColorW(choose_color);
}

WF_EXPORT BOOL ChooseFontW(void* choose_font)
{
    return g_dispatch.choose_font != 0 ? g_dispatch.choose_font(choose_font) : 0;
}

WF_EXPORT BOOL ChooseFontA(void* choose_font)
{
    return ChooseFontW(choose_font);
}

WF_EXPORT BOOL ChooseFont(void* choose_font)
{
    return ChooseFontW(choose_font);
}

WF_EXPORT BOOL PrintDlgW(void* print_dlg)
{
    return g_dispatch.print_dlg != 0 ? g_dispatch.print_dlg(print_dlg) : 0;
}

WF_EXPORT BOOL PrintDlgA(void* print_dlg)
{
    return PrintDlgW(print_dlg);
}

WF_EXPORT BOOL PrintDlg(void* print_dlg)
{
    return PrintDlgW(print_dlg);
}

WF_EXPORT HRESULT PrintDlgExW(void* print_dlg_ex)
{
    return g_dispatch.print_dlg_ex != 0 ? g_dispatch.print_dlg_ex(print_dlg_ex) : 0;
}

WF_EXPORT HRESULT PrintDlgExA(void* print_dlg_ex)
{
    return PrintDlgExW(print_dlg_ex);
}

WF_EXPORT HRESULT PrintDlgEx(void* print_dlg_ex)
{
    return PrintDlgExW(print_dlg_ex);
}

WF_EXPORT BOOL PageSetupDlgW(void* page_setup)
{
    return g_dispatch.page_setup_dlg != 0 ? g_dispatch.page_setup_dlg(page_setup) : 0;
}

WF_EXPORT BOOL PageSetupDlgA(void* page_setup)
{
    return PageSetupDlgW(page_setup);
}

WF_EXPORT BOOL PageSetupDlg(void* page_setup)
{
    return PageSetupDlgW(page_setup);
}

WF_EXPORT DWORD CommDlgExtendedError(void)
{
    return g_dispatch.comm_dlg_extended_error != 0 ? g_dispatch.comm_dlg_extended_error() : 0;
}
