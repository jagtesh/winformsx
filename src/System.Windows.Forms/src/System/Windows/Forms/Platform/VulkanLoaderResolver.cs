// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Resolves Vulkan loader availability for both GLFW window creation and
/// Impeller proc-address initialization without relying on external bootstrap.
/// </summary>
internal static class VulkanLoaderResolver
{
    private static readonly object s_lock = new();
    private static nint s_loadedHandle;
    private static string? s_loadedFrom;
    private static bool s_attempted;

    public static bool TryEnsureLoaded(out string? loadedFrom)
    {
        lock (s_lock)
        {
            if (s_loadedHandle != nint.Zero)
            {
                loadedFrom = s_loadedFrom;
                return true;
            }

            if (!s_attempted)
            {
                s_attempted = true;
                foreach (string candidate in GetCandidates())
                {
                    if (NativeLibrary.TryLoad(candidate, out nint handle) && handle != nint.Zero)
                    {
                        s_loadedHandle = handle;
                        s_loadedFrom = candidate;
                        break;
                    }
                }
            }

            loadedFrom = s_loadedFrom;
            return s_loadedHandle != nint.Zero;
        }
    }

    public static bool TryGetExport(string exportName, out nint export, out string? loadedFrom)
    {
        export = nint.Zero;
        if (!TryEnsureLoaded(out loadedFrom))
        {
            return false;
        }

        if (NativeLibrary.TryGetExport(s_loadedHandle, exportName, out export) && export != nint.Zero)
        {
            return true;
        }

        // A loader may already be present under a different soname in process.
        // Retry all candidates and keep the first that contains the requested export.
        foreach (string candidate in GetCandidates())
        {
            if (!NativeLibrary.TryLoad(candidate, out nint handle) || handle == nint.Zero)
            {
                continue;
            }

            if (!NativeLibrary.TryGetExport(handle, exportName, out export) || export == nint.Zero)
            {
                continue;
            }

            lock (s_lock)
            {
                s_loadedHandle = handle;
                s_loadedFrom = candidate;
                loadedFrom = candidate;
            }

            return true;
        }

        export = nint.Zero;
        return false;
    }

    public static bool TryConfigureRuntime(out string? detail)
    {
        detail = null;

        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        string? existingIcd = Environment.GetEnvironmentVariable("VK_ICD_FILENAMES");
        string? existingDrivers = Environment.GetEnvironmentVariable("VK_DRIVER_FILES");
        if (!string.IsNullOrWhiteSpace(existingIcd) || !string.IsNullOrWhiteSpace(existingDrivers))
        {
            detail = "VK_ICD_FILENAMES/VK_DRIVER_FILES already set";
            return true;
        }

        string? moltenVkPath = FindMoltenVkLibrary();
        if (string.IsNullOrWhiteSpace(moltenVkPath))
        {
            detail = "libMoltenVK.dylib not found";
            return false;
        }

        string icdPath = EnsureMoltenVkIcdJson(moltenVkPath);
        Environment.SetEnvironmentVariable("VK_ICD_FILENAMES", icdPath);
        Environment.SetEnvironmentVariable("VK_DRIVER_FILES", icdPath);
        detail = icdPath;
        return true;
    }

    private static string[] GetCandidates() =>
    [
        // macOS/Homebrew and common user-space loader install paths.
        "libvulkan.1.dylib",
        "libvulkan.dylib",
        "/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib",
        "/opt/homebrew/opt/vulkan-loader/lib/libvulkan.dylib",
        "/opt/homebrew/lib/libvulkan.1.dylib",
        "/usr/local/opt/vulkan-loader/lib/libvulkan.1.dylib",
        "/usr/local/opt/vulkan-loader/lib/libvulkan.dylib",
        "/usr/local/lib/libvulkan.1.dylib",
        "/usr/local/lib/libvulkan.dylib",
        "/opt/local/lib/libvulkan.1.dylib",
        "/opt/local/lib/libvulkan.dylib",

        // Linux.
        "libvulkan.so.1",
        "libvulkan.so",
        "/usr/lib/x86_64-linux-gnu/libvulkan.so.1",
        "/usr/lib/aarch64-linux-gnu/libvulkan.so.1",
        "/usr/lib64/libvulkan.so.1",
        "/usr/local/lib/libvulkan.so.1",

        // Windows.
        "vulkan-1.dll",
    ];

    private static string? FindMoltenVkLibrary()
    {
        string[] directCandidates =
        [
            "/opt/homebrew/lib/libMoltenVK.dylib",
            "/usr/local/lib/libMoltenVK.dylib",
            "/opt/local/lib/libMoltenVK.dylib",
        ];

        foreach (string candidate in directCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string[] cellarRoots =
        [
            "/opt/homebrew/Cellar/molten-vk",
            "/usr/local/Cellar/molten-vk",
        ];

        foreach (string root in cellarRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            string[] versions = Directory.GetDirectories(root);
            Array.Sort(versions, StringComparer.Ordinal);
            Array.Reverse(versions);
            foreach (string versionDir in versions)
            {
                string candidate = Path.Combine(versionDir, "lib", "libMoltenVK.dylib");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string EnsureMoltenVkIcdJson(string moltenVkPath)
    {
        string runtimeDir = Path.Combine(Path.GetTempPath(), "winformsx-vulkan-runtime");
        Directory.CreateDirectory(runtimeDir);
        string icdPath = Path.Combine(runtimeDir, "MoltenVK_icd.json");

        string escapedLibraryPath = moltenVkPath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        string json = $$"""
            {
              "file_format_version": "1.0.0",
              "ICD": {
                "library_path": "{{escapedLibraryPath}}",
                "api_version": "1.3.0"
              }
            }
            """;

        File.WriteAllText(icdPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return icdPath;
    }
}
