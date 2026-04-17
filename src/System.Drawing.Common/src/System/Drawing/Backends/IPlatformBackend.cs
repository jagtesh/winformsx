namespace System.Drawing;

public interface IPlatformBackend
{
    nint AcquireNextSurface();
    void PresentSurface(nint surface);
}
