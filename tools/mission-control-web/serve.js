const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const os = require('os');

const PORT = 3000;
const DEFAULT_WAN_ROOM = process.env.MR_WAN_ROOM || 'MR-FAMILY-7';
const PROJECT_ROOT = path.resolve(__dirname, '../../');
const HTML_FILE = path.join(__dirname, 'index.html');

const MIME_TYPES = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'text/javascript',
  '.svg': 'image/svg+xml',
  '.json': 'application/json',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.apk': 'application/vnd.android.package-archive'
};

function getLocalIp() {
  const interfaces = os.networkInterfaces();
  for (const name of Object.keys(interfaces)) {
    for (const iface of interfaces[name]) {
      if (iface.family === 'IPv4' && !iface.internal) {
        return iface.address;
      }
    }
  }
  return 'localhost';
}

// ============================================================================
// REAL-TIME 3D MULTIPLAYER STATE (3D WORLD COORDINATES X, Y, Z, YAW)
// ============================================================================
const activePlayers = {};
const sseClients = new Map();

const driversState = {
  7: { callsign: 'Lightning 7', icon: '⚡', code: 'MR-07-ROOKIE', rank: 'Rookie Class', age: 7, fuel: 3, credits: 500, stamps: 12 },
  10: { callsign: 'Thunder 10', icon: '🔥', code: 'MR-10-PRO', rank: 'Pro Class', age: 10, fuel: 4, credits: 1200, stamps: 28 },
  dad: { callsign: 'Pit Boss Dad', icon: '👑', code: 'MR-01-BOSS', rank: 'Commissioner', age: 40, fuel: 5, credits: 2500, stamps: 99 }
};

function broadcastEvent(type, payload) {
  const msg = `data: ${JSON.stringify({ type, payload, ts: Date.now() })}\n\n`;
  for (const [id, clientRes] of sseClients.entries()) {
    try {
      clientRes.write(msg);
    } catch (e) {
      sseClients.delete(id);
    }
  }
}

// 15Hz World State Broadcast
setInterval(() => {
  const now = Date.now();
  for (const id of Object.keys(activePlayers)) {
    const p = activePlayers[id];
    const timeoutMs = p.isWanBridged ? 12500 : 8000;
    if (now - p.lastSeen > timeoutMs) {
      delete activePlayers[id];
    } else if (p.isWanBridged && p.speedMps > 0.5) {
      // Smooth 15Hz dead-reckoning extrapolation for WAN players bridged to LAN
      const dt = 0.066;
      p.x += Math.sin(p.yaw || 0) * p.speedMps * dt;
      p.z += Math.cos(p.yaw || 0) * p.speedMps * dt;
      p.trackProgress = (p.trackProgress || 0) + p.speedMps * dt;
    }
  }
  if (sseClients.size > 0) {
    broadcastEvent('WORLD_STATE', {
      players: Object.values(activePlayers),
      driversState
    });
  }
}, 66);

// ============================================================================
// SERVER-SIDE GLOBAL INTERNET ROOM BRIDGE (BRIDGES 4G/5G WAN <-> HOME WI-FI LAN)
// ============================================================================
const bridgedRooms = new Set();
const seenWanPkts = new Set();

function ensureServerWanBridge(roomRaw) {
  const cleanRoom = String(roomRaw || DEFAULT_WAN_ROOM).trim().toUpperCase().replace(/[^A-Z0-9_-]/g, '') || DEFAULT_WAN_ROOM;
  if (bridgedRooms.has(cleanRoom)) return;
  bridgedRooms.add(cleanRoom);

  const topic = 'mr3d_wan_' + cleanRoom.toLowerCase();
  const streamUrl = `https://ntfy.sh/${encodeURIComponent(topic)}/json`;

  function connectBridgeStream() {
    const req = https.get(streamUrl, (res) => {
      let buf = '';
      res.on('data', chunk => {
        buf += chunk.toString();
        let idx;
        while ((idx = buf.indexOf('\n')) !== -1) {
          const line = buf.slice(0, idx).trim();
          buf = buf.slice(idx + 1);
          if (!line) continue;
          try {
            const outer = JSON.parse(line);
            const pkt = (outer && outer.event === 'message' && outer.message)
              ? JSON.parse(outer.message)
              : outer;
            if (!pkt || !pkt.eventType || (pkt.pktId && seenWanPkts.has(pkt.pktId))) continue;
            if (pkt.pktId) {
              seenWanPkts.add(pkt.pktId);
              if (seenWanPkts.size > 500) seenWanPkts.delete(seenWanPkts.values().next().value);
            }
            const { eventType, payload } = pkt;
            if (!payload) continue;
            if (eventType === 'PLAYER_SYNC' && payload.clientId) {
              // Only inject via WAN bridge if not already directly syncing on fast LAN within last 1.5s
              const existing = activePlayers[payload.clientId];
              if (!existing || existing.isWanBridged || (Date.now() - existing.lastSeen > 1500)) {
                activePlayers[payload.clientId] = Object.assign({}, payload, {
                  isWanBridged: true,
                  lastSeen: Date.now()
                });
              }
            } else if (['START_RACE', 'CAR_COLLISION', 'HORN', 'PIT_RADIO', 'VOICE_INTERCOM', 'STAMP_MISSION'].includes(eventType)) {
              broadcastEvent(eventType, payload);
            }
          } catch (e) {}
        }
      });
      res.on('end', () => setTimeout(connectBridgeStream, 4000));
    });
    req.on('error', () => setTimeout(connectBridgeStream, 5000));
  }
  connectBridgeStream();
}
ensureServerWanBridge(DEFAULT_WAN_ROOM);

