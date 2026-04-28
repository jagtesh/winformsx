// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Buffers.Binary;
using System.Drawing.Imaging;
#if NET9_0_OR_GREATER
using System.Drawing.Imaging.Effects;
#endif
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System.Drawing;

[Editor($"System.Drawing.Design.BitmapEditor, {AssemblyRef.SystemDrawingDesign}",
        $"System.Drawing.Design.UITypeEditor, {AssemblyRef.SystemDrawing}")]
[Serializable]
[TypeForwardedFrom(AssemblyRef.SystemDrawing)]
public sealed unsafe class Bitmap : Image, IPointer<GpBitmap>
{
    private static readonly Color s_defaultTransparentColor = Color.LightGray;
    private static readonly object s_bitmapLockGate = new();
    private static readonly Dictionary<IntPtr, Bitmap> s_syntheticIconHandles = [];
    private static readonly Dictionary<IntPtr, ManagedBitmapLock> s_bitmapLocks = [];
    private static int s_nextSyntheticIconHandle = 1;
    private int[]? _pixels;

    private Bitmap() { }

    internal Bitmap(GpBitmap* ptr) => SetNativeImage((GpImage*)ptr);

    public Bitmap(string filename) : this(filename, useIcm: false) { }

    public Bitmap(string filename, bool useIcm)
    {
        filename = Path.GetFullPath(filename);
        InitializeManagedBitmap(File.ReadAllBytes(filename));
    }

    public Bitmap(Stream stream) : this(stream, false)
    {
    }

