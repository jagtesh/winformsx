#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_frame_stress_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
STRESS_SCRIPT="/tmp/winformsx_frame_stress.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
sleep 0.3

: > "$RUN_LOG"
: > "$TRACE_LOG"

BEFORE="$OUT_DIR/before-stress.png"
AFTER="$OUT_DIR/after-stress.png"

is_nonblank_capture() {
  python3 - "$1" <<'PY'
import sys
from PIL import Image, ImageStat

image = Image.open(sys.argv[1]).convert("RGB")
width, height = image.size
if width < 200 or height < 200:
    raise SystemExit(1)

crop = image.crop((int(width * 0.05), int(height * 0.12), int(width * 0.95), int(height * 0.88)))
sample = crop.resize((220, max(1, int(220 * crop.height / crop.width))))
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
  echo "[FRAME_STRESS_FAIL] baseline capture is still blank after waiting"
  exit 1
fi

cat > "$STRESS_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"
let iterations = Int(ProcessInfo.processInfo.environment["WINFORMSX_FRAME_STRESS_ITERATIONS"] ?? "160") ?? 160
let intervalUs = useconds_t(Int(ProcessInfo.processInfo.environment["WINFORMSX_FRAME_STRESS_INTERVAL_US"] ?? "4000") ?? 4000)
let downUpUs = useconds_t(Int(ProcessInfo.processInfo.environment["WINFORMSX_FRAME_STRESS_DOWN_UP_US"] ?? "2500") ?? 2500)
let tabPoints: [(Double, Double)] = [
    (0.080, 0.125),
    (0.226, 0.125),
    (0.372, 0.125),
    (0.519, 0.125),
    (0.665, 0.125),
    (0.812, 0.125),
    (0.957, 0.125),
]

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

func click(_ rect: CGRect, normalizedX: Double, normalizedY: Double) {
    let point = CGPoint(
        x: rect.minX + rect.width * CGFloat(max(0.0, min(1.0, normalizedX))),
        y: rect.minY + rect.height * CGFloat(max(0.0, min(1.0, normalizedY))))
    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(downUpUs)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(downUpUs)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
}

guard let rect = targetWindow() else {
    print("[FRAME_STRESS_INPUT_ERROR] target window not found")
    exit(1)
}

for i in 0..<iterations {
    let point = tabPoints[i % tabPoints.count]
    click(rect, normalizedX: point.0, normalizedY: point.1)
    usleep(intervalUs)
}

print("[FRAME_STRESS_INPUT_OK] iterations=\(iterations) intervalUs=\(intervalUs)")
SWIFT

swift "$STRESS_SCRIPT"
sleep "${WINFORMSX_AFTER_STRESS_DELAY:-1.0}"
eng/capture-winforms-screen.sh "$AFTER"

python3 - "$BEFORE" "$AFTER" <<'PY'
import sys
from PIL import Image, ImageChops, ImageStat

def stats(path: str) -> tuple[float, float, int]:
    image = Image.open(path).convert("RGB")
    width, height = image.size
    if width < 200 or height < 200:
        raise SystemExit(f"[FRAME_STRESS_FAIL] capture too small: {path} {width}x{height}")

    crop = image.crop((int(width * 0.05), int(height * 0.12), int(width * 0.95), int(height * 0.88)))
    sample = crop.resize((220, max(1, int(220 * crop.height / crop.width))))
    stat = ImageStat.Stat(sample)
    colors = sample.getcolors(maxcolors=2_000_000) or []
    return sum(stat.mean) / 3.0, sum(stat.stddev) / 3.0, len(colors)

def tab_text_score(path: str) -> int:
    image = Image.open(path).convert("RGB")
    width, height = image.size
    tab_strip = image.crop((
        int(width * 0.06),
        int(height * 0.115),
        int(width * 0.94),
        int(height * 0.155)))
    strip_width, strip_height = tab_strip.size
    inner = tab_strip.crop((
        0,
        int(strip_height * 0.18),
        strip_width,
        int(strip_height * 0.82))).convert("L")
    return sum(1 for pixel in inner.getdata() if pixel < 100)

before, after = sys.argv[1], sys.argv[2]
before_mean, before_std, before_unique = stats(before)
after_mean, after_std, after_unique = stats(after)
if after_std < 18 or after_unique < 50:
    raise SystemExit(
        f"[FRAME_STRESS_FAIL] final capture appears blank "
        f"(mean={after_mean:.1f}, std={after_std:.1f}, unique={after_unique})")

if before_std >= 18 and before_unique >= 50 and after_std < before_std * 0.35:
    raise SystemExit(
        f"[FRAME_STRESS_FAIL] final capture lost most visible content "
        f"(before_std={before_std:.1f}, after_std={after_std:.1f})")

after_tab_text = tab_text_score(after)
if after_tab_text < 500:
    raise SystemExit(
        f"[FRAME_STRESS_FAIL] final capture lost tab/menu text "
        f"(tab_text_score={after_tab_text})")

print(
    "[FRAME_STRESS_OK] "
    f"before mean={before_mean:.1f} std={before_std:.1f} unique={before_unique}; "
    f"after mean={after_mean:.1f} std={after_std:.1f} unique={after_unique}; "
    f"tab_text_score={after_tab_text}")
PY

if rg -n "ThreadException|Paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_frame_stress_errors.log 2>/dev/null; then
  cat /tmp/winformsx_frame_stress_errors.log
  echo "[FRAME_STRESS_FAIL] runtime log contains errors"
  exit 1
fi

echo "[FRAME_STRESS_ARTIFACTS] $OUT_DIR"
