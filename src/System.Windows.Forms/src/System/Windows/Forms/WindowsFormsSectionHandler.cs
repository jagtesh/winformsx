// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms;

// According to our data base, following public class has no usage at runtime.
// Keeping the skeleton to unblock compile scenarios, if any.
// And to receive feedback if there are any users  of this class that we do not know
public sealed class WindowsFormsSection
{
    private static readonly WindowsFormsSection s_defaultSection = new();

    internal static WindowsFormsSection GetSection()
        => s_defaultSection;

    public WindowsFormsSection()
    {
    }

    public bool JitDebugging
    {
        get;
        set;
    }
}
