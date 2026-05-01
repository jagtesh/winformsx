// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef int32_t HRESULT;
typedef uint32_t DWORD;
typedef uint32_t COLORREF;

typedef struct RECT
{
    int32_t left;
    int32_t top;
    int32_t right;
    int32_t bottom;
} RECT;

typedef struct IMAGEINFO
{
    void* hbmImage;
    void* hbmMask;
    int32_t Unused1;
    int32_t Unused2;
    RECT rcImage;
} IMAGEINFO;

typedef struct INITCOMMONCONTROLSEX
{
    DWORD dwSize;
    DWORD dwICC;
} INITCOMMONCONTROLSEX;

static DWORD g_initialized_classes;

typedef struct WinFormsXImageList
{
    int32_t width;
    int32_t height;
    int32_t count;
    COLORREF back_color;
    uintptr_t bitmap;
} WinFormsXImageList;

static uintptr_t g_next_bitmap = 0x510000u;
static uintptr_t g_next_icon = 0x520000u;

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

WF_EXPORT void* ImageList_Create(int32_t cx, int32_t cy, uint32_t flags, int32_t cInitial, int32_t cGrow)
{
    (void)flags;
    (void)cInitial;
    (void)cGrow;

    WinFormsXImageList* image_list = (WinFormsXImageList*)calloc(1, sizeof(WinFormsXImageList));
    if (image_list == 0)
    {
        return 0;
    }

    image_list->width = cx;
    image_list->height = cy;
    image_list->back_color = 0xFFFFFFFFu;
    image_list->bitmap = ++g_next_bitmap;
    return image_list;
}

WF_EXPORT BOOL ImageList_Destroy(void* himl)
{
    free(himl);
    return 1;
}

WF_EXPORT int32_t ImageList_Add(void* himl, void* hbmImage, void* hbmMask)
{
    (void)hbmImage;
    (void)hbmMask;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0)
    {
        return -1;
    }

    return image_list->count++;
}

WF_EXPORT int32_t ImageList_ReplaceIcon(void* himl, int32_t i, void* hicon)
{
    (void)hicon;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0)
    {
        return -1;
    }

    if (i == -1)
    {
        return image_list->count++;
    }

    return i >= 0 && i < image_list->count ? i : -1;
}

WF_EXPORT BOOL ImageList_Replace(void* himl, int32_t i, void* hbmImage, void* hbmMask)
{
    (void)hbmImage;
    (void)hbmMask;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || i < 0 || i >= image_list->count)
    {
        return 0;
    }

    image_list->bitmap = ++g_next_bitmap;
    return 1;
}

WF_EXPORT int32_t ImageList_AddMasked(void* himl, void* hbmImage, COLORREF crMask)
{
    (void)hbmImage;
    (void)crMask;
    return ImageList_Add(himl, 0, 0);
}

WF_EXPORT void* ImageList_GetIcon(void* himl, int32_t i, uint32_t flags)
{
    (void)flags;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || i < 0 || i >= image_list->count)
    {
        return 0;
    }

    return (void*)(++g_next_icon);
}

WF_EXPORT BOOL ImageList_Remove(void* himl, int32_t i)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0)
    {
        return 0;
    }

    if (i == -1)
    {
        image_list->count = 0;
        return 1;
    }

    if (i < 0 || i >= image_list->count)
    {
        return 0;
    }

    image_list->count--;
    return 1;
}

WF_EXPORT int32_t ImageList_GetImageCount(void* himl)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    return image_list != 0 ? image_list->count : 0;
}

WF_EXPORT BOOL ImageList_GetIconSize(void* himl, int32_t* cx, int32_t* cy)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || cx == 0 || cy == 0)
    {
        return 0;
    }

    *cx = image_list->width;
    *cy = image_list->height;
    return 1;
}

WF_EXPORT BOOL ImageList_SetIconSize(void* himl, int32_t cx, int32_t cy)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0)
    {
        return 0;
    }

    image_list->width = cx;
    image_list->height = cy;
    image_list->count = 0;
    image_list->bitmap = ++g_next_bitmap;
    return 1;
}

WF_EXPORT BOOL ImageList_GetImageInfo(void* himl, int32_t i, IMAGEINFO* image_info)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || image_info == 0 || i < 0 || i >= image_list->count)
    {
        return 0;
    }

    image_info->hbmImage = (void*)image_list->bitmap;
    image_info->hbmMask = 0;
    image_info->Unused1 = 0;
    image_info->Unused2 = 0;
    image_info->rcImage.left = 0;
    image_info->rcImage.top = 0;
    image_info->rcImage.right = image_list->width;
    image_info->rcImage.bottom = image_list->height;
    return 1;
}

WF_EXPORT COLORREF ImageList_SetBkColor(void* himl, COLORREF clrBk)
{
    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0)
    {
        return 0xFFFFFFFFu;
    }

    COLORREF previous = image_list->back_color;
    image_list->back_color = clrBk;
    return previous;
}

WF_EXPORT BOOL ImageList_Write(void* himl, void* pstm)
{
    (void)pstm;
    return himl != 0;
}

WF_EXPORT HRESULT ImageList_WriteEx(void* himl, uint32_t dwFlags, void* pstm)
{
    (void)dwFlags;
    (void)pstm;
    return himl != 0 ? 0 : (HRESULT)0x80070057u;
}

WF_EXPORT BOOL ImageList_Draw(void* himl, int32_t i, void* hdcDst, int32_t x, int32_t y, uint32_t fStyle)
{
    (void)hdcDst;
    (void)x;
    (void)y;
    (void)fStyle;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || i < 0 || i >= image_list->count)
    {
        return 0;
    }

    return 1;
}

WF_EXPORT BOOL ImageList_DrawEx(
    void* himl,
    int32_t i,
    void* hdcDst,
    int32_t x,
    int32_t y,
    int32_t dx,
    int32_t dy,
    COLORREF rgbBk,
    COLORREF rgbFg,
    uint32_t fStyle)
{
    (void)hdcDst;
    (void)x;
    (void)y;
    (void)dx;
    (void)dy;
    (void)rgbBk;
    (void)rgbFg;
    (void)fStyle;

    WinFormsXImageList* image_list = (WinFormsXImageList*)himl;
    if (image_list == 0 || i < 0 || i >= image_list->count)
    {
        return 0;
    }

    return 1;
}
