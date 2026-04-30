using System.IO;
using System.Linq;

namespace System.Drawing;

internal static class FontFileResolver
{
    public static ResolvedFontFile? ResolveFontFile(string familyName, bool bold, bool italic)
    {
        string requestedFamily = NormalizeFamilyName(familyName);
        ResolvedFontFile? requested = FindInDirectories(
            familyName,
            requestedFamily,
            GetCandidateFileNames(familyName, bold, italic),
            bold,
            italic,
            substituted: false);

        if (requested is not null)
        {
            return requested;
        }

        foreach (FontSubstitution substitution in GetSubstitutions(familyName))
        {
            ResolvedFontFile? resolved = FindInDirectories(
                substitution.DisplayFamily,
                NormalizeFamilyName(substitution.DisplayFamily),
                GetCandidateFileNames(substitution.DisplayFamily, bold, italic)
                    .Concat(GetBundledCandidateFileNames(substitution.BundledFamily, bold, italic)),
                bold,
                italic,
                substituted: true);

            if (resolved is not null)
            {
                WarnSubstitution(familyName, resolved);
                return resolved;
            }
        }

        return null;
    }

    public static string? FindFontFile(string familyName, bool bold, bool italic) =>
        ResolveFontFile(familyName, bold, italic)?.Path;

