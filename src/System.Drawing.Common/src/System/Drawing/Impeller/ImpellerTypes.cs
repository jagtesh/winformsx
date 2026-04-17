// Blittable struct types matching Impeller's C API conventions.
// These are used directly in P/Invoke calls — no marshalling needed.
#pragma warning disable IDE1006 // Naming rule violation

using System.Runtime.InteropServices;

namespace System.Drawing.Impeller;

/// <summary>
/// 2D point in Impeller coordinate space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerPoint
{
    public float X;
    public float Y;

    public ImpellerPoint(float x, float y) { X = x; Y = y; }

    public static implicit operator ImpellerPoint((float x, float y) tuple) => new(tuple.x, tuple.y);
}

/// <summary>
/// 2D size.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerSize
{
    public float Width;
    public float Height;

    public ImpellerSize(float width, float height) { Width = width; Height = height; }
}

/// <summary>
/// 2D size with integer dimensions (int64_t).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerISize
{
    public long Width;
    public long Height;

    public ImpellerISize(long width, long height) { Width = width; Height = height; }
}

/// <summary>
/// Axis-aligned rectangle: origin (x, y) + size (width, height).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerRect
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public ImpellerRect(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }
}

/// <summary>
/// Rounded rectangle with per-corner radii.
/// Matches ImpellerRoundingRadii (ImpellerPoint per corner) in the C API.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerRoundedRect
{
    public ImpellerRect Rect;
    public ImpellerPoint TopLeft;
    public ImpellerPoint BottomLeft;
    public ImpellerPoint TopRight;
    public ImpellerPoint BottomRight;

    public static ImpellerRoundedRect Uniform(ImpellerRect rect, float radius)
    {
        var corner = new ImpellerPoint(radius, radius);
        return new ImpellerRoundedRect
        {
            Rect = rect,
            TopLeft = corner,
            TopRight = corner,
            BottomRight = corner,
            BottomLeft = corner,
        };
    }
}

/// <summary>
/// Color space for ImpellerColor.
/// </summary>
public enum ImpellerColorSpace : uint
{
    SRGB = 0,
    ExtendedSRGB = 1,
    DisplayP3 = 2,
}

/// <summary>
/// RGBA color with color space, matching Impeller's ImpellerColor struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerColor
{
    public float R;
    public float G;
    public float B;
    public float A;
    public ImpellerColorSpace ColorSpace;

    public ImpellerColor(float r, float g, float b, float a = 1.0f, ImpellerColorSpace cs = ImpellerColorSpace.SRGB)
    {
        R = r; G = g; B = b; A = a; ColorSpace = cs;
    }

    public static ImpellerColor FromArgb(byte a, byte r, byte g, byte b) =>
        new(r / 255f, g / 255f, b / 255f, a / 255f);

    public static ImpellerColor FromArgb(int argb) =>
        FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    // Common colors
    public static readonly ImpellerColor White = new(1, 1, 1, 1);
    public static readonly ImpellerColor Black = new(0, 0, 0, 1);
    public static readonly ImpellerColor Transparent = new(0, 0, 0, 0);
    public static readonly ImpellerColor Red = new(1, 0, 0, 1);
    public static readonly ImpellerColor Green = new(0, 1, 0, 1);
    public static readonly ImpellerColor Blue = new(0, 0, 1, 1);
}

/// <summary>
/// 4x4 column-major transformation matrix.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerMatrix
{
    // Column-major: m[col][row]
    public float M00, M01, M02, M03;
    public float M10, M11, M12, M13;
    public float M20, M21, M22, M23;
    public float M30, M31, M32, M33;

    public static readonly ImpellerMatrix Identity = new()
    {
        M00 = 1, M11 = 1, M22 = 1, M33 = 1,
    };

    public static ImpellerMatrix CreateTranslation(float tx, float ty)
    {
        var m = Identity;
        m.M30 = tx;
        m.M31 = ty;
        return m;
    }

    public static ImpellerMatrix CreateScale(float sx, float sy)
    {
        var m = Identity;
        m.M00 = sx;
        m.M11 = sy;
        return m;
    }
}

