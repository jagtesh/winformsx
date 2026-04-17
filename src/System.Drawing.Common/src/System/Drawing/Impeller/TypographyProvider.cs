#pragma warning disable IDE1006
using System.Drawing.Impeller;
#pragma warning disable IDE1006
using System.Runtime.InteropServices;

namespace System.Drawing;

/// <summary>
/// Manages the Impeller typography context and font registration.
/// In WASM mode, returns nint.Zero — text measurement uses fallback approximations.
/// </summary>
internal static class TypographyProvider
{
    private static nint _typographyContext;
    private static bool _initialized;
    private static readonly object _lock = new();
    private static byte[]? _fontData;

    public static nint Context
    {
        get
        {
            if (!_initialized)
            {
                lock (_lock)
                {
                    if (!_initialized)
                        Initialize();
                }
            }

            return _typographyContext;
        }
    }

    private static void Initialize()
    {
#if !BROWSER
        _typographyContext = NativeMethods.ImpellerTypographyContextNew();
        if (_typographyContext == nint.Zero)
            throw new InvalidOperationException("Failed to create Impeller TypographyContext.");

        var fontPaths = new[]
        {
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\tahoma.ttf",
        };

        foreach (var path in fontPaths)
        {
            if (IO.File.Exists(path))
            {
                RegisterFont(path, null);
                break;
            }
        }
#endif
        _initialized = true;
    }

    public static bool RegisterFont(string filePath, string? familyAlias = null)
    {
#if !BROWSER
        if (_typographyContext == nint.Zero)
            _ = Context;

            _ = Context;

        _fontData = IO.File.ReadAllBytes(filePath);
        var pinnedData = GCHandle.Alloc(_fontData, GCHandleType.Pinned);

        var mapping = new ImpellerMapping
        {
            Data = pinnedData.AddrOfPinnedObject(),
            Length = (ulong)_fontData.Length,
            OnRelease = nint.Zero,
        };

        bool result;
        if (familyAlias is not null)
        {
            result = NativeMethods.ImpellerTypographyContextRegisterFontWithAlias(
                _typographyContext, ref mapping, nint.Zero, familyAlias);
        }
        else
        {
            result = NativeMethods.ImpellerTypographyContextRegisterFont(
                _typographyContext, ref mapping, nint.Zero, nint.Zero);
        }

        return result;
#else
        return true; // No-op in WASM
#endif
    }
}
