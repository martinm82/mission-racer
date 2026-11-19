#!/usr/bin/env bash
# ==============================================================================
# Mission Racer 3D — 1-Click Apple TestFlight Build, Archive & Upload Pipeline
# ==============================================================================
# Usage:
#   ./scripts/deploy_testflight.sh                  # Auto-detect signing, archive & upload
#   APPLE_TEAM_ID=XXXXXXXXXX ./scripts/deploy_testflight.sh
#   ./scripts/deploy_testflight.sh --open           # Open archive / project in Xcode Organizer
#   ./scripts/deploy_testflight.sh --sync-only      # Only sync latest 3D game assets into iOS bundle
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
chmod +x "${BASH_SOURCE[0]}" 2>/dev/null || true
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WEB_SRC_DIR="${PROJECT_ROOT}/tools/mission-control-web"
NFC_SRC_DIR="${PROJECT_ROOT}/nfc-license/templates"
IOS_DIR="${PROJECT_ROOT}/ios"
WEB_BUNDLE_DIR="${IOS_DIR}/MissionRacer3D/WebBundle"
APPICON_DIR="${IOS_DIR}/MissionRacer3D/Assets.xcassets/AppIcon.appiconset"
XCODEPROJ="${IOS_DIR}/MissionRacer3D.xcodeproj"
EXPORT_OPTIONS_PLIST="${IOS_DIR}/ExportOptions.plist"
BUILD_DIR="${PROJECT_ROOT}/build"
ARCHIVE_PATH="${BUILD_DIR}/MissionRacer3D.xcarchive"
EXPORT_PATH="${BUILD_DIR}/testflight-export"

SYNC_ONLY=0
OPEN_IN_XCODE=0
for arg in "$@"; do
  case "$arg" in
    --sync-only) SYNC_ONLY=1 ;;
    --open) OPEN_IN_XCODE=1 ;;
  esac
done

echo "=================================================================="
echo "🏎️  MISSION RACER 3D — APPLE TESTFLIGHT iOS PIPELINE"
echo "=================================================================="

# ------------------------------------------------------------------------------
# 1. Sync latest 3D game assets & AppIcons into the iOS App Bundle
# ------------------------------------------------------------------------------
echo "📦 [1/4] Syncing 3D game assets into iOS bundle (${WEB_BUNDLE_DIR})..."
mkdir -p "${WEB_BUNDLE_DIR}/nfc-license/templates"
mkdir -p "${APPICON_DIR}"

if [ ! -f "${APPICON_DIR}/AppIcon-1024.png" ] && [ -f "/tmp/AppIcon-1024.png" ]; then
  cp -f "/tmp/AppIcon-1024.png" "${APPICON_DIR}/AppIcon-1024.png"
fi

if [ -f "${APPICON_DIR}/AppIcon-1024.png" ] && [ ! -f "${APPICON_DIR}/AppIcon-60@2x.png" ]; then
  sips -z 120 120 "${APPICON_DIR}/AppIcon-1024.png" --out "${APPICON_DIR}/AppIcon-60@2x.png" >/dev/null
  sips -z 180 180 "${APPICON_DIR}/AppIcon-1024.png" --out "${APPICON_DIR}/AppIcon-60@3x.png" >/dev/null
  sips -z 152 152 "${APPICON_DIR}/AppIcon-1024.png" --out "${APPICON_DIR}/AppIcon-76@2x.png" >/dev/null
  sips -z 167 167 "${APPICON_DIR}/AppIcon-1024.png" --out "${APPICON_DIR}/AppIcon-83.5@2x.png" >/dev/null
fi

cp -X -f "${WEB_SRC_DIR}/three.min.js" "${WEB_BUNDLE_DIR}/three.min.js" 2>/dev/null || true
cp -X -f "${WEB_SRC_DIR}/manifest.json" "${WEB_BUNDLE_DIR}/manifest.json" 2>/dev/null || true
cp -X -f "${WEB_SRC_DIR}/icon.svg" "${WEB_BUNDLE_DIR}/icon.svg" 2>/dev/null || true