/// <summary>
/// Impeller drawing style: fill or stroke.
/// </summary>
public enum ImpellerDrawStyle : uint
{
    Fill = 0,
    Stroke = 1,
}

/// <summary>
/// Impeller stroke cap style.
/// </summary>
public enum ImpellerStrokeCap : uint
{
    Butt = 0,
    Round = 1,
    Square = 2,
}

/// <summary>
/// Impeller stroke join style.
/// </summary>
public enum ImpellerStrokeJoin : uint
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

/// <summary>
/// Impeller blend mode.
/// </summary>
public enum ImpellerBlendMode : uint
{
    Clear = 0,
    Source = 1,
    Destination = 2,
    SourceOver = 3,
    DestinationOver = 4,
    SourceIn = 5,
    DestinationIn = 6,
    SourceOut = 7,
    DestinationOut = 8,
    SourceAtop = 9,
    DestinationAtop = 10,
    Xor = 11,
    Plus = 12,
    Modulate = 13,
    Screen = 14,
    Overlay = 15,
    Darken = 16,
    Lighten = 17,
    ColorDodge = 18,
    ColorBurn = 19,
    HardLight = 20,
    SoftLight = 21,
    Difference = 22,
    Exclusion = 23,
    Multiply = 24,
    Hue = 25,
    Saturation = 26,
    Color = 27,
    Luminosity = 28,
}

/// <summary>
/// Clip operation.
/// </summary>
public enum ImpellerClipOp : uint
{
    Difference = 0,
    Intersect = 1,
}

/// <summary>
/// Path fill type.
/// </summary>
public enum ImpellerFillType : uint
{
    NonZero = 0,
    EvenOdd = 1,
}

// ─── Vulkan Interop Structs ─────────────────────────────────────────────

/// <summary>
/// Settings for creating a Vulkan Impeller context.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerContextVulkanSettings
{
    /// <summary>User data pointer passed back to the proc address callback.</summary>
    public nint UserData;
    /// <summary>Callback: (void* vk_instance, const char* proc_name, void* user_data) -> void*</summary>
    public nint ProcAddressCallback;
    /// <summary>Enable Vulkan validation layers (0 = false, 1 = true).</summary>
    public byte EnableVulkanValidation;
}

/// <summary>
/// Internal Vulkan handles from an Impeller Vulkan context.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerContextVulkanInfo
{
    public nint VkInstance;
    public nint VkPhysicalDevice;
    public nint VkDevice;
    public uint GraphicsQueueFamilyIndex;
}

// ─── ImpellerMapping ────────────────────────────────────────────────────

/// <summary>
/// A mapping of data bytes with an optional release callback.
/// Used for font data, texture data, etc.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ImpellerMapping
{
    public nint Data;       // const uint8_t*
    public ulong Length;    // uint64_t
    public nint OnRelease;  // ImpellerCallback (void (*)(void* user_data))
}

// ─── Pixel Format ───────────────────────────────────────────────────────

public enum ImpellerPixelFormat : uint
{
    RGBA8888 = 0,
}

// ─── Texture Descriptor ─────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct ImpellerTextureDescriptor
{
    public ImpellerPixelFormat PixelFormat;
    public ImpellerISize Size;
    public uint MipCount;
}

// ─── Typography Enums ───────────────────────────────────────────────────

public enum ImpellerFontWeight : uint
{
    W100 = 0, // Thin
    W200 = 1, // Extra-Light
    W300 = 2, // Light
    W400 = 3, // Normal/Regular
    W500 = 4, // Medium
    W600 = 5, // Semi-Bold
    W700 = 6, // Bold
    W800 = 7, // Extra-Bold
    W900 = 8, // Black
}

public enum ImpellerFontStyle : uint
{
    Normal = 0,
    Italic = 1,
}

public enum ImpellerTextAlignment : uint
{
    Left = 0,
    Right = 1,
    Center = 2,
    Justify = 3,
    Start = 4,
    End = 5,
}

public enum ImpellerTextDirection : uint
{
    RTL = 0,
    LTR = 1,
}
