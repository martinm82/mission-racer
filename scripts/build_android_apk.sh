#!/usr/bin/env bash
# ==============================================================================
# Mission Racer 3D — 1-Click Native Android APK Builder (Tablet & Phone)
# ==============================================================================
# Produces a signed, ready-to-install native Android app:
#   build/MissionRacer3D.apk
# Also served automatically at:
#   http://<Mac-LAN-IP>:3000/MissionRacer3D.apk
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ANDROID_DIR="${PROJECT_ROOT}/android"
APP_SRC_MAIN="${ANDROID_DIR}/app/src/main"
ASSETS_DIR="${APP_SRC_MAIN}/assets"
RES_DIR="${APP_SRC_MAIN}/res"
WEB_SRC_DIR="${PROJECT_ROOT}/tools/mission-control-web"
NFC_SRC_DIR="${PROJECT_ROOT}/nfc-license/templates"
BUILD_DIR="${PROJECT_ROOT}/build"
APK_OUT="${BUILD_DIR}/MissionRacer3D.apk"

export JAVA_HOME="/usr/local/opt/java/libexec/openjdk.jdk/Contents/Home"
if [ ! -d "${JAVA_HOME}" ]; then
  export JAVA_HOME="/usr/local/opt/java"
fi
export PATH="${JAVA_HOME}/bin:${PATH}"

echo "=================================================================="
echo "🤖 MISSION RACER 3D — NATIVE ANDROID TABLET & PHONE APK BUILDER"
echo "=================================================================="
java -version

# ------------------------------------------------------------------------------
# 1. Sync 3D Game Assets & Launcher Icon into Android Project
# ------------------------------------------------------------------------------
echo "📦 [1/4] Bundling 3D tracks, supercars, audio & NFC assets into Android APK..."
mkdir -p "${ASSETS_DIR}/nfc-license/templates"
mkdir -p "${RES_DIR}/drawable"
mkdir -p "${BUILD_DIR}"

cat "${WEB_SRC_DIR}/three.min.js" > "${ASSETS_DIR}/three.min.js"
cat "${WEB_SRC_DIR}/wan-multiplayer.js" > "${ASSETS_DIR}/wan-multiplayer.js"
cat "${WEB_SRC_DIR}/manifest.json" > "${ASSETS_DIR}/manifest.json"
cat "${WEB_SRC_DIR}/icon.svg" > "${ASSETS_DIR}/icon.svg"
if [ -d "${NFC_SRC_DIR}" ]; then
  for svg in "${NFC_SRC_DIR}/"*.svg; do
    [ -f "$svg" ] && cat "$svg" > "${ASSETS_DIR}/nfc-license/templates/$(basename "$svg")"
  done
fi

# Convert root-relative paths to asset-relative paths for file:///android_asset/index.html
sed \
  -e 's|href="/manifest.json"|href="manifest.json"|g' \
  -e 's|href="/icon.svg"|href="icon.svg"|g' \
  -e 's|src="/three.min.js"|src="three.min.js"|g' \
  -e 's|src="/wan-multiplayer.js"|src="wan-multiplayer.js"|g' \
  -e 's|/nfc-license/templates/|nfc-license/templates/|g' \
  "${WEB_SRC_DIR}/index.html" > "${ASSETS_DIR}/index.html"

# Also keep iOS WebBundle in sync
IOS_WEBBUNDLE="${PROJECT_ROOT}/ios/MissionRacer3D/WebBundle"
if [ -d "${IOS_WEBBUNDLE}" ]; then
  cp -X -f "${WEB_SRC_DIR}/wan-multiplayer.js" "${IOS_WEBBUNDLE}/wan-multiplayer.js"
  cp -X -f "${ASSETS_DIR}/index.html" "${IOS_WEBBUNDLE}/index.html"
fi

if [ -f "/tmp/icon-192.png" ]; then
  cat "/tmp/icon-192.png" > "${RES_DIR}/drawable/ic_launcher.png"
elif [ ! -f "${RES_DIR}/drawable/ic_launcher.png" ]; then
  sips -z 192 192 "${PROJECT_ROOT}/ios/MissionRacer3D/Assets.xcassets/AppIcon.appiconset/AppIcon-1024.png" --out "/tmp/ic_launcher.png" >/dev/null
  cat "/tmp/ic_launcher.png" > "${RES_DIR}/drawable/ic_launcher.png"
fi

# ------------------------------------------------------------------------------
# 2. Ensure Android SDK (platforms;android-34 & build-tools;34.0.0)
# ------------------------------------------------------------------------------
if [ -d "${BUILD_DIR}/android-sdk/platforms/android-34" ]; then
  ANDROID_SDK_ROOT="${BUILD_DIR}/android-sdk"
else
  ANDROID_SDK_ROOT="${HOME}/Library/Android/sdk"
fi
CMDLINE_TOOLS_BIN="${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager"

