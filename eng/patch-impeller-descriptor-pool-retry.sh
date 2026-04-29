#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: eng/patch-impeller-descriptor-pool-retry.sh PATH_TO_NATIVE_IMPELLER_LIBRARY" >&2
  exit 2
fi

library="$1"
if [[ ! -f "$library" ]]; then
  echo "Impeller native library not found: $library" >&2
  exit 1
fi

library_info="$(file "$library")"
case "$library_info:$(basename "$library")" in
  *"Mach-O 64-bit dynamically linked shared library arm64"*:libimpeller.dylib)
    offset=$((0x4a8678))
    expected="080f8552888cb8721f00086b01020054"
    replacement="080080521f2003d51f00086b00020054"
    ;;
  *"PE32+ executable (DLL)"*"x86-64"*:impeller.dll)
    offset=$((0x34a01c))
    # Replace "cmp eax, VK_ERROR_OUT_OF_POOL_MEMORY; jne skip_retry" with
    # "test eax, eax; je skip_retry" so all non-success results retry once.
    expected="3d782864c47546"
    replacement="85c09090907446"
    ;;
  *)
    echo "No descriptor-pool retry patch is defined for this library: $library_info" >&2
    exit 0
    ;;
esac

if [[ ${#expected} -ne ${#replacement} ]]; then
  echo "Impeller descriptor-pool retry patch has mismatched byte lengths." >&2
  exit 1
fi

patch_length=$((${#expected} / 2))
actual="$(xxd -p -s "$offset" -l "$patch_length" "$library" | tr -d '\n')"
if [[ "$actual" == "$replacement" ]]; then
  echo "Impeller descriptor-pool retry patch already applied: $library"
  exit 0
fi

if [[ "$actual" != "$expected" ]]; then
  echo "Impeller descriptor-pool retry patch did not match expected bytes in $library" >&2
  echo "  offset:   $(printf '0x%x' "$offset")" >&2
  echo "  expected: $expected" >&2
  echo "  actual:   $actual" >&2
  exit 1
fi

printf '%s' "$replacement" | xxd -r -p | dd of="$library" bs=1 seek="$offset" conv=notrunc status=none

if command -v codesign >/dev/null 2>&1; then
  codesign --force --sign - "$library" >/dev/null 2>&1 || true
fi

echo "Applied Impeller descriptor-pool retry patch: $library"
