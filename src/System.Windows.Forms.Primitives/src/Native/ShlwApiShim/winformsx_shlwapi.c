// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <string.h>

#if !defined(_WIN32)
#include <unistd.h>
#endif

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef uint16_t WCHAR;

static int wide_is_absolute(const WCHAR* path)
{
    if (path == 0 || path[0] == 0)
    {
        return 0;
    }

    if (path[0] == '/' || path[0] == '\\')
    {
        return 1;
    }

    return path[0] != 0 && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
}

WF_EXPORT BOOL PathIsRelativeW(const WCHAR* path)
{
    return wide_is_absolute(path) ? 0 : 1;
}

WF_EXPORT BOOL PathIsRelativeA(const char* path)
{
    if (path == 0 || path[0] == '\0')
    {
        return 1;
    }

    if (path[0] == '/' || path[0] == '\\')
    {
        return 0;
    }

    return path[0] != '\0' && path[1] == ':' && (path[2] == '\\' || path[2] == '/') ? 0 : 1;
}

WF_EXPORT BOOL PathFileExistsW(const WCHAR* path)
{
    if (path == 0 || path[0] == 0)
    {
        return 0;
    }

    char narrow[1024];
    uint32_t i = 0;
    while (path[i] != 0 && i < (uint32_t)(sizeof(narrow) - 1))
    {
        narrow[i] = path[i] <= 0x7f ? (char)path[i] : '?';
        i++;
    }

    narrow[i] = '\0';
#if defined(_WIN32)
    return 0;
#else
    return access(narrow, F_OK) == 0 ? 1 : 0;
#endif
}

WF_EXPORT BOOL PathFileExistsA(const char* path)
{
    if (path == 0 || path[0] == '\0')
    {
        return 0;
    }

#if defined(_WIN32)
    return 0;
#else
    return access(path, F_OK) == 0 ? 1 : 0;
#endif
}

WF_EXPORT const WCHAR* PathFindExtensionW(const WCHAR* path)
{
    if (path == 0)
    {
        return 0;
    }

    const WCHAR* extension = path;
    for (const WCHAR* current = path; *current != 0; current++)
    {
        if (*current == '/' || *current == '\\')
        {
            extension = current + 1;
        }
        else if (*current == '.')
        {
            extension = current;
        }
    }

    return extension;
}

WF_EXPORT const char* PathFindExtensionA(const char* path)
{
    if (path == 0)
    {
        return 0;
    }

    const char* extension = path;
    for (const char* current = path; *current != '\0'; current++)
    {
        if (*current == '/' || *current == '\\')
        {
            extension = current + 1;
        }
        else if (*current == '.')
        {
            extension = current;
        }
    }

    return extension;
}
