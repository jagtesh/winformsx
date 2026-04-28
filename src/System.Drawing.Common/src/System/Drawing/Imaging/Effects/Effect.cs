// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET9_0_OR_GREATER

namespace System.Drawing.Imaging.Effects;

/// <summary>
///  Base class for all effects.
/// </summary>
public abstract unsafe class Effect : IDisposable
{
    private byte[]? _parameters;
    private bool _disposed;

    internal CGpEffect* NativeEffect => null;

    internal Guid Id { get; }

    internal ReadOnlySpan<byte> Parameters => _parameters;

    private protected Effect(Guid guid) => Id = guid;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private protected void SetParameters<T>(ref T parameters) where T : unmanaged
    {
        if (_disposed)
        {
            throw Status.InvalidParameter.GetException();
        }

        _parameters = new byte[sizeof(T)];
        fixed (byte* destination = _parameters)
        {
            *(T*)destination = parameters;
        }
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref="Effect"/>
    /// </summary>
    ~Effect() => Dispose(disposing: false);

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;
        _parameters = null;
    }
}
#endif
