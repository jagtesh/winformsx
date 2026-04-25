// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Impeller;

public static class ImpellerSwapchainManager
{
    public static nint CreateVulkanSwapchain(nint impellerContext, nint vkSurfaceKHR)
    {
        return NativeMethods.ImpellerVulkanSwapchainCreateNew(impellerContext, vkSurfaceKHR);
    }

    public static nint AcquireNextSurface(nint swapchain)
    {
        return NativeMethods.ImpellerVulkanSwapchainAcquireNextSurfaceNew(swapchain);
    }

    public static void PresentSurface(nint surface)
    {
        NativeMethods.ImpellerSurfacePresent(surface);
    }

    public static bool GetVulkanInfo(nint impellerContext, out ImpellerContextVulkanInfo vulkanInfo)
    {
        return NativeMethods.ImpellerContextGetVulkanInfo(impellerContext, out vulkanInfo);
    }
}
