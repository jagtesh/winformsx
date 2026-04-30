// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Runtime.Serialization;

namespace System.Windows.Forms;

/// <summary>
///  Represents the image used to paint the mouse pointer. Different cursor shapes are used to inform the user
///  what operation the mouse will have.
/// </summary>
[TypeConverter(typeof(CursorConverter))]
[Editor($"System.Drawing.Design.CursorEditor, {AssemblyRef.SystemDrawingDesign}", typeof(UITypeEditor))]
public sealed class Cursor : IDisposable, ISerializable, IHandle<HICON>, IHandle<HANDLE>, IHandle<HCURSOR>
{
    private static Size s_cursorSize = Size.Empty;
    private static Rectangle s_clip = SystemInformation.VirtualScreen;
    private static Cursor? s_currentCursor;
    private static bool s_currentCursorInitialized;
    private static int s_nextSyntheticCursorHandle;
    private static Point s_position;

    private readonly byte[]? _cursorData;
    private readonly bool _disposeHandle;
    private HCURSOR _handle;

    /// <summary>
    ///  If created by the <see cref="Cursors"/> class, this is the property name that created it.
    /// </summary>
    internal string? CursorsProperty { get; }

    internal unsafe Cursor(PCWSTR nResourceId, string cursorsProperty)
    {
        GC.SuppressFinalize(this);
        _ = nResourceId;
        CursorsProperty = cursorsProperty;
        _handle = CreateSyntheticCursorHandle();
    }

