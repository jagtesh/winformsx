// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef int32_t INT;
typedef uint32_t UINT;
typedef uintptr_t ULONG_PTR;

#define GP_STATUS_OK 0
#define GP_STATUS_INVALID_PARAMETER 2
#define GP_STATUS_NOT_IMPLEMENTED 6

typedef struct WinFormsXGdiplusStartupInput
{
    UINT gdiplusVersion;
    void* debugEventCallback;
    BOOL suppressBackgroundThread;
    BOOL suppressExternalCodecs;
} WinFormsXGdiplusStartupInput;

typedef struct WinFormsXGdiplusStartupOutput
{
    void* notificationHook;
    void* notificationUnhook;
} WinFormsXGdiplusStartupOutput;

static ULONG_PTR g_next_token = 0x670000u;

WF_EXPORT INT GdiplusStartup(
    ULONG_PTR* token,
    const WinFormsXGdiplusStartupInput* input,
    WinFormsXGdiplusStartupOutput* output)
{
    if (token != 0)
    {
        *token = 0;
    }

    if (token == 0 || input == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    if (input->gdiplusVersion < 1u || input->gdiplusVersion > 2u)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *token = ++g_next_token;

    if (output != 0)
    {
        output->notificationHook = 0;
        output->notificationUnhook = 0;
    }

    return GP_STATUS_OK;
}

WF_EXPORT void GdiplusShutdown(ULONG_PTR token)
{
    (void)token;
}

WF_EXPORT INT GdipGetImageDecodersSize(UINT* numberOfDecoders, UINT* size)
{
    if (numberOfDecoders == 0 || size == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *numberOfDecoders = 0;
    *size = 0;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipCreateBitmapFromScan0(
    INT width,
    INT height,
    INT stride,
    INT pixelFormat,
    const void* scan0,
    void** bitmap)
{
    (void)stride;
    (void)pixelFormat;
    (void)scan0;

    if (bitmap == 0 || width <= 0 || height <= 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *bitmap = 0;
    return GP_STATUS_NOT_IMPLEMENTED;
}
