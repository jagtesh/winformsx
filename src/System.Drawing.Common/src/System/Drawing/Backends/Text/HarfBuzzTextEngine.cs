using System.Collections.Concurrent;

namespace System.Drawing;

internal sealed class HarfBuzzTextEngine : ITextEngine
{
    private static readonly ConcurrentDictionary<TextShapeKey, Lazy<HarfBuzzTextShaper>> s_shapers = new();

    private readonly ManagedGlyphPainter _glyphPainter = new();

    public void DrawString(
        IRenderingBackend backend,
        string text,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        DrawShapedString(backend, GetShaper(fontFamily, fontSize, bold, italic), text, x, y, color);
    }

    public void DrawStringAligned(
        IRenderingBackend backend,
        string text,
        RectangleF bounds,
        ContentAlignment alignment,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        SizeF measured = MeasureString(text, fontFamily, fontSize, bold, italic);
        float x = bounds.X;
        float y = bounds.Y;

        if (alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter)
        {
            x += MathF.Max(0f, (bounds.Width - measured.Width) / 2f);
        }
        else if (alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight)
        {
            x += MathF.Max(0f, bounds.Width - measured.Width);
        }

        if (alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight)
        {
            y += MathF.Max(0f, (bounds.Height - measured.Height) / 2f);
        }
        else if (alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight)
        {
            y += MathF.Max(0f, bounds.Height - measured.Height);
        }

        DrawString(backend, text, x, y, color, fontFamily, fontSize, bold, italic);
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        return GetShaper(fontFamily, fontSize, bold, italic).Measure(text);
    }

    private static HarfBuzzTextShaper GetShaper(string fontFamily, float fontSize, bool bold, bool italic)
    {
        TextShapeKey key = new(fontFamily, MathF.Max(1f, fontSize), bold, italic);
        return s_shapers.GetOrAdd(key, static value => new Lazy<HarfBuzzTextShaper>(() => CreateShaper(value))).Value;
    }

    private static HarfBuzzTextShaper CreateShaper(TextShapeKey key)
    {
        ResolvedFontFile? font = FontFileResolver.ResolveFontFile(key.FontFamily, key.Bold, key.Italic);
        return font is null
            ? throw new InvalidOperationException($"No font file could be resolved for '{key.FontFamily}'.")
            : new HarfBuzzTextShaper(font.Path, font.FamilyName, key.FontSize, key.Bold, key.Italic);
    }

    private void DrawShapedString(
        IRenderingBackend backend,
        HarfBuzzTextShaper shaper,
        string text,
        float x,
        float y,
        Color color)
    {
        string normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        float lineHeight = MathF.Max(1f, shaper.GetMetrics().LineHeight);
        float currentY = y;
        List<RectangleF>? impellerTextRun = backend is ImpellerRenderingBackend ? [] : null;

        foreach (string line in lines)
        {
            if (UseNativeImpellerText() && backend is ImpellerRenderingBackend nativeTextBackend)
            {
                string nativeLine = SanitizeForNativeText(line);
                float width = MathF.Max(1f, shaper.Measure(line).Width + 2f);
                if (nativeTextBackend.DrawNativeText(
                    nativeLine,
                    x,
                    currentY,
                    width,
                    color,
                    shaper.FamilyName,
                    shaper.FontSize,
                    shaper.Bold,
                    shaper.Italic,
                    lineHeight))
                {
                    currentY += lineHeight;
                    continue;
                }
            }

            float cursorX = x;
            foreach (ShapedGlyph glyph in shaper.Shape(line))
            {
                if (glyph.Cluster < line.Length)
                {
                    char character = line[(int)glyph.Cluster];
                    float glyphX = cursorX + glyph.XOffset;
                    float glyphY = currentY + glyph.YOffset;
                    float advance = MathF.Max(1f, glyph.XAdvance);

                    if (impellerTextRun is not null)
                    {
                        _glyphPainter.AppendGlyphRectangles(impellerTextRun, character, glyphX, glyphY, advance, lineHeight);
                    }
                    else
                    {
                        _glyphPainter.DrawGlyph(backend, character, glyphX, glyphY, advance, lineHeight, color);
                    }
                }

                cursorX += glyph.XAdvance;
            }

            currentY += lineHeight;
        }

        if (impellerTextRun is { Count: > 0 } && backend is ImpellerRenderingBackend impellerBackend)
        {
            impellerBackend.FillRectPath(impellerTextRun, color);
        }
    }

    private static string SanitizeForNativeText(string text)
    {
        bool changed = false;
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is < ' ' or > '~')
            {
                chars[i] = '?';
                changed = true;
            }
        }

        if (changed)
        {
            WinFormsXCompatibilityWarning.Once(
                "ImpellerText.NonAsciiSanitized",
                "Impeller native text is currently limited to ASCII-safe UI text; unsupported characters were substituted before rendering.");
        }

        return changed ? new string(chars) : text;
    }

    private static bool UseNativeImpellerText() =>
        string.Equals(
            Environment.GetEnvironmentVariable("WINFORMSX_USE_NATIVE_IMPELLER_TEXT"),
            "1",
            StringComparison.Ordinal);

    private readonly record struct TextShapeKey(string FontFamily, float FontSize, bool Bold, bool Italic);
}
