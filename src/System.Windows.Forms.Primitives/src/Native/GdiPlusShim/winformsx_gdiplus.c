// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdio.h>

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
#define GP_STATUS_OUT_OF_MEMORY 3
#define GP_STATUS_NOT_IMPLEMENTED 6
#define WF_GDIPLUS_MAX_IMAGES 128
#define WF_GDIPLUS_IMAGE_FLAGS_NONE 0
#define WF_GDIPLUS_PIXEL_FORMAT_32BPP_ARGB 0x0026200Au

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

typedef struct WinFormsXGdiplusImage
{
    ULONG_PTR handle;
    UINT width;
    UINT height;
    UINT flags;
    UINT pixelFormat;
    float horizontalResolution;
    float verticalResolution;
    BOOL disposed;
} WinFormsXGdiplusImage;

static ULONG_PTR g_next_token = 0x670000u;
static ULONG_PTR g_next_image = 0x680000u;
static WinFormsXGdiplusImage g_images[WF_GDIPLUS_MAX_IMAGES];

static WinFormsXGdiplusImage* WinFormsXFindImage(void* image)
{
    ULONG_PTR handle = (ULONG_PTR)image;
    if (handle == 0)
    {
        return 0;
    }

    for (UINT i = 0; i < WF_GDIPLUS_MAX_IMAGES; i++)
    {
        if (g_images[i].handle == handle && !g_images[i].disposed)
        {
            return &g_images[i];
        }
    }

    return 0;
}

static INT WinFormsXCreateSyntheticImage(UINT width, UINT height, void** image)
{
    if (image == 0 || width == 0 || height == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *image = 0;

    for (UINT i = 0; i < WF_GDIPLUS_MAX_IMAGES; i++)
    {
        if (g_images[i].handle == 0 || g_images[i].disposed)
        {
            g_images[i].handle = ++g_next_image;
            g_images[i].width = width;
            g_images[i].height = height;
            g_images[i].flags = WF_GDIPLUS_IMAGE_FLAGS_NONE;
            g_images[i].pixelFormat = WF_GDIPLUS_PIXEL_FORMAT_32BPP_ARGB;
            g_images[i].horizontalResolution = 96.0f;
            g_images[i].verticalResolution = 96.0f;
            g_images[i].disposed = 0;
            *image = (void*)g_images[i].handle;
            return GP_STATUS_OK;
        }
    }

    return GP_STATUS_OUT_OF_MEMORY;
}

static BOOL WinFormsXPathExists(const uint16_t* filename)
{
    char path[1024];
    UINT i = 0;

    if (filename == 0 || filename[0] == 0)
    {
        return 0;
    }

    for (; i < (UINT)(sizeof(path) - 1); i++)
    {
        uint16_t ch = filename[i];
        if (ch == 0)
        {
            break;
        }

        path[i] = ch <= 0x7Fu ? (char)ch : '?';
    }

    if (i == 0 || i == (UINT)(sizeof(path) - 1))
    {
        return 0;
    }

    path[i] = '\0';

    FILE* file = fopen(path, "rb");
    if (file == 0)
    {
        return 0;
    }

    fclose(file);
    return 1;
}


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

WF_EXPORT INT GdipCreateBitmapFromHBITMAP(void* hbm, void* hpal, void** bitmap)
{
    (void)hpal;

    if (bitmap == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *bitmap = 0;
    if (hbm == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    return WinFormsXCreateSyntheticImage(16, 16, bitmap);
}

WF_EXPORT INT GdipCreateBitmapFromHICON(void* hicon, void** bitmap)
{
    if (bitmap == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *bitmap = 0;
    if (hicon == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    return WinFormsXCreateSyntheticImage(32, 32, bitmap);
}

WF_EXPORT INT GdipLoadImageFromFile(const uint16_t* filename, void** image)
{
    if (image == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *image = 0;
    if (!WinFormsXPathExists(filename))
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    return WinFormsXCreateSyntheticImage(64, 64, image);
}

WF_EXPORT INT GdipLoadImageFromFileICM(const uint16_t* filename, void** image)
{
    return GdipLoadImageFromFile(filename, image);
}

WF_EXPORT INT GdipLoadImageFromStream(void* stream, void** image)
{
    if (image == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *image = 0;
    return stream != 0 ? GP_STATUS_NOT_IMPLEMENTED : GP_STATUS_INVALID_PARAMETER;
}

WF_EXPORT INT GdipLoadImageFromStreamICM(void* stream, void** image)
{
    return GdipLoadImageFromStream(stream, image);
}

WF_EXPORT INT GdipDisposeImage(void* image)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    entry->disposed = 1;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImageWidth(void* image, UINT* width)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || width == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *width = entry->width;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImageHeight(void* image, UINT* height)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || height == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *height = entry->height;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImageFlags(void* image, UINT* flags)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || flags == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *flags = entry->flags;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImagePixelFormat(void* image, INT* pixelFormat)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || pixelFormat == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *pixelFormat = (INT)entry->pixelFormat;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImageHorizontalResolution(void* image, float* resolution)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || resolution == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *resolution = entry->horizontalResolution;
    return GP_STATUS_OK;
}

WF_EXPORT INT GdipGetImageVerticalResolution(void* image, float* resolution)
{
    WinFormsXGdiplusImage* entry = WinFormsXFindImage(image);
    if (entry == 0 || resolution == 0)
    {
        return GP_STATUS_INVALID_PARAMETER;
    }

    *resolution = entry->verticalResolution;
    return GP_STATUS_OK;
}
