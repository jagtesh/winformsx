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
    private static readonly List<byte[]> s_fontBlobs = [];
    private static readonly HashSet<string> s_registeredAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> s_requestedFamilyToAlias = new(StringComparer.OrdinalIgnoreCase);
    private const string DefaultFamilyAlias = "WinFormsDefault";

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

        string[] fontPaths = GetCandidateFontPaths();
        foreach (string path in fontPaths)
        {
            if (IO.File.Exists(path) && RegisterFont(path, DefaultFamilyAlias))
            {
                break;
            }
        }
#endif
        _initialized = true;
    }

    private static string[] GetCandidateFontPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                @"C:\Windows\Fonts\segoeui.ttf",
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\tahoma.ttf"
            ];
        }

        if (OperatingSystem.IsMacOS())
        {
            return
            [
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/System/Library/Fonts/Supplemental/Helvetica.ttc",
                "/System/Library/Fonts/Supplemental/Menlo.ttc",
                "/System/Library/Fonts/Supplemental/Verdana.ttf"
            ];
        }

        return
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/opentype/noto/NotoSans-Regular.ttf"
        ];
    }

    public static bool RegisterFont(string filePath, string? familyAlias = null)
    {
#if !BROWSER
        if (_typographyContext == nint.Zero)
            _ = Context;

        byte[] fontData = IO.File.ReadAllBytes(filePath);
        s_fontBlobs.Add(fontData);
        var pinnedData = GCHandle.Alloc(fontData, GCHandleType.Pinned);

        var mapping = new ImpellerMapping
        {
            Data = pinnedData.AddrOfPinnedObject(),
            Length = (ulong)fontData.Length,
            OnRelease = nint.Zero,
        };

        bool result;
        if (familyAlias is not null)
        {
            result = NativeMethods.ImpellerTypographyContextRegisterFontWithAlias(
                _typographyContext, ref mapping, nint.Zero, familyAlias);
            if (result)
            {
                s_registeredAliases.Add(familyAlias);
            }
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

    public static string? ResolveFontFamily(string? requested)
    {
#if BROWSER
        return requested;
#else
        if (_typographyContext == nint.Zero)
        {
            _ = Context;
        }

        if (!string.IsNullOrWhiteSpace(requested) && s_registeredAliases.Contains(requested))
        {
            return requested;
        }

        return s_registeredAliases.Contains(DefaultFamilyAlias) ? DefaultFamilyAlias : null;
#endif
    }

    public static string? ResolveFontFamily(string? requested, bool bold, bool italic)
    {
#if BROWSER
        return requested;
#else
        if (_typographyContext == nint.Zero)
        {
            _ = Context;
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return ResolveFontFamily(requested);
        }

        string requestKey = $"{requested}|{(bold ? 'b' : '-')}{(italic ? 'i' : '-')}";
        if (s_requestedFamilyToAlias.TryGetValue(requestKey, out string? cachedAlias))
        {
            return cachedAlias;
        }

        string? fontPath = FindFontFile(requested, bold, italic);
        if (fontPath is null)
        {
            return ResolveFontFamily(requested);
        }

        string alias = $"wf_{requested.Replace(' ', '_')}_{(bold ? "b" : "n")}{(italic ? "i" : "n")}";
        if (!s_registeredAliases.Contains(alias) && !RegisterFont(fontPath, alias))
        {
            return ResolveFontFamily(requested);
        }

        s_requestedFamilyToAlias[requestKey] = alias;
        return alias;
#endif
    }

    // Reused from ../src/WinFormsX/TextRenderer.cs and adapted for backend registration.
    private static string? FindFontFile(string familyName, bool bold, bool italic)
    {
        string fileName = GetFontFileName(familyName, bold, italic);

        string[] searchPaths;
        if (OperatingSystem.IsWindows())
        {
            searchPaths =
            [
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)),
                @"C:\Windows\Fonts",
            ];
        }
        else if (OperatingSystem.IsMacOS())
        {
            searchPaths =
            [
                "/System/Library/Fonts",
                "/System/Library/Fonts/Supplemental",
                "/Library/Fonts",
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts"),
            ];
        }
        else
        {
            searchPaths =
            [
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
            ];
        }

        foreach (string dir in searchPaths)
        {
            if (!IO.Directory.Exists(dir))
            {
                continue;
            }

            string path = IO.Path.Combine(dir, fileName);
            if (IO.File.Exists(path))
            {
                return path;
            }

            try
            {
                string[] files = IO.Directory.GetFiles(dir, fileName, IO.SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string GetFontFileName(string familyName, bool bold, bool italic)
    {
        string normalized = familyName.ToLowerInvariant().Replace(" ", "");

        string suffix = (bold, italic) switch
        {
            (true, true) => "bi",
            (true, false) => "b",
            (false, true) => "i",
            _ => "",
        };

        return normalized switch
        {
            "segoeui" => $"segoeui{suffix}.ttf",
            "arial" => $"arial{suffix}.ttf",
            "timesnewroman" => $"times{suffix}.ttf",
            "couriernew" => $"cour{suffix}.ttf",
            "calibri" => $"calibri{suffix}.ttf",
            "consolas" => $"consola{suffix}.ttf",
            "verdana" => $"verdana{suffix}.ttf",
            "tahoma" => $"tahoma{suffix}.ttf",
            "trebuchetms" => $"trebuc{suffix}.ttf",
            "georgia" => $"georgia{suffix}.ttf",
            "impact" => "impact.ttf",
            "comicsansms" => $"comic{suffix}.ttf",
            "sfpro" or "sfsystemfont" => "SFNS.ttf",
            "helvetica" => "Helvetica.ttf",
            "helveticaneue" => "HelveticaNeue.ttf",
            "dejavusans" => $"DejaVuSans{(suffix == "b" ? "-Bold" : suffix == "i" ? "-Oblique" : "")}.ttf",
            "liberationsans" => $"LiberationSans-{(suffix == "b" ? "Bold" : suffix == "i" ? "Italic" : "Regular")}.ttf",
            "notosans" => $"NotoSans-{(suffix == "b" ? "Bold" : suffix == "i" ? "Italic" : "Regular")}.ttf",
            _ => $"{familyName}{suffix}.ttf",
        };
    }
}
