// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;

namespace System.Drawing.Drawing2D;

public sealed unsafe class Matrix : MarshalByRefObject, IDisposable
{
    private Matrix3x2 _matrix;
    private bool _disposed;

    internal GdiPlus.Matrix* NativeMatrix => null;

    public Matrix()
    {
        _matrix = Matrix3x2.Identity;
    }

    public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
    {
        _matrix = new Matrix3x2(m11, m12, m21, m22, dx, dy);
    }

    /// <summary>
    ///  Construct a <see cref="Matrix"/> utilizing the given <paramref name="matrix"/>.
    /// </summary>
    /// <param name="matrix">Matrix data to construct from.</param>
    public Matrix(Matrix3x2 matrix)
    {
        _matrix = matrix;
    }

    private Matrix(GdiPlus.Matrix* nativeMatrix) => _matrix = Matrix3x2.Identity;

    internal static GdiPlus.Matrix* CreateNativeHandle(Matrix3x2 matrix)
    {
        return null;
    }

    public Matrix(RectangleF rect, params PointF[] plgpts)
    {
        ArgumentNullException.ThrowIfNull(plgpts);
        if (plgpts.Length != 3)
            throw Status.InvalidParameter.GetException();

        _matrix = Matrix3x2.CreateTranslation(plgpts[0].X - rect.X, plgpts[0].Y - rect.Y);
    }

    public Matrix(Rectangle rect, params Point[] plgpts)
    {
        ArgumentNullException.ThrowIfNull(plgpts);
        if (plgpts.Length != 3)
            throw Status.InvalidParameter.GetException();

        _matrix = Matrix3x2.CreateTranslation(plgpts[0].X - rect.X, plgpts[0].Y - rect.Y);
    }

    public void Dispose()
    {
        DisposeInternal();
        GC.SuppressFinalize(this);
    }

    private void DisposeInternal()
    {
        _disposed = true;
    }

    ~Matrix() => DisposeInternal();

    public Matrix Clone()
    {
        ThrowIfDisposed();
        return new Matrix(_matrix);
    }

    public float[] Elements
    {
        get
        {
            float[] elements = new float[6];
            GetElements(elements);
            return elements;
        }
    }

    /// <summary>
    ///  Gets/sets the elements for the matrix.
    /// </summary>
    public Matrix3x2 MatrixElements
    {
        get
        {
            ThrowIfDisposed();
            return _matrix;
        }
        set
        {
            ThrowIfDisposed();
            _matrix = value;
        }
    }

    internal void GetElements(Span<float> elements)
    {
        Debug.Assert(elements.Length >= 6);

        ThrowIfDisposed();
        elements[0] = _matrix.M11;
        elements[1] = _matrix.M12;
        elements[2] = _matrix.M21;
        elements[3] = _matrix.M22;
        elements[4] = _matrix.M31;
        elements[5] = _matrix.M32;
    }

    public float OffsetX => Offset.X;

    public float OffsetY => Offset.Y;

