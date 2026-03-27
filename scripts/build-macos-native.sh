#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must run on macOS." >&2
  exit 1
fi

APP_NAME="${APP_NAME:-KunQiongBrowser}"
BINARY_NAME="${BINARY_NAME:-KunQiongBrowser}"
MIN_MACOS="${MIN_MACOS:-12.0}"
APP_VERSION="${APP_VERSION:-1.0.0}"
PORTABLE_MODE="${PORTABLE_MODE:-1}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SOURCE_FILE="${REPO_ROOT}/mac_app/KunQiongBrowserMac.swift"
OUT_ROOT="${REPO_ROOT}/artifacts/macos/native"
PORTABLE_OUT="${REPO_ROOT}/artifacts/macos/portable"
X64_OUT="${OUT_ROOT}/osx-x64"
ARM64_OUT="${OUT_ROOT}/osx-arm64"
UNIVERSAL_OUT="${OUT_ROOT}/universal"

if [[ ! -f "${SOURCE_FILE}" ]]; then
  echo "Source not found: ${SOURCE_FILE}" >&2
  exit 1
fi

mkdir -p "${X64_OUT}" "${ARM64_OUT}" "${UNIVERSAL_OUT}" "${PORTABLE_OUT}"

echo "Build config:"
echo "  APP_NAME      = ${APP_NAME}"
echo "  APP_VERSION   = ${APP_VERSION}"
echo "  MIN_MACOS     = ${MIN_MACOS}"
echo "  PORTABLE_MODE = ${PORTABLE_MODE}"

compile_arch() {
  local target="$1"
  local output_dir="$2"

  echo "Compiling ${target}..."
  swiftc "${SOURCE_FILE}" \
    -O \
    -target "${target}" \
    -framework Cocoa \
    -framework WebKit \
    -o "${output_dir}/${BINARY_NAME}"
}

create_info_plist() {
  local plist_path="$1"
  cat > "${plist_path}" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>${BINARY_NAME}</string>
  <key>CFBundleIdentifier</key>
  <string>com.kunqiong.browser.mac</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${APP_NAME}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSMinimumSystemVersion</key>
  <string>${MIN_MACOS}</string>
  <key>NSPrincipalClass</key>
  <string>NSApplication</string>
</dict>
</plist>
EOF
}

create_app_bundle() {
  local output_dir="$1"
  local app_dir="${output_dir}/${APP_NAME}.app"
  local contents_dir="${app_dir}/Contents"
  local macos_dir="${contents_dir}/MacOS"
  local resources_dir="${contents_dir}/Resources"

  rm -rf "${app_dir}"
  mkdir -p "${macos_dir}" "${resources_dir}"

  cp "${output_dir}/${BINARY_NAME}" "${macos_dir}/${BINARY_NAME}"
  chmod +x "${macos_dir}/${BINARY_NAME}"
  create_info_plist "${contents_dir}/Info.plist"

  if [[ "${PORTABLE_MODE}" == "1" ]]; then
    touch "${resources_dir}/portable.mode"
    mkdir -p "${resources_dir}/portable-data"
    touch "${resources_dir}/portable-data/.keep"
  fi

  if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "${app_dir}" >/dev/null 2>&1 || true
  fi
}

package_zip() {
  local output_dir="$1"
  local arch_tag="$2"
  local app_dir="${output_dir}/${APP_NAME}.app"
  local flavor="standard"
  if [[ "${PORTABLE_MODE}" == "1" ]]; then
    flavor="portable"
  fi
  local zip_name="${APP_NAME}-macos-${arch_tag}-${flavor}-v${APP_VERSION}.zip"
  local zip_path="${PORTABLE_OUT}/${zip_name}"
  rm -f "${zip_path}"
  ditto -c -k --sequesterRsrc --keepParent "${app_dir}" "${zip_path}"
}

compile_arch "x86_64-apple-macos${MIN_MACOS}" "${X64_OUT}"
compile_arch "arm64-apple-macos${MIN_MACOS}" "${ARM64_OUT}"

echo "Creating universal binary..."
lipo -create \
  -output "${UNIVERSAL_OUT}/${BINARY_NAME}" \
  "${X64_OUT}/${BINARY_NAME}" \
  "${ARM64_OUT}/${BINARY_NAME}"

create_app_bundle "${X64_OUT}"
create_app_bundle "${ARM64_OUT}"
create_app_bundle "${UNIVERSAL_OUT}"

package_zip "${X64_OUT}" "x64"
package_zip "${ARM64_OUT}" "arm64"
package_zip "${UNIVERSAL_OUT}" "universal"

CHECKSUM_FILE="${PORTABLE_OUT}/SHA256SUMS.txt"
(
  cd "${PORTABLE_OUT}"
  shasum -a 256 ./*.zip > "${CHECKSUM_FILE}"
)

echo
echo "Build complete:"
echo "  x64 app       -> ${X64_OUT}/${APP_NAME}.app"
echo "  arm64 app     -> ${ARM64_OUT}/${APP_NAME}.app"
echo "  universal app -> ${UNIVERSAL_OUT}/${APP_NAME}.app"
echo "  portable zips -> ${PORTABLE_OUT}"
echo "  checksums     -> ${CHECKSUM_FILE}"
