// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace System.Drawing.Text;

/// <summary>
///  When inherited, enumerates the FontFamily objects in a collection of fonts.
/// </summary>
public abstract unsafe class FontCollection : IDisposable
{
    private readonly List<FontFamily> _families = [];
    private readonly bool _throwWhenDisposed;
    private bool _disposed;

    internal FontCollection(bool throwWhenDisposed = false) => _throwWhenDisposed = throwWhenDisposed;

    /// <summary>
    ///  Disposes of this <see cref='FontCollection'/>
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;
    }

    /// <summary>
    ///  Gets the array of <see cref='FontFamily'/> objects associated with this <see cref='FontCollection'/>.
    /// </summary>
    public FontFamily[] Families
    {
        get
        {
            if (_disposed && _throwWhenDisposed)
            {
                throw Status.InvalidParameter.GetException();
            }

            return [.. _families];
        }
    }

    internal void AddFamily(FontFamily family)
    {
        ThrowIfDisposed();

        foreach (FontFamily existing in _families)
        {
            if (string.Equals(existing.Name, family.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        _families.Add(family);
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed && _throwWhenDisposed)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    ~FontCollection() => Dispose(disposing: false);
}
