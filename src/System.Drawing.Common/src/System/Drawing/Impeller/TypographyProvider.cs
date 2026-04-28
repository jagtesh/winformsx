#pragma warning disable IDE1006
using System.Drawing.Impeller;
#pragma warning disable IDE1006
using System.Linq;
using System.Runtime.InteropServices;

namespace System.Drawing;

internal static class TypographyProvider
{
    private const string DefaultFamilyAlias = "WinFormsDefault";

    private static readonly object s_lock = new();
    private static readonly List<byte[]> s_fontBlobs = [];
    private static readonly List<GCHandle> s_pinnedFonts = [];
    private static readonly HashSet<string> s_registeredAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> s_requestedFamilyToAlias = new(StringComparer.OrdinalIgnoreCase);

    private static nint s_typographyContext;
    private static bool s_initialized;

    public static nint Context
    {
        get
        {
            EnsureInitialized();
            return s_typographyContext;
        }
    }

    public static string? ResolveFontFamily(string? requested) =>
        ResolveFontFamily(requested, bold: false, italic: false);

    public static string? ResolveFontFamily(string? requested, bool bold, bool italic)
    {
#if BROWSER
        return requested;
#else
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(requested))
        {
            return s_registeredAliases.Contains(DefaultFamilyAlias) ? DefaultFamilyAlias : null;
        }

        string requestKey = $"{requested}|{(bold ? 'b' : '-')}{(italic ? 'i' : '-')}";
        if (s_requestedFamilyToAlias.TryGetValue(requestKey, out string? cachedAlias))
        {
            return cachedAlias;
        }

        ResolvedFontFile? font = FontFileResolver.ResolveFontFile(requested, bold, italic);
        if (font is null)
        {
            return s_registeredAliases.Contains(DefaultFamilyAlias) ? DefaultFamilyAlias : null;
        }

        string alias = CreateAlias(font.FamilyName, bold, italic);
        if (!s_registeredAliases.Contains(alias) && !RegisterFont(font.Path, alias))
        {
            return s_registeredAliases.Contains(DefaultFamilyAlias) ? DefaultFamilyAlias : null;
        }

        s_requestedFamilyToAlias[requestKey] = alias;
        return alias;
#endif
    }

    public static bool RegisterFont(string filePath, string? familyAlias = null)
    {
#if !BROWSER
        EnsureInitialized();

        byte[] fontData = IO.File.ReadAllBytes(filePath);
        s_fontBlobs.Add(fontData);
        GCHandle pinnedData = GCHandle.Alloc(fontData, GCHandleType.Pinned);
        s_pinnedFonts.Add(pinnedData);

        ImpellerMapping mapping = new()
        {
            Data = pinnedData.AddrOfPinnedObject(),
            Length = (ulong)fontData.Length,
            OnRelease = nint.Zero,
        };

        bool result = familyAlias is not null
            ? NativeMethods.ImpellerTypographyContextRegisterFontWithAlias(s_typographyContext, ref mapping, nint.Zero, familyAlias)
            : NativeMethods.ImpellerTypographyContextRegisterFont(s_typographyContext, ref mapping, nint.Zero, nint.Zero);

        if (result && familyAlias is not null)
        {
            s_registeredAliases.Add(familyAlias);
        }

        return result;
#else
        return true;
#endif
    }

    private static void EnsureInitialized()
    {
        if (s_initialized)
        {
            return;
        }

        lock (s_lock)
        {
            if (s_initialized)
            {
                return;
            }

#if !BROWSER
            s_typographyContext = NativeMethods.ImpellerTypographyContextNew();
            if (s_typographyContext == nint.Zero)
            {
                throw new InvalidOperationException("Failed to create Impeller TypographyContext.");
            }

            ResolvedFontFile? defaultFont = FontFileResolver.ResolveFontFile("Segoe UI", bold: false, italic: false);
            if (defaultFont is not null)
            {
                _ = RegisterFontCore(defaultFont.Path, DefaultFamilyAlias);
            }
#endif
            s_initialized = true;
        }
    }

    private static bool RegisterFontCore(string filePath, string familyAlias)
    {
        byte[] fontData = IO.File.ReadAllBytes(filePath);
        s_fontBlobs.Add(fontData);
        GCHandle pinnedData = GCHandle.Alloc(fontData, GCHandleType.Pinned);
        s_pinnedFonts.Add(pinnedData);

        ImpellerMapping mapping = new()
        {
            Data = pinnedData.AddrOfPinnedObject(),
            Length = (ulong)fontData.Length,
            OnRelease = nint.Zero,
        };

        bool result = NativeMethods.ImpellerTypographyContextRegisterFontWithAlias(s_typographyContext, ref mapping, nint.Zero, familyAlias);
        if (result)
        {
            s_registeredAliases.Add(familyAlias);
        }

        return result;
    }

    private static string CreateAlias(string familyName, bool bold, bool italic)
    {
        string sanitized = new(familyName.Select(static c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"wf_{sanitized}_{(bold ? "b" : "n")}{(italic ? "i" : "n")}";
    }
}
