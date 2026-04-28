namespace System.Drawing;

internal interface ITextEngine
{
    void DrawString(
        IRenderingBackend backend,
        string text,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic);

    void DrawStringAligned(
        IRenderingBackend backend,
        string text,
        RectangleF bounds,
        ContentAlignment alignment,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic);

    SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic);
}
