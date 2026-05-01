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
typedef void* HWND;
typedef void* LPVOID;
typedef void* HGLOBAL;
typedef void* IDataObject;
typedef void* IDropSource;
typedef void* IDropTarget;
typedef void* ILockBytes;
typedef void* IStream;
typedef void* IUnknown;
typedef void* IMalloc;
typedef struct IOleAdviseHolder IOleAdviseHolder;
typedef struct IDataAdviseHolder IDataAdviseHolder;

#define S_OK ((HRESULT)0)
#define S_FALSE ((HRESULT)1)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define E_OUTOFMEMORY ((HRESULT)0x8007000Eu)
#define E_NOINTERFACE ((HRESULT)0x80004002u)
#define E_NOTIMPL ((HRESULT)0x80004001u)
#define REGDB_E_CLASSNOTREG ((HRESULT)0x80040154u)
#define CLIPBRD_E_BAD_DATA ((HRESULT)0x800401D3u)
#define DRAGDROP_S_CANCEL ((HRESULT)0x00040101u)
#define DRAGDROP_E_NOTREGISTERED ((HRESULT)0x80040100u)
#define DRAGDROP_E_ALREADYREGISTERED ((HRESULT)0x80040101u)
#define DRAGDROP_E_INVALIDHWND ((HRESULT)0x80040102u)
#define TYMED_HGLOBAL ((DWORD)1)
#define TYMED_ISTREAM ((DWORD)4)

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

typedef struct LockBytesState
{
    HGLOBAL hglobal;
    BOOL delete_on_release;
    int used;
} LockBytesState;

typedef struct StreamState
{
    HGLOBAL hglobal;
    BOOL delete_on_release;
    int used;
} StreamState;

typedef struct AdviseHolderState
{
    int used;
} AdviseHolderState;

typedef struct STGMEDIUM
{
    DWORD tymed;
    LPVOID storage;
    IUnknown* unknown_for_release;
} STGMEDIUM;

static DWORD g_initialize_count;
static IDataObject* g_clipboard;
static IUnknown* g_message_filter;
static DropRegistration g_drop_registrations[128];
static LockBytesState g_lockbytes_states[64];
static StreamState g_stream_states[64];
static AdviseHolderState g_ole_advise_holders[64];
static AdviseHolderState g_data_advise_holders[64];
static uintptr_t g_next_synthetic_hglobal = (uintptr_t)0x10000;
static uint32_t g_next_guid_counter = 1;

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

static HGLOBAL ensure_hglobal(HGLOBAL hglobal)
{
    if (hglobal != 0)
    {
        return hglobal;
    }

    uintptr_t next = g_next_synthetic_hglobal;
    g_next_synthetic_hglobal += (uintptr_t)0x100;
    return (HGLOBAL)next;
}

static LockBytesState* allocate_lockbytes_state(void)
{
    for (int i = 0; i < 64; i++)
    {
        if (g_lockbytes_states[i].used == 0)
        {
            g_lockbytes_states[i].used = 1;
            return &g_lockbytes_states[i];
        }
    }

    return 0;
}

static StreamState* allocate_stream_state(void)
{
    for (int i = 0; i < 64; i++)
    {
        if (g_stream_states[i].used == 0)
        {
            g_stream_states[i].used = 1;
            return &g_stream_states[i];
        }
    }

    return 0;
}

static StreamState* find_stream_state(IStream* stream)
{
    for (int i = 0; i < 64; i++)
    {
        if (g_stream_states[i].used != 0 && stream == (IStream*)&g_stream_states[i])
        {
            return &g_stream_states[i];
        }
    }

    return 0;
}

