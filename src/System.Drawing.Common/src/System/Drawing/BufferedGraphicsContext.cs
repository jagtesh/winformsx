// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace System.Drawing;

/// <summary>
///  The BufferedGraphicsContext class can be used to perform standard double buffer rendering techniques.
/// </summary>
public sealed class BufferedGraphicsContext : IDisposable
{
    private Size _maximumBuffer;
    private Size _bufferSize = Size.Empty;
    private Size _virtualSize;
    private Point _targetLoc;
    private Bitmap? _bufferBitmap;
    private Graphics? _compatGraphics;
    private BufferedGraphics? _buffer;
    private int _busy;
    private bool _invalidateWhenFree;

    private const int BufferFree = 0; // The graphics buffer is free to use.
    private const int BufferBusyPainting = 1; // The graphics buffer is busy being created/painting.
    private const int BufferBusyDisposing = 2; // The graphics buffer is busy disposing.

    /// <summary>
    /// Basic constructor.
    /// </summary>
    public BufferedGraphicsContext()
    {
        // By default, the size of our max buffer will be 3 x standard button size.
        _maximumBuffer.Width = 75 * 3;
        _maximumBuffer.Height = 32 * 3;
    }

    /// <summary>
    /// Allows you to set the maximum width and height of the buffer that will be retained in memory.
    /// You can allocate a buffer of any size, however any request for a buffer that would have a total
    /// memory footprint larger that the maximum size will be allocated temporarily and then discarded
    /// with the BufferedGraphics is released.
    /// </summary>
    public Size MaximumBuffer
    {
        get => _maximumBuffer;
        set
        {
            if (value.Width <= 0 || value.Height <= 0)
            {
                throw new ArgumentException(SR.Format(SR.InvalidArgumentValue, nameof(MaximumBuffer), value), nameof(value));
            }

            // If we've been asked to decrease the size of the maximum buffer,
            // then invalidate the older & larger buffer.
            if (value.Width * value.Height < _maximumBuffer.Width * _maximumBuffer.Height)
            {
                Invalidate();
            }

            _maximumBuffer = value;
        }
    }

    ~BufferedGraphicsContext() => Dispose(false);

    /// <summary>
    /// Returns a BufferedGraphics that is matched for the specified target Graphics object.
    /// </summary>
    public BufferedGraphics Allocate(Graphics targetGraphics, Rectangle targetRectangle)
    {
        if (ShouldUseTempManager(targetRectangle))
        {
            return AllocBufferInTempManager(targetGraphics, HDC.Null, targetRectangle);
        }

        return AllocBuffer(targetGraphics, HDC.Null, targetRectangle);
    }

    /// <summary>
    /// Returns a BufferedGraphics that is matched for the specified target HDC object.
    /// </summary>
    public BufferedGraphics Allocate(IntPtr targetDC, Rectangle targetRectangle)
    {
        if (ShouldUseTempManager(targetRectangle))
        {
            return AllocBufferInTempManager(null, (HDC)targetDC, targetRectangle);
        }

        return AllocBuffer(null, (HDC)targetDC, targetRectangle);
    }

    /// <summary>
    /// Returns a BufferedGraphics that is matched for the specified target HDC object.
    /// </summary>
    private BufferedGraphics AllocBuffer(Graphics? targetGraphics, HDC targetDC, Rectangle targetRectangle)
    {
        int oldBusy = Interlocked.CompareExchange(ref _busy, BufferBusyPainting, BufferFree);

        // In the case were we have contention on the buffer - i.e. two threads
        // trying to use the buffer at the same time, we just create a temp
        // buffer manager and have the buffer dispose of it when it is done.
        if (oldBusy != BufferFree)
        {
            return AllocBufferInTempManager(targetGraphics, targetDC, targetRectangle);
        }

        Graphics surface;
        _targetLoc = new Point(targetRectangle.X, targetRectangle.Y);

        try
        {
            if (targetGraphics is not null)
            {
                if (targetGraphics.IsDisposed)
                {
                    throw new ArgumentException(null, (string?)null);
                }

                if (targetGraphics.IsHdcBusy)
                {
                    throw new InvalidOperationException(SR.GraphicsBufferCurrentlyBusy);
                }

                IntPtr destDc = targetGraphics.GetHdc();
                try
                {
                    surface = CreateBuffer((HDC)destDc, targetRectangle.Width, targetRectangle.Height);
                }
                finally
                {
                    targetGraphics.ReleaseHdcInternal(destDc);
                }
            }
            else
            {
                if (targetDC.IsNull && !targetRectangle.IsEmpty)
                {
                    throw new ArgumentNullException("hdc");
                }

                if ((nint)targetDC == -1)
                {
                    throw new ArgumentException(null, (string?)null);
                }

                surface = CreateBuffer(targetDC, targetRectangle.Width, targetRectangle.Height);
            }

            _buffer = new BufferedGraphics(_bufferBitmap, surface, this, targetGraphics, targetDC, _targetLoc, _virtualSize);
        }
        catch
        {
            // Free the buffer so it can be disposed.
            _busy = BufferFree;
            throw;
        }

        return _buffer;
    }

