#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_hover_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
HOVER_SCRIPT="/tmp/winformsx_hover_window.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

if [[ "${WINFORMSX_REUSE_RUNNING:-0}" != "1" ]]; then
  pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
  sleep 0.3
fi

: > "$RUN_LOG"
: > "$TRACE_LOG"

BEFORE="$OUT_DIR/before-hover.png"
AFTER="$OUT_DIR/after-hover.png"

is_nonblank_capture() {
  python3 - "$1" <<'PY'
import sys
from PIL import Image, ImageStat

path = sys.argv[1]
image = Image.open(path).convert("RGB")
width, height = image.size
if width < 200 or height < 200:
    raise SystemExit(1)

crop = image.crop((int(width * 0.05), int(height * 0.12), int(width * 0.95), int(height * 0.88)))
sample = crop.resize((200, max(1, int(200 * crop.height / crop.width))))
stat = ImageStat.Stat(sample)
colors = sample.getcolors(maxcolors=2_000_000) or []
stddev = sum(stat.stddev) / 3.0
unique = len(colors)
raise SystemExit(0 if stddev >= 18 and unique >= 50 else 1)
PY
}

baseline_ready=0
for _ in {1..10}; do
  eng/capture-winforms-screen.sh "$BEFORE"
  if is_nonblank_capture "$BEFORE"; then
    baseline_ready=1
    break
  fi

  sleep "${WINFORMSX_BASELINE_RETRY_DELAY:-0.2}"
done

if [[ "$baseline_ready" != "1" ]]; then
  echo "[HOVER_FAIL] baseline capture is still blank after waiting"
  exit 1
fi

cat > "$HOVER_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"
let moves = Int(ProcessInfo.processInfo.environment["WINFORMSX_HOVER_MOVES"] ?? "240") ?? 240
let delayMicros = UInt32(ProcessInfo.processInfo.environment["WINFORMSX_HOVER_DELAY_US"] ?? "4000") ?? 4000

func isTarget(owner: String, name: String) -> Bool {
    owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" || name == targetTitle
}

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
    guard width > 0 && height > 0 else { continue }

    for i in 0..<max(1, moves) {
        let t = CGFloat(i) / CGFloat(max(1, moves - 1))
        let row = CGFloat((i / 24) % 8)
        let direction: CGFloat = Int(row).isMultiple(of: 2) ? 1 : -1
        let nx = direction > 0 ? 0.08 + (0.84 * t) : 0.92 - (0.84 * t)
        let ny = 0.18 + (0.70 * (row / 7.0))
        let point = CGPoint(x: x + width * nx, y: y + height * ny)
        CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
        usleep(delayMicros)
    }

    print("[HOVER_OK] moves=\(moves) owner=\(owner) name=\(name)")
    exit(0)
}

print("[HOVER_ERROR] target window not found")
exit(1)
SWIFT

swift "$HOVER_SCRIPT"
sleep "${WINFORMSX_AFTER_HOVER_DELAY:-0.8}"
eng/capture-winforms-screen.sh "$AFTER"

if ! is_nonblank_capture "$AFTER"; then
  echo "[HOVER_FAIL] after-hover capture appears blank"
  exit 1
fi

if rg -n "ThreadException|Paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_hover_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_hover_regression_errors.log
  echo "[HOVER_FAIL] runtime log contains errors"
  exit 1
fi

echo "[HOVER_OK] artifacts=$OUT_DIR"
