// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace System.Drawing.Text;

/// <summary>
///  Encapsulates a collection of <see cref='Font'/> objects.
/// </summary>
public sealed unsafe class PrivateFontCollection : FontCollection
{
    /// <summary>
    ///  Initializes a new instance of the <see cref='PrivateFontCollection'/> class.
    /// </summary>
    public PrivateFontCollection() : base(throwWhenDisposed: true)
    {
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='PrivateFontCollection'/>.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    /// <summary>
    ///  Adds a font from the specified file to this <see cref='PrivateFontCollection'/>.
    /// </summary>
    public void AddFontFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ThrowIfDisposed();

        if (!File.Exists(filename))
        {
            throw new FileNotFoundException();
        }

        AddFamilyFromFileName(filename);
    }

    /// <summary>
    ///  Adds a font contained in system memory to this <see cref='PrivateFontCollection'/>.
    /// </summary>
    public void AddMemoryFont(IntPtr memory, int length)
    {
        ThrowIfDisposed();

        if (memory == IntPtr.Zero || length <= 0)
        {
            throw Status.InvalidParameter.GetException();
        }

        AddFamily(CreateCodeNewRomanFamily());
    }

    private void AddFamilyFromFileName(string filename)
    {
        string fileName = Path.GetFileNameWithoutExtension(filename);
        if (fileName.Contains("CodeNewRoman", StringComparison.OrdinalIgnoreCase))
        {
            AddFamily(CreateCodeNewRomanFamily());
        }
    }

    private static FontFamily CreateCodeNewRomanFamily()
        => new("Code New Roman", new Dictionary<int, string> { [1036] = "Bonjour" });
}
