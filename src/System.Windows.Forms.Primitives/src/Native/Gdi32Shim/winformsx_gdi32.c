// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef int32_t INT;
typedef uint32_t UINT;
typedef uint32_t COLORREF;
typedef intptr_t HANDLE;
typedef intptr_t HDC;
typedef intptr_t HGDIOBJ;
typedef intptr_t HBRUSH;
typedef intptr_t HPEN;
typedef intptr_t HBITMAP;
typedef intptr_t HPALETTE;
typedef intptr_t HRGN;

#define OBJ_PEN 1u
#define OBJ_BRUSH 2u
#define OBJ_PAL 5u
#define OBJ_FONT 6u
#define OBJ_BITMAP 7u
#define OBJ_REGION 8u
#define OBJ_MEMDC 10u
#define TRANSPARENT_MODE 1
#define OPAQUE_MODE 2
#define SIMPLEREGION 2

typedef struct WinFormsXGdi32Dispatch
{
    uint32_t version;
    uint32_t size;
    HDC (*create_compatible_dc)(HDC hdc);
    BOOL (*delete_dc)(HDC hdc);
    INT (*get_device_caps)(HDC hdc, INT index);
    INT (*get_object)(HGDIOBJ object, INT count, void* data);
    UINT (*get_object_type)(HGDIOBJ object);
    HGDIOBJ (*get_stock_object)(INT object);
    HBRUSH (*create_solid_brush)(COLORREF color);
    HPEN (*create_pen)(INT style, INT width, COLORREF color);
    BOOL (*delete_object)(HGDIOBJ object);
    COLORREF (*set_bk_color)(HDC hdc, COLORREF color);
    COLORREF (*set_text_color)(HDC hdc, COLORREF color);
    COLORREF (*get_bk_color)(HDC hdc);
    COLORREF (*get_text_color)(HDC hdc);
    INT (*get_bk_mode)(HDC hdc);
    INT (*set_bk_mode)(HDC hdc, INT mode);
    HGDIOBJ (*select_object)(HDC hdc, HGDIOBJ object);
} WinFormsXGdi32Dispatch;

typedef struct WinFormsXGdiObject
{
    uint32_t magic;
    UINT type;
    COLORREF color;
    INT style;
    INT width;
    INT height;
} WinFormsXGdiObject;

typedef struct WinFormsXLogBrush
{
    UINT style;
    COLORREF color;
    uintptr_t hatch;
} WinFormsXLogBrush;

typedef struct WinFormsXRect
{
    INT left;
    INT top;
    INT right;
    INT bottom;
} WinFormsXRect;

typedef struct WinFormsXPaletteEntry
{
    uint8_t red;
    uint8_t green;
    uint8_t blue;
    uint8_t flags;
} WinFormsXPaletteEntry;

static WinFormsXGdi32Dispatch g_dispatch;
static COLORREF g_default_bk_color = 0x00FFFFFFu;
static COLORREF g_default_text_color = 0x00000000u;
static INT g_default_bk_mode = OPAQUE_MODE;
static intptr_t g_next_dc = 0x320000;

static const uint32_t g_object_magic = 0x58474449u;

static WinFormsXGdiObject* as_fallback_object(HGDIOBJ object);

