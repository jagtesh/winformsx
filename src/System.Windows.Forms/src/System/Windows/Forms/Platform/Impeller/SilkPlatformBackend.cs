// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Drawing.Impeller;
using Silk.NET.Core.Native;

namespace System.Windows.Forms.Platform.Impeller;

internal sealed class SilkPlatformBackend : IPlatformBackend
{
    private readonly Silk.NET.Windowing.IWindow? _window;
    private readonly nint _impellerContext;
    private readonly bool _useWrappedFbo;
    private readonly nint _swapchain;
    private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");

    public unsafe SilkPlatformBackend(Silk.NET.Windowing.IWindow? window, nint impellerContext)
    {
        _window = window;
        _impellerContext = impellerContext;
        Trace($"[SilkPlatformBackend] ctor start windowNull={window is null} ctx=0x{impellerContext:X}");
        if (window is null)
        {
            Trace("[SilkPlatformBackend] window null");
            return;
        }

        if (impellerContext == nint.Zero)
        {
            Trace("[SilkPlatformBackend] context zero");
            return;
        }

        if (ImpellerSwapchainManager.GetVulkanInfo(impellerContext, out var vulkanInfo))
        {
            Trace($"[SilkPlatformBackend] vkInfo instance=0x{vulkanInfo.VkInstance:X}");
            var vkHandle = new VkHandle(vulkanInfo.VkInstance);
            var vkSurfaceHandle = window.VkSurface?.Create<Silk.NET.Core.Native.VkNonDispatchableHandle>(vkHandle, null);
            Trace($"[SilkPlatformBackend] vkSurface hasValue={vkSurfaceHandle.HasValue}");
            if (vkSurfaceHandle.HasValue && vkSurfaceHandle.Value.Handle != 0UL)
            {
                Trace($"[SilkPlatformBackend] vkSurface=0x{vkSurfaceHandle.Value.Handle:X}");
                _swapchain = ImpellerSwapchainManager.CreateVulkanSwapchain(impellerContext, (nint)vkSurfaceHandle.Value.Handle);
                Trace($"[SilkPlatformBackend] swapchain=0x{_swapchain:X}");
            }
        }
        else
        {
            _useWrappedFbo = true;
            Trace("[SilkPlatformBackend] GetVulkanInfo failed -> using wrapped FBO path");
        }
    }

    public nint AcquireNextSurface()
    {
        if (_swapchain != nint.Zero)
        {
            return ImpellerSwapchainManager.AcquireNextSurface(_swapchain);
        }

        if (_useWrappedFbo && _window is not null && _impellerContext != nint.Zero)
        {
            int width = Math.Max(1, _window.Size.X);
            int height = Math.Max(1, _window.Size.Y);
            var size = new ImpellerISize(width, height);
            // FBO 0 = default framebuffer for current context.
            return Drawing.Impeller.NativeMethods.ImpellerSurfaceCreateWrappedFBONew(
                _impellerContext,
                0,
                (uint)ImpellerPixelFormat.RGBA8888,
                ref size);
        }

        return nint.Zero;
    }

    public void PresentSurface(nint surface)
    {
        if (_swapchain != nint.Zero && surface != nint.Zero)
        {
            ImpellerSwapchainManager.PresentSurface(surface);
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
