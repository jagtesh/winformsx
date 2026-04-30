// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <string.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t HRESULT;
typedef uint32_t DWORD;

#define S_OK ((HRESULT)0)
#define E_INVALIDARG ((HRESULT)0x80070057u)

typedef struct DwmAttributeEntry
{
    void* hwnd;
    DWORD attribute;
    uint8_t value[64];
    DWORD size;
    int used;
} DwmAttributeEntry;

static DwmAttributeEntry g_attributes[128];

static DwmAttributeEntry* find_entry(void* hwnd, DWORD attribute)
{
    for (int i = 0; i < 128; i++)
    {
        if (g_attributes[i].used != 0 && g_attributes[i].hwnd == hwnd && g_attributes[i].attribute == attribute)
        {
            return &g_attributes[i];
        }
    }

    return 0;
}

static DwmAttributeEntry* get_or_create_entry(void* hwnd, DWORD attribute)
{
    DwmAttributeEntry* existing = find_entry(hwnd, attribute);
    if (existing != 0)
    {
        return existing;
    }

    for (int i = 0; i < 128; i++)
    {
        if (g_attributes[i].used == 0)
        {
            g_attributes[i].used = 1;
            g_attributes[i].hwnd = hwnd;
            g_attributes[i].attribute = attribute;
            g_attributes[i].size = 0;
            memset(g_attributes[i].value, 0, sizeof(g_attributes[i].value));
            return &g_attributes[i];
        }
    }

    return 0;
}

WF_EXPORT HRESULT DwmSetWindowAttribute(void* hwnd, DWORD attribute, const void* value, DWORD size)
{
    if (value == 0 || size == 0 || size > sizeof(g_attributes[0].value))
    {
        return E_INVALIDARG;
    }

    DwmAttributeEntry* entry = get_or_create_entry(hwnd, attribute);
    if (entry == 0)
    {
        return E_INVALIDARG;
    }

    memcpy(entry->value, value, size);
    entry->size = size;
    return S_OK;
}

WF_EXPORT HRESULT DwmGetWindowAttribute(void* hwnd, DWORD attribute, void* value, DWORD size)
{
    if (value == 0 || size == 0)
    {
        return E_INVALIDARG;
    }

    memset(value, 0, size);

    DwmAttributeEntry* entry = find_entry(hwnd, attribute);
    if (entry == 0)
    {
        return S_OK;
    }

    DWORD copy_size = entry->size < size ? entry->size : size;
    memcpy(value, entry->value, copy_size);
    return S_OK;
}
