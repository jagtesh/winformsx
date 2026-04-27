# UniWinForms - Impeller Rendering Backend Handoff

## 1. Current State
The project is transitioning to a synthetic UI rendering pipeline powered by the **Impeller** graphics engine via **Silk.NET**. The primary objective is to render `System.Windows.Forms` cross-platform without relying on underlying OS handles (`HWND`) or GDI Device Contexts (`HDC`).

**Achievements so far:**
- Integrated the `PlatformApi` abstraction layer.
- `WinFormsX.Samples` successfully hosts synthetic controls.
- Addressed multiple hard crashes during Handle creation by adding `if (Graphics.IsBackendActive)` guards to native Win32 message dispatch paths in specific controls (`ComboBox.cs`, `ListBox.cs`).
- Synthetic rendering for `ComboBox` (dropdown arrow) using `Graphics.FillPolygon` is implemented, bypassing `ControlPaint.DrawComboButton` which previously crashed due to `PInvoke.DrawFrameControl` requiring an `HDC`.

## 2. Active Issues
1. **Application Crash on Dropdown Open**
   - Clicking the `Lists & Combos` tab no longer crashes on load, but **opening the ComboBox dropdown still causes a hard crash (Exit Code 1).**
   - `ComboBox.ShowSyntheticDropDown` instantiates a synthetic `ListBox` to simulate the dropdown and attempts to add it to the top-level form's `Controls` collection.
   - The crash happens natively (likely within Silk.NET, Impeller, or unmanaged CsWin32 execution space) because our managed `Application.OnThreadException` hooks in `ImpellerWindowInterop.cs` are not capturing it.
2. **Invasive Control-Level Modifications (Architectural Smell)**
   - We have inserted `if (Graphics.IsBackendActive)` logic directly into core WinForms controls (e.g., `ComboBox.NativeAdd`, `ListBox.NativeSetSelected`).
   - *This violates the fundamental cross-platform architecture goal*, which dictates that standard controls should remain untouched, while the lower-level API layers (PAL) absorb and route native requests.

## 3. Constraints & Root Causes
- **Native Message Dispatch (`PInvoke.SendMessage`)**: The root of most synthetic rendering crashes. WinForms heavily relies on `User32.SendMessageW` to mutate native control state (e.g., `LB_ADDSTRING`). Because `Graphics.IsBackendActive` operates with virtual/dummy `HWND` pointers, the OS rejects the message, returns an error code (`-1`), which the managed control interprets as an `OutOfMemoryException` or Win32 failure.
- **The PAL Gap**: We have an `ImpellerMessageInterop` in the `Platform` namespace, but the 2,900+ call sites across the WinForms repository still use `PInvoke.SendMessage` directly.
- **Exception Masking**: When an exception escapes the Impeller event loop, the native Silk.NET process tears down the host application. If `Application.OnThreadException` is invoked, it may attempt to instantiate a native `ThreadExceptionDialog` (which itself tries to create `HWND`s and crashes).

## 4. Strategic Fix Implementation (Next Steps)
To resolve these issues cleanly and adhere to the architectural vision:

### Step A: Implement a Global `SendMessage` Interceptor
Instead of guarding individual `NativeAdd` or `NativeInsert` methods inside the UI controls, we must intercept messages at the `PInvoke` layer.
- Edit `src\System.Windows.Forms.Primitives\src\Windows\Win32\PInvoke.SendMessage.cs`.
- Add an interceptor to the manually crafted `SendMessage` overloads:
```csharp
public static unsafe LRESULT SendMessage<T>(T hWnd, MessageId Msg, WPARAM wParam, string? lParam) where T : IHandle<HWND>
{
    if (Graphics.IsBackendActive)
    {
        return PlatformApi.Message.SendMessage(hWnd.Handle, (uint)Msg, wParam, (LPARAM)c);
    }
    // ... default P/Invoke behavior
}
```
- The `ImpellerMessageInterop` must then emulate an in-memory Win32 ListBox/ComboBox state (handling `CB_ADDSTRING`, `LB_GETTEXT`, `LB_SETCURSEL`) and returning expected Win32 `LRESULT` values.

### Step B: Revert Invasive Control Edits
Once the `SendMessage` shim is securely established, all `Graphics.IsBackendActive` guards should be rolled back from:
- `ComboBox.cs` (`NativeAdd`, `NativeClear`, `NativeInsert`, `NativeRemoveAt`, `UpdateItemHeight`)
- `ListBox.cs` (`NativeAdd`, `NativeSetSelected`)

### Step C: Secure Exception Handling
- Ensure `Application.OnThreadException` in the Impeller loop does not trigger native UI Dialogs. If the backend is active, exceptions must be routed exclusively to `Console.Error` or a pure-managed synthetic dialog to prevent cascading unmanaged crashes.