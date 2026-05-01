#pragma warning disable SA1513
#pragma warning disable CA1852
namespace System.Drawing.Impeller;

public static class ImpellerBackendInitializer
{
    private static readonly object s_sync = new();
    private static readonly Dictionary<nint, ImpellerRenderingBackend> s_backends = [];
    private static readonly Dictionary<nint, nint> s_backendOwnersByHwnd = [];
    private static nint s_defaultHwnd;

    [ThreadStatic]
    private static nint s_currentHwnd;

    /// <summary>
    /// Registers a rendering backend factory. The <paramref name="contextAndBackendFactory"/>
    /// receives an HWND and must return (backendHwnd, impellerContext, platformBackend).
    /// This keeps Vulkan context creation in the platform layer where the
    /// proc address resolver is available.
    /// </summary>
    public static void Register(Func<nint, (nint backendHwnd, nint impellerContext, IPlatformBackend backend)?> contextAndBackendFactory)
    {
        Graphics.BackendFactory = (hwnd) =>
        {
            if (hwnd == IntPtr.Zero)
            {
                hwnd = s_currentHwnd;
            }

            if (hwnd == IntPtr.Zero)
            {
                lock (s_sync)
                {
                    hwnd = s_defaultHwnd;
                }
            }

            if (hwnd == IntPtr.Zero) return null;

            lock (s_sync)
            {
                if (s_backends.TryGetValue(hwnd, out ImpellerRenderingBackend? existing))
                {
                    return existing;
                }
            }

            try
            {
                var result = contextAndBackendFactory(hwnd);
                if (result is null)
                {
                    return null;
                }

                nint backendHwnd = result.Value.backendHwnd == nint.Zero
                    ? hwnd
                    : result.Value.backendHwnd;

                lock (s_sync)
                {
                    if (s_backends.TryGetValue(backendHwnd, out ImpellerRenderingBackend? existing))
                    {
                        s_backends[hwnd] = existing;
                        s_backendOwnersByHwnd[hwnd] = backendHwnd;
                        return existing;
                    }

                    ImpellerRenderingBackend backend = new(result.Value.backend, result.Value.impellerContext);
                    s_backends[backendHwnd] = backend;
                    s_backendOwnersByHwnd[backendHwnd] = backendHwnd;
                    if (hwnd != backendHwnd)
                    {
                        s_backends[hwnd] = backend;
                        s_backendOwnersByHwnd[hwnd] = backendHwnd;
                    }

                    if (s_defaultHwnd == nint.Zero)
                    {
                        s_defaultHwnd = backendHwnd;
                    }

                    return backend;
                }
            }
            catch
            {
                return null;
            }
        };
    }

    public static IDisposable UseBackendForHwnd(nint hwnd)
    {
        nint priorHwnd = s_currentHwnd;
        s_currentHwnd = hwnd;
        return new BackendScope(priorHwnd);
    }

    public static void RemoveBackend(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return;
        }

        ImpellerRenderingBackend? backendToDispose = null;
        lock (s_sync)
        {
            if (!s_backendOwnersByHwnd.TryGetValue(hwnd, out nint ownerHwnd))
            {
                return;
            }

            if (ownerHwnd != hwnd)
            {
                s_backends.Remove(hwnd);
                s_backendOwnersByHwnd.Remove(hwnd);
                return;
            }

            if (s_backends.TryGetValue(ownerHwnd, out backendToDispose))
            {
                List<nint> aliases = [];
                foreach (KeyValuePair<nint, nint> entry in s_backendOwnersByHwnd)
                {
                    if (entry.Value == ownerHwnd)
                    {
                        aliases.Add(entry.Key);
                    }
                }

                foreach (nint alias in aliases)
                {
                    s_backends.Remove(alias);
                    s_backendOwnersByHwnd.Remove(alias);
                }
            }

            if (s_defaultHwnd == ownerHwnd)
            {
                s_defaultHwnd = nint.Zero;
                foreach (nint candidateOwner in s_backendOwnersByHwnd.Values)
                {
                    s_defaultHwnd = candidateOwner;
                    break;
                }
            }
        }

        backendToDispose?.Dispose();
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
            return (hwnd, ctx, backend);
        });
    }

    private sealed class BackendScope : IDisposable
    {
        private readonly nint _priorHwnd;
        private bool _disposed;

        public BackendScope(nint priorHwnd)
        {
            _priorHwnd = priorHwnd;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            s_currentHwnd = _priorHwnd;
            _disposed = true;
        }
    }
}
