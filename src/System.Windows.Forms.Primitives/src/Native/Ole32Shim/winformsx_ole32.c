// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef int32_t HRESULT;
typedef uint32_t DWORD;
typedef void* HWND;
typedef void* LPVOID;
typedef void* IDataObject;
typedef void* IDropSource;
typedef void* IDropTarget;
typedef void* IUnknown;

#define S_OK ((HRESULT)0)
#define S_FALSE ((HRESULT)1)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define REGDB_E_CLASSNOTREG ((HRESULT)0x80040154u)
#define CLIPBRD_E_BAD_DATA ((HRESULT)0x800401D3u)
#define DRAGDROP_S_CANCEL ((HRESULT)0x00040101u)
#define DRAGDROP_E_NOTREGISTERED ((HRESULT)0x80040100u)
#define DRAGDROP_E_ALREADYREGISTERED ((HRESULT)0x80040101u)
#define DRAGDROP_E_INVALIDHWND ((HRESULT)0x80040102u)

typedef struct GUID
{
    uint32_t Data1;
    uint16_t Data2;
    uint16_t Data3;
    uint8_t Data4[8];
} GUID;

typedef struct DropRegistration
{
    HWND hwnd;
    IDropTarget* target;
    int used;
} DropRegistration;

static DWORD g_initialize_count;
static IDataObject* g_clipboard;
static DropRegistration g_drop_registrations[128];

static DropRegistration* find_registration(HWND hwnd)
{
    for (int i = 0; i < 128; i++)
    {
        if (g_drop_registrations[i].used != 0 && g_drop_registrations[i].hwnd == hwnd)
        {
            return &g_drop_registrations[i];
        }
    }

    return 0;
}

WF_EXPORT HRESULT OleInitialize(LPVOID reserved)
{
    (void)reserved;
    g_initialize_count++;
    return g_initialize_count == 1 ? S_OK : S_FALSE;
}

WF_EXPORT void OleUninitialize(void)
{
    if (g_initialize_count > 0)
    {
        g_initialize_count--;
    }
}

WF_EXPORT HRESULT CoInitialize(LPVOID reserved)
{
    return OleInitialize(reserved);
}

WF_EXPORT HRESULT CoInitializeEx(LPVOID reserved, DWORD coInit)
{
    (void)coInit;
    return OleInitialize(reserved);
}

WF_EXPORT void CoUninitialize(void)
{
    OleUninitialize();
}

WF_EXPORT HRESULT CoCreateInstance(const GUID* rclsid, IUnknown* outer, DWORD clsContext, const GUID* riid, void** object)
{
    (void)rclsid;
    (void)outer;
    (void)clsContext;
    (void)riid;
    if (object != 0)
    {
        *object = 0;
    }

    return REGDB_E_CLASSNOTREG;
}

WF_EXPORT HRESULT CoGetClassObject(const GUID* rclsid, DWORD clsContext, LPVOID reserved, const GUID* riid, void** object)
{
    (void)rclsid;
    (void)clsContext;
    (void)reserved;
    (void)riid;
    if (object != 0)
    {
        *object = 0;
    }

    return REGDB_E_CLASSNOTREG;
}

WF_EXPORT HRESULT OleSetClipboard(IDataObject* dataObject)
{
    g_clipboard = dataObject;
    return S_OK;
}

WF_EXPORT HRESULT OleGetClipboard(IDataObject** dataObject)
{
    if (dataObject == 0)
    {
        return E_INVALIDARG;
    }

    *dataObject = g_clipboard;
    return g_clipboard == 0 ? CLIPBRD_E_BAD_DATA : S_OK;
}

WF_EXPORT HRESULT OleFlushClipboard(void)
{
    return S_OK;
}

WF_EXPORT HRESULT RegisterDragDrop(HWND hwnd, IDropTarget* dropTarget)
{
    if (hwnd == 0)
    {
        return DRAGDROP_E_INVALIDHWND;
    }

    if (find_registration(hwnd) != 0)
    {
        return DRAGDROP_E_ALREADYREGISTERED;
    }

    for (int i = 0; i < 128; i++)
    {
        if (g_drop_registrations[i].used == 0)
        {
            g_drop_registrations[i].used = 1;
            g_drop_registrations[i].hwnd = hwnd;
            g_drop_registrations[i].target = dropTarget;
            return S_OK;
        }
    }

    return E_INVALIDARG;
}

WF_EXPORT HRESULT RevokeDragDrop(HWND hwnd)
{
    DropRegistration* registration = find_registration(hwnd);
    if (registration == 0)
    {
        return DRAGDROP_E_NOTREGISTERED;
    }

    registration->used = 0;
    registration->hwnd = 0;
    registration->target = 0;
    return S_OK;
}

WF_EXPORT HRESULT DoDragDrop(IDataObject* dataObject, IDropSource* dropSource, DWORD allowedEffects, DWORD* effect)
{
    (void)dataObject;
    (void)dropSource;
    (void)allowedEffects;
    if (effect != 0)
    {
        *effect = 0;
    }

    return DRAGDROP_S_CANCEL;
}
