using System.Drawing.Impeller;

namespace System.Drawing;

/// <summary>
/// Internal extensions for converting BCL types to Impeller native types.
/// </summary>
internal static class ImpellerExtensions
{
    public static ImpellerColor ToImpellerColor(this Color c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public static ImpellerPoint ToImpellerPoint(this PointF p) => new(p.X, p.Y);

    public static ImpellerRect ToImpellerRect(this Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    public static ImpellerRect ToImpellerRect(this RectangleF r) => new(r.X, r.Y, r.Width, r.Height);
}
