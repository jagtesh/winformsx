using System.Collections.Concurrent;
using System.IO;

namespace System.Drawing;

internal sealed class HarfBuzzTextEngine : ITextEngine
{
    private static readonly ConcurrentDictionary<TextShapeKey, Lazy<HarfBuzzTextShaper?>> s_shapers = new();

    private readonly ManagedVectorTextEngine _primitivePainter = new();
    private volatile bool _harfBuzzUnavailable;

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
        HarfBuzzTextShaper? shaper = GetShaper(fontFamily, fontSize, bold, italic);
        if (shaper is not null)
        {
            try
            {
                DrawShapedString(backend, shaper, text, x, y, color, fontSize);
                return;
            }
            catch (Exception ex) when (IsHarfBuzzLoadFailure(ex))
            {
                _harfBuzzUnavailable = true;
            }
        }

        _primitivePainter.DrawString(backend, text, x, y, color, fontSize);
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

        HarfBuzzTextShaper? shaper = GetShaper(fontFamily, fontSize, bold, italic);
        if (shaper is not null)
        {
            try
            {
                return shaper.Measure(text);
            }
            catch (Exception ex) when (IsHarfBuzzLoadFailure(ex))
            {
                _harfBuzzUnavailable = true;
            }
        }

        return _primitivePainter.MeasureString(text, fontSize);
    }

    private HarfBuzzTextShaper? GetShaper(string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (_harfBuzzUnavailable)
        {
            return null;
        }

        TextShapeKey key = new(fontFamily, MathF.Max(1f, fontSize), bold, italic);
        try
        {
            return s_shapers.GetOrAdd(key, static value => new Lazy<HarfBuzzTextShaper?>(() => CreateShaper(value))).Value;
        }
        catch (Exception ex) when (IsHarfBuzzLoadFailure(ex))
        {
            _harfBuzzUnavailable = true;
            return null;
        }
    }

    private static HarfBuzzTextShaper? CreateShaper(TextShapeKey key)
    {
        string? fontPath = FontFileResolver.FindFontFile(key.FontFamily, key.Bold, key.Italic);
        return fontPath is null ? null : new HarfBuzzTextShaper(fontPath, key.FontSize);
    }

    private void DrawShapedString(
        IRenderingBackend backend,
        HarfBuzzTextShaper shaper,
        string text,
        float x,
        float y,
        Color color,
        float fontSize)
    {
        string normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        float lineHeight = MathF.Max(_primitivePainter.GetLineHeight(fontSize), shaper.GetMetrics().LineHeight);
        float currentY = y;

        foreach (string line in lines)
        {
            float cursorX = x;
            foreach (ShapedGlyph glyph in shaper.Shape(line))
            {
                if (glyph.Cluster < line.Length)
                {
                    _primitivePainter.DrawGlyph(
                        backend,
                        line[(int)glyph.Cluster],
                        cursorX + glyph.XOffset,
                        currentY + glyph.YOffset,
                        color,
                        fontSize);
                }

                cursorX += glyph.XAdvance;
            }

            currentY += lineHeight;
        }
    }

    private static bool IsHarfBuzzLoadFailure(Exception ex)
    {
        return ex is DllNotFoundException
            or EntryPointNotFoundException
            or BadImageFormatException
            or FileNotFoundException;
    }

    private readonly record struct TextShapeKey(string FontFamily, float FontSize, bool Bold, bool Italic);
}
