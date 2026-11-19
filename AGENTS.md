# Mission Racer 3D — AI Coding Agent Guide (`AGENTS.md`)

Welcome, AI coding agent. This repository contains **Mission Racer 3D**, a cross-platform 3D space-themed kids' racing and educational mission game with real-time hybrid LAN + 4G/5G WAN multiplayer, push-to-talk / hands-free voice intercom, native haptics, and zero-reinstall Cloud Over-The-Air (OTA) updates.

Always read and follow this document before modifying code, running builds, or publishing updates.

---

## 1. Repository Architecture & Multi-Target Map

Mission Racer 3D supports **four primary runtime targets** and a shared build/deployment pipeline:

```text
mission-racer/
├── tools/mission-control-web/      # Primary Three.js WebGL 3D Engine + Multiplayer Server
│   ├── index.html                  # Complete 3D WebGL racing engine, procedural synthesizer, UI & HUD
│   ├── wan-multiplayer.js          # Hybrid LAN WebSocket + 4G/5G Internet WebRTC/MQTT multiplayer & voice intercom
│   ├── serve.js                    # Node.js Pit Control server, LAN WebSocket broker & voice relay
│   └── ota-manifest.json           # Cloud OTA version manifest polled by iOS & Android apps on boot
├── ios/                            # Native iOS App (iPhone & iPad / TestFlight)
│   ├── project.yml                 # XcodeGen project specification
│   └── MissionRacer3D/
│       ├── MissionRacerViewController.swift  # WKWebView host, CoreHaptics bridge, AVAudioRecorder AAC intercom & Cloud OTA updater
│       └── WebBundle/              # Bundled fallback copy of index.html & wan-multiplayer.js
├── android/                        # Native Android App (Amazon Fire HD Tablets & Android Phones)
│   └── app/src/main/
│       ├── AndroidManifest.xml     # Immersive landscape config, audio/network permissions
│       └── java/com/migasiew/missionracer/
│           └── MainActivity.java   # Immersive sticky fullscreen WebView, Vibrator haptics bridge & Cloud OTA updater
├── unity/                          # Unity 6 (6000.x) Universal Render Pipeline (URP) Native 3D Project
│   ├── Assets/Scripts/             # Runtime C# gameplay, vehicle physics, progression & audio managers
│   ├── Assets/Editor/              # Automated build pipelines
│   │   ├── MissionRacerTestFlightBuilder.cs  # Headless iOS Xcode / TestFlight archive builder
│   │   └── MissionRacerAndroidBuilder.cs     # Headless Android APK / AAB builder
│   └── Packages/manifest.json      # Unity 6 URP & Input System dependencies
└── scripts/                        # Build, Sync & Cloud OTA Deployment Automation
    ├── build_android_apk.sh        # Syncs WebBundle into Android assets & builds signed MissionRacer3D.apk
    ├── deploy_testflight.sh        # Builds & uploads iOS app to App Store Connect / TestFlight
    ├── publish_ota_update.sh       # Bumps OTA version, syncs bundles, rebuilds APK & publishes to GitHub Pages
    └── start_internet_multiplayer.sh # Starts local LAN server + public HTTPS tunnel for WAN testing
```

---

## 2. Agent Verification & Deployment Rules

Whenever you modify gameplay, UI, audio, or networking code, you **must** execute the following verification and synchronization checklist:

### Rule 1: Always Validate JavaScript Syntax Before Building
Because `tools/mission-control-web/index.html` embeds the main 3D engine script, any syntax error will cause a black screen on iOS/Android. Always validate both `wan-multiplayer.js` and the inline script in `index.html`:

```bash
node --check tools/mission-control-web/wan-multiplayer.js
node -e '
const fs = require("fs");
const html = fs.readFileSync("tools/mission-control-web/index.html", "utf8");
const match = html.match(/<script>([\s\S]*?)<\/script>/);
if (!match) { console.error("No inline script found"); process.exit(1); }
new Function(match[1]);
console.log("index.html inline JS syntax OK");
'
```

