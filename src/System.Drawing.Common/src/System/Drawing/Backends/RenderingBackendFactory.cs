// Rendering backend factory — the ONLY place #if BROWSER exists in the drawing pipeline.

namespace System.Drawing;

/// <summary>
/// Creates the appropriate rendering backend based on compile-time target.
/// This is the single point where the platform check occurs.
/// </summary>
internal static class RenderingBackendFactory
{
    /// <summary>
    /// Create a rendering backend.
    /// For Impeller, pass the platform backend and Impeller context.
    /// For Canvas2D, these parameters are ignored.
    /// </summary>
    public static IRenderingBackend Create(IPlatformBackend? platformBackend = null, nint impellerContext = 0)
    {
#if BROWSER
        return new Canvas2DRenderingBackend();
#else
        return new ImpellerRenderingBackend(platformBackend!, impellerContext);
#endif
    }

    /// <summary>
    /// Create a lightweight rendering backend for measurement only (no frame needed).
    /// </summary>
    public static IRenderingBackend CreateForMeasurement()
    {
#if BROWSER
        return new Canvas2DRenderingBackend();
#else
        // Use a dummy backend for measurement — no platform backend needed
        // since MeasureString only uses TypographyProvider, not the builder.
        return new ImpellerRenderingBackend(null!, nint.Zero);
#endif
    }
}
