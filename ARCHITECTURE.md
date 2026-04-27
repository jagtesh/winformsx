# UniWinForms Architecture & Design Principles

## Core Tenet: Cross-Platform Abstraction
UniWinForms is designed to be a true, cross-platform fork of the `dotnet/winforms` framework. The central architectural intent of this project is to create a seamless **Platform Abstraction Layer (PAL)** that transparently handles underlying OS specifics (like rendering, windowing, and message dispatch) without altering the core WinForms control implementations.

### 1. Do Not Modify UI Controls
The golden rule of UniWinForms development is that **the source code within standard UI controls (e.g., `ComboBox.cs`, `ListBox.cs`, `Button.cs`) must remain as close to the upstream Microsoft implementation as possible.**
- **NO INVASIVE GUARDS:** Do not scatter `if (Graphics.IsBackendActive)` checks inside native control methods or properties.
- **NO SYNTHETIC LOGIC IN CONTROLS:** Controls should not contain conditional logic to manually paint polygons or bypass native messaging just because they are running under the Impeller backend.
- **ALLOWED EDITS:** The *only* acceptable modifications to control source code are for ripping out explicitly platform-locked code (e.g., legacy COM/ActiveX components) or applying targeted fixes that do not violate the overall architecture.

### 2. Shim at the Lowest Level (P/Invoke)
Because WinForms was originally built to wrap Win32 `HWND` controls, it relies heavily on dispatching messages to native window handles via `User32.SendMessageW`. 
Rather than updating thousands of `SendMessage` call sites or inserting conditional logic in every control, **the interception must occur at the P/Invoke boundary.**

**The Correct Approach:**
1. The `CsWin32` source generator provides the `[DllImport]` signatures.
2. The manual P/Invoke wrappers in `System.Windows.Forms.Primitives\src\Windows\Win32\` (e.g., `PInvoke.SendMessage.cs`) must intercept the calls.
3. If `Graphics.IsBackendActive` is true, the `PInvoke` wrapper redirects the call to the **PlatformApi** (e.g., `PlatformApi.Message.SendMessage`).
4. The `ImpellerMessageInterop` then processes the message, emulating the Win32 state in-memory (such as adding items to a virtual list or computing heights) and returning the expected Win32 `LRESULT`.

By doing this, the standard `ComboBox.cs` can blindly call `PInvoke.SendMessage(..., PInvoke.CB_ADDSTRING, ...)` exactly as it always has. It does not need to know whether the message is being handled by the Windows kernel or the Impeller PAL.

### 3. The Platform Abstraction Layer (PAL)
The PAL defines interfaces for all core OS interactions:
- `IWindowInterop` (Window creation, styling, sizing)
- `IMessageInterop` (Message dispatch, posting, and retrieval)
- `IGdiInterop` (Graphics, Device Contexts, Fonts)
- `IInputInterop` (Mouse, Keyboard state)

Providers (such as `ImpellerWindowInterop` and `ImpellerMessageInterop`) implement these interfaces. The `PlatformApi` static singleton routes calls to the active provider.

### 4. Rendering Strategy
WinForms relies on GDI/GDI+ (`System.Drawing`) and `ControlPaint` for drawing primitive elements. When running under a synthetic backend like Impeller:
- Do not rewrite `OnPaint` logic inside the WinForms controls.
- Instead, the abstraction layer must intercept `PInvoke.DrawFrameControl` or provide a compatible `Graphics` Device Context (`HDC`) proxy that translates native GDI calls into Impeller/Vulkan geometry commands.

### Summary
If a control crashes or fails to render under the Impeller backend, **the fix belongs in the PAL or the P/Invoke interception layer**, not in the control itself. Respect the boundaries to ensure the framework remains maintainable and compatible with future upstream `dotnet/winforms` updates.