    internal PointF Offset
    {
        get
        {
            Span<float> elements = stackalloc float[6];
            GetElements(elements);
            return new PointF(elements[4], elements[5]);
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _matrix = Matrix3x2.Identity;
    }

    public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

    public void Multiply(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        ThrowIfDisposed();
        matrix.ThrowIfDisposed();
        if (ReferenceEquals(matrix, this))
            throw new InvalidOperationException(SR.GdiplusObjectBusy);

        _matrix = order == MatrixOrder.Append ? _matrix * matrix._matrix : matrix._matrix * _matrix;
    }

    public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

    public void Translate(float offsetX, float offsetY, MatrixOrder order)
    {
        ThrowIfDisposed();
        Matrix3x2 translation = Matrix3x2.CreateTranslation(offsetX, offsetY);
        _matrix = order == MatrixOrder.Append ? _matrix * translation : translation * _matrix;
    }

    public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

    public void Scale(float scaleX, float scaleY, MatrixOrder order)
    {
        ThrowIfDisposed();
        Matrix3x2 scale = Matrix3x2.CreateScale(scaleX, scaleY);
        _matrix = order == MatrixOrder.Append ? _matrix * scale : scale * _matrix;
    }

    public void Rotate(float angle) => Rotate(angle, MatrixOrder.Prepend);

    public void Rotate(float angle, MatrixOrder order)
    {
        ThrowIfDisposed();
        Matrix3x2 rotation = Matrix3x2.CreateRotation(angle * MathF.PI / 180.0f);
        _matrix = order == MatrixOrder.Append ? _matrix * rotation : rotation * _matrix;
    }

    public void RotateAt(float angle, PointF point) => RotateAt(angle, point, MatrixOrder.Prepend);
    public void RotateAt(float angle, PointF point, MatrixOrder order)
    {
        if (order == MatrixOrder.Prepend)
        {
            Translate(point.X, point.Y, order);
            Rotate(angle, order);
            Translate(-point.X, -point.Y, order);
        }
        else
        {
            Translate(-point.X, -point.Y, order);
            Rotate(angle, order);
            Translate(point.X, point.Y, order);
        }
    }

    public void Shear(float shearX, float shearY)
    {
        Shear(shearX, shearY, MatrixOrder.Prepend);
    }

    public void Shear(float shearX, float shearY, MatrixOrder order)
    {
        ThrowIfDisposed();
        Matrix3x2 shear = new(1, shearY, shearX, 1, 0, 0);
        _matrix = order == MatrixOrder.Append ? _matrix * shear : shear * _matrix;
    }

    public void Invert()
    {
        ThrowIfDisposed();
        if (!Matrix3x2.Invert(_matrix, out Matrix3x2 inverted))
            throw Status.InvalidParameter.GetException();

        _matrix = inverted;
    }

    /// <inheritdoc cref="TransformPoints(Point[])"/>
    public void TransformPoints(params PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(pts.AsSpan(), includeTranslation: true);
    }

    /// <inheritdoc cref="TransformPoints(Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformPoints(params ReadOnlySpan<PointF> pts)
    {
        ThrowIfDisposed();
    }

    /// <summary>
    ///  Applies the geometric transform this <see cref="Matrix"/> represents to an array of points.
    /// </summary>
    /// <param name="pts">The points to transform.</param>
    public void TransformPoints(params Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(pts.AsSpan(), includeTranslation: true);
    }

    /// <inheritdoc cref="TransformPoints(Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformPoints(params ReadOnlySpan<Point> pts)
    {
        ThrowIfDisposed();
    }

    /// <summary>
    ///  Multiplies each vector in an array by the matrix. The translation elements of this matrix (third row) are ignored.
    /// </summary>
    /// <param name="pts">The points to transform.</param>
    public void TransformVectors(params PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(pts.AsSpan(), includeTranslation: false);
    }

    /// <inheritdoc cref="TransformVectors(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformVectors(params ReadOnlySpan<PointF> pts)
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc cref="TransformVectors(PointF[])"/>
    public void VectorTransformPoints(params Point[] pts) => TransformVectors(pts);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="TransformVectors(PointF[])"/>
    public void VectorTransformPoints(params ReadOnlySpan<Point> pts) => TransformVectors(pts);
#endif

    /// <inheritdoc cref="TransformVectors(PointF[])"/>
    public void TransformVectors(params Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(pts.AsSpan(), includeTranslation: false);
    }

    /// <inheritdoc cref="TransformVectors(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformVectors(params ReadOnlySpan<Point> pts)
    {
        ThrowIfDisposed();
    }

    public bool IsInvertible
    {
        get
        {
            ThrowIfDisposed();
            return Matrix3x2.Invert(_matrix, out _);
        }
    }

    public bool IsIdentity
    {
        get
        {
            ThrowIfDisposed();
            return _matrix.IsIdentity;
        }
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not Matrix matrix2)
            return false;

        ThrowIfDisposed();
        matrix2.ThrowIfDisposed();
        return _matrix.Equals(matrix2._matrix);
    }

    public override int GetHashCode() => base.GetHashCode();

    private void TransformPointSpan(Span<PointF> pts, bool includeTranslation)
    {
        ThrowIfDisposed();
        Matrix3x2 matrix = includeTranslation ? _matrix : _matrix with { M31 = 0, M32 = 0 };
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 transformed = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), matrix);
            pts[i] = new PointF(transformed.X, transformed.Y);
        }
    }

    private void TransformPointSpan(Span<Point> pts, bool includeTranslation)
    {
        ThrowIfDisposed();
        Matrix3x2 matrix = includeTranslation ? _matrix : _matrix with { M31 = 0, M32 = 0 };
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 transformed = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), matrix);
            pts[i] = new Point((int)MathF.Round(transformed.X), (int)MathF.Round(transformed.Y));
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
