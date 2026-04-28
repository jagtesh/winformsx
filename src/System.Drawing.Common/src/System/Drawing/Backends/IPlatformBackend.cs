namespace System.Drawing;

public interface IPlatformBackend
{
    nint AcquireNextSurface();
    bool PresentSurface(nint surface);
}
