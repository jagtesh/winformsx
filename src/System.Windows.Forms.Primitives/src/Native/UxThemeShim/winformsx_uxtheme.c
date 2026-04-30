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
typedef int32_t INT;
typedef uint32_t DWORD;
typedef uint32_t UINT;
typedef uint32_t COLORREF;
typedef void* HWND;
typedef void* HDC;
typedef void* HTHEME;
typedef void* HRGN;
typedef uint16_t WCHAR;

#define TRUE ((BOOL)1)
#define FALSE ((BOOL)0)
#define S_OK ((HRESULT)0)
#define S_FALSE ((HRESULT)1)
#define E_INVALIDARG ((HRESULT)0x80070057u)

typedef struct RECT
{
    int32_t left;
    int32_t top;
    int32_t right;
    int32_t bottom;
} RECT;

typedef struct SIZE
{
    int32_t cx;
    int32_t cy;
} SIZE;

typedef struct POINT
{
    int32_t x;
    int32_t y;
} POINT;

typedef struct MARGINS
{
    int32_t cxLeftWidth;
    int32_t cxRightWidth;
    int32_t cyTopHeight;
    int32_t cyBottomHeight;
} MARGINS;

static HTHEME g_theme = (HTHEME)(uintptr_t)0x710001;
static DWORD g_theme_app_properties = 0;

static void copy_rect(const RECT* source, RECT* destination)
{
    if (destination == 0)
    {
        return;
    }

    if (source == 0)
    {
        memset(destination, 0, sizeof(RECT));
        return;
    }

    *destination = *source;
}

static void inset_rect(const RECT* source, RECT* destination, int32_t inset)
{
    copy_rect(source, destination);
    if (destination == 0)
    {
        return;
    }

    if (destination->right - destination->left > inset * 2)
    {
        destination->left += inset;
        destination->right -= inset;
    }

    if (destination->bottom - destination->top > inset * 2)
    {
        destination->top += inset;
        destination->bottom -= inset;
    }
}

static HRESULT write_empty_string(WCHAR* buffer, int32_t count)
{
    if (buffer == 0 || count <= 0)
    {
        return E_INVALIDARG;
    }

    buffer[0] = 0;
    return S_OK;
}

WF_EXPORT HTHEME OpenThemeData(HWND hwnd, const WCHAR* classList)
{
    (void)hwnd;
    (void)classList;
    return g_theme;
}

WF_EXPORT HRESULT CloseThemeData(HTHEME theme)
{
    (void)theme;
    return S_OK;
}

WF_EXPORT HRESULT SetWindowTheme(HWND hwnd, const WCHAR* subAppName, const WCHAR* subIdList)
{
    (void)hwnd;
    (void)subAppName;
    (void)subIdList;
    return S_OK;
}

WF_EXPORT DWORD GetThemeAppProperties(void)
{
    return g_theme_app_properties;
}

WF_EXPORT void SetThemeAppProperties(DWORD flags)
{
    g_theme_app_properties = flags;
}

WF_EXPORT BOOL IsAppThemed(void)
{
    return FALSE;
}

WF_EXPORT BOOL IsThemeActive(void)
{
    return FALSE;
}

WF_EXPORT BOOL IsThemePartDefined(HTHEME theme, int partId, int stateId)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    return FALSE;
}

WF_EXPORT BOOL IsThemeBackgroundPartiallyTransparent(HTHEME theme, int partId, int stateId)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    return FALSE;
}

WF_EXPORT HRESULT DrawThemeBackground(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* rect, const RECT* clipRect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)rect;
    (void)clipRect;
    return S_OK;
}

WF_EXPORT HRESULT DrawThemeEdge(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* destRect, UINT edge, UINT flags, RECT* contentRect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)edge;
    (void)flags;
    inset_rect(destRect, contentRect, 1);
    return S_OK;
}

WF_EXPORT HRESULT DrawThemeParentBackground(HWND hwnd, HDC hdc, RECT* rect)
{
    (void)hwnd;
    (void)hdc;
    (void)rect;
    return S_OK;
}

WF_EXPORT HRESULT DrawThemeText(HTHEME theme, HDC hdc, int partId, int stateId, const WCHAR* text, int charCount, DWORD textFlags, DWORD textFlags2, const RECT* rect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)text;
    (void)charCount;
    (void)textFlags;
    (void)textFlags2;
    (void)rect;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeBackgroundContentRect(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* boundingRect, RECT* contentRect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    inset_rect(boundingRect, contentRect, 2);
    return S_OK;
}

WF_EXPORT HRESULT GetThemeBackgroundExtent(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* contentRect, RECT* extentRect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    copy_rect(contentRect, extentRect);
    return S_OK;
}

WF_EXPORT HRESULT GetThemeBackgroundRegion(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* rect, HRGN* region)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)rect;
    if (region == 0)
    {
        return E_INVALIDARG;
    }

    *region = 0;
    return S_FALSE;
}

