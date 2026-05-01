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
typedef uintptr_t UINT_PTR;
typedef uintptr_t DWORD_PTR;
typedef uint32_t UINT;
typedef uint32_t DWORD;
typedef uint16_t WCHAR;
typedef uint16_t WORD;
typedef void* HWND;
typedef void* HINSTANCE;
typedef void* HICON;

#define S_OK ((HRESULT)0)
#define E_POINTER ((HRESULT)0x80004003u)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define E_NOTIMPL ((HRESULT)0x80004001u)
#define MAX_PATH 260u
#define SHGSI_ICON 0x00000100u
#define SHGSI_SMALLICON 0x00000001u
#define SHGFI_ICON 0x000000100u

typedef struct WinFormsXStockIconInfo
{
    DWORD cbSize;
    HICON hIcon;
    INT iSysImageIndex;
    INT iIcon;
    WCHAR szPath[MAX_PATH];
} WinFormsXStockIconInfo;

typedef struct WinFormsXShellFileInfoW
{
    HICON hIcon;
    INT iIcon;
    DWORD dwAttributes;
    WCHAR szDisplayName[MAX_PATH];
    WCHAR szTypeName[80];
} WinFormsXShellFileInfoW;

typedef struct WinFormsXShellFileInfoA
{
    HICON hIcon;
    INT iIcon;
    DWORD dwAttributes;
    char szDisplayName[MAX_PATH];
    char szTypeName[80];
} WinFormsXShellFileInfoA;

static void copy_ascii_to_wchar(WCHAR* destination, UINT destination_count, const char* source)
{
    UINT index = 0;
    if (destination == 0 || destination_count == 0)
    {
        return;
    }

    if (source != 0)
    {
        while (index + 1 < destination_count && source[index] != '\0')
        {
            destination[index] = (WCHAR)(uint8_t)source[index];
            index++;
        }
    }

    destination[index] = 0;
}

static void copy_ascii_to_ansi(char* destination, UINT destination_count, const char* source)
{
    UINT index = 0;
    if (destination == 0 || destination_count == 0)
    {
        return;
    }

    if (source != 0)
    {
        while (index + 1 < destination_count && source[index] != '\0')
        {
            destination[index] = source[index];
            index++;
        }
    }

    destination[index] = '\0';
}

static HICON create_deterministic_icon_handle(INT seed, UINT slot, BOOL small_icon)
{
    UINT_PTR value = 0x530000u;
    value += (UINT_PTR)((seed & 0x7FFF) * 32);
    value += (UINT_PTR)(slot * 4u);
    value += small_icon ? 1u : 2u;
    return (HICON)value;
}

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

WF_EXPORT HRESULT SHGetStockIconInfo(INT stockIconId, UINT flags, WinFormsXStockIconInfo* stockIconInfo)
{
    if (stockIconInfo == 0)
    {
        return E_POINTER;
    }

    if (stockIconInfo->cbSize < sizeof(WinFormsXStockIconInfo))
    {
        return E_INVALIDARG;
    }

    stockIconInfo->hIcon = 0;
    stockIconInfo->iSysImageIndex = stockIconId < 0 ? 0 : stockIconId;
    stockIconInfo->iIcon = stockIconId < 0 ? 0 : stockIconId;
    copy_ascii_to_wchar(stockIconInfo->szPath, MAX_PATH, "WinFormsX\\StockIcon\\default.ico");

    if ((flags & SHGSI_ICON) != 0)
    {
        stockIconInfo->hIcon = create_deterministic_icon_handle(
            stockIconInfo->iIcon,
            0,
            (flags & SHGSI_SMALLICON) != 0);
    }

    (void)flags;
    return S_OK;
}

WF_EXPORT HICON ExtractAssociatedIconW(HINSTANCE instance, WCHAR* iconPath, WORD* iconIndex)
{
    WORD resolved_index = iconIndex == 0 ? 0 : *iconIndex;
    (void)instance;

    if (iconPath != 0)
    {
        copy_ascii_to_wchar(iconPath, MAX_PATH, "WinFormsX.ico");
    }

    if (iconIndex != 0)
    {
        *iconIndex = resolved_index;
    }

    return create_deterministic_icon_handle((INT)resolved_index, 1u, 0);
}

WF_EXPORT HICON ExtractAssociatedIconA(HINSTANCE instance, char* iconPath, WORD* iconIndex)
{
    WORD resolved_index = iconIndex == 0 ? 0 : *iconIndex;
    (void)instance;

    if (iconPath != 0)
    {
        copy_ascii_to_ansi(iconPath, MAX_PATH, "WinFormsX.ico");
    }

    if (iconIndex != 0)
    {
        *iconIndex = resolved_index;
    }

    return create_deterministic_icon_handle((INT)resolved_index, 1u, 0);
}

WF_EXPORT UINT ExtractIconExW(const WCHAR* file, INT iconIndex, HICON* largeIcons, HICON* smallIcons, UINT iconCount)
{
    UINT extracted = 0;
    UINT index;
    (void)file;

    if (iconCount == 0)
    {
        return 0;
    }

    for (index = 0; index < iconCount; index++)
    {
        if (largeIcons != 0)
        {
            largeIcons[index] = create_deterministic_icon_handle(iconIndex, index, 0);
            extracted = index + 1;
        }

        if (smallIcons != 0)
        {
            smallIcons[index] = create_deterministic_icon_handle(iconIndex, index, 1);
            extracted = index + 1;
        }
    }

    if (largeIcons == 0 && smallIcons == 0)
    {
        return 1;
    }

    return extracted;
}

WF_EXPORT UINT ExtractIconExA(const char* file, INT iconIndex, HICON* largeIcons, HICON* smallIcons, UINT iconCount)
{
    (void)file;
    return ExtractIconExW(0, iconIndex, largeIcons, smallIcons, iconCount);
}

WF_EXPORT DWORD_PTR SHGetFileInfoW(const WCHAR* path, DWORD fileAttributes, WinFormsXShellFileInfoW* shellFileInfo, UINT cbFileInfo, UINT flags)
{
    if (shellFileInfo == 0 || cbFileInfo < sizeof(WinFormsXShellFileInfoW))
    {
        return 0;
    }

    shellFileInfo->hIcon = (flags & SHGFI_ICON) != 0 ? create_deterministic_icon_handle(0, 5u, 0) : 0;
    shellFileInfo->iIcon = 0;
    shellFileInfo->dwAttributes = fileAttributes;
    copy_ascii_to_wchar(shellFileInfo->szDisplayName, MAX_PATH, "WinFormsX File");
    copy_ascii_to_wchar(shellFileInfo->szTypeName, 80u, "WinFormsX Type");

    (void)path;
    return 1;
}

WF_EXPORT DWORD_PTR SHGetFileInfoA(const char* path, DWORD fileAttributes, WinFormsXShellFileInfoA* shellFileInfo, UINT cbFileInfo, UINT flags)
{
    if (shellFileInfo == 0 || cbFileInfo < sizeof(WinFormsXShellFileInfoA))
    {
        return 0;
    }

    shellFileInfo->hIcon = (flags & SHGFI_ICON) != 0 ? create_deterministic_icon_handle(0, 5u, 0) : 0;
    shellFileInfo->iIcon = 0;
    shellFileInfo->dwAttributes = fileAttributes;
    copy_ascii_to_ansi(shellFileInfo->szDisplayName, MAX_PATH, "WinFormsX File");
    copy_ascii_to_ansi(shellFileInfo->szTypeName, 80u, "WinFormsX Type");

    (void)path;
    return 1;
}
