// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Windows.Forms.Platform;

namespace System.Windows.Forms.Tests;

public class ImpellerWindowInteropTests
{
    [Fact]
    public void CreateWindowEx_MessageOnlyTopLevel_DoesNotCreateBackendSurface()
    {
        ImpellerWindowInterop windowInterop = new();
        HWND hwnd = windowInterop.CreateWindowEx(
            default,
            "WinFormsXTestMessageOnly",
            null,
            WINDOW_STYLE.WS_OVERLAPPED,
            0,
            0,
            0,
            0,
            HWND.HWND_MESSAGE,
            HMENU.Null,
            HINSTANCE.Null,
            null);

        try
        {
            hwnd.Should().NotBe(HWND.Null);
            windowInterop.GetWindowState(hwnd).Should().NotBeNull();
            windowInterop.GetSilkWindow(hwnd).Should().BeNull();
        }
        finally
        {
            windowInterop.DestroyWindow(hwnd);
        }
    }

    [Theory]
    [InlineData(200, 100, 900, 600, 200, 100)]
    [InlineData(2000, 1300, 900, 600, 899, 599)]
    public void ClampMousePointToLogical_TreatsSilkMousePositionAsLogicalClientCoordinates(
        float x,
        float y,
        int logicalW,
        int logicalH,
        int expectedX,
        int expectedY)
    {
        Point point = ImpellerWindowInterop.ClampMousePointToLogical(
            new PointF(x, y),
            logicalW,
            logicalH);

        point.Should().Be(new Point(expectedX, expectedY));
    }

    [Theory]
    [InlineData(200, 100, 900, 600, 1800, 1200, 100, 50)]
    [InlineData(200, 100, 900, 600, 900, 600, 200, 100)]
    [InlineData(2000, 1300, 900, 600, 1800, 1200, 899, 599)]
    public void ScaleMousePointToLogical_ScalesFramebufferCoordinatesToLogicalClient(
        float x,
        float y,
        int logicalW,
        int logicalH,
        int framebufferW,
        int framebufferH,
        int expectedX,
        int expectedY)
    {
        Point point = ImpellerWindowInterop.ScaleMousePointToLogical(
            new PointF(x, y),
            logicalW,
            logicalH,
            framebufferW,
            framebufferH);

        point.Should().Be(new Point(expectedX, expectedY));
    }
}