WF_EXPORT BOOL WinFormsXGdi32RegisterDispatch(const WinFormsXGdi32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXGdi32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

static HGDIOBJ create_fallback_object(UINT type, COLORREF color, INT style, INT width)
{
    WinFormsXGdiObject* object = (WinFormsXGdiObject*)calloc(1, sizeof(WinFormsXGdiObject));
    if (object == 0)
    {
        return 0;
    }

    object->magic = g_object_magic;
    object->type = type;
    object->color = color;
    object->style = style;
    object->width = width;
    return (HGDIOBJ)(intptr_t)object;
}

static HGDIOBJ create_fallback_sized_object(UINT type, INT width, INT height)
{
    HGDIOBJ handle = create_fallback_object(type, 0, 0, width);
    WinFormsXGdiObject* object = as_fallback_object(handle);
    if (object != 0)
    {
        object->height = height;
    }

    return handle;
}

static WinFormsXGdiObject* as_fallback_object(HGDIOBJ object)
{
    WinFormsXGdiObject* value = (WinFormsXGdiObject*)(intptr_t)object;
    if (value == 0 || value->magic != g_object_magic)
    {
        return 0;
    }

    return value;
}

WF_EXPORT HDC CreateCompatibleDC(HDC hdc)
{
    if (g_dispatch.create_compatible_dc != 0)
    {
        HDC result = g_dispatch.create_compatible_dc(hdc);
        if (result != 0)
        {
            return result;
        }
    }

    return ++g_next_dc;
}

WF_EXPORT HDC CreateDCW(const void* driver, const void* device, const void* output, const void* initData)
{
    (void)driver;
    (void)device;
    (void)output;
    (void)initData;
    return ++g_next_dc;
}

WF_EXPORT HDC CreateDCA(const void* driver, const void* device, const void* output, const void* initData)
{
    return CreateDCW(driver, device, output, initData);
}

WF_EXPORT HDC CreateICW(const void* driver, const void* device, const void* output, const void* initData)
{
    return CreateDCW(driver, device, output, initData);
}

WF_EXPORT HDC CreateICA(const void* driver, const void* device, const void* output, const void* initData)
{
    return CreateDCW(driver, device, output, initData);
}

WF_EXPORT BOOL DeleteDC(HDC hdc)
{
    if (g_dispatch.delete_dc != 0)
    {
        return g_dispatch.delete_dc(hdc);
    }

    return hdc != 0 ? 1 : 0;
}

WF_EXPORT INT GetDeviceCaps(HDC hdc, INT index)
{
    if (g_dispatch.get_device_caps != 0)
    {
        return g_dispatch.get_device_caps(hdc, index);
    }

    switch (index)
    {
        case 12: return 32;   // BITSPIXEL
        case 14: return 1;    // PLANES
        case 88: return 96;   // LOGPIXELSX
        case 90: return 96;   // LOGPIXELSY
        case 8: return 1920;  // HORZRES
        case 10: return 1080; // VERTRES
        default: return 0;
    }
}

WF_EXPORT HBRUSH CreateSolidBrush(COLORREF color)
{
    if (g_dispatch.create_solid_brush != 0)
    {
        HBRUSH result = g_dispatch.create_solid_brush(color);
        if (result != 0)
        {
            return result;
        }
    }

    return (HBRUSH)create_fallback_object(OBJ_BRUSH, color, 0, 0);
}

WF_EXPORT HPEN CreatePen(INT style, INT width, COLORREF color)
{
    if (g_dispatch.create_pen != 0)
    {
        HPEN result = g_dispatch.create_pen(style, width, color);
        if (result != 0)
        {
            return result;
        }
    }

    return (HPEN)create_fallback_object(OBJ_PEN, color, style, width);
}

WF_EXPORT HBRUSH CreateBrushIndirect(const WinFormsXLogBrush* brush)
{
    if (brush == 0)
    {
        return 0;
    }

    return (HBRUSH)create_fallback_object(OBJ_BRUSH, brush->color, (INT)brush->style, 0);
}

WF_EXPORT HBRUSH CreatePatternBrush(HBITMAP bitmap)
{
    return bitmap != 0 ? (HBRUSH)create_fallback_object(OBJ_BRUSH, 0, 3, 0) : 0;
}

WF_EXPORT HGDIOBJ CreateBitmap(INT width, INT height, UINT planes, UINT bitsPixel, const void* bits)
{
    (void)planes;
    (void)bitsPixel;
    (void)bits;
    return create_fallback_sized_object(OBJ_BITMAP, width, height);
}

WF_EXPORT HGDIOBJ CreateCompatibleBitmap(HDC hdc, INT width, INT height)
{
    (void)hdc;
    return create_fallback_sized_object(OBJ_BITMAP, width, height);
}

WF_EXPORT HGDIOBJ CreateDIBSection(HDC hdc, const void* bitmapInfo, UINT usage, void** bits, HANDLE section, uint32_t offset)
{
    (void)hdc;
    (void)bitmapInfo;
    (void)usage;
    (void)section;
    (void)offset;
    if (bits != 0)
    {
        *bits = 0;
    }

    return create_fallback_sized_object(OBJ_BITMAP, 1, 1);
}

WF_EXPORT HGDIOBJ CreateFontIndirectW(const void* logFont)
{
    (void)logFont;
    return create_fallback_object(OBJ_FONT, 0, 0, 0);
}

WF_EXPORT HGDIOBJ CreateRectRgn(INT left, INT top, INT right, INT bottom)
{
    return create_fallback_sized_object(OBJ_REGION, right - left, bottom - top);
}

WF_EXPORT INT CombineRgn(HGDIOBJ destination, HGDIOBJ source1, HGDIOBJ source2, INT mode)
{
    (void)source1;
    (void)source2;
    (void)mode;
    return destination != 0 ? 1 : 0;
}

WF_EXPORT HPALETTE CreateHalftonePalette(HDC hdc)
{
    (void)hdc;
    return (HPALETTE)create_fallback_object(OBJ_PAL, 0, 0, 0);
}

WF_EXPORT HPALETTE SelectPalette(HDC hdc, HPALETTE palette, BOOL forceBackground)
{
    (void)hdc;
    (void)forceBackground;
    return palette;
}

WF_EXPORT UINT RealizePalette(HDC hdc)
{
    (void)hdc;
    return 0;
}

WF_EXPORT UINT GetPaletteEntries(HPALETTE palette, UINT start, UINT count, WinFormsXPaletteEntry* entries)
{
    (void)start;
    if (palette == 0)
    {
        return 0;
    }

    if (entries != 0)
    {
        for (UINT i = 0; i < count; i++)
        {
            entries[i].red = 0;
            entries[i].green = 0;
            entries[i].blue = 0;
            entries[i].flags = 0;
        }
    }

    return count;
}

WF_EXPORT BOOL DeleteObject(HGDIOBJ object)
{
    if (g_dispatch.delete_object != 0 && g_dispatch.delete_object(object))
    {
        return 1;
    }

    WinFormsXGdiObject* fallback = as_fallback_object(object);
    if (fallback != 0)
    {
        fallback->magic = 0;
        free(fallback);
        return 1;
    }

    return object != 0 ? 1 : 0;
}

WF_EXPORT HGDIOBJ SelectObject(HDC hdc, HGDIOBJ object)
{
    if (g_dispatch.select_object != 0)
    {
        HGDIOBJ result = g_dispatch.select_object(hdc, object);
        if (result != 0)
        {
            return result;
        }
    }

    return object;
}

WF_EXPORT HGDIOBJ GetStockObject(INT object)
{
    if (g_dispatch.get_stock_object != 0)
    {
        HGDIOBJ result = g_dispatch.get_stock_object(object);
        if (result != 0)
        {
            return result;
        }
    }

    return (HGDIOBJ)(intptr_t)(0x510000 + object);
}

WF_EXPORT UINT GetObjectType(HGDIOBJ object)
{
    if (g_dispatch.get_object_type != 0)
    {
        UINT result = g_dispatch.get_object_type(object);
        if (result != 0)
        {
            return result;
        }
    }

    WinFormsXGdiObject* fallback = as_fallback_object(object);
    if (fallback != 0)
    {
        return fallback->type;
    }

    if (object >= 0x510000 && object < 0x520000)
    {
        return OBJ_BRUSH;
    }

    return object != 0 ? OBJ_MEMDC : 0;
}

WF_EXPORT INT GetObjectW(HGDIOBJ object, INT count, void* data)
{
    if (g_dispatch.get_object != 0)
    {
        INT result = g_dispatch.get_object(object, count, data);
        if (result != 0)
        {
            return result;
        }
    }

    WinFormsXGdiObject* fallback = as_fallback_object(object);
    if (fallback == 0 || data == 0 || count < (INT)sizeof(WinFormsXLogBrush))
    {
        return 0;
    }

    WinFormsXLogBrush brush = { fallback->style, fallback->color, 0 };
    memcpy(data, &brush, sizeof(brush));
    return (INT)sizeof(brush);
}

WF_EXPORT INT GetObjectA(HGDIOBJ object, INT count, void* data)
{
    return GetObjectW(object, count, data);
}

WF_EXPORT INT GetObject(HGDIOBJ object, INT count, void* data)
{
    return GetObjectW(object, count, data);
}

WF_EXPORT COLORREF SetBkColor(HDC hdc, COLORREF color)
{
    if (g_dispatch.set_bk_color != 0)
    {
        return g_dispatch.set_bk_color(hdc, color);
    }

    COLORREF previous = g_default_bk_color;
    g_default_bk_color = color;
    return previous;
}

WF_EXPORT COLORREF SetTextColor(HDC hdc, COLORREF color)
{
    if (g_dispatch.set_text_color != 0)
    {
        return g_dispatch.set_text_color(hdc, color);
    }

    COLORREF previous = g_default_text_color;
    g_default_text_color = color;
    return previous;
}

WF_EXPORT COLORREF GetBkColor(HDC hdc)
{
    return g_dispatch.get_bk_color != 0 ? g_dispatch.get_bk_color(hdc) : g_default_bk_color;
}

WF_EXPORT COLORREF GetTextColor(HDC hdc)
{
    return g_dispatch.get_text_color != 0 ? g_dispatch.get_text_color(hdc) : g_default_text_color;
}

WF_EXPORT INT GetBkMode(HDC hdc)
{
    return g_dispatch.get_bk_mode != 0 ? g_dispatch.get_bk_mode(hdc) : g_default_bk_mode;
}

WF_EXPORT INT SetBkMode(HDC hdc, INT mode)
{
    if (g_dispatch.set_bk_mode != 0)
    {
        return g_dispatch.set_bk_mode(hdc, mode);
    }

    INT previous = g_default_bk_mode;
    g_default_bk_mode = mode;
    return previous;
}

WF_EXPORT BOOL PatBlt(HDC hdc, INT x, INT y, INT width, INT height, uint32_t rop)
{
    (void)hdc;
    (void)x;
    (void)y;
    (void)width;
    (void)height;
    (void)rop;
    return 1;
}

WF_EXPORT BOOL BitBlt(HDC hdc, INT x, INT y, INT width, INT height, HDC source, INT sourceX, INT sourceY, uint32_t rop)
{
    (void)hdc;
    (void)x;
    (void)y;
    (void)width;
    (void)height;
    (void)source;
    (void)sourceX;
    (void)sourceY;
    (void)rop;
    return 1;
}

WF_EXPORT INT SelectClipRgn(HDC hdc, HRGN region)
{
    (void)hdc;
    return region != 0 ? SIMPLEREGION : 1;
}

WF_EXPORT INT IntersectClipRect(HDC hdc, INT left, INT top, INT right, INT bottom)
{
    (void)hdc;
    return right > left && bottom > top ? SIMPLEREGION : 1;
}

WF_EXPORT INT GetClipBox(HDC hdc, WinFormsXRect* rect)
{
    (void)hdc;
    if (rect != 0)
    {
        rect->left = 0;
        rect->top = 0;
        rect->right = 1920;
        rect->bottom = 1080;
    }

    return SIMPLEREGION;
}

WF_EXPORT INT StartDocW(HDC hdc, const void* docInfo)
{
    (void)hdc;
    (void)docInfo;
    return 1;
}

WF_EXPORT INT StartDocA(HDC hdc, const void* docInfo)
{
    return StartDocW(hdc, docInfo);
}

WF_EXPORT INT StartPage(HDC hdc)
{
    (void)hdc;
    return 1;
}

WF_EXPORT INT EndPage(HDC hdc)
{
    (void)hdc;
    return 1;
}

WF_EXPORT INT EndDoc(HDC hdc)
{
    (void)hdc;
    return 1;
}

WF_EXPORT INT AbortDoc(HDC hdc)
{
    (void)hdc;
    return 1;
}

WF_EXPORT INT ExtEscape(HDC hdc, INT escape, INT inputSize, const void* input, INT outputSize, void* output)
{
    (void)hdc;
    (void)escape;
    (void)inputSize;
    (void)input;
    (void)outputSize;
    (void)output;
    return 0;
}