WF_EXPORT HRESULT GetThemeBool(HTHEME theme, int partId, int stateId, int propId, BOOL* value)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (value == 0)
    {
        return E_INVALIDARG;
    }

    *value = FALSE;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeColor(HTHEME theme, int partId, int stateId, int propId, COLORREF* color)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (color == 0)
    {
        return E_INVALIDARG;
    }

    *color = 0;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeDocumentationProperty(const WCHAR* themeName, const WCHAR* propertyName, WCHAR* value, int valueCount)
{
    (void)themeName;
    (void)propertyName;
    return write_empty_string(value, valueCount);
}

WF_EXPORT HRESULT GetThemeEnumValue(HTHEME theme, int partId, int stateId, int propId, int* value)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (value == 0)
    {
        return E_INVALIDARG;
    }

    *value = 0;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeFilename(HTHEME theme, int partId, int stateId, int propId, WCHAR* filename, int filenameCount)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    return write_empty_string(filename, filenameCount);
}

WF_EXPORT HRESULT GetThemeFont(HTHEME theme, HDC hdc, int partId, int stateId, int propId, void* font)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (font == 0)
    {
        return E_INVALIDARG;
    }

    memset(font, 0, 92);
    return S_OK;
}

WF_EXPORT HRESULT GetThemeInt(HTHEME theme, int partId, int stateId, int propId, int* value)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (value == 0)
    {
        return E_INVALIDARG;
    }

    *value = 0;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeMargins(HTHEME theme, HDC hdc, int partId, int stateId, int propId, const RECT* rect, MARGINS* margins)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)propId;
    (void)rect;
    if (margins == 0)
    {
        return E_INVALIDARG;
    }

    memset(margins, 0, sizeof(MARGINS));
    return S_OK;
}

WF_EXPORT HRESULT GetThemePartSize(HTHEME theme, HDC hdc, int partId, int stateId, const RECT* rect, int sizeType, SIZE* size)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)rect;
    (void)sizeType;
    if (size == 0)
    {
        return E_INVALIDARG;
    }

    size->cx = 13;
    size->cy = 13;
    return S_OK;
}

WF_EXPORT HRESULT GetThemePosition(HTHEME theme, int partId, int stateId, int propId, POINT* point)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    if (point == 0)
    {
        return E_INVALIDARG;
    }

    point->x = 0;
    point->y = 0;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeString(HTHEME theme, int partId, int stateId, int propId, WCHAR* buffer, int bufferCount)
{
    (void)theme;
    (void)partId;
    (void)stateId;
    (void)propId;
    return write_empty_string(buffer, bufferCount);
}

WF_EXPORT BOOL GetThemeSysBool(HTHEME theme, int boolId)
{
    (void)theme;
    (void)boolId;
    return FALSE;
}

WF_EXPORT HRESULT GetThemeSysInt(HTHEME theme, int intId, int* value)
{
    (void)theme;
    (void)intId;
    if (value == 0)
    {
        return E_INVALIDARG;
    }

    *value = 0;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeTextExtent(HTHEME theme, HDC hdc, int partId, int stateId, const WCHAR* text, int charCount, DWORD textFlags, const RECT* boundingRect, RECT* extentRect)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)textFlags;
    if (extentRect == 0)
    {
        return E_INVALIDARG;
    }

    if (boundingRect != 0)
    {
        *extentRect = *boundingRect;
        return S_OK;
    }

    int32_t length = charCount > 0 ? charCount : 0;
    if (length == 0 && text != 0)
    {
        while (text[length] != 0)
        {
            length++;
        }
    }

    extentRect->left = 0;
    extentRect->top = 0;
    extentRect->right = length * 7;
    extentRect->bottom = 16;
    return S_OK;
}

WF_EXPORT HRESULT GetThemeTextMetrics(HTHEME theme, HDC hdc, int partId, int stateId, void* metrics)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    if (metrics == 0)
    {
        return E_INVALIDARG;
    }

    memset(metrics, 0, 60);
    ((int32_t*)metrics)[0] = 16;
    ((int32_t*)metrics)[4] = 7;
    ((int32_t*)metrics)[5] = 16;
    return S_OK;
}

WF_EXPORT HRESULT HitTestThemeBackground(HTHEME theme, HDC hdc, int partId, int stateId, DWORD options, const RECT* rect, HRGN region, POINT point, uint16_t* hitTestCode)
{
    (void)theme;
    (void)hdc;
    (void)partId;
    (void)stateId;
    (void)options;
    (void)region;
    if (hitTestCode == 0)
    {
        return E_INVALIDARG;
    }

    BOOL inside = rect != 0
        && point.x >= rect->left
        && point.x < rect->right
        && point.y >= rect->top
        && point.y < rect->bottom;
    *hitTestCode = inside ? 0x01 : 0x00;
    return S_OK;
}

WF_EXPORT HRESULT GetCurrentThemeName(WCHAR* themeFileName, int themeFileNameCount, WCHAR* colorBuff, int colorBuffCount, WCHAR* sizeBuff, int sizeBuffCount)
{
    HRESULT result = write_empty_string(themeFileName, themeFileNameCount);
    if (result != S_OK)
    {
        return result;
    }

    result = write_empty_string(colorBuff, colorBuffCount);
    if (result != S_OK)
    {
        return result;
    }

    return write_empty_string(sizeBuff, sizeBuffCount);
}
