// ============================================================================
// MISSION RACER 3D — CROSS-NETWORK / INTERNET MULTIPLAYER ENGINE (LAN + WAN)
// ============================================================================
// Enables seamless 3D multiplayer racing, collisions, race starts & voice
// intercom across ANY network (Home Wi-Fi LAN, 4G/5G Cellular, Hotspots, Travel)
// using a shared Family Room Code (default: "MR-FAMILY-7").
// ============================================================================
(function () {
  "use strict";

  const DEFAULT_ROOM_CODE = "MR-FAMILY-7";
  const NTFY_BASE = "https://ntfy.sh";
  const WAN_SYNC_INTERVAL_MS = 1250; // Smooth 1.25s cloud sync + 60fps spline/velocity dead-reckoning

  function sanitizeRoomCode(raw) {
    const cleaned = String(raw || DEFAULT_ROOM_CODE)
      .trim()
      .toUpperCase()
      .replace(/[^A-Z0-9_-]/g, "");
    return cleaned || DEFAULT_ROOM_CODE;
  }

  // Allow URL query ?room=MR-FAMILY-7 or localStorage override
  const urlParams = new URLSearchParams(window.location.search || "");
  const initialRoom = sanitizeRoomCode(
    urlParams.get("room") || localStorage.getItem("mr_wan_room_code") || DEFAULT_ROOM_CODE
  );
  localStorage.setItem("mr_wan_room_code", initialRoom);

  const wanState = {
    roomCode: initialRoom,
    lanConnected: false,
    wanEsConnected: false,
    wanWsConnected: false,
    es: null,
    ws: null,
    lastWanSyncSentMs: 0,
    seenPacketIds: new Set(),
    wanPlayers: {} // clientId -> player object received via WAN
  };

  window.MR_WAN = wanState;

  function getTopicName(roomCode) {
    return "mr3d_wan_" + sanitizeRoomCode(roomCode).toLowerCase();
  }

  function makePacketId() {
    return (window.myClientId || "c") + "_" + Date.now().toString(36) + "_" + Math.random().toString(36).slice(2, 6);
  }

  function updateWanStatusBadge() {
    const badge = document.getElementById("wanStatusBadge");
    const roomInput = document.getElementById("wanRoomCodeInput");
    const topOnlinePill = document.getElementById("onlineStatus");

    if (roomInput && document.activeElement !== roomInput) {
      roomInput.value = wanState.roomCode;
    }

    const wanActive = wanState.wanEsConnected || wanState.wanWsConnected;
    if (badge) {
      if (wanState.lanConnected && wanActive) {
        badge.innerHTML = `🟢 <strong>Hybrid LAN + Internet Room (${wanState.roomCode}) Active</strong> — Wi-Fi & 4G/5G Connected!`;
        badge.style.color = "#10b981";
        badge.style.borderColor = "rgba(16,185,129,0.5)";
        badge.style.background = "rgba(16,185,129,0.14)";
      } else if (wanActive) {
        badge.innerHTML = `🌍 <strong>Internet Room Active (${wanState.roomCode})</strong> — Ready for 4G/5G & Any Wi-Fi!`;
        badge.style.color = "#00e5ff";
        badge.style.borderColor = "rgba(0,229,255,0.5)";
        badge.style.background = "rgba(0,229,255,0.14)";
      } else if (wanState.lanConnected) {
        badge.innerHTML = `📶 <strong>Home Wi-Fi LAN Connected</strong> (Connecting Internet Room ${wanState.roomCode}...)`;
        badge.style.color = "#fbbf24";
      } else {
        badge.innerHTML = `🔄 <strong>Connecting to Internet Room (${wanState.roomCode})...</strong>`;
        badge.style.color = "#fbbf24";
      }
    }

    if (topOnlinePill) {
      topOnlinePill.title = `Room: ${wanState.roomCode} (${wanState.lanConnected ? "LAN+" : ""}WAN)`;
    }
  }

  // Build local player snapshot for WAN sync
  function buildLocalPlayerSnapshot() {
    if (typeof drivers === "undefined" || typeof playerState === "undefined") return null;
    const d = drivers[currentDriverKey] || drivers[7];
    const callsign = (typeof getActiveCallsign === "function") ? getActiveCallsign() : d.callsign;
    return {
      clientId: window.myClientId || myClientId,
      driverKey: currentDriverKey,
      callsign: callsign,
      icon: d.icon,
      x: Number(playerState.worldPos.x.toFixed(2)),
      y: Number(playerState.worldPos.y.toFixed(2)),
      z: Number(playerState.worldPos.z.toFixed(2)),
      yaw: Number(playerState.yaw.toFixed(3)),
      steer: Number(playerState.steerAngle.toFixed(2)),
      speed: Math.round(Math.abs(playerState.speed) * 3.6),
      speedMps: Number((playerState.speed || 0).toFixed(2)),
      s: Number((playerState.s || 0).toFixed(2)),
      offset: Number((playerState.offset || 0).toFixed(2)),
      headingError: Number((playerState.headingError || 0).toFixed(3)),
      trackProgress: Math.round(playerState.s),
      lap: playerState.lap,
      color: vehicleColor,
      neonColor: neonColor,
      carModel: currentCarModel,
      isBoosting: Boolean(playerState.isBoosting),
      trackTheme: typeof currentTrackTheme !== "undefined" ? currentTrackTheme : "monaco",
      roomCode: wanState.roomCode
    };
  }

  // Publish packet to Global Internet Room (WebSocket + HTTPS Ntfy Relay)
  function publishWanPacket(eventType, payload) {
    const pkt = {
      pktId: makePacketId(),
      room: wanState.roomCode,
      senderId: typeof myClientId !== "undefined" ? myClientId : window.myClientId,
      eventType: eventType,
      payload: payload,
      ts: Date.now()
    };
    wanState.seenPacketIds.add(pkt.pktId);
    if (wanState.seenPacketIds.size > 400) {
      const first = wanState.seenPacketIds.values().next().value;
      wanState.seenPacketIds.delete(first);
    }

    const rawJson = JSON.stringify(pkt);

    // 1. Send over primary WebSocket relay if open
    if (wanState.ws && wanState.ws.readyState === 1) {
      try {
        wanState.ws.send(rawJson);
      } catch (e) {}
    }

    // 2. Send over guaranteed HTTPS port-443 cloud relay (works through strict cellular NATs & hotspots)
    const topic = getTopicName(wanState.roomCode);
    fetch(`${NTFY_BASE}/${encodeURIComponent(topic)}`, {
      method: "POST",
      body: rawJson
    }).catch(() => {});
  }

  // Handle incoming WAN packet from any remote iPhone / Android tablet / Mac
  function handleIncomingWanPacket(pkt) {
    if (!pkt || typeof pkt !== "object" || !pkt.eventType) return;
    if (pkt.pktId && wanState.seenPacketIds.has(pkt.pktId)) return;
    if (pkt.pktId) wanState.seenPacketIds.add(pkt.pktId);

    const myId = typeof myClientId !== "undefined" ? myClientId : window.myClientId;
    if (pkt.senderId && pkt.senderId === myId) return;

    const { eventType, payload } = pkt;
    if (!payload) return;

    if (eventType === "PLAYER_SYNC") {
      if (!payload.clientId || payload.clientId === myId) return;
      payload.lastSeen = Date.now();
      wanState.wanPlayers[payload.clientId] = payload;

      if (typeof getOrCreateRemotePlayer === "function") {
        const rp = getOrCreateRemotePlayer(payload);
        if (rp) {
          rp.speedMps = Number(payload.speedMps) || ((Number(payload.speed) || 0) / 3.6);
          rp.splineS = Number.isFinite(Number(payload.s)) ? Number(payload.s) : rp.splineS;
          rp.splineOffset = Number.isFinite(Number(payload.offset)) ? Number(payload.offset) : rp.splineOffset;
          rp.headingError = Number(payload.headingError) || 0;
          rp.isWanPeer = true;
        }
      }
      refreshCombinedLobbyList();
    } else if (eventType === "START_RACE") {
      if (typeof currentTrackTheme !== "undefined" && payload.trackTheme && payload.trackTheme !== currentTrackTheme) {
        if (typeof load3DTrack === "function") load3DTrack(payload.trackTheme);
      } else if (typeof resetGridPositions === "function") {
        resetGridPositions();
      }
      if (typeof closePitModal === "function") closePitModal();
      if (typeof playCountdownSynth === "function") playCountdownSynth();
      if (typeof showToast === "function") {
        showToast(`🚦 [Internet Room] ${payload.startedBy || "Squad Leader"} started the Grand Prix! GO!`);
      }
    } else if (eventType === "CAR_COLLISION") {
      if (payload.targetId === myId && payload.attackerId !== myId && typeof playerState !== "undefined") {
        const roadW = typeof ROAD_WIDTH !== "undefined" ? ROAD_WIDTH : 15;
        const maxOffset = roadW * 0.5 - 1.15;
        playerState.offset = THREE.MathUtils.clamp(playerState.offset + (Number(payload.pushOffset) || 0), -maxOffset, maxOffset);
        playerState.speed = THREE.MathUtils.clamp(playerState.speed + (Number(payload.boostDelta) || 0), -10, 58);
        playerState.headingError = THREE.MathUtils.clamp(playerState.headingError + (Number(payload.headingKick) || 0), -0.85, 0.85);
        if (typeof playCrashSound === "function") playCrashSound(Number(payload.intensity) || 0.85);
        window._crashShake = 0.45;
        if (typeof spawnCollisionSparks === "function" && typeof playerCarMesh !== "undefined") {
          spawnCollisionSparks(playerCarMesh.position, 14);
        }
        if (typeof showToast === "function") {
          showToast(`💥 RAMMED by ${payload.attackerIcon || "🏎️"} ${payload.attackerName || "Racer"}!`);
        }
      }
    } else if (eventType === "HORN") {
      if (payload.clientId !== myId) {
        if (typeof playSillyHorn === "function") playSillyHorn(false);
        if (typeof showToast === "function") showToast(`📢 ${payload.callsign || "Racer"} honked their Horn!`);
      }
    } else if (eventType === "PIT_RADIO") {
      if (payload.clientId !== myId) {
        const txt = payload.text || "Voice Callout";
        if (typeof showToast === "function") showToast(`📻 ${payload.callsign || "Racer"}: "${txt}"`);
        if ("speechSynthesis" in window) {
          try {
            window.speechSynthesis.speak(new SpeechSynthesisUtterance(`${payload.callsign || "Racer"} says ${txt}`));
          } catch (e) {}
        }
      }
    } else if (eventType === "VOICE_INTERCOM") {
      if (payload.clientId !== myId) {
        if (typeof window.handleIncomingVoiceIntercom === "function") {
          window.handleIncomingVoiceIntercom(payload);
        } else {
          if (typeof showToast === "function") {
            showToast(`🎙️ ${payload.icon || "🎙️"} ${payload.callsign || "Racer"}: ${payload.text || "Voice Message"}`);
          }
          if (payload.audioUrl) {
            try {
              const a = new Audio(payload.audioUrl);
              a.volume = 1.0;
              a.play().catch(() => {});
            } catch (e) {}
          } else if (payload.text && "speechSynthesis" in window) {
            try {
              window.speechSynthesis.speak(new SpeechSynthesisUtterance(`${payload.callsign || "Racer"} says ${payload.text}`));
            } catch (e) {}
          }
        }
      }
    } else if (eventType === "STAMP_MISSION") {
      const { driverKey, driver, title, credits, fuel, stampedBy } = payload;
      if (typeof drivers !== "undefined" && drivers[driverKey]) {
        if (driver) {
          drivers[driverKey].fuel = driver.fuel;
          drivers[driverKey].credits = driver.credits;
          drivers[driverKey].stamps = driver.stamps;
        } else {
          drivers[driverKey].fuel = Math.min(5, (drivers[driverKey].fuel || 3) + (Number(fuel) || 1));
          drivers[driverKey].credits = (drivers[driverKey].credits || 500) + (Number(credits) || 150);
          drivers[driverKey].stamps = (drivers[driverKey].stamps || 10) + 1;
        }
        if (typeof renderLicense === "function") renderLicense();
      }
      if (typeof playCountdownSynth === "function") playCountdownSynth();
      if (typeof showToast === "function") {
        showToast(`🔨 ${stampedBy || "Pit Boss Dad"} stamped "${title}"! +${credits}🪙 +${fuel}⛽`);
      }
    }
  }

  function refreshCombinedLobbyList() {
    if (typeof updateLobbyUi !== "function") return;
    const now = Date.now();
    const combinedMap = {};

    // Include local player
    const me = buildLocalPlayerSnapshot();
    if (me) combinedMap[me.clientId] = me;

    // Include any active remote players in 3D scene
    if (typeof remotePlayersMap !== "undefined") {
      Object.keys(remotePlayersMap).forEach(id => {
        const rp = remotePlayersMap[id];
        if (now - rp.lastSeen <= 12000) {
          combinedMap[id] = {
            clientId: id,
            callsign: rp.callsign || "Racer",
            icon: rp.icon || "🏎️",
            speed: Math.round((rp.speedMps || 0) * 3.6),
            lap: rp.lap || 1,
            color: rp.color || "#10b981",
            trackTheme: rp.trackTheme || (typeof currentTrackTheme !== "undefined" ? currentTrackTheme : "monaco")
          };
        }
      });
    }

    // Overlay fresh WAN player metadata
    Object.keys(wanState.wanPlayers).forEach(id => {
      const wp = wanState.wanPlayers[id];
      if (now - wp.lastSeen <= 12000) {
        combinedMap[id] = wp;
      } else {
        delete wanState.wanPlayers[id];
      }
    });

    updateLobbyUi(Object.values(combinedMap));
  }

  // Connect to Global Internet Room via HTTPS EventSource + WebSocket
  function connectWanRoom(roomCode) {
    const cleanCode = sanitizeRoomCode(roomCode);
    wanState.roomCode = cleanCode;
    localStorage.setItem("mr_wan_room_code", cleanCode);

    if (wanState.es) {
      try { wanState.es.close(); } catch (e) {}
      wanState.es = null;
    }
    if (wanState.ws) {
      try { wanState.ws.close(); } catch (e) {}
      wanState.ws = null;
    }
    wanState.wanEsConnected = false;
    wanState.wanWsConnected = false;
    updateWanStatusBadge();

    const topic = getTopicName(cleanCode);

    // 1. Guaranteed HTTPS Cloud EventSource Relay (ntfy.sh JSON stream over port 443)
    try {
      const esUrl = `${NTFY_BASE}/${encodeURIComponent(topic)}/json`;
      const es = new EventSource(esUrl);
      wanState.es = es;

      es.onopen = () => {
        wanState.wanEsConnected = true;
        updateWanStatusBadge();
        // Immediately announce presence to the room
        const snap = buildLocalPlayerSnapshot();
        if (snap) publishWanPacket("PLAYER_SYNC", snap);
      };

      const unpackNtfyEnvelope = (rawStr) => {
        try {
          const outer = JSON.parse(rawStr);
          if (!outer) return;
          if (outer.attachment && outer.attachment.url) {
            fetch(outer.attachment.url)
              .then(r => r.json())
              .then(inner => handleIncomingWanPacket(inner))
              .catch(() => {});
            return;
          }
          if (outer.event === "message" && outer.message) {
            const inner = JSON.parse(outer.message);
            handleIncomingWanPacket(inner);
          } else if (outer.eventType) {
            handleIncomingWanPacket(outer);
          }
        } catch (e) {}
      };

      es.onmessage = (evt) => {
        unpackNtfyEnvelope(evt.data);
      };

      es.onerror = () => {
        wanState.wanEsConnected = false;
        updateWanStatusBadge();
      };
    } catch (e) {}

    // 2. Fast WebSocket Relay channel (wss://ntfy.sh/<topic>/ws) for ultra-low-latency packets
    try {
      const wsUrl = `wss://ntfy.sh/${encodeURIComponent(topic)}/ws`;
      const ws = new WebSocket(wsUrl);
      wanState.ws = ws;

      ws.onopen = () => {
        wanState.wanWsConnected = true;
        updateWanStatusBadge();
      };

      ws.onmessage = (evt) => {
        try {
          const outer = JSON.parse(evt.data);
          if (outer && outer.attachment && outer.attachment.url) {
            fetch(outer.attachment.url)
              .then(r => r.json())
              .then(inner => handleIncomingWanPacket(inner))
              .catch(() => {});
            return;
          }
          if (outer && outer.event === "message" && outer.message) {
            const inner = JSON.parse(outer.message);
            handleIncomingWanPacket(inner);
          } else if (outer && outer.eventType) {
            handleIncomingWanPacket(outer);
          }
        } catch (e) {}
      };

      ws.onclose = () => {
        wanState.wanWsConnected = false;
        updateWanStatusBadge();
      };
    } catch (e) {}
  }

  // Public helper to switch Family Room Code from the UI
  window.joinInternetRoom = function (customCode) {
    const input = document.getElementById("wanRoomCodeInput");
    const targetCode = sanitizeRoomCode(customCode || (input ? input.value : wanState.roomCode));
    if (input) input.value = targetCode;
    connectWanRoom(targetCode);
    if (typeof showToast === "function") {
      showToast(`🌍 Joined Internet Room: ${targetCode} (Works on 5G & Any Wi-Fi!)`);
    }
  };

  window.copyInternetInviteLink = function () {
    const url = new URL(window.location.href);
    url.searchParams.set("room", wanState.roomCode);
    const shareText = url.toString();
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(shareText).then(() => {
        if (typeof showToast === "function") showToast(`🔗 Copied Room Link (${wanState.roomCode})!`);
      }).catch(() => {});
    } else if (typeof showToast === "function") {
      showToast(`🌍 Share Room Code: ${wanState.roomCode}`);
    }
  };

  // ============================================================================
  // HOOK INTO MISSION RACER 3D FUNCTIONS & 60FPS DEAD-RECKONING INTERPOLATION
  // ============================================================================
  function installMultiplayerHooks() {
    // 1. Wrap postMpEvent so every race start, collision, horn, stamp & voice intercom
    // is broadcast across BOTH local Wi-Fi AND the Global Internet Room!
    if (typeof window.postMpEvent === "function" && !window.postMpEvent._wanWrapped) {
      const origPostMpEvent = window.postMpEvent;
      const wrappedPost = function (eventType, payload) {
        origPostMpEvent(eventType, payload);
        publishWanPacket(eventType, payload);
      };
      wrappedPost._wanWrapped = true;
      window.postMpEvent = wrappedPost;
    }

    // 2. Wrap sendSyncNow so local Wi-Fi stays at 70ms while Global Internet Room
    // syncs cleanly every 1.25s (plus immediate sync on car/color/driver change).
    if (typeof window.sendSyncNow === "function" && !window.sendSyncNow._wanWrapped) {
      const origSendSyncNow = window.sendSyncNow;
      const wrappedSync = function (forceWan) {
        origSendSyncNow();
        const now = Date.now();
        if (forceWan || now - wanState.lastWanSyncSentMs >= WAN_SYNC_INTERVAL_MS) {
          wanState.lastWanSyncSentMs = now;
          const snap = buildLocalPlayerSnapshot();
          if (snap) publishWanPacket("PLAYER_SYNC", snap);
        }
      };
      wrappedSync._wanWrapped = true;
      window.sendSyncNow = wrappedSync;
    }

    // 3. Wrap updateLobbyUi so WAN players remain visible even when LAN SSE sends an empty list
    if (typeof window.updateLobbyUi === "function" && !window.updateLobbyUi._wanWrapped) {
      const origUpdateLobbyUi = window.updateLobbyUi;
      const wrappedLobby = function (lanPlayers) {
        const now = Date.now();
        const merged = {};
        if (Array.isArray(lanPlayers)) {
          if (lanPlayers.length > 0) wanState.lanConnected = true;
          lanPlayers.forEach(p => {
            if (p && p.clientId) merged[p.clientId] = p;
          });
        }
        Object.keys(wanState.wanPlayers).forEach(id => {
          const wp = wanState.wanPlayers[id];
          if (now - wp.lastSeen <= 12000) {
            if (!merged[id]) merged[id] = wp;
            if (typeof getOrCreateRemotePlayer === "function" && id !== (window.myClientId || myClientId)) {
              getOrCreateRemotePlayer(wp);
            }
          } else {
            delete wanState.wanPlayers[id];
          }
        });
        origUpdateLobbyUi(Object.values(merged));
        updateWanStatusBadge();
      };
      wrappedLobby._wanWrapped = true;
      window.updateLobbyUi = wrappedLobby;
    }
  }

  // 4. 60fps Dead-Reckoning Velocity Extrapolation for Remote WAN Cars
  // Between 1.25s Internet Room packets, advance each remote car's targetPos
  // smoothly along its spline velocity / heading vector so cars never pause or jump!
  let lastDrTime = performance.now();
  function deadReckoningTick(now) {
    const dt = Math.min(0.05, Math.max(0.001, (now - lastDrTime) / 1000));
    lastDrTime = now;

    if (typeof remotePlayersMap !== "undefined") {
      const nowMs = Date.now();
      Object.values(remotePlayersMap).forEach(rp => {
        if (!rp || nowMs - rp.lastSeen > 12000) return;
        // Keep WAN peers alive for up to 12 seconds between packets
        const spdMps = Number(rp.speedMps) || ((Number(rp.speed) || 0) / 3.6);
        if (spdMps > 0.5) {
          if (Number.isFinite(rp.splineS) && typeof getFrameAtDistance === "function") {
            rp.splineS += spdMps * dt;
            rp.trackProgress = Math.round(rp.splineS);
            const f = getFrameAtDistance(rp.splineS);
            const pos = f.pt.clone().addScaledVector(f.right, rp.splineOffset || 0);
            rp.targetPos.copy(pos);
            rp.targetYaw = Math.atan2(f.tan.x, f.tan.z) + (rp.headingError || 0);
          } else {
            // 3D world heading velocity extrapolation
            rp.targetPos.x += Math.sin(rp.targetYaw) * spdMps * dt;
            rp.targetPos.z += Math.cos(rp.targetYaw) * spdMps * dt;
          }
        }
      });
    }
    requestAnimationFrame(deadReckoningTick);
  }

  // Ensure the Internet Room UI card exists in #mpanel-pairing
  function ensureInternetMultiplayerCard() {
    if (document.getElementById("wanRoomCard")) {
      updateWanStatusBadge();
      return;
    }
    const pairingPanel = document.getElementById("mpanel-pairing");
    if (!pairingPanel) return;

    const card = document.createElement("div");
    card.id = "wanRoomCard";
    card.className = "panel-box";
    card.style.marginBottom = "12px";
    card.style.border = "1.5px solid rgba(0, 229, 255, 0.45)";
    card.style.background = "linear-gradient(135deg, rgba(2, 132, 199, 0.16), rgba(15, 23, 42, 0.85))";
    card.innerHTML = `
      <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px; margin-bottom:8px;">
        <h3 style="font-size:15px; margin:0; color:#00e5ff;">🌍 Play Across Any Network (Internet Room Code)</h3>
        <span id="wanStatusBadge" style="font-size:11px; padding:4px 10px; border-radius:999px; border:1px solid rgba(0,229,255,0.45); background:rgba(0,229,255,0.14); color:#00e5ff;">
          🌍 Connecting Internet Room...
        </span>
      </div>
      <p style="font-size:12px; color:var(--dim); margin-bottom:10px;">
        Not on the same Wi-Fi? Enter the same <strong>Family Room Code</strong> on your iPhones, iPads and Android Fire Tablet to race together over <strong>4G/5G cellular, hotspots, or different Wi-Fi networks</strong> anywhere!
      </p>
      <div style="display:flex; gap:8px; flex-wrap:wrap; align-items:center;">
        <input id="wanRoomCodeInput" type="text" value="${wanState.roomCode}" placeholder="MR-FAMILY-7"
          style="flex:1; min-width:140px; padding:10px 12px; border-radius:10px; border:1.5px solid rgba(0,229,255,0.45); background:rgba(4,8,20,0.85); color:#fff; font-weight:900; font-size:14px; letter-spacing:1px; text-transform:uppercase;" />
        <button class="sync-race-btn" onclick="joinInternetRoom()" style="padding:10px 16px; background:linear-gradient(135deg,#0284c7,#00e5ff); color:#040814; font-weight:900;">
          🔗 Join Room
        </button>
        <button class="glass-btn" onclick="copyInternetInviteLink()" style="padding:10px 12px;">
          📋 Copy Link
        </button>
      </div>
    `;
    pairingPanel.insertBefore(card, pairingPanel.firstChild);
    updateWanStatusBadge();
  }

  function bootWanMultiplayer() {
    ensureInternetMultiplayerCard();
    installMultiplayerHooks();
    connectWanRoom(wanState.roomCode);
    requestAnimationFrame(deadReckoningTick);
    // Periodic cleanup & keepalive sync every 3.5s even if stationary
    setInterval(() => {
      installMultiplayerHooks();
      const snap = buildLocalPlayerSnapshot();
      if (snap && Date.now() - wanState.lastWanSyncSentMs >= 3200) {
        wanState.lastWanSyncSentMs = Date.now();
        publishWanPacket("PLAYER_SYNC", snap);
      }
      refreshCombinedLobbyList();
    }, 2000);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", bootWanMultiplayer);
  } else {
    bootWanMultiplayer();
  }
})();
