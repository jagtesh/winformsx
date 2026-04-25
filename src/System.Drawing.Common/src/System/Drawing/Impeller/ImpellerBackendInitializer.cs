#pragma warning disable SA1513
#pragma warning disable CA1852
namespace System.Drawing.Impeller;

public static class ImpellerBackendInitializer
{
    private static ImpellerRenderingBackend? s_activeBackend;
    private static bool s_initFailed;

    /// <summary>
    /// Registers a rendering backend factory. The <paramref name="contextAndBackendFactory"/>
    /// receives an HWND and must return (impellerContext, platformBackend).
    /// This keeps Vulkan context creation in the platform layer where the
    /// proc address resolver is available.
    /// </summary>
    public static void Register(Func<nint, (nint impellerContext, IPlatformBackend backend)?> contextAndBackendFactory)
    {
        Graphics.BackendFactory = (hwnd) =>
        {
            // Once the backend exists, always return it (singleton per context).
            if (s_activeBackend is not null)
            {
                return s_activeBackend;
            }

            // Permanently failed — don't retry
            if (s_initFailed) return null;

            // First call must provide a real HWND to bootstrap the platform backend.
            if (hwnd == IntPtr.Zero) return null;

            try
            {
                var result = contextAndBackendFactory(hwnd);
                if (result is null)
                {
                    s_initFailed = true;
                    return null;
                }

                s_activeBackend = new ImpellerRenderingBackend(result.Value.backend, result.Value.impellerContext);
                return s_activeBackend;
            }
            catch
            {
                s_initFailed = true;
                return null;
            }
        };
    }

    /// <summary>
    /// Simplified registration for cases where context creation is handled internally.
    /// </summary>
    public static void Register(Func<nint, nint, IPlatformBackend>? platformBackendFactory = null)
    {
        Register((hwnd) =>
        {
            var settings = new ImpellerContextVulkanSettings();
            var ctx = NativeMethods.ImpellerContextCreateVulkanNew(NativeMethods.ImpellerGetVersion(), ref settings);
            if (ctx == nint.Zero) return null;

            IPlatformBackend backend = platformBackendFactory is object
                ? platformBackendFactory(hwnd, ctx)
                : throw new InvalidOperationException("No platform backend factory provided.");
            return (ctx, backend);
        });
    }
}