    /// <summary>
    /// Returns a BufferedGraphics that is matched for the specified target HDC object.
    /// </summary>
    private static BufferedGraphics AllocBufferInTempManager(Graphics? targetGraphics, HDC targetDC, Rectangle targetRectangle)
    {
        BufferedGraphicsContext? tempContext = null;
        BufferedGraphics? tempBuffer = null;

        try
        {
            tempContext = new BufferedGraphicsContext();
            tempBuffer = tempContext.AllocBuffer(targetGraphics, targetDC, targetRectangle);
            tempBuffer.DisposeContext = true;
        }
        finally
        {
            if (tempContext is not null && tempBuffer is not { DisposeContext: true })
            {
                tempContext.Dispose();
            }
        }

        return tempBuffer;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This routine allows us to control the point were we start using throw away
    /// managers for painting. Since the buffer manager stays around (by default)
    /// for the life of the app, we don't want to consume too much memory
    /// in the buffer. However, re-allocating the buffer for small things (like
    /// buttons, labels, etc) will hit us on runtime performance.
    /// </summary>
    private bool ShouldUseTempManager(Rectangle targetBounds)
    {
        return (targetBounds.Width * targetBounds.Height) > (MaximumBuffer.Width * MaximumBuffer.Height);
    }

    /// <summary>
    ///  Returns a Graphics object representing a buffer.
    /// </summary>
    private Graphics CreateBuffer(HDC src, int width, int height)
    {
        // Create the compat DC.
        _busy = BufferBusyDisposing;
        DisposeDC();
        _busy = BufferBusyPainting;

        // Recreate the bitmap if necessary.
        int requestedWidth = Math.Max(1, width);
        int requestedHeight = Math.Max(1, height);

        if (_bufferBitmap is null || requestedWidth > _bufferSize.Width || requestedHeight > _bufferSize.Height)
        {
            int optWidth = Math.Max(requestedWidth, _bufferSize.Width);
            int optHeight = Math.Max(requestedHeight, _bufferSize.Height);

            _busy = BufferBusyDisposing;
            DisposeBitmap();
            _busy = BufferBusyPainting;

            _bufferBitmap = new Bitmap(optWidth, optHeight);
            _bufferSize = new Size(optWidth, optHeight);
        }

        _compatGraphics = Graphics.FromImage(_bufferBitmap!);
        _compatGraphics.TranslateTransform(-_targetLoc.X, -_targetLoc.Y);
        _virtualSize = new Size(width, height);

        GC.KeepAlive(this);
        return _compatGraphics;
    }

    /// <summary>
    ///  Disposes the DC, but leaves the bitmap alone.
    /// </summary>
    private void DisposeDC() => GC.KeepAlive(this);

    /// <summary>
    ///  Disposes the bitmap, will ASSERT if bitmap is being used (checks oldbitmap). if ASSERTed, call DisposeDC() first.
    /// </summary>
    private void DisposeBitmap()
    {
        if (_bufferBitmap is not null)
        {
            _bufferBitmap.Dispose();
            _bufferBitmap = null;
            _bufferSize = Size.Empty;
        }

        GC.KeepAlive(this);
    }

    /// <summary>
    /// Disposes of the Graphics buffer.
    /// </summary>
    private void Dispose(bool disposing)
    {
        int oldBusy = Interlocked.CompareExchange(ref _busy, BufferBusyDisposing, BufferFree);

        if (disposing)
        {
            if (oldBusy == BufferBusyPainting)
            {
                throw new InvalidOperationException(SR.GraphicsBufferCurrentlyBusy);
            }

            if (_compatGraphics is not null)
            {
                _compatGraphics.Dispose();
                _compatGraphics = null;
            }
        }

        DisposeDC();
        DisposeBitmap();

        if (_buffer is not null)
        {
            _buffer.Dispose();
            _buffer = null;
        }

        _bufferSize = Size.Empty;
        _virtualSize = Size.Empty;

        _busy = BufferFree;
    }

    /// <summary>
    /// Invalidates the cached graphics buffer.
    /// </summary>
    public void Invalidate()
    {
        int oldBusy = Interlocked.CompareExchange(ref _busy, BufferBusyDisposing, BufferFree);

        // If we're not busy with our buffer, lets clean it up now
        if (oldBusy == BufferFree)
        {
            Dispose();
            _busy = BufferFree;
        }
        else
        {
            // This will indicate to free the buffer as soon as it becomes non-busy.
            _invalidateWhenFree = true;
        }
    }

    /// <summary>
    /// Returns a Graphics object representing a buffer.
    /// </summary>
    internal void ReleaseBuffer()
    {
        _buffer = null;
        if (_invalidateWhenFree)
        {
            // Clears everything including the bitmap.
            _busy = BufferBusyDisposing;
            Dispose();
        }
        else
        {
            // Otherwise, just dispose the DC. A new one will be created next time.
            _busy = BufferBusyDisposing;

            // Only clears out the DC.
            DisposeDC();
        }

        _busy = BufferFree;
    }
}