    public Bitmap(Stream stream, bool useIcm)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream memory = new();
        stream.CopyTo(memory);
        InitializeManagedBitmap(memory.ToArray());
    }

    public Bitmap(Type type, string resource) : this(GetResourceStream(type, resource))
    {
    }

    private static Stream GetResourceStream(Type type, string resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);

        return type.Module.Assembly.GetManifestResourceStream(type, resource)
            ?? throw new ArgumentException(SR.Format(SR.ResourceNotFound, type, resource));
    }

    public Bitmap(int width, int height) : this(width, height, PixelFormat.Format32bppArgb)
    {
    }

    public Bitmap(int width, int height, Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        InitializeManagedBitmap(width, height, PixelFormat.Format32bppArgb);
        GC.KeepAlive(g);
    }

    public Bitmap(int width, int height, int stride, PixelFormat format, IntPtr scan0)
    {
        InitializeManagedBitmap(width, height, format);

        if (scan0 != IntPtr.Zero && _pixels is not null)
        {
            CopyScan0(width, height, stride, scan0);
        }
    }

    public Bitmap(int width, int height, PixelFormat format)
    {
        InitializeManagedBitmap(width, height, format);
    }

    public Bitmap(Image original) : this(original, original.Width, original.Height)
    {
    }

    public Bitmap(Image original, Size newSize) : this(original, newSize.Width, newSize.Height)
    {
    }

    public Bitmap(Image original, int width, int height) : this(width, height, PixelFormat.Format32bppArgb)
    {
        ArgumentNullException.ThrowIfNull(original);
        using var g = Graphics.FromImage(this);
        g.Clear(Color.Transparent);
        g.DrawImage(original, 0, 0, width, height);
    }

    private Bitmap(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    nint IPointer<GpBitmap>.Pointer => (nint)((Image)this).Pointer();

    private void InitializeManagedBitmap(int width, int height, PixelFormat format)
    {
        SetManagedImage(width, height, format);
        _pixels = new int[checked(width * height)];
        Array.Fill(_pixels, Color.Black.ToArgb());
    }

    internal ReadOnlySpan<int> ManagedPixels => _pixels;

    internal Bitmap CloneManagedBitmap()
    {
        ThrowIfDisposed();

        Bitmap clone = new(Width, Height, PixelFormat);
        clone.SetManagedImage(Width, Height, PixelFormat, HorizontalResolution, VerticalResolution, RawFormat);
        if (_pixels is not null && clone._pixels is not null)
        {
            _pixels.AsSpan().CopyTo(clone._pixels);
        }

        clone.Palette = Palette;
        foreach (Imaging.PropertyItem item in PropertyItems)
        {
            clone.SetPropertyItem(item);
        }

        return clone;
    }

    private void InitializeManagedBitmap(ReadOnlySpan<byte> data)
    {
        if (TryInitializeIcon(data) || TryInitializePng(data) || TryInitializeBmp(data) || TryInitializeJpeg(data))
        {
            return;
        }

        throw new ArgumentException(SR.InvalidImage);
    }

    private bool TryInitializePng(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length < 33 || !data[..8].SequenceEqual(signature) || !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }

        int width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4)));
        int height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4)));
        byte colorType = data[25];
        PixelFormat format = colorType switch
        {
            6 => PixelFormat.Format32bppArgb,
            2 => PixelFormat.Format24bppRgb,
            3 => PixelFormat.Format8bppIndexed,
            0 => PixelFormat.Format8bppIndexed,
            _ => PixelFormat.Format32bppArgb
        };

        InitializeManagedBitmap(width, height, format);
        SetManagedImage(width, height, format, rawFormat: ImageFormat.Png);
        SetManagedPropertyItem(CreatePropertyItem(771, 1, [0]));
        return true;
    }

    private bool TryInitializeIcon(ReadOnlySpan<byte> data)
    {
        if (data.Length < 22
            || BinaryPrimitives.ReadUInt16LittleEndian(data[..2]) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2, 2)) != 1
            || BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2)) == 0)
        {
            return false;
        }

        uint bytesInResource = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(14, 4));
        uint imageOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(18, 4));
        if (bytesInResource == 0 || imageOffset >= data.Length || imageOffset + bytesInResource > data.Length)
        {
            return false;
        }

        int width = data[6] == 0 ? 256 : data[6];
        int height = data[7] == 0 ? 256 : data[7];
        InitializeManagedBitmap(width, height, PixelFormat.Format32bppArgb);
        SetManagedImage(width, height, PixelFormat.Format32bppArgb, rawFormat: ImageFormat.Icon);
        return true;
    }

    private bool TryInitializeBmp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 30 || data[0] != (byte)'B' || data[1] != (byte)'M')
        {
            return false;
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(14, 4));
        if (headerSize < 12 || data.Length < 14 + headerSize)
        {
            throw new ArgumentException(SR.InvalidImage);
        }

        int width;
        int height;
        ushort bitCount;
        if (headerSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(18, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(20, 2));
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(24, 2));
        }
        else
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(18, 4));
            height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(22, 4)));
            bitCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(28, 2));
        }

        PixelFormat format = bitCount switch
        {
            1 => PixelFormat.Format1bppIndexed,
            4 => PixelFormat.Format4bppIndexed,
            8 => PixelFormat.Format8bppIndexed,
            16 => PixelFormat.Format16bppRgb555,
            24 => PixelFormat.Format24bppRgb,
            32 => PixelFormat.Format32bppRgb,
            _ => PixelFormat.Format32bppArgb
        };

        InitializeManagedBitmap(width, height, format);
        SetManagedImage(width, height, format, rawFormat: ImageFormat.Bmp);
        return true;
    }

    private bool TryInitializeJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        int offset = 2;
        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            byte marker = data[offset + 1];
            offset += 2;
            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            if (offset + 2 > data.Length)
            {
                break;
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (length < 2 || offset + length > data.Length)
            {
                break;
            }

            ReadOnlySpan<byte> segment = data.Slice(offset + 2, length - 2);
            if (marker == 0xFE)
            {
                SetManagedPropertyItem(CreatePropertyItem(0x9286, 2, segment.ToArray()));
            }
            else if (marker == 0xDB)
            {
                ReadQuantizationTables(segment);
            }
            else if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                int height = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(1, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(3, 2));
                InitializeManagedBitmap(width, height, PixelFormat.Format24bppRgb);
                SetManagedImage(width, height, PixelFormat.Format24bppRgb, rawFormat: ImageFormat.Jpeg);
            }

            offset += length;
        }

        return RawFormat.Guid == ImageFormat.Jpeg.Guid;
    }

    private void ReadQuantizationTables(ReadOnlySpan<byte> segment)
    {
        ReadOnlySpan<byte> zigZagToNaturalOrder =
        [
            0, 1, 5, 6, 14, 15, 27, 28,
            2, 4, 7, 13, 16, 26, 29, 42,
            3, 8, 12, 17, 25, 30, 41, 43,
            9, 11, 18, 24, 31, 40, 44, 53,
            10, 19, 23, 32, 39, 45, 52, 54,
            20, 22, 33, 38, 46, 51, 55, 60,
            21, 34, 37, 47, 50, 56, 59, 61,
            35, 36, 48, 49, 57, 58, 62, 63
        ];

        int offset = 0;
        Imaging.PropertyItem? luminance = null;
        Imaging.PropertyItem? chrominance = null;
        while (offset < segment.Length)
        {
            byte info = segment[offset++];
            int precision = info >> 4;
            int tableId = info & 0x0F;
            int valueSize = precision == 0 ? 1 : 2;
            int tableLength = 64 * valueSize;
            if (offset + tableLength > segment.Length)
            {
                return;
            }

            byte[] value = new byte[128];
            for (int i = 0; i < 64; i++)
            {
                int sourceIndex = zigZagToNaturalOrder[i] * valueSize;
                ushort quantized = valueSize == 1
                    ? segment[offset + sourceIndex]
                    : BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(offset + sourceIndex, 2));
                BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(i * 2, 2), quantized);
            }

            if (tableId == 0)
            {
                luminance = CreatePropertyItem(0x5090, 3, value);
            }
            else if (tableId == 1)
            {
                chrominance = CreatePropertyItem(0x5091, 3, value);
            }

            offset += tableLength;
        }

        if (chrominance is not null)
        {
            SetManagedPropertyItem(chrominance);
        }

        if (luminance is not null)
        {
            SetManagedPropertyItem(luminance);
        }
    }

    private static Imaging.PropertyItem CreatePropertyItem(int id, short type, byte[] value) =>
        new()
        {
            Id = id,
            Len = value.Length,
            Type = type,
            Value = value
        };

    private void CopyScan0(int width, int height, int stride, IntPtr scan0)
    {
        int bytesPerPixel = Image.GetPixelFormatSize(PixelFormat) / 8;
        if (bytesPerPixel < 3)
        {
            return;
        }

        byte* source = (byte*)scan0;
        for (int y = 0; y < height; y++)
        {
            byte* row = source + (y * stride);
            for (int x = 0; x < width; x++)
            {
                byte* pixel = row + (x * bytesPerPixel);
                int alpha = bytesPerPixel >= 4 ? pixel[3] : 255;
                _pixels![y * width + x] = Color.FromArgb(alpha, pixel[2], pixel[1], pixel[0]).ToArgb();
            }
        }
    }

    public static Bitmap FromHicon(IntPtr hicon)
    {
        if (hicon == IntPtr.Zero)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        lock (s_bitmapLockGate)
        {
            if (s_syntheticIconHandles.TryGetValue(hicon, out Bitmap? bitmap))
            {
                return bitmap.CloneManagedBitmap();
            }
        }

        throw new ArgumentException(SR.GdiplusInvalidParameter);
    }

    public static Bitmap FromResource(IntPtr hinstance, string bitmapName)
    {
        ArgumentNullException.ThrowIfNull(bitmapName);
        throw new ArgumentException(SR.GdiplusInvalidParameter);
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IntPtr GetHbitmap() => GetHbitmap(Color.LightGray);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IntPtr GetHbitmap(Color background)
    {
        ThrowIfDisposed();
        if (Width >= short.MaxValue || Height >= short.MaxValue)
        {
            throw new ArgumentException(SR.GdiplusInvalidSize);
        }

        GC.KeepAlive(background);
        return CreateSyntheticBitmapHandle();
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IntPtr GetHicon()
    {
        ThrowIfDisposed();
        if (PixelFormat == PixelFormat.Format16bppGrayScale)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        return CreateSyntheticBitmapHandle();
    }

    public Bitmap Clone(RectangleF rect, PixelFormat format)
    {
        if (rect.Width == 0 || rect.Height == 0)
        {
            throw new ArgumentException(SR.Format(SR.GdiplusInvalidRectangle, rect.ToString()));
        }

        Rectangle rounded = Rectangle.Round(rect);
        return CloneManagedBitmapArea(rounded, format);
    }

    public void MakeTransparent()
    {
        Color transparent = s_defaultTransparentColor;
        if (Height > 0 && Width > 0)
        {
            transparent = GetPixel(0, Size.Height - 1);
        }

        if (transparent.A < 255)
        {
            // It's already transparent, and if we proceeded, we will do something
            // unintended like making black transparent
            return;
        }

        MakeTransparent(transparent);
    }

    public void MakeTransparent(Color transparentColor)
    {
        if (RawFormat.Guid == ImageFormat.Icon.Guid)
        {
            throw new InvalidOperationException(SR.CantMakeIconTransparent);
        }

        Size size = Size;
        if (_pixels is null)
        {
            throw new NotSupportedException($"{nameof(MakeTransparent)} requires a managed bitmap backing store.");
        }

        int transparentRgb = transparentColor.ToArgb() & 0x00FFFFFF;
        for (int i = 0; i < _pixels.Length; i++)
        {
            if ((_pixels[i] & 0x00FFFFFF) == transparentRgb)
            {
                _pixels[i] = 0;
            }
        }

        SetManagedImage(size.Width, size.Height, PixelFormat.Format32bppArgb, HorizontalResolution, VerticalResolution, RawFormat);
    }

    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format) =>
        LockBits(rect, flags, format, new());

    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format, BitmapData bitmapData)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);

        ThrowIfDisposed();
        lock (s_bitmapLockGate)
        {
            foreach (ManagedBitmapLock activeLock in s_bitmapLocks.Values)
            {
                if (ReferenceEquals(activeLock.Bitmap, this))
                {
                    throw new InvalidOperationException(SR.GdiplusObjectBusy);
                }
            }
        }

        ValidateLockBits(rect, flags, format);
        if (format == PixelFormat.Format16bppGrayScale && flags == ImageLockMode.WriteOnly)
        {
            bitmapData.Width = rect.Width;
            bitmapData.Height = rect.Height;
            bitmapData.PixelFormat = format;
            bitmapData.Stride = GetStride(rect.Width, format);
            bitmapData.Reserved = (int)flags | (format == PixelFormat ? 0 : 0x10000);
            bitmapData.Scan0 = IntPtr.Zero;
            return bitmapData;
        }

        int stride = GetStride(rect.Width, format);
        byte[] buffer = new byte[checked(stride * rect.Height)];
        if ((flags & ImageLockMode.WriteOnly) == 0)
        {
            CopyPixelsToBuffer(rect, format, stride, buffer);
        }

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        bitmapData.Width = rect.Width;
        bitmapData.Height = rect.Height;
        bitmapData.PixelFormat = format;
        bitmapData.Stride = stride;
        bitmapData.Scan0 = handle.AddrOfPinnedObject();
        bitmapData.Reserved = (int)flags | (format == PixelFormat ? 0 : 0x10000);

        lock (s_bitmapLockGate)
        {
            s_bitmapLocks.Add(bitmapData.Scan0, new ManagedBitmapLock(this, rect, flags, format, stride, buffer, handle));
        }

        return bitmapData;
    }

    public void UnlockBits(BitmapData bitmapdata)
    {
        ArgumentNullException.ThrowIfNull(bitmapdata);

        if (bitmapdata.PixelFormat == PixelFormat.Format16bppGrayScale)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        ManagedBitmapLock activeLock;
        lock (s_bitmapLockGate)
        {
            if (!s_bitmapLocks.Remove(bitmapdata.Scan0, out activeLock!))
            {
                throw new ArgumentException(SR.GdiplusInvalidParameter);
            }
        }

        try
        {
            if ((activeLock.Flags & ImageLockMode.WriteOnly) != 0)
            {
                activeLock.Bitmap.CopyBufferToPixels(activeLock.Rect, activeLock.Format, activeLock.Stride, activeLock.Buffer);
            }
        }
        finally
        {
            activeLock.Handle.Free();
            bitmapdata.Scan0 = IntPtr.Zero;
        }
    }

    public Color GetPixel(int x, int y)
    {
        ThrowIfDisposed();
        ThrowIfGetSetPixelUnsupported();

        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), SR.ValidRangeX);
        }

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), SR.ValidRangeY);
        }

        return _pixels is null
            ? throw new NotSupportedException($"{nameof(GetPixel)} requires a managed bitmap backing store.")
            : Color.FromArgb(_pixels[(y * Width) + x]);
    }

    public void SetPixel(int x, int y, Color color)
    {
        ThrowIfDisposed();
        ThrowIfGetSetPixelUnsupported();

        if ((PixelFormat & PixelFormat.Indexed) != 0)
        {
            throw new InvalidOperationException(SR.GdiplusCannotSetPixelFromIndexedPixelFormat);
        }

        if (x < 0 || x >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), SR.ValidRangeX);
        }

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), SR.ValidRangeY);
        }

        if (_pixels is null)
        {
            throw new NotSupportedException($"{nameof(SetPixel)} requires a managed bitmap backing store.");
        }

        _pixels[(y * Width) + x] = color.ToArgb();
    }

    public void SetResolution(float xDpi, float yDpi)
    {
        ThrowIfDisposed();
        SetManagedImage(Width, Height, PixelFormat, xDpi, yDpi);
    }

    private void ThrowIfGetSetPixelUnsupported()
    {
        if (PixelFormat == PixelFormat.Format16bppGrayScale)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }
    }

    public Bitmap Clone(Rectangle rect, PixelFormat format)
    {
        if (rect.Width == 0 || rect.Height == 0)
        {
            throw new ArgumentException(SR.Format(SR.GdiplusInvalidRectangle, rect.ToString()));
        }

        return CloneManagedBitmapArea(rect, format);
    }

    private Bitmap CloneManagedBitmapArea(Rectangle rect, PixelFormat format)
    {
        ThrowIfDisposed();
        if (!IsSupportedManagedPixelFormat(format) || PixelFormat == PixelFormat.Format16bppGrayScale)
        {
            throw Status.OutOfMemory.GetException();
        }

        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 || rect.Right > Width || rect.Bottom > Height)
        {
            throw Status.OutOfMemory.GetException();
        }

        if (_pixels is null)
        {
            throw new NotSupportedException($"{nameof(Clone)} requires a managed bitmap backing store.");
        }

        Bitmap clone = new(rect.Width, rect.Height, format);
        clone.SetManagedImage(rect.Width, rect.Height, format, HorizontalResolution, VerticalResolution, RawFormat);
        for (int y = 0; y < rect.Height; y++)
        {
            for (int x = 0; x < rect.Width; x++)
            {
                int argb = _pixels[((rect.Y + y) * Width) + rect.X + x];
                if (!Image.IsAlphaPixelFormat(format))
                {
                    Color color = Color.FromArgb(argb);
                    argb = Color.FromArgb(255, color.R, color.G, color.B).ToArgb();
                }

                clone._pixels![y * rect.Width + x] = argb;
            }
        }

        return clone;
    }

    private static bool IsSupportedManagedPixelFormat(PixelFormat format) =>
        format is PixelFormat.Format1bppIndexed
            or PixelFormat.Format4bppIndexed
            or PixelFormat.Format8bppIndexed
            or PixelFormat.Format16bppRgb555
            or PixelFormat.Format16bppRgb565
            or PixelFormat.Format16bppArgb1555
            or PixelFormat.Format24bppRgb
            or PixelFormat.Format32bppRgb
            or PixelFormat.Format32bppArgb
            or PixelFormat.Format32bppPArgb
            or PixelFormat.Format48bppRgb
            or PixelFormat.Format64bppArgb
            or PixelFormat.Format64bppPArgb;

    private static int GetStride(int width, PixelFormat format)
    {
        int bitsPerPixel = Image.GetPixelFormatSize(format);
        return checked((((width * bitsPerPixel) + 31) / 32) * 4);
    }

    private static void ValidateLockBitsCore(Rectangle rect, ImageLockMode flags, PixelFormat format)
    {
        if (flags is not (ImageLockMode.ReadOnly or ImageLockMode.WriteOnly or ImageLockMode.ReadWrite))
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        if (!IsSupportedManagedPixelFormat(format) || format == PixelFormat.Format16bppGrayScale)
        {
            if (format == PixelFormat.Format16bppGrayScale && flags == ImageLockMode.WriteOnly)
            {
                return;
            }

            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        if (rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }
    }

    private void ValidateLockBits(Rectangle rect, ImageLockMode flags, PixelFormat format)
    {
        ValidateLockBitsCore(rect, flags, format);
        if (rect.Right > Width || rect.Bottom > Height)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }
    }

    private IntPtr CreateSyntheticBitmapHandle()
    {
        Bitmap handleBitmap = CloneManagedBitmapArea(new Rectangle(0, 0, Width, Height), PixelFormat.Format32bppArgb);
        lock (s_bitmapLockGate)
        {
            IntPtr handle = new(s_nextSyntheticIconHandle++);
            s_syntheticIconHandles.Add(handle, handleBitmap);
            return handle;
        }
    }

    private void CopyPixelsToBuffer(Rectangle rect, PixelFormat format, int stride, byte[] buffer)
    {
        if (_pixels is null)
        {
            throw new NotSupportedException($"{nameof(LockBits)} requires a managed bitmap backing store.");
        }

        for (int y = 0; y < rect.Height; y++)
        {
            Span<byte> row = buffer.AsSpan(y * stride, stride);
            for (int x = 0; x < rect.Width; x++)
            {
                Color color = Color.FromArgb(_pixels[((rect.Y + y) * Width) + rect.X + x]);
                WritePixel(row, x, format, color);
            }
        }
    }

    private void CopyBufferToPixels(Rectangle rect, PixelFormat format, int stride, byte[] buffer)
    {
        if (_pixels is null)
        {
            throw new NotSupportedException($"{nameof(UnlockBits)} requires a managed bitmap backing store.");
        }

        for (int y = 0; y < rect.Height; y++)
        {
            ReadOnlySpan<byte> row = buffer.AsSpan(y * stride, stride);
            for (int x = 0; x < rect.Width; x++)
            {
                _pixels[((rect.Y + y) * Width) + rect.X + x] = ReadPixel(row, x, format).ToArgb();
            }
        }
    }

    private static void WritePixel(Span<byte> row, int x, PixelFormat format, Color color)
    {
        switch (format)
        {
            case PixelFormat.Format1bppIndexed:
                if (color.GetBrightness() >= 0.5f)
                {
                    row[x >> 3] |= (byte)(0x80 >> (x & 7));
                }

                break;
            case PixelFormat.Format4bppIndexed:
                int nibble = (int)Math.Round(color.GetBrightness() * 15);
                if ((x & 1) == 0)
                {
                    row[x >> 1] = (byte)((row[x >> 1] & 0x0F) | (nibble << 4));
                }
                else
                {
                    row[x >> 1] = (byte)((row[x >> 1] & 0xF0) | nibble);
                }

                break;
            case PixelFormat.Format8bppIndexed:
                row[x] = (byte)Math.Round(color.GetBrightness() * 255);
                break;
            case PixelFormat.Format24bppRgb:
                row[(x * 3) + 0] = color.B;
                row[(x * 3) + 1] = color.G;
                row[(x * 3) + 2] = color.R;
                break;
            default:
                row[(x * 4) + 0] = color.B;
                row[(x * 4) + 1] = color.G;
                row[(x * 4) + 2] = color.R;
                row[(x * 4) + 3] = Image.IsAlphaPixelFormat(format) ? color.A : (byte)255;
                break;
        }
    }

    private static Color ReadPixel(ReadOnlySpan<byte> row, int x, PixelFormat format)
    {
        return format switch
        {
            PixelFormat.Format1bppIndexed => ((row[x >> 3] & (0x80 >> (x & 7))) == 0) ? Color.Black : Color.White,
            PixelFormat.Format4bppIndexed => Color.FromArgb(255, GetNibble(row[x >> 1], x) * 17, GetNibble(row[x >> 1], x) * 17, GetNibble(row[x >> 1], x) * 17),
            PixelFormat.Format8bppIndexed => Color.FromArgb(255, row[x], row[x], row[x]),
            PixelFormat.Format24bppRgb => Color.FromArgb(255, row[(x * 3) + 2], row[(x * 3) + 1], row[(x * 3) + 0]),
            _ => Color.FromArgb(
                Image.IsAlphaPixelFormat(format) ? row[(x * 4) + 3] : 255,
                row[(x * 4) + 2],
                row[(x * 4) + 1],
                row[(x * 4) + 0])
        };
    }

    private static int GetNibble(byte value, int x) => (x & 1) == 0 ? value >> 4 : value & 0x0F;

    private sealed record ManagedBitmapLock(
        Bitmap Bitmap,
        Rectangle Rect,
        ImageLockMode Flags,
        PixelFormat Format,
        int Stride,
        byte[] Buffer,
        GCHandle Handle);

