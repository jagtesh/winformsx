#pragma warning disable SA1513
#pragma warning disable CA1852
namespace System.Drawing.Impeller;

public static class ImpellerBackendInitializer
{
    private static nint s_impellerContext;
    private static ImpellerRenderingBackend? s_activeBackend;

    public static void Register()
    {
        Graphics.BackendFactory = (hwnd) =>
        {
            if (hwnd == IntPtr.Zero) return null;
            if (s_impellerContext == nint.Zero)
            {
                var settings = new ImpellerContextVulkanSettings();
                s_impellerContext = NativeMethods.ImpellerContextCreateVulkanNew(NativeMethods.ImpellerGetVersion(), ref settings);
            }
            if (s_activeBackend is null)
            {
                s_activeBackend = new ImpellerRenderingBackend(new DummyPlatformBackend(hwnd), s_impellerContext);
            }
            return s_activeBackend;
        };
    }

    private sealed class DummyPlatformBackend : IPlatformBackend
    {
        private readonly nint _hwnd;
        public DummyPlatformBackend(nint hwnd) { _hwnd = hwnd; }
        public nint AcquireNextSurface()
        {
            var size = new ImpellerISize(100, 100);
            return NativeMethods.ImpellerSurfaceCreateWrappedFBONew(s_impellerContext, 0, 0, ref size);
        }
        public void PresentSurface(nint surface) => NativeMethods.ImpellerSurfacePresent(surface);
    }
}
