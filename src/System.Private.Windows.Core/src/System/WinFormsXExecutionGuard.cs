// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System;

internal enum WinFormsXExecutionKind
{
    MessageDispatch,
    Paint,
    Layout,
    Invalidation,
    Input,
    Selection,
}

internal static class WinFormsXExecutionGuard
{
    private const int KindCount = 6;
    private const int DefaultMaxDepth = 128;

    [ThreadStatic]
    private static int[]? s_depths;

    public static Scope Enter(WinFormsXExecutionKind kind, string context, int maxDepth = DefaultMaxDepth)
    {
        int[] depths = s_depths ??= new int[KindCount];
        int index = (int)kind;
        int next = SafeArithmetic.CheckedIncrement(depths[index], $"{kind} depth");
        if (next > maxDepth)
        {
            throw new InvalidOperationException(
                $"WinFormsX {kind} re-entered too deeply at depth {next}. Context: {context}");
        }

        depths[index] = next;
        return new Scope(index);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly int _index;

        internal Scope(int index)
        {
            _index = index;
        }

        public void Dispose()
        {
            int[]? depths = s_depths;
            if (depths is null)
            {
                return;
            }

            depths[_index] = Math.Max(0, SafeArithmetic.CheckedDecrement(depths[_index], "WinFormsX execution depth"));
        }
    }
}
