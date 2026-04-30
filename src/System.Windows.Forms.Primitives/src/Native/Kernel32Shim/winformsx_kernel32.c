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
typedef uint32_t DWORD;
typedef uint16_t WCHAR;

typedef struct WinFormsXKernel32Dispatch
{
    uint32_t version;
    uint32_t size;
    void* (*get_current_process)(void);
    DWORD (*get_current_process_id)(void);
    DWORD (*get_current_thread_id)(void);
    void* (*get_module_handle)(const WCHAR* module_name);
    DWORD (*get_module_file_name)(void* module, WCHAR* filename, DWORD size);
    DWORD (*get_last_error)(void);
    void (*set_last_error)(DWORD error);
} WinFormsXKernel32Dispatch;

static WinFormsXKernel32Dispatch g_dispatch;
static DWORD g_last_error;

static DWORD copy_ascii_path(char* buffer, DWORD size)
{
    const char* fallback = "dotnet";
    size_t length = strlen(fallback);
    if (buffer == 0 || size == 0)
    {
        return 0;
    }

    size_t copy_length = length < size ? length : size;
    memcpy(buffer, fallback, copy_length);
    if (copy_length < size)
    {
        buffer[copy_length] = '\0';
    }

    return (DWORD)copy_length;
}

WF_EXPORT BOOL WinFormsXKernel32RegisterDispatch(const WinFormsXKernel32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXKernel32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT void* GetCurrentProcess(void)
{
    if (g_dispatch.get_current_process != 0)
    {
        return g_dispatch.get_current_process();
    }

    return (void*)(intptr_t)-1;
}

WF_EXPORT DWORD GetCurrentProcessId(void)
{
    if (g_dispatch.get_current_process_id != 0)
    {
        return g_dispatch.get_current_process_id();
    }

    return 1;
}

WF_EXPORT DWORD GetCurrentThreadId(void)
{
    if (g_dispatch.get_current_thread_id != 0)
    {
        return g_dispatch.get_current_thread_id();
    }

    return 1;
}

WF_EXPORT void* GetModuleHandleW(const WCHAR* module_name)
{
    if (g_dispatch.get_module_handle != 0)
    {
        return g_dispatch.get_module_handle(module_name);
    }

    return (void*)(intptr_t)0x400000;
}

WF_EXPORT void* GetModuleHandleA(const char* module_name)
{
    (void)module_name;
    return GetModuleHandleW(0);
}

WF_EXPORT void* GetModuleHandle(const WCHAR* module_name)
{
    return GetModuleHandleW(module_name);
}

WF_EXPORT DWORD GetModuleFileNameW(void* module, WCHAR* filename, DWORD size)
{
    if (g_dispatch.get_module_file_name != 0)
    {
        return g_dispatch.get_module_file_name(module, filename, size);
    }

    if (filename == 0 || size == 0)
    {
        return 0;
    }

    const WCHAR fallback[] = { 'd', 'o', 't', 'n', 'e', 't', 0 };
    DWORD length = 6;
    DWORD copy_length = length < size ? length : size;
    for (DWORD i = 0; i < copy_length; i++)
    {
        filename[i] = fallback[i];
    }

    if (copy_length < size)
    {
        filename[copy_length] = 0;
    }

    return copy_length;
}

WF_EXPORT DWORD GetModuleFileNameA(void* module, char* filename, DWORD size)
{
    (void)module;
    return copy_ascii_path(filename, size);
}

WF_EXPORT DWORD GetModuleFileName(void* module, WCHAR* filename, DWORD size)
{
    return GetModuleFileNameW(module, filename, size);
}

WF_EXPORT DWORD GetLastError(void)
{
    if (g_dispatch.get_last_error != 0)
    {
        return g_dispatch.get_last_error();
    }

    return g_last_error;
}

WF_EXPORT void SetLastError(DWORD error)
{
    g_last_error = error;
    if (g_dispatch.set_last_error != 0)
    {
        g_dispatch.set_last_error(error);
    }
}