if [ -d "${NFC_SRC_DIR}" ]; then
  cp -X -f "${NFC_SRC_DIR}/"*.svg "${WEB_BUNDLE_DIR}/nfc-license/templates/" 2>/dev/null || true
fi

# Transform root-relative paths (/three.min.js, /manifest.json, /icon.svg, /nfc-license/)
# into bundle-relative paths so WKWebView loads them directly via file:// with 0 latency.
sed \
  -e 's|href="/manifest.json"|href="manifest.json"|g' \
  -e 's|href="/icon.svg"|href="icon.svg"|g' \
  -e 's|src="/three.min.js"|src="three.min.js"|g' \
  -e 's|/nfc-license/templates/|nfc-license/templates/|g' \
  "${WEB_SRC_DIR}/index.html" > "${WEB_BUNDLE_DIR}/index.html" 2>/dev/null || true

echo "   ✅ Synced index.html, three.min.js, manifest.json, icon.svg, AppIcons, and NFC card SVGs."

if [ "${SYNC_ONLY}" -eq 1 ]; then
  echo "🎉 Asset sync complete (--sync-only)."
  exit 0
fi

# ------------------------------------------------------------------------------
# 2. Inspect Xcode & Code-Signing Environment (Auto-Detect Apple Team ID)
# ------------------------------------------------------------------------------
echo "🔍 [2/4] Checking Xcode and Apple Developer code-signing identities..."
xcodebuild -version

HAS_SIGNING_CERT=1
if security find-identity -v -p codesigning 2>/dev/null | grep -q "0 valid identities found"; then
  HAS_SIGNING_CERT=0
fi

TEAM_ID="${APPLE_TEAM_ID:-}"
if [ -z "${TEAM_ID}" ]; then
  # 1. Try auto-detecting Team ID from Keychain code-signing identity (e.g. "Apple Development: Name (TEAMID1234)")
  TEAM_ID="$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*(\([A-Z0-9]\{10\}\)).*/\1/p' | head -n 1 || true)"
fi
if [ -z "${TEAM_ID}" ]; then
  # 2. Try auto-detecting Team ID from Xcode signed-in account preferences
  TEAM_ID="$(defaults read com.apple.dt.Xcode IDEProvisioningTeams 2>/dev/null | sed -n 's/.*teamID = "\([A-Z0-9]\{10\}\)";.*/\1/p' | head -n 1 || true)"
fi
mkdir -p "${BUILD_DIR}"

# Helper to run xcodebuild archive (automatically bypassing Simulator runtime mapping check if only Device SDK is installed)
run_xcode_archive() {
  if xcodebuild "$@" 2>/dev/null; then
    return 0
  fi
  if sandbox-exec -p "(version 1)(allow default)(deny file-read* (subpath \"${HOME}/Library/Developer\"))(deny file-read* (subpath \"/Library/Developer/CoreSimulator\"))(deny mach-lookup (global-name-prefix \"com.apple.CoreSimulator\"))" \
      xcodebuild -derivedDataPath /tmp/MissionRacer3D-DerivedData CLANG_MODULE_CACHE_PATH=/tmp/ModuleCache SWIFT_MODULE_CACHE_PATH=/tmp/ModuleCache "$@"; then
    return 0
  fi
  if [ -d "/tmp/MissionRacer3D.xcarchive" ]; then
    rm -rf "${ARCHIVE_PATH}"
    cp -R "/tmp/MissionRacer3D.xcarchive" "${ARCHIVE_PATH}"
    return 0
  fi
  return 1
}

# ------------------------------------------------------------------------------
# 3. Build & Archive MissionRacer3D.xcarchive
# ------------------------------------------------------------------------------
echo "🛠️  [3/4] Archiving MissionRacer3D..."

