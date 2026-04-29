#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_textbox_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
INPUT_SCRIPT="/tmp/winformsx_textbox_input.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
sleep 0.3

BEFORE="$OUT_DIR/before-textbox.png"
AFTER="$OUT_DIR/after-textbox.png"

eng/capture-winforms-screen.sh "$BEFORE"

cat > "$INPUT_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"

func isTarget(owner: String, name: String) -> Bool {
    owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" || name == targetTitle
}

func targetWindow() -> CGRect? {
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
    for window in list {
        let owner = (window[kCGWindowOwnerName as String] as? String) ?? ""
        let name = (window[kCGWindowName as String] as? String) ?? ""
        guard isTarget(owner: owner, name: name) else { continue }
        let bounds = (window[kCGWindowBounds as String] as? [String: Any]) ?? [:]
        let x = CGFloat((bounds["X"] as? Double) ?? 0)
        let y = CGFloat((bounds["Y"] as? Double) ?? 0)
        let width = CGFloat((bounds["Width"] as? Double) ?? 0)
        let height = CGFloat((bounds["Height"] as? Double) ?? 0)
        if width > 0 && height > 0 {
            return CGRect(x: x, y: y, width: width, height: height)
        }
    }

    return nil
}

func click(_ rect: CGRect, _ nx: Double, _ ny: Double) {
    let point = CGPoint(
        x: rect.minX + rect.width * CGFloat(nx),
        y: rect.minY + rect.height * CGFloat(ny))
    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(40_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(40_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
}

func key(_ code: CGKeyCode) {
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true)?.post(tap: .cghidEventTap)
    usleep(20_000)
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false)?.post(tap: .cghidEventTap)
}

guard let rect = targetWindow() else {
    print("[TEXTBOX_INPUT_ERROR] target window not found")
    exit(1)
}

click(rect, 0.226, 0.125) // Basic Controls tab.
usleep(250_000)
click(rect, 0.165, 0.322) // Name TextBox.
usleep(120_000)

// Type a visible suffix. macOS virtual key 7 is "x" on the standard layout.
for _ in 0..<8 {
    key(7)
}

print("[TEXTBOX_INPUT_OK]")
SWIFT

swift "$INPUT_SCRIPT"
sleep "${WINFORMSX_AFTER_TEXT_DELAY:-0.5}"
eng/capture-winforms-screen.sh "$AFTER"

python3 - "$BEFORE" "$AFTER" <<'PY'
import sys
from PIL import Image, ImageChops, ImageStat

before = Image.open(sys.argv[1]).convert("RGB")
after = Image.open(sys.argv[2]).convert("RGB")
width, height = after.size

if width < 200 or height < 200:
    raise SystemExit("[TEXTBOX_FAIL] capture too small")

# Crop the left input area, ignoring title/menu/tab chrome.
crop = (
    int(width * 0.06),
    int(height * 0.25),
    int(width * 0.36),
    int(height * 0.40),
)

delta = ImageChops.difference(before.crop(crop), after.crop(crop))
stat = ImageStat.Stat(delta)
score = sum(stat.mean) / 3.0
if score < 0.75:
    raise SystemExit(f"[TEXTBOX_FAIL] textbox region did not visibly change after typing (score={score:.3f})")

print(f"[TEXTBOX_OK] visual-diff={score:.3f}")
PY

if rg -n "ThreadException|Paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_textbox_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_textbox_regression_errors.log
  echo "[TEXTBOX_FAIL] runtime log contains errors"
  exit 1
fi

echo "[TEXTBOX_ARTIFACTS] $OUT_DIR"
