// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef uint32_t DWORD;

typedef struct INITCOMMONCONTROLSEX
{
    DWORD dwSize;
    DWORD dwICC;
} INITCOMMONCONTROLSEX;

static DWORD g_initialized_classes;

WF_EXPORT void InitCommonControls(void)
{
    g_initialized_classes |= 0x000000FFu;
}

WF_EXPORT BOOL InitCommonControlsEx(const INITCOMMONCONTROLSEX* picce)
{
    if (picce == 0 || picce->dwSize != sizeof(INITCOMMONCONTROLSEX))
    {
        return 0;
    }

    g_initialized_classes |= picce->dwICC;
    return 1;
}

WF_EXPORT DWORD WinFormsXComCtl32GetInitializedClasses(void)
{
    return g_initialized_classes;
}