if [ ! -f "${ANDROID_SDK_ROOT}/platforms/android-34/android.jar" ] || [ ! -f "${ANDROID_SDK_ROOT}/build-tools/34.0.0/aapt2" ] || [ ! -f "${ANDROID_SDK_ROOT}/platform-tools/adb" ]; then
  echo "⬇️  [2/4] Setting up Android SDK Build-Tools 34.0.0, Platform-Tools (adb) & Android 34 Platform..."
  mkdir -p "${ANDROID_SDK_ROOT}/cmdline-tools"
  if [ ! -f "${CMDLINE_TOOLS_BIN}" ]; then
    curl -fL "https://dl.google.com/android/repository/commandlinetools-mac-11076708_latest.zip" -o "/tmp/cmdtools.zip"
    rm -rf "/tmp/cmdline-tools-extract"
    mkdir -p "/tmp/cmdline-tools-extract"
    unzip -q "/tmp/cmdtools.zip" -d "/tmp/cmdline-tools-extract"
    rm -rf "${ANDROID_SDK_ROOT}/cmdline-tools/latest"
    mv "/tmp/cmdline-tools-extract/cmdline-tools" "${ANDROID_SDK_ROOT}/cmdline-tools/latest"
  fi
  yes | "${CMDLINE_TOOLS_BIN}" --sdk_root="${ANDROID_SDK_ROOT}" --licenses >/dev/null 2>&1 || true
  "${CMDLINE_TOOLS_BIN}" --sdk_root="${ANDROID_SDK_ROOT}" "platforms;android-34" "build-tools;34.0.0" "platform-tools"
else
  echo "✅ [2/4] Android SDK 34, Build-Tools 34.0.0 & Platform-Tools (adb) already installed."
fi

# Provide convenient symlink inside project build/android-sdk as well
if [ "${ANDROID_SDK_ROOT}" != "${BUILD_DIR}/android-sdk" ]; then
  ln -sfn "${ANDROID_SDK_ROOT}" "${BUILD_DIR}/android-sdk"
fi

AAPT2="${ANDROID_SDK_ROOT}/build-tools/34.0.0/aapt2"
D8="${ANDROID_SDK_ROOT}/build-tools/34.0.0/d8"
ZIPALIGN="${ANDROID_SDK_ROOT}/build-tools/34.0.0/zipalign"
APKSIGNER="${ANDROID_SDK_ROOT}/build-tools/34.0.0/apksigner"
ANDROID_JAR="${ANDROID_SDK_ROOT}/platforms/android-34/android.jar"

# ------------------------------------------------------------------------------
# 3. Compile Resources, Java Sources & DEX Bytecode
# ------------------------------------------------------------------------------
echo "🛠️  [3/4] Compiling Native Android Tablet & Phone APK..."
WORK_DIR="$(mktemp -d /tmp/missionracer-apk-build.XXXXXX)"
trap 'rm -rf "${WORK_DIR}"' EXIT
mkdir -p "${WORK_DIR}/compiled_res" "${WORK_DIR}/gen" "${WORK_DIR}/classes" "${WORK_DIR}/dex"

"${AAPT2}" compile --dir "${RES_DIR}" -o "${WORK_DIR}/compiled_res/res.zip"

"${AAPT2}" link \
  -o "${WORK_DIR}/unaligned.apk" \
  -I "${ANDROID_JAR}" \
  --manifest "${APP_SRC_MAIN}/AndroidManifest.xml" \
  -A "${ASSETS_DIR}" \
  --java "${WORK_DIR}/gen" \
  "${WORK_DIR}/compiled_res/res.zip"

javac --release 8 \
  -cp "${ANDROID_JAR}" \
  -d "${WORK_DIR}/classes" \
  "${WORK_DIR}/gen/com/migasiew/missionracer3d/R.java" \
  "${APP_SRC_MAIN}/java/com/migasiew/missionracer3d/MainActivity.java"

CLASS_FILES=()
while IFS= read -r -d '' f; do
  CLASS_FILES+=("$f")
done < <(find "${WORK_DIR}/classes" -name "*.class" -print0)

"${D8}" --lib "${ANDROID_JAR}" --output "${WORK_DIR}/dex" "${CLASS_FILES[@]}"

(cd "${WORK_DIR}/dex" && zip -q "${WORK_DIR}/unaligned.apk" classes.dex)

"${ZIPALIGN}" -f -p 4 "${WORK_DIR}/unaligned.apk" "${WORK_DIR}/aligned.apk"

# ------------------------------------------------------------------------------
# 4. Sign APK with Android Keystore
# ------------------------------------------------------------------------------
echo "🔐 [4/4] Signing MissionRacer3D.apk..."
KEYSTORE="${BUILD_DIR}/android-debug.keystore"
if [ ! -f "${KEYSTORE}" ]; then
  keytool -genkeypair \
    -v \
    -keystore "${KEYSTORE}" \
    -alias androiddebugkey \
    -keyalg RSA \
    -keysize 2048 \
    -validity 10000 \
    -storepass android \
    -keypass android \
    -dname "CN=Android Debug,O=Android,C=US" >/dev/null 2>&1
fi

"${APKSIGNER}" sign \
  --ks "${KEYSTORE}" \
  --ks-key-alias androiddebugkey \
  --ks-pass pass:android \
  --key-pass pass:android \
  --out "${APK_OUT}" \
  "${WORK_DIR}/aligned.apk"

"${APKSIGNER}" verify "${APK_OUT}"

echo "=================================================================="
echo "✅ NATIVE ANDROID APK BUILT & SIGNED SUCCESSFULLY!"
echo "• APK File:       ${APK_OUT} ($(du -h "${APK_OUT}" | awk '{print $1}'))"
echo "• Download URL:   http://$(ipconfig getifaddr en0 2>/dev/null || echo localhost):3000/MissionRacer3D.apk"
echo "=================================================================="
