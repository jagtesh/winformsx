// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Drawing.Impeller;
using Silk.NET.Core.Native;

namespace System.Windows.Forms.Platform.Impeller;

internal sealed class SilkPlatformBackend : IPlatformBackend
{
    private readonly nint _swapchain;

    public unsafe SilkPlatformBackend(Silk.NET.Windowing.IWindow? window, nint impellerContext)
    {
        if (window is null)
        {
            return;
        }

        if (impellerContext == nint.Zero)
        {
            return;
        }

        if (ImpellerSwapchainManager.GetVulkanInfo(impellerContext, out var vulkanInfo))
        {
            var vkHandle = new VkHandle(vulkanInfo.VkInstance);
            var vkSurfaceHandle = window.VkSurface?.Create<Silk.NET.Core.Native.VkNonDispatchableHandle>(vkHandle, null);
            if (vkSurfaceHandle.HasValue && vkSurfaceHandle.Value.Handle != 0UL)
            {
                _swapchain = ImpellerSwapchainManager.CreateVulkanSwapchain(impellerContext, (nint)vkSurfaceHandle.Value.Handle);
            }
        }
    }

    public nint AcquireNextSurface()
    {
        if (_swapchain != nint.Zero)
        {
            return ImpellerSwapchainManager.AcquireNextSurface(_swapchain);
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
}
