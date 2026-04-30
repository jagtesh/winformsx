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
typedef uint32_t DWORD;
typedef intptr_t HWND;
typedef intptr_t HIMC;

typedef struct WinFormsXImm32Dispatch
{
    uint32_t version;
    uint32_t size;
    HIMC (*imm_associate_context)(HWND hwnd, HIMC himc);
    HIMC (*imm_create_context)(void);
    HIMC (*imm_get_context)(HWND hwnd);
    BOOL (*imm_get_conversion_status)(HIMC himc, DWORD* conversion, DWORD* sentence);
    BOOL (*imm_get_open_status)(HIMC himc);
    BOOL (*imm_notify_ime)(HIMC himc, DWORD action, DWORD index, DWORD value);
    BOOL (*imm_release_context)(HWND hwnd, HIMC himc);
    BOOL (*imm_set_conversion_status)(HIMC himc, DWORD conversion, DWORD sentence);
    BOOL (*imm_set_open_status)(HIMC himc, BOOL open);
} WinFormsXImm32Dispatch;

static WinFormsXImm32Dispatch g_dispatch;

WF_EXPORT BOOL WinFormsXImm32RegisterDispatch(const WinFormsXImm32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXImm32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT HIMC ImmAssociateContext(HWND hwnd, HIMC himc)
{
    return g_dispatch.imm_associate_context != 0
        ? g_dispatch.imm_associate_context(hwnd, himc)
        : 0;
}

WF_EXPORT HIMC ImmCreateContext(void)
{
    return g_dispatch.imm_create_context != 0 ? g_dispatch.imm_create_context() : 0;
}

WF_EXPORT HIMC ImmGetContext(HWND hwnd)
{
    return g_dispatch.imm_get_context != 0 ? g_dispatch.imm_get_context(hwnd) : 0;
}

WF_EXPORT BOOL ImmGetConversionStatus(HIMC himc, DWORD* conversion, DWORD* sentence)
{
    if (conversion != 0)
    {
        *conversion = 0;
    }

    if (sentence != 0)
    {
        *sentence = 0;
    }

    return g_dispatch.imm_get_conversion_status != 0
        ? g_dispatch.imm_get_conversion_status(himc, conversion, sentence)
        : 0;
}

WF_EXPORT BOOL ImmGetOpenStatus(HIMC himc)
{
    return g_dispatch.imm_get_open_status != 0 ? g_dispatch.imm_get_open_status(himc) : 0;
}

WF_EXPORT BOOL ImmNotifyIME(HIMC himc, DWORD action, DWORD index, DWORD value)
{
    return g_dispatch.imm_notify_ime != 0
        ? g_dispatch.imm_notify_ime(himc, action, index, value)
        : 0;
}

WF_EXPORT BOOL ImmReleaseContext(HWND hwnd, HIMC himc)
{
    return g_dispatch.imm_release_context != 0
        ? g_dispatch.imm_release_context(hwnd, himc)
        : 0;
}

WF_EXPORT BOOL ImmSetConversionStatus(HIMC himc, DWORD conversion, DWORD sentence)
{
    return g_dispatch.imm_set_conversion_status != 0
        ? g_dispatch.imm_set_conversion_status(himc, conversion, sentence)
        : 0;
}

WF_EXPORT BOOL ImmSetOpenStatus(HIMC himc, BOOL open)
{
    return g_dispatch.imm_set_open_status != 0
        ? g_dispatch.imm_set_open_status(himc, open)
        : 0;
}
