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
typedef uintptr_t UINT_PTR;
typedef uint32_t UINT;
typedef uint16_t WCHAR;
typedef void* HWND;
typedef void* HINSTANCE;

#define S_OK ((HRESULT)0)
#define E_POINTER ((HRESULT)0x80004003u)
#define E_NOTIMPL ((HRESULT)0x80004001u)

WF_EXPORT BOOL Shell_NotifyIconW(uint32_t message, void* data)
{
    (void)message;
    (void)data;
    return 1;
}

WF_EXPORT BOOL Shell_NotifyIconA(uint32_t message, void* data)
{
    return Shell_NotifyIconW(message, data);
}

WF_EXPORT void DragAcceptFiles(HWND hwnd, BOOL accept)
{
    (void)hwnd;
    (void)accept;
}

WF_EXPORT UINT DragQueryFileW(void* hdrop, UINT file, WCHAR* buffer, UINT count)
{
    (void)hdrop;
    (void)file;
    if (buffer != 0 && count > 0)
    {
        buffer[0] = 0;
    }

    return 0;
}

WF_EXPORT UINT DragQueryFileA(void* hdrop, UINT file, char* buffer, UINT count)
{
    (void)hdrop;
    (void)file;
    if (buffer != 0 && count > 0)
    {
        buffer[0] = '\0';
    }

    return 0;
}

WF_EXPORT void* SHBrowseForFolderW(void* browseInfo)
{
    (void)browseInfo;
    return 0;
}

WF_EXPORT void* SHBrowseForFolderA(void* browseInfo)
{
    return SHBrowseForFolderW(browseInfo);
}

WF_EXPORT BOOL SHGetPathFromIDListEx(void* pidl, WCHAR* path, uint32_t pathCount, uint32_t flags)
{
    (void)pidl;
    (void)flags;
    if (path != 0 && pathCount > 0)
    {
        path[0] = 0;
    }

    return 0;
}

WF_EXPORT BOOL SHGetPathFromIDListW(void* pidl, WCHAR* path)
{
    return SHGetPathFromIDListEx(pidl, path, path == 0 ? 0 : 260, 0);
}

WF_EXPORT BOOL SHGetPathFromIDListA(void* pidl, char* path)
{
    (void)pidl;
    if (path != 0)
    {
        path[0] = '\0';
    }

    return 0;
}

WF_EXPORT HRESULT SHCreateShellItem(void* pidlParent, void* shellFolder, void* pidl, void** shellItem)
{
    (void)pidlParent;
    (void)shellFolder;
    (void)pidl;
    if (shellItem == 0)
    {
        return E_POINTER;
    }

    *shellItem = 0;
    return E_NOTIMPL;
}

WF_EXPORT HINSTANCE ShellExecuteW(HWND hwnd, const WCHAR* operation, const WCHAR* file, const WCHAR* parameters, const WCHAR* directory, int32_t showCommand)
{
    (void)hwnd;
    (void)operation;
    (void)file;
    (void)parameters;
    (void)directory;
    (void)showCommand;
    return (HINSTANCE)(UINT_PTR)42;
}

WF_EXPORT HINSTANCE ShellExecuteA(HWND hwnd, const char* operation, const char* file, const char* parameters, const char* directory, int32_t showCommand)
{
    (void)operation;
    (void)file;
    (void)parameters;
    (void)directory;
    return ShellExecuteW(hwnd, 0, 0, 0, 0, showCommand);
}

WF_EXPORT HINSTANCE FindExecutableW(const WCHAR* file, const WCHAR* directory, WCHAR* result)
{
    (void)file;
    (void)directory;
    if (result != 0)
    {
        result[0] = 0;
    }

    return (HINSTANCE)(UINT_PTR)31;
}

WF_EXPORT HINSTANCE FindExecutableA(const char* file, const char* directory, char* result)
{
    (void)file;
    (void)directory;
    if (result != 0)
    {
        result[0] = '\0';
    }

    return (HINSTANCE)(UINT_PTR)31;
}
