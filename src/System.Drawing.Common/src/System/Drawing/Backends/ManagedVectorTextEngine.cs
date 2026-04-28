namespace System.Drawing;

internal sealed class ManagedVectorTextEngine
{
    private const int GlyphHeight = 7;
    private const int GlyphAdvance = 6;

    public void DrawString(IRenderingBackend backend, string text, float x, float y, Color color, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        float scale = ResolveScale(fontSize);
        float cursorX = x;
        float cursorY = y;
        float lineHeight = (GlyphHeight + 3) * scale;

        foreach (char raw in text)
        {
            if (raw == '\r')
            {
                continue;
            }

            if (raw == '\n')
            {
                cursorX = x;
                cursorY += lineHeight;
                continue;
            }

            DrawGlyph(backend, raw, cursorX, cursorY, scale, color);
            cursorX += GlyphAdvance * scale;
        }
    }

    public void DrawGlyph(IRenderingBackend backend, char raw, float x, float y, Color color, float fontSize)
    {
        DrawGlyph(backend, raw, x, y, ResolveScale(fontSize), color);
    }

    public void DrawStringAligned(
        IRenderingBackend backend,
        string text,
        RectangleF bounds,
        ContentAlignment alignment,
        Color color,
        float fontSize)
    {
        SizeF measured = MeasureString(text, fontSize);
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

        DrawString(backend, text, x, y, color, fontSize);
    }

    public SizeF MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        float scale = ResolveScale(fontSize);
        int lineChars = 0;
        int maxChars = 0;
        int lines = 1;

        foreach (char c in text)
        {
            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                maxChars = Math.Max(maxChars, lineChars);
                lineChars = 0;
                lines++;
                continue;
            }

            lineChars++;
        }

        maxChars = Math.Max(maxChars, lineChars);
        return new SizeF(maxChars * GlyphAdvance * scale, lines * (GlyphHeight + 3) * scale);
    }

    public float GetLineHeight(float fontSize)
        => (GlyphHeight + 3) * ResolveScale(fontSize);

    private static float ResolveScale(float fontSize)
        => MathF.Max(1f, fontSize / GlyphHeight);

    private static void DrawGlyph(IRenderingBackend backend, char raw, float x, float y, float scale, Color color)
    {
        string[] pattern = GetPattern(raw);
        for (int row = 0; row < pattern.Length; row++)
        {
            string line = pattern[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] == '1')
                {
                    backend.FillRect(x + (col * scale), y + (row * scale), scale, scale, color);
                }
            }
        }
    }

    private static string[] GetPattern(char raw)
    {
        char c = char.ToUpperInvariant(raw);
        return c switch
        {
            'A' => ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
            'B' => ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
            'C' => ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
            'D' => ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
            'E' => ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
            'F' => ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
            'G' => ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
            'H' => ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
            'I' => ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
            'J' => ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
            'K' => ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
            'L' => ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
            'M' => ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
            'N' => ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
            'O' => ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
            'P' => ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
            'Q' => ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
            'R' => ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
            'S' => ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
            'T' => ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
            'U' => ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
            'V' => ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
            'W' => ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
            'X' => ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
            'Y' => ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
            'Z' => ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
            '0' => ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
            '1' => ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
            '2' => ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
            '3' => ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
            '4' => ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
            '5' => ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
            '6' => ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
            '7' => ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
            '8' => ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
            '9' => ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
            '.' => ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
            ',' => ["00000", "00000", "00000", "00000", "01100", "00100", "01000"],
            ':' => ["00000", "01100", "01100", "00000", "01100", "01100", "00000"],
            ';' => ["00000", "01100", "01100", "00000", "01100", "00100", "01000"],
            '-' => ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
            '_' => ["00000", "00000", "00000", "00000", "00000", "00000", "11111"],
            '+' => ["00000", "00100", "00100", "11111", "00100", "00100", "00000"],
            '/' => ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
            '\\' => ["10000", "01000", "01000", "00100", "00010", "00010", "00001"],
            '(' => ["00010", "00100", "01000", "01000", "01000", "00100", "00010"],
            ')' => ["01000", "00100", "00010", "00010", "00010", "00100", "01000"],
            '[' => ["01110", "01000", "01000", "01000", "01000", "01000", "01110"],
            ']' => ["01110", "00010", "00010", "00010", "00010", "00010", "01110"],
            '&' => ["01100", "10010", "10100", "01000", "10101", "10010", "01101"],
            '!' => ["00100", "00100", "00100", "00100", "00100", "00000", "00100"],
            '?' => ["01110", "10001", "00001", "00010", "00100", "00000", "00100"],
            '\'' => ["00100", "00100", "01000", "00000", "00000", "00000", "00000"],
            '"' => ["01010", "01010", "01010", "00000", "00000", "00000", "00000"],
            ' ' => ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
            _ => ["11111", "10001", "00010", "00100", "00000", "00100", "00000"],
        };
    }
}
