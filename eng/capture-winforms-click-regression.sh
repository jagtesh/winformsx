#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_click_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
CLICK_SCRIPT="/tmp/winformsx_click_window.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

if [[ "${WINFORMSX_REUSE_RUNNING:-0}" != "1" ]]; then
  pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
  sleep 0.3
fi

: > "$RUN_LOG"
: > "$TRACE_LOG"

BEFORE="$OUT_DIR/before-click.png"
AFTER="$OUT_DIR/after-click.png"
CLICK_POINTS=("${(@s: :)${WINFORMSX_CLICK_POINTS:-0.50,0.35 0.25,0.20 0.15,0.55}}")

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
  echo "[REGRESSION_FAIL] baseline capture is still blank after waiting"
  exit 1
fi

cat > "$CLICK_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"
let normalizedX = Double(ProcessInfo.processInfo.environment["WINFORMSX_CLICK_X"] ?? "0.50") ?? 0.50
let normalizedY = Double(ProcessInfo.processInfo.environment["WINFORMSX_CLICK_Y"] ?? "0.35") ?? 0.35

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

    let point = CGPoint(
        x: x + width * CGFloat(max(0.0, min(1.0, normalizedX))),
        y: y + height * CGFloat(max(0.0, min(1.0, normalizedY))))

    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(50_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(50_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    print("[CLICK_OK] \(Int(point.x)),\(Int(point.y)) owner=\(owner) name=\(name)")
    exit(0)
}

print("[CLICK_ERROR] target window not found")
exit(1)
SWIFT

AFTER_IMAGES=()
index=1
for point in "${CLICK_POINTS[@]}"; do
  export WINFORMSX_CLICK_X="${point%,*}"
  export WINFORMSX_CLICK_Y="${point#*,}"
  swift "$CLICK_SCRIPT"
  sleep "${WINFORMSX_AFTER_CLICK_DELAY:-0.8}"
  image="$OUT_DIR/after-click-$index.png"
  eng/capture-winforms-screen.sh "$image"
  AFTER_IMAGES+=("$image")
  cp "$image" "$AFTER"
  index=$((index + 1))
done

python3 - "$BEFORE" "${AFTER_IMAGES[@]}" <<'PY'
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageStat

def stats(path: str) -> tuple[float, float, int]:
    image = Image.open(path).convert("RGB")
    width, height = image.size
    if width < 200 or height < 200:
        raise SystemExit(f"[REGRESSION_FAIL] capture too small: {path} {width}x{height}")

    # Ignore desktop margins/title chrome and inspect the client-heavy region.
    crop = image.crop((int(width * 0.05), int(height * 0.12), int(width * 0.95), int(height * 0.88)))
    sample = crop.resize((200, max(1, int(200 * crop.height / crop.width))))
    stat = ImageStat.Stat(sample)
    colors = sample.getcolors(maxcolors=2_000_000) or []
    return sum(stat.mean) / 3.0, sum(stat.stddev) / 3.0, len(colors)

before, *after_images = sys.argv[1:]
before_mean, before_std, before_unique = stats(before)
results = []
for after in after_images:
    after_mean, after_std, after_unique = stats(after)
    label = Path(after).name
    if after_std < 18 or after_unique < 50:
        raise SystemExit(
            f"[REGRESSION_FAIL] {label} appears blank "
            f"(mean={after_mean:.1f}, std={after_std:.1f}, unique={after_unique})")

    if before_std >= 18 and before_unique >= 50 and after_std < before_std * 0.35:
        raise SystemExit(
            f"[REGRESSION_FAIL] {label} lost most visible content "
            f"(before_std={before_std:.1f}, after_std={after_std:.1f})")

    results.append(f"{label} mean={after_mean:.1f} std={after_std:.1f} unique={after_unique}")

print(
    "[REGRESSION_OK] "
    f"before mean={before_mean:.1f} std={before_std:.1f} unique={before_unique}; "
    + "; ".join(results))
PY

if rg -n "ThreadException|Paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_click_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_click_regression_errors.log
  echo "[REGRESSION_FAIL] runtime log contains errors"
  exit 1
fi

echo "[REGRESSION_ARTIFACTS] $OUT_DIR"
