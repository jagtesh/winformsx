#pragma warning disable IDE0005
using System.Drawing;
#pragma warning disable IDE0005
using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

internal class ImpellerPlatformProvider : IPlatformProvider
{
    private readonly WindowsPlatformProvider _baseProvider = new();

    public static void Initialize()
    {
        // Tell System.Drawing.Common to use Impeller
        System.Drawing.Impeller.ImpellerBackendInitializer.Register();
    }

    public IUser32Interop User32 => _baseProvider.User32;
    public IGdi32Interop Gdi32 => _baseProvider.Gdi32;
    public IUxThemeInterop UxTheme => _baseProvider.UxTheme;


}