function parseJsonBody(req, callback) {
  let body = '';
  req.on('data', chunk => {
    body += chunk.toString();
    if (body.length > 2 * 1024 * 1024) req.destroy();
  });
  req.on('end', () => {
    try {
      callback(null, body ? JSON.parse(body) : {});
    } catch (err) {
      callback(err, null);
    }
  });
}

const server = http.createServer((req, res) => {
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type'
    });
    res.end();
    return;
  }

  const parsedUrl = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const pathname = decodeURI(parsedUrl.pathname);

  if (pathname === '/api/info' && req.method === 'GET') {
    const localIp = getLocalIp();
    res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
    res.end(JSON.stringify({
      localIp,
      port: PORT,
      lanUrl: `http://${localIp}:${PORT}`,
      roomCode: DEFAULT_WAN_ROOM,
      onlineCount: Object.keys(activePlayers).length
    }));
    return;
  }

  if (pathname === '/api/stream' && req.method === 'GET') {
    const clientId = parsedUrl.searchParams.get('clientId') || ('c_' + Math.random().toString(36).slice(2, 9));
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive',
      'Access-Control-Allow-Origin': '*'
    });
    res.write(`data: ${JSON.stringify({ type: 'CONNECTED', payload: { clientId, driversState, players: Object.values(activePlayers) } })}\n\n`);
    sseClients.set(clientId, res);

    req.on('close', () => {
      sseClients.delete(clientId);
      delete activePlayers[clientId];
    });
    return;
  }

  if (pathname === '/api/sync' && req.method === 'POST') {
    parseJsonBody(req, (err, data) => {
      if (err || !data || !data.clientId) {
        res.writeHead(400, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
        res.end(JSON.stringify({ error: 'Invalid sync payload' }));
        return;
      }
      activePlayers[data.clientId] = {
        clientId: data.clientId,
        driverKey: data.driverKey || 7,
        callsign: data.callsign || 'Lightning 7',
        icon: data.icon || '⚡',
        x: Number(data.x) || 0,
        y: Number(data.y) || 0,
        z: Number(data.z) || 0,
        yaw: Number(data.yaw) || 0,
        steer: Number(data.steer) || 0,
        speed: Number(data.speed) || 0,
        speedMps: Number(data.speedMps) || ((Number(data.speed) || 0) / 3.6),
        s: Number(data.s) || Number(data.trackProgress) || 0,
        offset: Number(data.offset) || 0,
        headingError: Number(data.headingError) || 0,
        trackProgress: Number(data.trackProgress) || 0,
        lap: Number(data.lap) || 1,
        color: data.color || '#ff3344',
        neonColor: data.neonColor || '#00e5ff',
        carModel: data.carModel || 'porsche911',
        isBoosting: Boolean(data.isBoosting),
        trackTheme: data.trackTheme || 'monaco',
        roomCode: data.roomCode || DEFAULT_WAN_ROOM,
        lastSeen: Date.now()
      };
      if (data.roomCode && typeof ensureServerWanBridge === 'function') {
        ensureServerWanBridge(data.roomCode);
      }
      res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
      res.end(JSON.stringify({ ok: true }));
    });
    return;
  }

  if (pathname === '/api/event' && req.method === 'POST') {
    parseJsonBody(req, (err, data) => {
      if (err || !data || !data.eventType) {
        res.writeHead(400, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
        res.end(JSON.stringify({ error: 'Invalid event' }));
        return;
      }
      const { eventType, payload } = data;
      if (eventType === 'STAMP_MISSION') {
        const targetKey = payload.driverKey || 7;
        const d = driversState[targetKey];
        if (d) {
          d.fuel = Math.min(5, d.fuel + (Number(payload.fuel) || 1));
          d.credits += (Number(payload.credits) || 150);
          d.stamps += 1;
        }
        broadcastEvent('STAMP_MISSION', {
          driverKey: targetKey,
          driver: d,
          title: payload.title || 'Mission',
          credits: payload.credits || 150,
          fuel: payload.fuel || 1,
          stampedBy: payload.stampedBy || 'Pit Boss Dad'
        });
      } else if (eventType === 'START_RACE') {
        broadcastEvent('START_RACE', {
          startedBy: payload.startedBy || 'Squad Leader',
          trackTheme: payload.trackTheme || 'monaco'
        });
      } else if (eventType === 'HORN') {
        broadcastEvent('HORN', {
          clientId: payload.clientId,
          callsign: payload.callsign || 'Racer'
        });
      } else if (eventType === 'PIT_RADIO') {
        broadcastEvent('PIT_RADIO', {
          clientId: payload.clientId,
          callsign: payload.callsign || 'Racer',
          text: payload.text || '🎙️ Voice Transmission'
        });
      } else if (eventType === 'VOICE_INTERCOM') {
        broadcastEvent('VOICE_INTERCOM', {
          clientId: payload.clientId,
          callsign: payload.callsign || 'Racer',
          icon: payload.icon || '🎙️',
          audioUrl: payload.audioUrl || '',
          text: payload.text || ''
        });
      } else if (eventType === 'CAR_COLLISION') {
        broadcastEvent('CAR_COLLISION', {
          attackerId: payload.attackerId,
          attackerName: payload.attackerName || 'Racer',
          attackerIcon: payload.attackerIcon || '🏎️',
          targetId: payload.targetId,
          pushOffset: Number(payload.pushOffset) || 0,
          boostDelta: Number(payload.boostDelta) || 0,
          headingKick: Number(payload.headingKick) || 0,
          intensity: Number(payload.intensity) || 0.7
        });
      }
      res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
      res.end(JSON.stringify({ ok: true, driversState }));
    });
    return;
  }

  if (pathname === '/ota-manifest.json' || pathname === '/api/ota-manifest') {
    try {
      const crypto = require('crypto');
      const idxBuf = fs.readFileSync(HTML_FILE);
      const wanPath = path.join(__dirname, 'wan-multiplayer.js');
      const wanBuf = fs.existsSync(wanPath) ? fs.readFileSync(wanPath) : Buffer.from('');
      const sha256 = crypto.createHash('sha256').update(idxBuf).update(wanBuf).digest('hex');
      const mtimeSec = Math.floor(fs.statSync(HTML_FILE).mtimeMs / 1000);
      const d = new Date(mtimeSec * 1000);
      const ver = `1.2.${d.getUTCMonth() + 1}${String(d.getUTCDate()).padStart(2, '0')}.${String(d.getUTCHours()).padStart(2, '0')}${String(d.getUTCMinutes()).padStart(2, '0')}`;
      res.writeHead(200, {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*',
        'Cache-Control': 'no-cache, no-store, must-revalidate'
      });
      res.end(JSON.stringify({
        version: ver,
        buildTimestamp: mtimeSec,
        sha256,
        files: ['index.html', 'wan-multiplayer.js'],
        apkUrl: '/MissionRacer3D.apk'
      }, null, 2));
    } catch (e) {
      res.writeHead(500, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' });
      res.end(JSON.stringify({ error: e.message }));
    }
    return;
  }

  let filePath;
  if (pathname === '/' || pathname === '/index.html') {
    filePath = HTML_FILE;
  } else if (pathname === '/MissionRacer3D.apk' || pathname === '/apk') {
    filePath = path.join(PROJECT_ROOT, 'build/MissionRacer3D.apk');
  } else if (pathname === '/apple-touch-icon.png' || pathname === '/apple-touch-icon-precomposed.png') {
    filePath = fs.existsSync('/tmp/apple-touch-icon.png')
      ? '/tmp/apple-touch-icon.png'
      : path.join(PROJECT_ROOT, 'ios/MissionRacer3D/Assets.xcassets/AppIcon.appiconset/AppIcon-60@3x.png');
  } else if (pathname === '/icon-192.png') {
    filePath = fs.existsSync('/tmp/icon-192.png')
      ? '/tmp/icon-192.png'
      : path.join(PROJECT_ROOT, 'ios/MissionRacer3D/Assets.xcassets/AppIcon.appiconset/AppIcon-60@3x.png');
  } else if (pathname === '/icon-512.png') {
    filePath = fs.existsSync('/tmp/icon-512.png')
      ? '/tmp/icon-512.png'
      : path.join(PROJECT_ROOT, 'ios/MissionRacer3D/Assets.xcassets/AppIcon.appiconset/AppIcon-1024.png');
  } else if (pathname === '/three.min.js' || pathname === '/wan-multiplayer.js' || pathname === '/manifest.json' || pathname === '/sw.js' || pathname === '/icon.svg') {
    filePath = path.join(__dirname, pathname.replace(/^\//, ''));
  } else {
    filePath = path.join(PROJECT_ROOT, pathname.replace(/^\//, ''));
  }

  const ext = path.extname(filePath).toLowerCase();
  const contentType = MIME_TYPES[ext] || 'application/octet-stream';

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('File Not Found');
      return;
    }
    res.writeHead(200, {
      'Content-Type': contentType,
      'Access-Control-Allow-Origin': '*',
      'Cache-Control': 'no-store, no-cache, must-revalidate, proxy-revalidate, max-age=0',
      'Pragma': 'no-cache',
      'Expires': '0'
    });
    res.end(data);
  });
});

server.listen(PORT, '0.0.0.0', () => {
  const localIp = getLocalIp();
  console.log('================================================================');
  console.log('🏎️  MISSION RACER 3D: THREE.JS WEBGL 3D ENGINE & MULTIPLAYER');
  console.log('================================================================');
  console.log(`• Mac Browser:          http://localhost:${PORT}`);
  console.log(`• Install on Phones:    http://${localIp}:${PORT}`);
  console.log('================================================================');
});
