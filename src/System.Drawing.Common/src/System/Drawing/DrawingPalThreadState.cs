// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing;

internal static class DrawingPalThreadState
{
    [ThreadStatic]
    private static IDictionary<object, object>? s_threadData;

    /// <summary>
    ///  Stores shared drawing objects per thread so mutable public objects are not reused concurrently.
    /// </summary>
    internal static IDictionary<object, object> ThreadData => s_threadData ??= new Dictionary<object, object>();
}