static void* allocate_opaque_holder(AdviseHolderState* holders, int count)
{
    for (int i = 0; i < count; i++)
    {
        if (holders[i].used == 0)
        {
            holders[i].used = 1;
            return &holders[i];
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

WF_EXPORT void* CoTaskMemAlloc(uintptr_t size)
{
    return malloc(size);
}

WF_EXPORT void* CoTaskMemRealloc(void* block, uintptr_t size)
{
    return realloc(block, size);
}

WF_EXPORT void CoTaskMemFree(void* block)
{
    free(block);
}

WF_EXPORT HRESULT CoGetMalloc(DWORD memContext, IMalloc** mallocObject)
{
    (void)memContext;
    if (mallocObject == 0)
    {
        return E_INVALIDARG;
    }

    *mallocObject = 0;
    return E_NOTIMPL;
}

WF_EXPORT HRESULT CoCreateGuid(GUID* guid)
{
    if (guid == 0)
    {
        return E_INVALIDARG;
    }

    uint32_t counter = g_next_guid_counter++;
    guid->Data1 = 0x57465800u | (counter & 0xFFu);
    guid->Data2 = 0x4F4Cu;
    guid->Data3 = 0x4532u;
    guid->Data4[0] = 0x80u;
    guid->Data4[1] = 0x00u;
    guid->Data4[2] = (uint8_t)((counter >> 24) & 0xFFu);
    guid->Data4[3] = (uint8_t)((counter >> 16) & 0xFFu);
    guid->Data4[4] = (uint8_t)((counter >> 8) & 0xFFu);
    guid->Data4[5] = (uint8_t)(counter & 0xFFu);
    guid->Data4[6] = 0x33u;
    guid->Data4[7] = 0x32u;
    return S_OK;
}

WF_EXPORT HRESULT CoRegisterMessageFilter(IUnknown* messageFilter, IUnknown** previousMessageFilter)
{
    if (previousMessageFilter != 0)
    {
        *previousMessageFilter = g_message_filter;
    }

    g_message_filter = messageFilter;
    return S_OK;
}

WF_EXPORT HRESULT CreateILockBytesOnHGlobal(HGLOBAL hGlobal, BOOL fDeleteOnRelease, ILockBytes** lockBytes)
{
    if (lockBytes == 0)
    {
        return E_INVALIDARG;
    }

    LockBytesState* state = allocate_lockbytes_state();
    if (state == 0)
    {
        *lockBytes = 0;
        return E_OUTOFMEMORY;
    }

    state->hglobal = ensure_hglobal(hGlobal);
    state->delete_on_release = fDeleteOnRelease;
    *lockBytes = (ILockBytes*)state;
    return S_OK;
}

WF_EXPORT HRESULT CreateOleAdviseHolder(IOleAdviseHolder** adviseHolder)
{
    if (adviseHolder == 0)
    {
        return E_INVALIDARG;
    }

    void* holder = allocate_opaque_holder(g_ole_advise_holders, 64);
    if (holder == 0)
    {
        *adviseHolder = 0;
        return E_OUTOFMEMORY;
    }

    *adviseHolder = (IOleAdviseHolder*)holder;
    return S_OK;
}

WF_EXPORT HRESULT CreateDataAdviseHolder(IDataAdviseHolder** adviseHolder)
{
    if (adviseHolder == 0)
    {
        return E_INVALIDARG;
    }

    void* holder = allocate_opaque_holder(g_data_advise_holders, 64);
    if (holder == 0)
    {
        *adviseHolder = 0;
        return E_OUTOFMEMORY;
    }

    *adviseHolder = (IDataAdviseHolder*)holder;
    return S_OK;
}

WF_EXPORT HRESULT CreateStreamOnHGlobal(HGLOBAL hGlobal, BOOL fDeleteOnRelease, IStream** stream)
{
    if (stream == 0)
    {
        return E_INVALIDARG;
    }

    StreamState* state = allocate_stream_state();
    if (state == 0)
    {
        *stream = 0;
        return E_OUTOFMEMORY;
    }

    state->hglobal = ensure_hglobal(hGlobal);
    state->delete_on_release = fDeleteOnRelease;
    *stream = (IStream*)state;
    return S_OK;
}

WF_EXPORT HRESULT GetHGlobalFromStream(IStream* stream, HGLOBAL* hGlobal)
{
    if (hGlobal == 0)
    {
        return E_INVALIDARG;
    }

    StreamState* state = find_stream_state(stream);
    if (state == 0)
    {
        *hGlobal = 0;
        return E_INVALIDARG;
    }

    *hGlobal = state->hglobal;
    return S_OK;
}

WF_EXPORT void ReleaseStgMedium(STGMEDIUM* medium)
{
    if (medium == 0)
    {
        return;
    }

    if (medium->tymed == TYMED_ISTREAM)
    {
        StreamState* state = find_stream_state((IStream*)medium->storage);
        if (state != 0)
        {
            state->used = 0;
            state->hglobal = 0;
            state->delete_on_release = 0;
        }
    }

    medium->tymed = 0;
    medium->storage = 0;
    medium->unknown_for_release = 0;
}

WF_EXPORT HRESULT OleCreatePictureIndirect(const void* pictureDescription, const GUID* iid, BOOL ownsHandle, void** pictureObject)
{
    (void)ownsHandle;
    if (pictureObject != 0)
    {
        *pictureObject = 0;
    }

    if (pictureDescription == 0 || iid == 0 || pictureObject == 0)
    {
        return E_INVALIDARG;
    }

    return E_NOINTERFACE;
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

WF_EXPORT HRESULT OleIsCurrentClipboard(IDataObject* dataObject)
{
    return dataObject == g_clipboard ? S_OK : S_FALSE;
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