#if NET9_0_OR_GREATER
    /// <summary>
    ///  Alters the bitmap by applying the given <paramref name="effect"/>.
    /// </summary>
    /// <param name="effect">The effect to apply.</param>
    /// <param name="area">The area to apply to, or <see cref="Rectangle.Empty"/> for the entire image.</param>
    public void ApplyEffect(Effect effect, Rectangle area = default)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ThrowIfDisposed();
        if (!area.IsEmpty && (area.X < 0 || area.Y < 0 || area.Right > Width || area.Bottom > Height))
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        GC.KeepAlive(this);
        GC.KeepAlive(effect);
        throw new NotSupportedException("Bitmap effects are not implemented in the managed drawing PAL yet.");
    }

    /// <summary>
    ///  Converts the bitmap to the specified <paramref name="format"/> using the given <paramref name="ditherType"/>.
    ///  The original pixel data is replaced with the new format.
    /// </summary>
    /// <param name="format">
    ///  <para>
    ///   The new pixel format. <see cref="PixelFormat.Format16bppGrayScale"/> is not supported.
    ///  </para>
    /// </param>
    /// <param name="ditherType">
    ///  <para>
    ///   The dithering algorithm. Pass <see cref="DitherType.None"/> when the conversion does not reduce the bit depth
    ///   of the pixel data.
    ///  </para>
    ///  <para>
    ///   This must be <see cref="DitherType.Solid"/> or <see cref="DitherType.ErrorDiffusion"/> if the <paramref name="paletteType"/>
    ///   is <see cref="PaletteType.Custom"/> or <see cref="PaletteType.FixedBlackAndWhite"/>.
    ///  </para>
    /// </param>
    /// <param name="paletteType">
    ///  <para>
    ///   The palette type to use when the pixel format is indexed. Ignored for non-indexed pixel formats.
    ///  </para>
    /// </param>
    /// <param name="palette">
    ///  <para>
    ///   Pointer to a <see cref="ColorPalette"/> that specifies the palette whose indexes are stored in the pixel data
    ///   of the converted bitmap. This must be specified for indexed pixel formats.
    ///  </para>
    ///  <para>
    ///   This palette (called the actual palette) does not have to have the type specified by
    ///   the <paramref name="paletteType"/> parameter. The <paramref name="paletteType"/> parameter specifies a standard
    ///   palette that can be used by any of the ordered or spiral dithering algorithms. If the actual palette has a type
    ///   other than that specified by the <paramref name="paletteType"/> parameter, then
    ///   <see cref="ConvertFormat(PixelFormat, DitherType, PaletteType, ColorPalette?, float)"/> performs a nearest-color
    ///   conversion from the standard palette to the actual palette.
    ///  </para>
    /// </param>
    /// <param name="alphaThresholdPercent">
    ///  <para>
    ///   Real number in the range 0 through 100 that specifies which pixels in the source bitmap will map to the
    ///   transparent color in the converted bitmap.
    ///  </para>
    ///  <para>
    ///   A value of 0 specifies that none of the source pixels map to the transparent color. A value of 100
    ///   specifies that any pixel that is not fully opaque will map to the transparent color. A value of t specifies
    ///   that any source pixel less than t percent of fully opaque will map to the transparent color. Note that for
    ///   the alpha threshold to be effective, the palette must have a transparent color. If the palette does not have
    ///   a transparent color, pixels with alpha values below the threshold will map to color that most closely
    ///   matches (0, 0, 0, 0), usually black.
    ///  </para>
    /// </param>
    /// <remarks>
    ///  <para>
    ///   <paramref name="paletteType"/> and <paramref name="palette"/> really only have relevance with indexed pixel
    ///   formats. You can pass a <see cref="ColorPalette"/> for non-indexed pixel formats, but it has no impact on the
    ///   transformation and will effective just call <see cref="Image.Palette"/> to set the palette when the conversion
    ///   is complete.
    ///  </para>
    /// </remarks>
    public void ConvertFormat(
        PixelFormat format,
        DitherType ditherType,
        PaletteType paletteType = PaletteType.Custom,
        ColorPalette? palette = null,
        float alphaThresholdPercent = 0.0f)
    {
        ThrowIfDisposed();
        if (!IsSupportedManagedPixelFormat(format) || format == PixelFormat.Format16bppGrayScale)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        if (alphaThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }

        if (!Image.IsAlphaPixelFormat(format) && _pixels is not null)
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                Color color = Color.FromArgb(_pixels[i]);
                _pixels[i] = Color.FromArgb(255, color.R, color.G, color.B).ToArgb();
            }
        }

        SetManagedImage(Width, Height, format, HorizontalResolution, VerticalResolution, RawFormat);
        if (palette is not null)
        {
            Palette = palette;
        }

        GC.KeepAlive(this);
    }

    /// <summary>
    ///  Converts the bitmap to the specified <paramref name="format"/>.
    ///  The original pixel data is replaced with the new format.
    /// </summary>
    /// <param name="format">
    ///  <para>
    ///   The new pixel format. <see cref="PixelFormat.Format16bppGrayScale"/> is not supported.
    ///  </para>
    /// </param>
    public void ConvertFormat(PixelFormat format)
    {
        PixelFormat currentFormat = PixelFormat;
        int targetSize = ((int)format >> 8) & 0xff;
        int sourceSize = ((int)currentFormat >> 8) & 0xff;

        if (!format.HasFlag(PixelFormat.Indexed))
        {
            ConvertFormat(format, targetSize > sourceSize ? DitherType.None : DitherType.Solid);
            return;
        }

        int paletteSize = targetSize switch { 1 => 2, 4 => 16, _ => 256 };
        bool hasAlpha = format.HasFlag(PixelFormat.Alpha);
        if (hasAlpha)
        {
            paletteSize++;
        }

        ColorPalette palette = ColorPalette.CreateOptimalPalette(paletteSize, hasAlpha, this);
        ConvertFormat(format, DitherType.ErrorDiffusion, PaletteType.Custom, palette, .25f);
    }
#endif
}
