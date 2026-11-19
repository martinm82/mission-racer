#!/usr/bin/env bash
# ==============================================================================
# ☁️ MISSION RACER 3D — 1-COMMAND CLOUD OTA PUBLISHER (GITHUB PAGES & NETLIFY)
# ==============================================================================
# Publishes the latest 3D game bundle (index.html + wan-multiplayer.js + APK)
# to GitHub Pages (https://martinm82.github.io/mission-racer) so your kids'
# iPhones and Android Fire HD 8 tablet automatically receive the update
# Over-The-Air from their home network whenever they open the app!
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WEB_DIR="${PROJECT_ROOT}/tools/mission-control-web"

echo "=================================================================="
echo "☁️  MISSION RACER 3D — PUBLISHING CLOUD OTA UPDATE"
echo "=================================================================="

# 1. Generate fresh ota-manifest.json with SHA-256 & timestamp version
node -e "
  const fs = require('fs'), crypto = require('crypto'), path = require('path');
  const webDir = '${WEB_DIR}';
  const idx = fs.readFileSync(path.join(webDir, 'index.html'));
  const wan = fs.readFileSync(path.join(webDir, 'wan-multiplayer.js'));
  const sha256 = crypto.createHash('sha256').update(idx).update(wan).digest('hex');
  const d = new Date();
  const version = '1.2.' + (d.getUTCMonth()+1) + String(d.getUTCDate()).padStart(2,'0') + '.' + String(d.getUTCHours()).padStart(2,'0') + String(d.getUTCMinutes()).padStart(2,'0');
  const manifest = {
    version,
    buildTimestamp: Math.floor(Date.now() / 1000),
    sha256,
    files: ['index.html', 'wan-multiplayer.js'],
    apkUrl: 'MissionRacer3D.apk'
  };
  fs.writeFileSync(path.join(webDir, 'ota-manifest.json'), JSON.stringify(manifest, null, 2));
  console.log('📦 Generated ota-manifest.json -> v' + version + ' (SHA-256: ' + sha256.slice(0, 12) + '...)');
"

# 2. Sync iOS WebBundle & Rebuild Android APK
cp -f "${WEB_DIR}/index.html" "${PROJECT_ROOT}/ios/MissionRacer3D/WebBundle/index.html"
cp -f "${WEB_DIR}/wan-multiplayer.js" "${PROJECT_ROOT}/ios/MissionRacer3D/WebBundle/wan-multiplayer.js"
cp -f "${WEB_DIR}/ota-manifest.json" "${PROJECT_ROOT}/ios/MissionRacer3D/WebBundle/ota-manifest.json"

bash "${PROJECT_ROOT}/scripts/build_android_apk.sh"
cp -f "${PROJECT_ROOT}/build/MissionRacer3D.apk" "${WEB_DIR}/MissionRacer3D.apk"

# 3. Push to GitHub (triggers GitHub Pages & Netlify OTA deployment)
cd "${PROJECT_ROOT}"
if ! git remote get-url origin >/dev/null 2>&1; then
  echo "🔗 Creating/Linking GitHub repository martinm82/mission-racer..."
  gh repo create martinm82/mission-racer --public --source=. --remote=origin 2>/dev/null || \
    git remote add origin "https://github.com/martinm82/mission-racer.git"
fi

# Deploy static OTA files directly to gh-pages branch for instant (<15s) GitHub Pages availability
OTA_TMP="/tmp/mission-racer-gh-pages"
rm -rf "${OTA_TMP}"
mkdir -p "${OTA_TMP}/nfc-license/templates"
cp -f "${WEB_DIR}/index.html" "${OTA_TMP}/index.html"
cp -f "${WEB_DIR}/wan-multiplayer.js" "${OTA_TMP}/wan-multiplayer.js"
cp -f "${WEB_DIR}/ota-manifest.json" "${OTA_TMP}/ota-manifest.json"
cp -f "${WEB_DIR}/three.min.js" "${OTA_TMP}/three.min.js"
cp -f "${WEB_DIR}/manifest.json" "${OTA_TMP}/manifest.json"
cp -f "${WEB_DIR}/icon.svg" "${OTA_TMP}/icon.svg"
cp -f "${PROJECT_ROOT}/build/MissionRacer3D.apk" "${OTA_TMP}/MissionRacer3D.apk"
cp -f "${PROJECT_ROOT}/nfc-license/templates/"*.svg "${OTA_TMP}/nfc-license/templates/" 2>/dev/null || true
touch "${OTA_TMP}/.nojekyll"

(
  cd "${OTA_TMP}"
  git init -q
  git checkout -q -b gh-pages
  git add .
  git -c commit.gpgsign=false commit -q -m "Cloud OTA Update $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  git push -f "https://github.com/martinm82/mission-racer.git" gh-pages:gh-pages
)

# Enable GitHub Pages on gh-pages branch if not already enabled
gh api -X POST repos/martinm82/mission-racer/pages -f source='{"branch":"gh-pages","path":"/"}' >/dev/null 2>&1 || \
gh api -X PUT repos/martinm82/mission-racer/pages -f source='{"branch":"gh-pages","path":"/"}' >/dev/null 2>&1 || true

echo "=================================================================="
echo "✅ CLOUD OTA UPDATE PUBLISHED LIVE!"
echo "• Global OTA URL:      https://martinm82.github.io/mission-racer"
echo "• OTA Manifest:        https://martinm82.github.io/mission-racer/ota-manifest.json"
echo "• Direct APK Download: https://martinm82.github.io/mission-racer/MissionRacer3D.apk"
echo "=================================================================="
