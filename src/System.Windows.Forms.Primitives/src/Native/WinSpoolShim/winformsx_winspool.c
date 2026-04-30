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
typedef uint32_t DWORD;
typedef uint16_t WORD;

typedef struct WinFormsXWinSpoolDispatch
{
    uint32_t version;
    uint32_t size;
    BOOL (*enum_printers)(DWORD flags, const void* name, DWORD level, void* printer_enum, DWORD buffer_size, DWORD* needed, DWORD* returned);
    int32_t (*device_capabilities)(const void* device, const void* port, DWORD capability, void* output, const void* devmode);
    int32_t (*document_properties)(void* hwnd, void* printer, void* device_name, void* devmode_output, void* devmode_input, DWORD mode);
} WinFormsXWinSpoolDispatch;

static WinFormsXWinSpoolDispatch g_dispatch;

WF_EXPORT BOOL WinFormsXWinSpoolRegisterDispatch(const WinFormsXWinSpoolDispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXWinSpoolDispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT BOOL EnumPrintersW(DWORD flags, const void* name, DWORD level, void* printer_enum, DWORD buffer_size, DWORD* needed, DWORD* returned)
{
    if (g_dispatch.enum_printers != 0)
    {
        return g_dispatch.enum_printers(flags, name, level, printer_enum, buffer_size, needed, returned);
    }

    if (needed != 0)
    {
        *needed = 0;
    }

    if (returned != 0)
    {
        *returned = 0;
    }

    return 1;
}

WF_EXPORT BOOL EnumPrintersA(DWORD flags, const void* name, DWORD level, void* printer_enum, DWORD buffer_size, DWORD* needed, DWORD* returned)
{
    return EnumPrintersW(flags, name, level, printer_enum, buffer_size, needed, returned);
}

WF_EXPORT BOOL EnumPrinters(DWORD flags, const void* name, DWORD level, void* printer_enum, DWORD buffer_size, DWORD* needed, DWORD* returned)
{
    return EnumPrintersW(flags, name, level, printer_enum, buffer_size, needed, returned);
}

WF_EXPORT int32_t DeviceCapabilitiesW(const void* device, const void* port, WORD capability, void* output, const void* devmode)
{
    if (g_dispatch.device_capabilities != 0)
    {
        return g_dispatch.device_capabilities(device, port, capability, output, devmode);
    }

    switch (capability)
    {
        case 18: // DC_COPIES
        case 32: // DC_COLORDEVICE
            return 1;
        case 7: // DC_DUPLEX
        case 17: // DC_ORIENTATION
            return 0;
        default:
            return -1;
    }
}

WF_EXPORT int32_t DeviceCapabilitiesA(const void* device, const void* port, WORD capability, void* output, const void* devmode)
{
    return DeviceCapabilitiesW(device, port, capability, output, devmode);
}

WF_EXPORT int32_t DeviceCapabilities(const void* device, const void* port, WORD capability, void* output, const void* devmode)
{
    return DeviceCapabilitiesW(device, port, capability, output, devmode);
}

WF_EXPORT int32_t DocumentPropertiesW(void* hwnd, void* printer, void* device_name, void* devmode_output, void* devmode_input, DWORD mode)
{
    if (g_dispatch.document_properties != 0)
    {
        return g_dispatch.document_properties(hwnd, printer, device_name, devmode_output, devmode_input, mode);
    }

    return devmode_output == 0 ? 220 : 1;
}

WF_EXPORT int32_t DocumentPropertiesA(void* hwnd, void* printer, void* device_name, void* devmode_output, void* devmode_input, DWORD mode)
{
    return DocumentPropertiesW(hwnd, printer, device_name, devmode_output, devmode_input, mode);
}

WF_EXPORT int32_t DocumentProperties(void* hwnd, void* printer, void* device_name, void* devmode_output, void* devmode_input, DWORD mode)
{
    return DocumentPropertiesW(hwnd, printer, device_name, devmode_output, devmode_input, mode);
}
