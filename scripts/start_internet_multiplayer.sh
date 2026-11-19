#!/usr/bin/env bash
# ==============================================================================
# Mission Racer 3D — 1-Click Global Internet Multiplayer Launcher
# ==============================================================================
# 1. Starts the local Mission Racer 3D server on port 3000 (if not already up)
# 2. Opens a free public HTTPS Internet URL tunnel so iPhones on 4G/5G cellular
#    or friends/family on any Wi-Fi network can open the game directly in Safari
#    or Chrome — while native Android/iOS apps automatically connect via the
#    Global Internet Room Code (default: MR-FAMILY-7)!
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PORT=3000

echo "=================================================================="
echo "🌍 MISSION RACER 3D — CROSS-NETWORK & 4G/5G INTERNET MULTIPLAYER"
echo "=================================================================="

if ! lsof -iTCP:${PORT} -sTCP:LISTEN -t >/dev/null 2>&1; then
  echo "🚀 Starting Mission Racer 3D Server on port ${PORT}..."
  nohup node "${PROJECT_ROOT}/tools/mission-control-web/serve.js" >/tmp/mission_racer_serve.log 2>&1 &
  sleep 1
else
  echo "✅ Mission Racer 3D Server is already running on port ${PORT}."
fi

LOCAL_IP="$(ipconfig getifaddr en0 2>/dev/null || echo localhost)"
echo "• Home Wi-Fi LAN URL:     http://${LOCAL_IP}:${PORT}"
echo "• Global Room Code:       MR-FAMILY-7 (Works across 4G/5G & any Wi-Fi!)"
echo "------------------------------------------------------------------"

if command -v cloudflared >/dev/null 2>&1; then
  echo "🌐 Opening Cloudflare Quick HTTPS Tunnel to port ${PORT}..."
  cloudflared tunnel --url "http://localhost:${PORT}"
else
  echo "🌐 Opening Public HTTPS Tunnel (localhost.run) to port ${PORT}..."
  echo "   (Open the https://*.lhr.life link printed below on any iPhone on 5G!)"
  ssh -o StrictHostKeyChecking=no -o ServerAliveInterval=30 -R 80:localhost:${PORT} nokey@localhost.run
fi
