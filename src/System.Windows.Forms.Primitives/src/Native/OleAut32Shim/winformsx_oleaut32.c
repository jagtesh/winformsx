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

typedef int32_t HRESULT;
typedef uint16_t WCHAR;
typedef uint16_t VARTYPE;
typedef uint32_t UINT;
typedef uint32_t ULONG;
typedef void* BSTR;
typedef void* SAFEARRAY;

#define S_OK ((HRESULT)0)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define TYPE_E_CANTLOADLIBRARY ((HRESULT)0x80029C4Au)
#define VT_EMPTY ((VARTYPE)0)
#define VT_BSTR ((VARTYPE)8)

typedef struct GUID
{
    uint32_t Data1;
    uint16_t Data2;
    uint16_t Data3;
    uint8_t Data4[8];
} GUID;

typedef struct SAFEARRAYBOUND
{
    ULONG cElements;
    int32_t lLbound;
} SAFEARRAYBOUND;

static UINT string_length(const WCHAR* value)
{
    if (value == 0)
    {
        return 0;
    }

    UINT length = 0;
    while (value[length] != 0)
    {
        length++;
    }

    return length;
}

WF_EXPORT BSTR SysAllocStringLen(const WCHAR* value, UINT length)
{
    uint32_t byte_length = length * sizeof(WCHAR);
    uint8_t* allocation = (uint8_t*)malloc(sizeof(uint32_t) + byte_length + sizeof(WCHAR));
    if (allocation == 0)
    {
        return 0;
    }

    *((uint32_t*)allocation) = byte_length;
    WCHAR* text = (WCHAR*)(allocation + sizeof(uint32_t));
    if (value != 0 && length > 0)
    {
        memcpy(text, value, byte_length);
    }
    else if (length > 0)
    {
        memset(text, 0, byte_length);
    }

    text[length] = 0;
    return text;
}

WF_EXPORT BSTR SysAllocString(const WCHAR* value)
{
    return SysAllocStringLen(value, string_length(value));
}

WF_EXPORT void SysFreeString(BSTR value)
{
    if (value == 0)
    {
        return;
    }

    uint8_t* allocation = ((uint8_t*)value) - sizeof(uint32_t);
    free(allocation);
}

WF_EXPORT UINT SysStringLen(BSTR value)
{
    if (value == 0)
    {
        return 0;
    }

    uint8_t* allocation = ((uint8_t*)value) - sizeof(uint32_t);
    return *((uint32_t*)allocation) / sizeof(WCHAR);
}

WF_EXPORT UINT SysStringByteLen(BSTR value)
{
    if (value == 0)
    {
        return 0;
    }

    uint8_t* allocation = ((uint8_t*)value) - sizeof(uint32_t);
    return *((uint32_t*)allocation);
}

WF_EXPORT HRESULT VariantClear(void* variant)
{
    if (variant == 0)
    {
        return E_INVALIDARG;
    }

    VARTYPE* vt = (VARTYPE*)variant;
    if (*vt == VT_BSTR)
    {
        BSTR* value = (BSTR*)((uint8_t*)variant + 8);
        SysFreeString(*value);
    }

    memset(variant, 0, 24);
    *vt = VT_EMPTY;
    return S_OK;
}

WF_EXPORT HRESULT PropVariantClear(void* variant)
{
    return VariantClear(variant);
}

WF_EXPORT HRESULT LoadRegTypeLib(const GUID* libId, uint16_t majorVersion, uint16_t minorVersion, uint32_t lcid, void** typeLib)
{
    (void)libId;
    (void)majorVersion;
    (void)minorVersion;
    (void)lcid;
    if (typeLib != 0)
    {
        *typeLib = 0;
    }

    return TYPE_E_CANTLOADLIBRARY;
}

WF_EXPORT SAFEARRAY* SafeArrayCreate(VARTYPE vt, UINT dimensions, SAFEARRAYBOUND* bounds)
{
    (void)vt;
    (void)dimensions;
    (void)bounds;
    return 0;
}

WF_EXPORT SAFEARRAY* SafeArrayCreateEx(VARTYPE vt, UINT dimensions, SAFEARRAYBOUND* bounds, void* extra)
{
    (void)extra;
    return SafeArrayCreate(vt, dimensions, bounds);
}

WF_EXPORT HRESULT SafeArrayDestroy(SAFEARRAY* array)
{
    return array == 0 ? S_OK : E_INVALIDARG;
}
