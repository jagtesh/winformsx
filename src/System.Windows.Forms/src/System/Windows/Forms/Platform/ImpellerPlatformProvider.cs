// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace System.Windows.Forms.Platform;

using Silk.NET.Core.Contexts;
using Silk.NET.GLFW;
using Silk.NET.Windowing;
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
    private static GlProcAddressCallback? s_glProcCallback;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VulkanProcAddressCallback(nint vkInstance, nint procName, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint VkGetInstanceProcAddrDelegate(nint vkInstance, nint procName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GlProcAddressCallback(nint procName, nint userData);

    public ImpellerPlatformProvider()
    {
        GetDcScope.GetDCCallback = Gdi.GetDC;
        GetDcScope.GetDCExCallback = Gdi.GetDCEx;
        GetDcScope.ReleaseDCCallback = (hWnd, hdc) => Gdi.ReleaseDC(hWnd, hdc);

        Drawing.Impeller.ImpellerBackendInitializer.Register((hwnd) =>
        {
            Trace($"[BackendInit] start hwnd=0x{hwnd:X}");

            uint version = Drawing.Impeller.NativeMethods.ImpellerGetVersion();
            Trace($"[BackendInit] impeller version={version}");

            // Find the Silk.NET window for this HWND before choosing the renderer context.
            // If Vulkan window creation fell back to a normal Silk window, the only
            // presentable surface is the current OpenGL framebuffer.
            var windowInterop = (ImpellerWindowInterop)Window;
            HWND backendHwnd = windowInterop.GetBackendSurfaceOwner((HWND)hwnd);
            var silkWindow = backendHwnd == HWND.Null ? null : windowInterop.GetSilkWindow(backendHwnd);
            if (silkWindow is null)
            {
                Trace("[BackendInit] no Silk render surface owner");
                return null;
            }

            Trace($"[BackendInit] backendHwnd=0x{(nint)backendHwnd:X} silkWindow null? {silkWindow is null}");

            if (TryCreateOpenGlContext(silkWindow, version, out nint glCtx))
            {
                Trace($"[BackendInit] OpenGLES context=0x{glCtx:X}");
                var glBackend = new Platform.Impeller.SilkPlatformBackend(silkWindow, glCtx);
                Trace("[BackendInit] OpenGLES backend created");
                return ((nint backendHwnd, nint impellerContext, global::System.Drawing.IPlatformBackend backend)?)(backendHwnd, glCtx, glBackend);
            }

            // Create the Impeller Vulkan context with an explicit loader-backed proc resolver.
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

            var backend = new Platform.Impeller.SilkPlatformBackend(silkWindow, impellerCtx);
            Trace("[BackendInit] backend created");
            return ((nint backendHwnd, nint impellerContext, global::System.Drawing.IPlatformBackend backend)?)(backendHwnd, impellerCtx, backend);
        });
    }

    private static bool TryCreateOpenGlContext(IWindow? silkWindow, uint version, out nint context)
    {
        context = nint.Zero;
        if (silkWindow is not IGLContextSource glContextSource)
        {
            return false;
        }

        if (silkWindow is IViewProperties viewProperties
            && viewProperties.API.API == ContextAPI.Vulkan)
        {
            return false;
        }

        try
        {
            IGLContext? glContext = glContextSource.GLContext;
            if (glContext is null)
            {
                return false;
            }

            glContext.MakeCurrent();
            s_glProcCallback = GlProcAddressResolver;
            context = Drawing.Impeller.NativeMethods.ImpellerContextCreateOpenGLESNew(
                version,
                Marshal.GetFunctionPointerForDelegate(s_glProcCallback),
                nint.Zero);
            return context != nint.Zero;
        }
        catch (Exception ex)
        {
            Trace($"[BackendInit] OpenGLES context create failed: {ex.GetType().Name}: {ex.Message}");
            context = nint.Zero;
            return false;
        }
    }

    private static VkGetInstanceProcAddrDelegate? LoadVkGetInstanceProcAddr()
    {
        if (VulkanLoaderResolver.TryGetExport("vkGetInstanceProcAddr", out nint fn, out string? source)
            && fn != nint.Zero)
        {
            Trace($"[BackendInit] loaded vkGetInstanceProcAddr from {source ?? "<unknown>"}");
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

    private static nint GlProcAddressResolver(nint procNamePtr, nint userData)
    {
        if (procNamePtr == nint.Zero)
        {
            return nint.Zero;
        }

        string? procName = Marshal.PtrToStringAnsi(procNamePtr);
        if (string.IsNullOrWhiteSpace(procName))
        {
            return nint.Zero;
        }

        try
        {
            return GlfwProvider.GLFW.Value.GetProcAddress(procName);
        }
        catch
        {
            return nint.Zero;
        }
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
