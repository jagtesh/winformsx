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
typedef int32_t LONG;
typedef void* BSTR;
typedef void* SAFEARRAY;

#define S_OK ((HRESULT)0)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define TYPE_E_CANTLOADLIBRARY ((HRESULT)0x80029C4Au)
#define VT_EMPTY ((VARTYPE)0)
#define VT_I4 ((VARTYPE)3)
#define VT_BSTR ((VARTYPE)8)
#define VT_UI1 ((VARTYPE)17)
#define FADF_WINFORMSX 0x5846u

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
    LONG lLbound;
} SAFEARRAYBOUND;

typedef struct WinFormsXSafeArray
{
    uint16_t cDims;
    uint16_t fFeatures;
    ULONG cbElements;
    ULONG cLocks;
    void* pvData;
    VARTYPE vt;
    SAFEARRAYBOUND bounds[1];
} WinFormsXSafeArray;

static ULONG element_size(VARTYPE vt)
{
    switch (vt)
    {
        case VT_UI1:
            return 1;
        case VT_I4:
            return 4;
        case VT_BSTR:
            return (ULONG)sizeof(BSTR);
        default:
            return (ULONG)sizeof(intptr_t);
    }
}

static ULONG element_count(UINT dimensions, const SAFEARRAYBOUND* bounds)
{
    ULONG count = 1;
    for (UINT i = 0; i < dimensions; i++)
    {
        if (bounds[i].cElements == 0)
        {
            return 0;
        }

        count *= bounds[i].cElements;
    }

    return count;
}

static WinFormsXSafeArray* as_safe_array(SAFEARRAY* array)
{
    WinFormsXSafeArray* value = (WinFormsXSafeArray*)array;
    if (value == 0 || value->fFeatures != FADF_WINFORMSX)
    {
        return 0;
    }

    return value;
}

static int array_offset(const WinFormsXSafeArray* array, const LONG* indices, ULONG* offset)
{
    if (array == 0 || indices == 0 || offset == 0)
    {
        return 0;
    }

    ULONG stride = 1;
    ULONG result = 0;
    for (UINT i = 0; i < array->cDims; i++)
    {
        LONG index = indices[i];
        LONG lower = array->bounds[i].lLbound;
        LONG upper = lower + (LONG)array->bounds[i].cElements - 1;
        if (index < lower || index > upper)
        {
            return 0;
        }

        result += (ULONG)(index - lower) * stride;
        stride *= array->bounds[i].cElements;
    }

    *offset = result;
    return 1;
}

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
    if (dimensions == 0 || bounds == 0)
    {
        return 0;
    }

    ULONG count = element_count(dimensions, bounds);
    if (count == 0)
    {
        return 0;
    }

    size_t header_size = sizeof(WinFormsXSafeArray) + (dimensions - 1) * sizeof(SAFEARRAYBOUND);
    WinFormsXSafeArray* array = (WinFormsXSafeArray*)calloc(1, header_size);
    if (array == 0)
    {
        return 0;
    }

    array->cDims = (uint16_t)dimensions;
    array->fFeatures = FADF_WINFORMSX;
    array->cbElements = element_size(vt);
    array->vt = vt;
    memcpy(array->bounds, bounds, dimensions * sizeof(SAFEARRAYBOUND));

    array->pvData = calloc(count, array->cbElements);
    if (array->pvData == 0)
    {
        free(array);
        return 0;
    }

    return (SAFEARRAY*)array;
}

WF_EXPORT SAFEARRAY* SafeArrayCreateEx(VARTYPE vt, UINT dimensions, SAFEARRAYBOUND* bounds, void* extra)
{
    (void)extra;
    return SafeArrayCreate(vt, dimensions, bounds);
}

WF_EXPORT HRESULT SafeArrayDestroy(SAFEARRAY* array)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    if (array == 0)
    {
        return S_OK;
    }

    if (value == 0)
    {
        return E_INVALIDARG;
    }

    free(value->pvData);
    value->fFeatures = 0;
    free(value);
    return S_OK;
}

WF_EXPORT UINT SafeArrayGetDim(SAFEARRAY* array)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    return value == 0 ? 0 : value->cDims;
}

WF_EXPORT HRESULT SafeArrayGetLBound(SAFEARRAY* array, UINT dimension, LONG* lowerBound)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    if (value == 0 || lowerBound == 0 || dimension == 0 || dimension > value->cDims)
    {
        return E_INVALIDARG;
    }

    *lowerBound = value->bounds[dimension - 1].lLbound;
    return S_OK;
}

WF_EXPORT HRESULT SafeArrayGetUBound(SAFEARRAY* array, UINT dimension, LONG* upperBound)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    if (value == 0 || upperBound == 0 || dimension == 0 || dimension > value->cDims)
    {
        return E_INVALIDARG;
    }

    SAFEARRAYBOUND bound = value->bounds[dimension - 1];
    *upperBound = bound.lLbound + (LONG)bound.cElements - 1;
    return S_OK;
}

WF_EXPORT HRESULT SafeArrayAccessData(SAFEARRAY* array, void** data)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    if (value == 0 || data == 0)
    {
        return E_INVALIDARG;
    }

    value->cLocks++;
    *data = value->pvData;
    return S_OK;
}

WF_EXPORT HRESULT SafeArrayUnaccessData(SAFEARRAY* array)
{
    WinFormsXSafeArray* value = as_safe_array(array);
    if (value == 0)
    {
        return E_INVALIDARG;
    }

    if (value->cLocks > 0)
    {
        value->cLocks--;
    }

    return S_OK;
}

WF_EXPORT HRESULT SafeArrayPutElement(SAFEARRAY* array, LONG* indices, void* value)
{
    WinFormsXSafeArray* safe_array = as_safe_array(array);
    ULONG offset = 0;
    if (safe_array == 0 || value == 0 || !array_offset(safe_array, indices, &offset))
    {
        return E_INVALIDARG;
    }

    memcpy((uint8_t*)safe_array->pvData + offset * safe_array->cbElements, value, safe_array->cbElements);
    return S_OK;
}

WF_EXPORT HRESULT SafeArrayGetElement(SAFEARRAY* array, LONG* indices, void* value)
{
    WinFormsXSafeArray* safe_array = as_safe_array(array);
    ULONG offset = 0;
    if (safe_array == 0 || value == 0 || !array_offset(safe_array, indices, &offset))
    {
        return E_INVALIDARG;
    }

    memcpy(value, (uint8_t*)safe_array->pvData + offset * safe_array->cbElements, safe_array->cbElements);
    return S_OK;
}
