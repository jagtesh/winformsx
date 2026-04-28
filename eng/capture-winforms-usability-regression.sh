#!/bin/zsh
set -euo pipefail

OUT_ROOT="${1:-/tmp/winformsx_usability_regression}"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_ROOT"

WINFORMSX_CLICK_POINTS='0.226,0.108 0.372,0.108 0.665,0.108 0.812,0.108 0.957,0.108 0.080,0.065 0.080,0.112' \
WINFORMSX_AFTER_CLICK_DELAY="${WINFORMSX_AFTER_CLICK_DELAY:-1.0}" \
  eng/capture-winforms-click-regression.sh "$OUT_ROOT/navigation"

WINFORMSX_CLICK_POINTS='0.372,0.108 0.250,0.220 0.250,0.270 0.250,0.250 0.250,0.315 0.170,0.480 0.780,0.445' \
WINFORMSX_AFTER_CLICK_DELAY="${WINFORMSX_AFTER_CLICK_DELAY:-0.6}" \
  eng/capture-winforms-click-regression.sh "$OUT_ROOT/lists-combos"

WINFORMSX_CLICK_POINTS='0.957,0.108 0.120,0.300 0.840,0.305 0.240,0.670 0.840,0.440 0.160,0.340' \
WINFORMSX_AFTER_CLICK_DELAY="${WINFORMSX_AFTER_CLICK_DELAY:-0.6}" \
  eng/capture-winforms-click-regression.sh "$OUT_ROOT/data"

echo "[USABILITY_REGRESSION_OK] $OUT_ROOT"