    private static ResolvedFontFile? FindInDirectories(
        string displayFamily,
        string normalizedFamily,
        IEnumerable<string> candidateFileNames,
        bool bold,
        bool italic,
        bool substituted)
    {
        string[] candidates = candidateFileNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (string directory in GetSearchDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string candidate in candidates)
            {
                string direct = Path.Combine(directory, candidate);
                if (File.Exists(direct))
                {
                    return new ResolvedFontFile(direct, displayFamily, substituted);
                }
            }

            try
            {
                foreach (string file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    foreach (string candidate in candidates)
                    {
                        if (string.Equals(fileName, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            return new ResolvedFontFile(file, displayFamily, substituted);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
        }

        foreach (string directory in GetBundledFontDirectories())
        {
            foreach (string candidate in candidates)
            {
                string direct = Path.Combine(directory, candidate);
                if (File.Exists(direct))
                {
                    return new ResolvedFontFile(direct, displayFamily, substituted);
                }
            }
        }

        return null;
    }

    private static string[] GetSearchDirectories()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return
        [
            fonts,
            @"C:\Windows\Fonts",
            "/System/Library/Fonts",
            "/System/Library/Fonts/Supplemental",
            "/Library/Fonts",
            Path.Combine(profile, "Library", "Fonts"),
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(profile, ".fonts"),
            Path.Combine(profile, ".local", "share", "fonts"),
        ];
    }

    private static string[] GetBundledFontDirectories()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string assemblyDirectory = Path.GetDirectoryName(typeof(FontFileResolver).Assembly.Location) ?? baseDirectory;

        return
        [
            Path.Combine(baseDirectory, "System", "Drawing", "Fonts"),
            Path.Combine(baseDirectory, "fonts"),
            Path.Combine(assemblyDirectory, "System", "Drawing", "Fonts"),
            Path.Combine(assemblyDirectory, "fonts"),
            Path.Combine(GetRepositoryRootProbe(), "src", "System.Drawing.Common", "src", "System", "Drawing", "Fonts"),
        ];
    }

    private static string GetRepositoryRootProbe()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "System.Drawing.Common", "src", "System", "Drawing", "Fonts")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static IEnumerable<FontSubstitution> GetSubstitutions(string familyName)
    {
        string normalized = NormalizeFamilyName(familyName);

        return normalized switch
        {
            "timesnewroman" or "times" or "georgia" or "serif" => [new("Noto Serif", "NotoSerif")],
            "couriernew" or "courier" or "consolas" or "lucidaconsole" or "monospace" or "monospaced" => [new("Noto Sans Mono", "NotoSansMono")],
            "symbol" or "wingdings" or "webdings" => [new("Noto Sans", "NotoSans")],
            _ => [new("Noto Sans", "NotoSans")],
        };
    }

    private static IEnumerable<string> GetBundledCandidateFileNames(string bundledFamily, bool bold, bool italic)
    {
        string style = (bold, italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };

        yield return Path.Combine(bundledFamily, $"{bundledFamily}-{style}.ttf");

        if (bundledFamily == "NotoSansMono" && italic)
        {
            yield return Path.Combine(bundledFamily, $"{bundledFamily}-{(bold ? "Bold" : "Regular")}.ttf");
        }

        yield return Path.Combine(bundledFamily, $"{bundledFamily}-Regular.ttf");
    }

    private static string[] GetCandidateFileNames(string familyName, bool bold, bool italic)
    {
        string normalized = NormalizeFamilyName(familyName);
        string suffix = (bold, italic) switch
        {
            (true, true) => "bi",
            (true, false) => "b",
            (false, true) => "i",
            _ => string.Empty,
        };

        string family = familyName.Trim();
        return normalized switch
        {
            "segoeui" or "segoe" => [$"segoeui{suffix}.ttf", "SFNS.ttf", "Arial.ttf", "DejaVuSans.ttf"],
            "arial" => [$"arial{suffix}.ttf", bold ? "Arial Bold.ttf" : italic ? "Arial Italic.ttf" : "Arial.ttf"],
            "tahoma" => [$"tahoma{suffix}.ttf", "Tahoma.ttf", "Arial.ttf"],
            "verdana" => [$"verdana{suffix}.ttf", bold ? "Verdana Bold.ttf" : italic ? "Verdana Italic.ttf" : "Verdana.ttf"],
            "timesnewroman" => [$"times{suffix}.ttf", bold ? "Times New Roman Bold.ttf" : italic ? "Times New Roman Italic.ttf" : "Times New Roman.ttf"],
            "couriernew" => [$"cour{suffix}.ttf", "Courier.ttc", "Courier New.ttf"],
            "consolas" => [$"consola{suffix}.ttf", "SFNSMono.ttf", "Menlo.ttc"],
            "helvetica" or "helveticaneue" => ["HelveticaNeue.ttc", "Helvetica.ttf", "Arial.ttf"],
            "sfpro" or "sfsystemfont" => ["SFNS.ttf", "SFCompact.ttf", "HelveticaNeue.ttc"],
            "dejavusans" => [bold ? "DejaVuSans-Bold.ttf" : italic ? "DejaVuSans-Oblique.ttf" : "DejaVuSans.ttf"],
            "liberationsans" => [bold ? "LiberationSans-Bold.ttf" : italic ? "LiberationSans-Italic.ttf" : "LiberationSans-Regular.ttf"],
            "notosans" => [bold ? "NotoSans-Bold.ttf" : italic ? "NotoSans-Italic.ttf" : "NotoSans-Regular.ttf"],
            "notoserif" => [bold ? "NotoSerif-Bold.ttf" : italic ? "NotoSerif-Italic.ttf" : "NotoSerif-Regular.ttf"],
            "notosansmono" => [bold ? "NotoSansMono-Bold.ttf" : "NotoSansMono-Regular.ttf"],
            _ => [$"{family}.ttf", $"{family}.ttc", $"{family}.otf", "SFNS.ttf", "Arial.ttf", "DejaVuSans.ttf"],
        };
    }

    private static string NormalizeFamilyName(string familyName) =>
        familyName.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static void WarnSubstitution(string requestedFamily, ResolvedFontFile resolved)
    {
        WinFormsXCompatibilityWarning.Once(
            $"FontSubstitution:{NormalizeFamilyName(requestedFamily)}:{resolved.FamilyName}",
            $"Font '{requestedFamily}' is unavailable; WinFormsX is substituting '{resolved.FamilyName}' from '{resolved.Path}'.");
    }

    private readonly record struct FontSubstitution(string DisplayFamily, string BundledFamily);
}

internal sealed record ResolvedFontFile(string Path, string FamilyName, bool IsSubstitution);
