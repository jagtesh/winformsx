using System.IO;

namespace System.Drawing;

internal static class FontFileResolver
{
    public static string? FindFontFile(string familyName, bool bold, bool italic)
    {
        string[] candidates = GetCandidateFileNames(familyName, bold, italic);
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
                    return direct;
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
                            return file;
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

        return null;
    }

    private static string[] GetSearchDirectories()
    {
        if (OperatingSystem.IsMacOS())
        {
            return
            [
                "/System/Library/Fonts",
                "/System/Library/Fonts/Supplemental",
                "/Library/Fonts",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Fonts"),
            ];
        }

        if (OperatingSystem.IsWindows())
        {
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            return
            [
                fonts,
                @"C:\Windows\Fonts",
            ];
        }

        return
        [
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "fonts"),
        ];
    }

    private static string[] GetCandidateFileNames(string familyName, bool bold, bool italic)
    {
        string normalized = familyName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
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
            _ => [$"{family}.ttf", $"{family}.ttc", $"{family}.otf", "SFNS.ttf", "Arial.ttf", "DejaVuSans.ttf"],
        };
    }
}
