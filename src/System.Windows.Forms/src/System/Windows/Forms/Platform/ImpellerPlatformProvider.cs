// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

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

    // Prevent GC of the proc address callback while Impeller holds it
    private static VulkanProcAddressCallback? s_vkProcCallback;
    private static nint s_vulkanModule;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VulkanProcAddressCallback(nint vkInstance, nint procName, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VkGetInstanceProcAddrDelegate(nint instance, nint procName);
    private static VkGetInstanceProcAddrDelegate? s_vkGetInstanceProcAddr;

    public ImpellerPlatformProvider()
    {
        global::System.Drawing.Impeller.ImpellerBackendInitializer.Register((hwnd) =>
        {
            // 1. Create the Impeller Vulkan context with a proper proc address resolver
            s_vkProcCallback = VulkanProcAddressResolver;
            var settings = new global::System.Drawing.Impeller.ImpellerContextVulkanSettings
            {
                ProcAddressCallback = Marshal.GetFunctionPointerForDelegate(s_vkProcCallback),
                EnableVulkanValidation = 0,
            };

            uint version = global::System.Drawing.Impeller.NativeMethods.ImpellerGetVersion();
            nint impellerCtx = global::System.Drawing.Impeller.NativeMethods.ImpellerContextCreateVulkanNew(
                version, ref settings);
            if (impellerCtx == nint.Zero)
            {
                return null;
            }

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

            // 3. Create the platform backend (swapchain)
            var backend = new Platform.Impeller.SilkPlatformBackend(silkWindow, impellerCtx);
            return ((nint impellerContext, global::System.Drawing.IPlatformBackend backend)?)(impellerCtx, backend);
        });
    }

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern nint GetProcAddress(nint hModule, string lpProcName);

    private static nint VulkanProcAddressResolver(nint vkInstance, nint procNamePtr, nint userData)
    {
        if (s_vkGetInstanceProcAddr is null)
        {
            if (s_vulkanModule == nint.Zero)
            {
                s_vulkanModule = LoadLibraryW("vulkan-1.dll");
            }

            if (s_vulkanModule == nint.Zero) return nint.Zero;

            var addr = GetProcAddress(s_vulkanModule, "vkGetInstanceProcAddr");
            if (addr == nint.Zero) return nint.Zero;
            s_vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<VkGetInstanceProcAddrDelegate>(addr);
        }

        return s_vkGetInstanceProcAddr(vkInstance, procNamePtr);
    }
}