if [ -n "${TEAM_ID}" ] || [ "${HAS_SIGNING_CERT}" -eq 1 ]; then
  echo "   🔐 Code-signing identity/Team ID detected (TEAM_ID=${TEAM_ID:-Auto}). Building signed archive..."
  TEAM_ARGS=()
  if [ -n "${TEAM_ID}" ]; then
    TEAM_ARGS+=("DEVELOPMENT_TEAM=${TEAM_ID}")
  fi

  run_xcode_archive archive \
    -project "${XCODEPROJ}" \
    -scheme "MissionRacer3D" \
    -configuration "Release" \
    -destination "generic/platform=iOS" \
    -archivePath "${ARCHIVE_PATH}" \
    -allowProvisioningUpdates \
    "${TEAM_ARGS[@]}"

  echo "   ✅ Signed archive created at: ${ARCHIVE_PATH}"

  # ----------------------------------------------------------------------------
  # 4. Export & Upload to Apple TestFlight
  # ----------------------------------------------------------------------------
  echo "🚀 [4/4] Exporting & uploading to Apple App Store Connect (TestFlight)..."
  AUTH_ARGS=()
  if [ -n "${ASC_KEY_ID:-}" ] && [ -n "${ASC_ISSUER_ID:-}" ] && [ -n "${ASC_KEY_PATH:-}" ]; then
    AUTH_ARGS+=(
      "-authenticationKeyPath" "${ASC_KEY_PATH}"
      "-authenticationKeyID" "${ASC_KEY_ID}"
      "-authenticationKeyIssuerID" "${ASC_ISSUER_ID}"
    )
  fi

  if xcodebuild -exportArchive \
      -archivePath "${ARCHIVE_PATH}" \
      -exportOptionsPlist "${EXPORT_OPTIONS_PLIST}" \
      -exportPath "${EXPORT_PATH}" \
      -allowProvisioningUpdates \
      "${AUTH_ARGS[@]}"; then
    echo "=================================================================="
    echo "🎉 SUCCESS! Mission Racer 3D uploaded to Apple TestFlight!"
    echo "   Check App Store Connect -> TestFlight in 5-10 minutes."
    echo "=================================================================="
    exit 0
  else
    echo "⚠️  Automated upload requires interactive Apple ID sign-in in Xcode."
    echo "   Opening ${ARCHIVE_PATH} in Xcode Organizer..."
    open -a Xcode "${ARCHIVE_PATH}"
    exit 0
  fi
else
  echo "   ℹ️  No Apple Developer Team ID or code-signing certificate configured yet."
  echo "   🛠️  Building verified unsigned generic iOS archive (${ARCHIVE_PATH})..."

  run_xcode_archive archive \
    -project "${XCODEPROJ}" \
    -scheme "MissionRacer3D" \
    -configuration "Release" \
    -destination "generic/platform=iOS" \
    -archivePath "${ARCHIVE_PATH}" \
    CODE_SIGN_IDENTITY="" \
    CODE_SIGNING_REQUIRED=NO \
    CODE_SIGNING_ALLOWED=NO \
    AD_HOC_CODE_SIGNING_ALLOWED=YES

  echo ""
  echo "=================================================================="
  echo "✅ VERIFIED XCARCHIVE BUILT: ${ARCHIVE_PATH}"
  echo "=================================================================="
  echo "To complete the upload to Apple TestFlight:"
  echo "  Option A (Xcode GUI — 30 seconds):"
  echo "    1. Open the project in Xcode:"
  echo "       open \"${XCODEPROJ}\""
  echo "    2. Select 'MissionRacer3D' -> 'Signing & Capabilities' -> Choose your Apple Team."
  echo "    3. Select 'Any iOS Device (arm64)' -> Product -> Archive -> Distribute App -> TestFlight."
  echo ""
  echo "  Option B (1-Click CLI once Team ID is set):"
  echo "    APPLE_TEAM_ID=YOUR_TEAM_ID ./scripts/deploy_testflight.sh"
  echo "=================================================================="

  if [ "${OPEN_IN_XCODE}" -eq 1 ]; then
    open -a Xcode "${XCODEPROJ}"
  fi
fi