    internal Cursor(string resource, string cursorsProperty)
    {
        GC.SuppressFinalize(this);
        _ = resource;
        CursorsProperty = cursorsProperty;
        _handle = CreateSyntheticCursorHandle();
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Cursor"/> class from the specified <paramref name="handle"/>.
    /// </summary>
    public Cursor(IntPtr handle)
    {
        GC.SuppressFinalize(this);
        if (handle == 0)
        {
            throw new ArgumentException(string.Format(SR.InvalidGDIHandle, (typeof(Cursor)).Name), nameof(handle));
        }

        _handle = (HCURSOR)handle;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Cursor"/> class with the specified <paramref name="fileName"/>.
    /// </summary>
    public Cursor(string fileName)
    {
        _cursorData = File.ReadAllBytes(fileName);
        _disposeHandle = true;
        _handle = CreateSyntheticCursorHandle();
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Cursor"/> class from the specified <paramref name="resource"/>.
    /// </summary>
    public Cursor(Type type, string resource)
        : this(type.OrThrowIfNull().Module.Assembly.GetManifestResourceStream(type, resource)!)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Cursor"/> class from the
    ///  specified data <paramref name="stream"/>.
    /// </summary>
    public Cursor(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream memoryStream = new();

        // Reset stream position to start, there are no gaurantees it is at the start.
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        stream.CopyTo(memoryStream);
        _cursorData = memoryStream.ToArray();
        _disposeHandle = true;
        _handle = CreateSyntheticCursorHandle();
    }

    private Cursor(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info);
        _ = context;

        _cursorData = (byte[]?)info.GetValue("CursorData", typeof(byte[])) ?? [];
        _disposeHandle = true;
        _handle = CreateSyntheticCursorHandle();
    }

    /// <summary>
    ///  Gets or sets a <see cref="Rectangle"/> that represents the current clipping
    ///  rectangle for this <see cref="Cursor"/> in screen coordinates.
    /// </summary>
    public static Rectangle Clip
    {
        get => s_clip;
        set => s_clip = value.IsEmpty ? SystemInformation.VirtualScreen : Rectangle.Intersect(value, SystemInformation.VirtualScreen);
    }

    /// <summary>
    ///  Gets or sets a <see cref="Cursor"/> that represents the current mouse cursor.
    ///  The value is <see langword="null"/> if the current mouse cursor is not visible.
    /// </summary>
    public static Cursor? Current
    {
        get => s_currentCursorInitialized ? s_currentCursor : new Cursor(Cursors.Default.Handle);
        set
        {
            s_currentCursorInitialized = true;
            s_currentCursor = value;
        }
    }

    /// <summary>
    ///  Gets the Win32 handle for this <see cref="Cursor"/>.
    /// </summary>
    public IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsNull, this);
            return (nint)_handle;
        }
    }

    /// <summary>
    ///  Returns the "hot" location of the cursor.
    /// </summary>
    public Point HotSpot
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsNull, this);
            return Point.Empty;
        }
    }

    /// <summary>
    ///  Gets or sets a <see cref="Point"/> that specifies the current cursor position in screen coordinates.
    /// </summary>
    public static Point Position
    {
        get => s_position;
        set => s_position = value;
    }

    /// <summary>
    ///  Gets the size of this <see cref="Cursor"/> object.
    /// </summary>
    public Size Size
    {
        get
        {
            if (s_cursorSize.IsEmpty)
            {
                s_cursorSize = SystemInformation.CursorSize;
            }

            return s_cursorSize;
        }
    }

    [SRCategory(nameof(SR.CatData))]
    [Localizable(false)]
    [Bindable(true)]
    [SRDescription(nameof(SR.ControlTagDescr))]
    [DefaultValue(null)]
    [TypeConverter(typeof(StringConverter))]
    public object? Tag { get; set; }

    HICON IHandle<HICON>.Handle => (HICON)Handle;

    HANDLE IHandle<HANDLE>.Handle => (HANDLE)Handle;
    HCURSOR IHandle<HCURSOR>.Handle => _handle;

    /// <summary>
    ///  Duplicates this the Win32 handle of this <see cref="Cursor"/>.
    /// </summary>
    public IntPtr CopyHandle()
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        return (nint)CreateSyntheticCursorHandle();
    }

    /// <summary>
    ///  Cleans up the resources allocated by this object. Once called, the cursor object is no longer useful.
    /// </summary>
    public void Dispose()
    {
        if (_disposeHandle)
        {
            _handle = HCURSOR.Null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///  Draws this image to a graphics object.  The drawing command originates on the graphics
    ///  object, but a graphics object generally has no idea how to render a given image.  So,
    ///  it passes the call to the actual image.  This version crops the image to the given
    ///  dimensions and allows the user to specify a rectangle within the image to draw.
    /// </summary>
    private static void DrawImageCore(Graphics graphics)
        => ArgumentNullException.ThrowIfNull(graphics);

    /// <summary>
    ///  Draws this <see cref="Cursor"/> to a <see cref="Graphics"/>.
    /// </summary>
    public void Draw(Graphics g, Rectangle targetRect)
    {
        DrawImageCore(g);
    }

    /// <summary>
    ///  Draws this <see cref="Cursor"/> to a <see cref="Graphics"/>.
    /// </summary>
    public void DrawStretched(Graphics g, Rectangle targetRect)
    {
        DrawImageCore(g);
    }

    ~Cursor() => Dispose();

    void ISerializable.GetObjectData(SerializationInfo si, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(si);
        _ = context;

        si.AddValue("CursorData", GetData(), typeof(byte[]));
    }

    /// <summary>
    ///  Hides the cursor. For every call to Cursor.hide() there must be a balancing call to Cursor.show().
    /// </summary>
    public static void Hide()
    {
    }

    /// <summary>
    ///  Saves a picture from the requested stream.
    /// </summary>
    internal unsafe byte[] GetData()
    {
        if (_cursorData is null)
        {
            throw CursorsProperty is null
                ? new InvalidOperationException(SR.InvalidPictureFormat)
                : new FormatException(SR.CursorCannotCovertToBytes);
        }

        return (byte[])_cursorData.Clone();
    }

    /// <summary>
    ///  Displays the cursor. For every call to Cursor.show() there must have been
    ///  a previous call to Cursor.hide().
    /// </summary>
    public static void Show()
    {
    }

#pragma warning disable IDE0004 // Cast is required for generated Win32 handle structs.
    private static HCURSOR CreateSyntheticCursorHandle()
        => (HCURSOR)(nint)Interlocked.Increment(ref s_nextSyntheticCursorHandle);
#pragma warning restore IDE0004

    /// <summary>
    ///  Retrieves a human readable string representing this <see cref="Cursor"/>.
    /// </summary>
    public override string ToString() => $"[Cursor: {CursorsProperty ?? base.ToString()}]";

    public static bool operator ==(Cursor? left, Cursor? right)
    {
        return right is null || left is null ? left is null && right is null : left._handle == right._handle;
    }

    public static bool operator !=(Cursor? left, Cursor? right) => !(left == right);

    public override int GetHashCode() => (int)_handle.Value;

    public override bool Equals(object? obj) => obj is Cursor cursor && this == cursor;
}
