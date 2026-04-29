// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

namespace Windows.Win32;

internal static partial class PInvoke
{
    private static readonly ConcurrentDictionary<(nint Hwnd, SCROLLBAR_CONSTANTS Bar), SCROLLINFO> s_scrollInfo = new();

    private static BOOL GetSyntheticScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS nBar, ref SCROLLINFO scrollInfo)
    {
        if (!s_scrollInfo.TryGetValue(((nint)hwnd, nBar), out SCROLLINFO stored))
        {
            stored = new SCROLLINFO
            {
                cbSize = scrollInfo.cbSize,
                fMask = scrollInfo.fMask
            };
        }

        SCROLLINFO_MASK requested = scrollInfo.fMask;
        uint cbSize = scrollInfo.cbSize;

        if ((requested & SCROLLINFO_MASK.SIF_RANGE) != 0)
        {
            scrollInfo.nMin = stored.nMin;
            scrollInfo.nMax = stored.nMax;
        }

        if ((requested & SCROLLINFO_MASK.SIF_PAGE) != 0)
        {
            scrollInfo.nPage = stored.nPage;
        }

        if ((requested & SCROLLINFO_MASK.SIF_POS) != 0)
        {
            scrollInfo.nPos = stored.nPos;
        }

        if ((requested & SCROLLINFO_MASK.SIF_TRACKPOS) != 0)
        {
            scrollInfo.nTrackPos = stored.nTrackPos != 0 ? stored.nTrackPos : stored.nPos;
        }

        scrollInfo.cbSize = cbSize;
        scrollInfo.fMask = requested;
        return BOOL.TRUE;
    }

    private static int SetSyntheticScrollInfo(HWND hwnd, SCROLLBAR_CONSTANTS nBar, SCROLLINFO scrollInfo)
    {
        SCROLLINFO stored = s_scrollInfo.AddOrUpdate(
            ((nint)hwnd, nBar),
            scrollInfo,
            (_, existing) => MergeScrollInfo(existing, scrollInfo));

        return stored.nPos;
    }

    private static SCROLLINFO MergeScrollInfo(SCROLLINFO existing, SCROLLINFO incoming)
    {
        SCROLLINFO_MASK mask = incoming.fMask;

        if ((mask & SCROLLINFO_MASK.SIF_RANGE) != 0)
        {
            existing.nMin = incoming.nMin;
            existing.nMax = incoming.nMax;
        }

        if ((mask & SCROLLINFO_MASK.SIF_PAGE) != 0)
        {
            existing.nPage = incoming.nPage;
        }

        if ((mask & SCROLLINFO_MASK.SIF_POS) != 0)
        {
            existing.nPos = incoming.nPos;
        }

        if ((mask & SCROLLINFO_MASK.SIF_TRACKPOS) != 0)
        {
            existing.nTrackPos = incoming.nTrackPos;
        }

        existing.cbSize = incoming.cbSize;
        existing.fMask |= incoming.fMask;
        return existing;
    }
}
