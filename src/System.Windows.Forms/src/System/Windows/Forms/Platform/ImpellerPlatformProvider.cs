// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace System.Windows.Forms.Platform;

using System.Runtime.InteropServices;

/// <summary>
/// Impeller platform provider — the sole rendering/windowing backend.
/// All OS interactions are routed through this provider.
///
/// Implementation strategy:
/// - Window/Input/System: Backed by SDL3 or platform-native APIs
/// - GDI: Routed to WinFormsX IRenderingBackend / ImpellerRenderingBackend
/// - Message: Internal message queue (no Win32 message pump)
/// - Controls: Owner-drawn via Impeller (no comctl32)
/// - Dialogs: Platform-native dialogs
/// - Accessibility: Stub initially, then AT-SPI / NSAccessibility
/// </summary>
internal sealed class ImpellerPlatformProvider : IPlatformProvider
{
    public string Name => "Impeller";

    public IWindowInterop Window { get; } = new ImpellerWindowInterop();
    public IMessageInterop Message { get; } = new ImpellerMessageInterop();
    public IGdiInterop Gdi { get; } = new ImpellerGdiInterop();
    public IInputInterop Input { get; } = new ImpellerInputInterop();
    public ISystemInterop System { get; } = new ImpellerSystemInterop();
    public IDialogInterop Dialog { get; } = new ImpellerDialogInterop();
    public IControlInterop Control { get; } = new ImpellerControlInterop();
    public IAccessibilityInterop Accessibility { get; } = new ImpellerAccessibilityInterop();

    private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");
    private static VulkanProcAddressCallback? s_vkProcCallback;
    private static VkGetInstanceProcAddrDelegate? s_vkGetInstanceProcAddr;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VulkanProcAddressCallback(nint vkInstance, nint procName, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VkGetInstanceProcAddrDelegate(nint vkInstance, nint procName);

    public ImpellerPlatformProvider()
    {
        GetDcScope.GetDCCallback = Gdi.GetDC;
        GetDcScope.GetDCExCallback = Gdi.GetDCEx;
        GetDcScope.ReleaseDCCallback = (hWnd, hdc) => Gdi.ReleaseDC(hWnd, hdc);

        Drawing.Impeller.ImpellerBackendInitializer.Register((hwnd) =>
        {
            Trace($"[BackendInit] start hwnd=0x{hwnd:X}");
            // 1. Create the Impeller Vulkan context with an explicit loader-backed proc resolver.
            uint version = Drawing.Impeller.NativeMethods.ImpellerGetVersion();
            Trace($"[BackendInit] impeller version={version}");
            s_vkGetInstanceProcAddr ??= LoadVkGetInstanceProcAddr();
            if (s_vkGetInstanceProcAddr is null)
            {
                Trace("[BackendInit] failed to resolve vkGetInstanceProcAddr");
                return null;
            }

            s_vkProcCallback = VulkanProcAddressResolver;
            var settings = new global::System.Drawing.Impeller.ImpellerContextVulkanSettings
            {
                ProcAddressCallback = Marshal.GetFunctionPointerForDelegate(s_vkProcCallback),
                EnableVulkanValidation = 0,
            };

            nint impellerCtx = Drawing.Impeller.NativeMethods.ImpellerContextCreateVulkanNew(version, ref settings);
            if (impellerCtx == nint.Zero)
            {
                Trace("[BackendInit] Vulkan context create failed");
                return null;
            }

            Trace($"[BackendInit] Vulkan context=0x{impellerCtx:X}");

            // 2. Find the Silk.NET window for this HWND
            var windowInterop = (ImpellerWindowInterop)Window;
            var silkWindow = windowInterop.GetSilkWindow((HWND)hwnd);
            if (silkWindow is null)
            {
                // Traverse GetParent until we find one with a SilkWindow.
                var curr = (HWND)hwnd;
                while (curr != HWND.Null && silkWindow is null)
                {
                    curr = windowInterop.GetParent(curr);
                    silkWindow = windowInterop.GetSilkWindow(curr);
                }
            }

            Trace($"[BackendInit] silkWindow null? {silkWindow is null}");

            // 3. Create the platform backend (swapchain)
            var backend = new Platform.Impeller.SilkPlatformBackend(silkWindow, impellerCtx);
            Trace("[BackendInit] backend created");
            return ((nint impellerContext, global::System.Drawing.IPlatformBackend backend)?)(impellerCtx, backend);
        });
    }

    private static VkGetInstanceProcAddrDelegate? LoadVkGetInstanceProcAddr()
    {
        string[] candidates =
        [
            "libvulkan.1.dylib",
            "libvulkan.dylib",
            "libvulkan.so.1",
            "libvulkan.so",
            "vulkan-1.dll",
        ];

        foreach (string lib in candidates)
        {
            if (!NativeLibrary.TryLoad(lib, out nint handle) || handle == nint.Zero)
            {
                continue;
            }

            if (!NativeLibrary.TryGetExport(handle, "vkGetInstanceProcAddr", out nint fn) || fn == nint.Zero)
            {
                continue;
            }

            Trace($"[BackendInit] loaded vkGetInstanceProcAddr from {lib}");
            return Marshal.GetDelegateForFunctionPointer<VkGetInstanceProcAddrDelegate>(fn);
        }

        return null;
    }

    private static nint VulkanProcAddressResolver(nint vkInstance, nint procNamePtr, nint userData)
    {
        if (procNamePtr == nint.Zero || s_vkGetInstanceProcAddr is null)
        {
            return nint.Zero;
        }

        nint proc = s_vkGetInstanceProcAddr(vkInstance, procNamePtr);
        if (proc != nint.Zero)
        {
            return proc;
        }

        if (vkInstance != nint.Zero)
        {
            return s_vkGetInstanceProcAddr(nint.Zero, procNamePtr);
        }

        return nint.Zero;
    }

    private static void Trace(string message)
    {
        if (string.IsNullOrWhiteSpace(s_traceFile))
        {
            return;
        }

        try
        {
            File.AppendAllText(s_traceFile, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
