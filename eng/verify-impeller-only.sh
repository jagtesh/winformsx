#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

failures=0

check() {
  local title="$1"
  local pattern="$2"
  shift 2

  local matches
  matches="$(rg -n "$pattern" "$@" || true)"
  if [[ -n "$matches" ]]; then
    echo "[IMP_ONLY_ERROR] $title"
    echo "$matches"
    failures=$((failures + 1))
  fi
}

check_disallowed_imports() {
  local matches
  matches="$(rg -n '\[(DllImport|LibraryImport)\(' "${runtime_paths[@]}" \
    | rg -v 'src/System.Drawing.Common/src/System/Drawing/Impeller/NativeMethods.cs' \
    | rg -v 'src/System.Drawing.Common/src/System/Drawing/Backends/Text/HarfBuzzNative.cs' \
    || true)"

  if [[ -n "$matches" ]]; then
    echo "[IMP_ONLY_ERROR] Runtime native imports must be limited to Impeller and HarfBuzz bindings."
    echo "$matches"
    failures=$((failures + 1))
  fi
}

runtime_paths=(
  "src/System.Windows.Forms/src"
  "src/System.Windows.Forms.Primitives/src"
  "src/System.Drawing.Common/src"
  "src/System.Private.Windows.Core/src"
)

check_disallowed_imports

check \
  "Real Windows DLL imports are not allowed in WinFormsX runtime code." \
  '\[(DllImport|LibraryImport)\((Libraries\.)?(USER32|user32|User32|GDI32|gdi32|Gdi32|gdiplus|Gdiplus|COMCTL32|comctl32|Comctl32|COMDLG32|comdlg32|Comdlg32|KERNEL32|kernel32|Kernel32|SHELL32|shell32|Shell32|OLE32|ole32|Ole32)' \
  "${runtime_paths[@]}"

check \
  "Host SystemEvents are not allowed; route preference/display changes through PAL." \
  '\bSystemEvents\.' \
  "src/System.Windows.Forms/src" \
  "src/System.Drawing.Common/src"

check \
  "Active drawing code must not call GDI+ entrypoints; route through Drawing PAL." \
  '\bPInvoke(Core)?\.Gdip|Gdip\.CheckStatus|GdiPlusInitialization\.EnsureInitialized' \
  "src/System.Windows.Forms/src" \
  "src/System.Drawing.Common/src" \
  "src/System.Private.Windows.Core/src"

if [[ "$failures" -ne 0 ]]; then
  echo "[IMP_ONLY_ERROR] Found $failures Impeller-only architecture violation group(s)."
  exit 1
fi

echo "[IMP_ONLY_OK] WinFormsX runtime passed Impeller-only architecture checks."
