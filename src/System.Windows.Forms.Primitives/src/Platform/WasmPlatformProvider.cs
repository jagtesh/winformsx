namespace System.Windows.Forms.Platform;

internal class WasmPlatformProvider : IPlatformProvider
{
    private readonly WasmUser32Interop _user32 = new();
    private readonly WasmGdi32Interop _gdi32 = new();
    private readonly WasmUxThemeInterop _uxTheme = new();

    public IUser32Interop User32 => _user32;
    public IGdi32Interop Gdi32 => _gdi32;
    public IUxThemeInterop UxTheme => _uxTheme;

    private class WasmUser32Interop : IUser32Interop
    {
        public HWND CreateWindowEx(WINDOW_EX_STYLE dwExStyle, string lpClassName, string lpWindowName, WINDOW_STYLE dwStyle, int X, int Y, int nWidth, int nHeight, HWND hWndParent, global::Windows.Win32.UI.WindowsAndMessaging.HMENU hMenu, HINSTANCE hInstance, object? lpParam)
        {
            return new HWND((nint)0x9999);
        }

        public bool DestroyWindow(HWND hWnd) => true;

        public LRESULT DefWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
        {
            return new LRESULT(0);
        }

        public LRESULT SendMessage(HWND hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default)
        {
            return new LRESULT(0);
        }

        public bool PostMessage(HWND hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default)
        {
            return true;
        }

        public unsafe BOOL GetWindowRect(HWND hWnd, out RECT lpRect)
        {
            // TODO: Query main.js for actual canvas bounds.
            lpRect = new RECT { left = 0, top = 0, right = 900, bottom = 680 };
            return true;
        }

        public BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        {
            // Windows are drawn strictly on the canvas. Ignored for now.
            return true;
        }

    }

    private class WasmGdi32Interop : IGdi32Interop
    {
        // Simple mock HDC for Wasm rendering pipeline
        private HDC _wasmHdc = new HDC((nint)0x1234);

        public HDC GetDC(HWND hWnd) => _wasmHdc;
        
        public HDC GetDCEx(HWND hWnd, HRGN hrgnClip, GET_DCX_FLAGS flags) => _wasmHdc;
        
        public int ReleaseDC(HWND hWnd, HDC hDC) => 1;

        public BOOL BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop)
        {
            // TODO: Route BitBlt to Impeller via JSInterop
            return true;
        }

        public HDC CreateCompatibleDC(HDC hdc) => new HDC((nint)0x5678);
    }

    private class WasmUxThemeInterop : IUxThemeInterop
    {
        public BOOL IsAppThemed() => false; // Completely bypass native themes on WebAssembly!
    }
}
