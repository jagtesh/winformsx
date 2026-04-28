// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Drawing2D;

public sealed unsafe class GraphicsPathIterator : MarshalByRefObject, IDisposable
{
    private readonly PointF[] _points;
    private readonly byte[] _types;
    private int _position;
    private bool _disposed;
    private bool _returnedFullPathAfterRewind;

    public GraphicsPathIterator(GraphicsPath? path)
    {
        _points = path?.PathPoints ?? [];
        _types = path?.PathTypes ?? [];
    }

    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~GraphicsPathIterator() => Dispose();

    public int NextSubpath(out int startIndex, out int endIndex, out bool isClosed)
    {
        EnsurePath();
        if (_position >= _types.Length)
        {
            startIndex = endIndex = 0;
            isClosed = false;
            return 0;
        }

        startIndex = _position;
        endIndex = FindSubpathEnd(_types, startIndex);
        isClosed = IsClosed(_types[endIndex]);
        _position = endIndex + 1;
        if (!isClosed)
        {
            startIndex = endIndex = 0;
            return 0;
        }

        return endIndex - startIndex + 1;
    }

    public int NextSubpath(GraphicsPath? path, out bool isClosed)
    {
        int count = NextSubpath(out int startIndex, out int endIndex, out isClosed);
        CopyRangeToPath(path, startIndex, endIndex, count);
        return count;
    }

    public int NextPathType(out byte pathType, out int startIndex, out int endIndex)
    {
        EnsurePath();
        if (_position >= _types.Length)
        {
            pathType = 0;
            startIndex = endIndex = 0;
            return 0;
        }

        startIndex = _position;
        pathType = BaseType(_types[_position]);
        int i = _position + 1;
        while (i < _types.Length && BaseType(_types[i]) == pathType)
        {
            i++;
        }

        endIndex = i - 1;
        _position = i;
        return endIndex - startIndex + 1;
    }

    public int NextMarker(out int startIndex, out int endIndex)
    {
        EnsurePath();
        if (_position >= _types.Length)
        {
            startIndex = endIndex = 0;
            return 0;
        }

        startIndex = _position;
        int marker = Array.FindIndex(_types, _position, static t => (t & (byte)PathPointType.PathMarker) != 0);
        endIndex = marker >= 0 ? marker : _types.Length - 1;
        _position = endIndex + 1;
        return endIndex - startIndex + 1;
    }

    public int NextMarker(GraphicsPath? path)
    {
        if (_returnedFullPathAfterRewind)
        {
            _returnedFullPathAfterRewind = false;
            _position = _types.Length;
            CopyRangeToPath(path, 0, _types.Length - 1, _types.Length);
            return _types.Length;
        }

        int count = NextMarker(out int startIndex, out int endIndex);
        CopyRangeToPath(path, startIndex, endIndex, count);
        return count;
    }

    public int Count
    {
        get
        {
            EnsurePath();
            return _points.Length;
        }
    }

    public int SubpathCount
    {
        get
        {
            EnsurePath();
            if (_types.Length == 0)
            {
                return 0;
            }

            int count = 1;
            for (int i = 1; i < _types.Length; i++)
            {
                if (BaseType(_types[i]) == (byte)PathPointType.Start)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasCurve()
    {
        EnsurePath();
        foreach (byte type in _types)
        {
            if (BaseType(type) == (byte)PathPointType.Bezier)
            {
                return true;
            }
        }

        return false;
    }

    public void Rewind()
    {
        EnsurePath();
        _position = 0;
        _returnedFullPathAfterRewind = _types.Length > 0;
    }

    /// <inheritdoc cref="CopyData(ref PointF[], ref byte[], int, int)"/>
    public unsafe int Enumerate(ref PointF[] points, ref byte[] types)
        => Enumerate(points.OrThrowIfNull().AsSpan(), types.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="CopyData(ref PointF[], ref byte[], int, int)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    unsafe int Enumerate(Span<PointF> points, Span<byte> types)
    {
        EnsurePath();
        int count = Count;
        if (points.Length != types.Length || points.Length < count)
        {
            throw Status.InvalidParameter.GetException();
        }

        if (count == 0)
        {
            return 0;
        }

        return CopyData(points, types, 0, count - 1);
    }

    /// <summary>
    ///  Copies the <see cref="GraphicsPath.PathPoints"/> property and <see cref="GraphicsPath.PathTypes"/> property data
    ///  of the associated <see cref="GraphicsPath"/>.
    /// </summary>
    /// <param name="points">Upon return, contains <see cref="PointF"/> structures that represent the points in the path.</param>
    /// <param name="types">Upon return, contains bytes that represent the types of points in the path.</param>
    /// <param name="startIndex">The index of the first point to copy.</param>
    /// <param name="endIndex">The index of the last point to copy.</param>
    /// <returns>The number of points copied.</returns>
    public unsafe int CopyData(ref PointF[] points, ref byte[] types, int startIndex, int endIndex)
        => CopyData(points.OrThrowIfNull().AsSpan(), types.OrThrowIfNull().AsSpan(), startIndex, endIndex);

    /// <inheritdoc cref="CopyData(ref PointF[], ref byte[], int, int)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    unsafe int CopyData(Span<PointF> points, Span<byte> types, int startIndex, int endIndex)
    {
        EnsurePath();
        int count = endIndex - startIndex + 1;
        if (points.Length != types.Length
            || startIndex < 0
            || endIndex < startIndex
            || endIndex >= Count
            || count > points.Length)
        {
            throw Status.InvalidParameter.GetException();
        }

        _points.AsSpan(startIndex, count).CopyTo(points);
        _types.AsSpan(startIndex, count).CopyTo(types);
        return count;
    }

    private void CopyRangeToPath(GraphicsPath? path, int startIndex, int endIndex, int count)
    {
        if (path is null)
        {
            return;
        }

        if (count == 0)
        {
            path.Reset();
            return;
        }

        PointF[] points = new PointF[count];
        byte[] types = new byte[count];
        CopyData(points, types, startIndex, endIndex);
        path.ReplaceData(points, types);
    }

    private void EnsurePath()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static int FindSubpathEnd(byte[] types, int startIndex)
    {
        int i = startIndex + 1;
        while (i < types.Length && BaseType(types[i]) != (byte)PathPointType.Start)
        {
            i++;
        }

        return i - 1;
    }

    private static byte BaseType(byte type) => (byte)(type & (byte)PathPointType.PathTypeMask);

    private static bool IsClosed(byte type) => (type & (byte)PathPointType.CloseSubpath) != 0;
}