### Rule 2: Keep Native Bundles & APK Synchronized
Changes in `tools/mission-control-web/index.html` or `tools/mission-control-web/wan-multiplayer.js` must **always** be synced to the native iOS bundle and compiled into the Android APK:

1. **Sync iOS WebBundle**:
   ```bash
   cp tools/mission-control-web/index.html ios/MissionRacer3D/WebBundle/index.html
   cp tools/mission-control-web/wan-multiplayer.js ios/MissionRacer3D/WebBundle/wan-multiplayer.js
   ```
2. **Rebuild Android APK** (automatically copies `index.html` and `wan-multiplayer.js` into `android/app/src/main/assets/` and outputs `build/MissionRacer3D.apk`):
   ```bash
   bash scripts/build_android_apk.sh
   ```

### Rule 3: Publishing Cloud OTA Updates
Both the native iOS app (`MissionRacerViewController.swift`) and the native Android app (`MainActivity.java`) include a **Cloud OTA Auto-Updater** that checks `https://martinm82.github.io/mission-racer/ota-manifest.json` on startup.
- To push an instant update to all installed iPhones, iPads, and Fire HD tablets worldwide without reinstalling via USB or TestFlight, run:
  ```bash
  bash scripts/publish_ota_update.sh "Brief description of release changes"
  ```
- This script increments the `version` integer in `tools/mission-control-web/ota-manifest.json`, syncs `ios/MissionRacer3D/WebBundle/`, rebuilds `build/MissionRacer3D.apk`, commits the changes, and deploys the static bundle to the `gh-pages` branch (`https://martinm82.github.io/mission-racer`).

### Rule 4: Respect Mobile & Fire HD 8 GPU Budgets
- Guard heavy shaders, particle counts, and shadow maps behind the `IS_LITE_GPU` flag in `index.html`.
- Never use CSS `backdrop-filter: blur(...)` on HUD overlays rendered on top of `<canvas>` on Android/Fire tablets (causes severe compositor frame drops).
- Reuse `THREE.BufferGeometry` and `THREE.Material` instances across road segments, hazards, and collectibles to avoid garbage-collection stalls.

---

## 3. Unity 6 C# Coding & Formatting Guidelines Summary

All C# scripts under `unity/Assets/` target **Unity 6 (`6000.x`)** with the **Universal Render Pipeline (URP)**. Full rules are defined in [`CONTRIBUTING.md`](./CONTRIBUTING.md) and enforced via [`.editorconfig`](./.editorconfig):

- **Braces & Indentation**: Allman style braces (opening `{` on a new line) with **4-space indentation** (no tabs) and a **120-character** max line width.
- **Naming Conventions**:
  - `PascalCase` for namespaces, classes, structs, enums, interfaces (`IPascalCase`), public methods, and properties.
  - `_camelCase` (leading underscore) for `private` and `[SerializeField] private` instance fields.
  - `camelCase` for method parameters and local variables.
- **Inspector & Encapsulation**:
  - Never expose mutable `public` fields for Unity Inspector serialization; use `[SerializeField] private` with optional read-only public properties (`public float MaxSpeed => _maxSpeed;`).
  - Annotate components with `[RequireComponent(typeof(...))]` and group fields with `[Header("...")]` and `[Tooltip("...")]`.
- **Zero Per-Frame Allocations**:
  - Do not allocate heap memory inside `Update()`, `FixedUpdate()`, or `LateUpdate()` (avoid LINQ, string concatenation in hot loops, `GetComponent<T>()` per frame, or uncached `new WaitForSeconds()`).
  - Cache all component references in `Awake()`.
- **File Organization**:
  - Exactly one `MonoBehaviour` or `ScriptableObject` per file, and the filename must exactly match the type name (e.g., `ArcadeKartController.cs`).
