// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
namespace System.Drawing.Text;

/// <summary>
///  Represents the fonts installed on the system.
/// </summary>
public sealed unsafe class InstalledFontCollection : FontCollection
{
    /// <summary>
    ///  Initializes a new instance of the <see cref='InstalledFontCollection'/> class.
    /// </summary>
    public InstalledFontCollection() : base()
    {
        AddFamily(FontFamily.GenericSansSerif);
        AddFamily(FontFamily.GenericSerif);
        AddFamily(FontFamily.GenericMonospace);
        AddFamily(new FontFamily("Segoe UI", createDefaultOnFail: true));
        AddFamily(new FontFamily("Arial", createDefaultOnFail: true));
    }
}
