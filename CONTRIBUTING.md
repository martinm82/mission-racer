# Contributing to Mission Racer 3D

Thank you for contributing to **Mission Racer 3D**! This guide covers our development workflow, multi-platform testing standards, Cloud OTA release checklist, and official **Unity 6 (C#)**, **WebGL (Three.js)**, and **Native iOS/Android** code & formatting guidelines.

---

## 1. Development Workflow & Branching

1. **Branching**:
   - `main`: Production-ready source of truth for the WebGL engine, native iOS/Android wrappers, and Unity 6 project.
   - `gh-pages`: Auto-generated static deployment branch serving `https://martinm82.github.io/mission-racer` for browser play and Cloud OTA auto-updates. Do not hand-edit `gh-pages`; use `bash scripts/publish_ota_update.sh`.
   - Feature branches: Use descriptive prefixes such as `feat/voice-intercom`, `fix/fire-tablet-fps`, or `unity/urp-track-shader`.
2. **Local Development Server**:
   - Start the local LAN & Pit Control server:
     ```bash
     node tools/mission-control-web/serve.js
     ```
   - Start the hybrid LAN + public HTTPS tunnel for 4G/5G WAN testing:
     ```bash
     bash scripts/start_internet_multiplayer.sh
     ```

---

## 2. Unity 6 (C#) Code & Formatting Guidelines

All C# code in `unity/Assets/Scripts/` and `unity/Assets/Editor/` adheres to standard Microsoft C# conventions adapted for **Unity 6 (`6000.x`)** and high-frame-rate mobile rendering. Formatting rules are machine-enforced by [`.editorconfig`](./.editorconfig).

### 2.1 Naming Conventions

| Symbol Type | Convention | Example |
| :--- | :--- | :--- |
| Namespace | `PascalCase` | `MissionRacer.Vehicles` |
| Class / Struct / Record | `PascalCase` | `ArcadeKartController`, `LapTelemetryData` |
| Interface | `IPascalCase` | `IDamageable`, `IMissionObjective` |
| Enum type & members | `PascalCase` | `RaceState.Countdown`, `CameraMode.Cockpit` |
| Public Method / Event / Property | `PascalCase` | `ApplyTurboBoost()`, `CurrentSpeedKmh` |
| Private & `[SerializeField]` Field | `_camelCase` | `_maxForwardSpeed`, `_rigidbody` |
| Constant (`const` / `static readonly`) | `PascalCase` | `MaxPlayersPerRoom`, `GravityAcceleration` |
| Local Variable & Method Parameter | `camelCase` | `deltaTime`, `steerInput`, `targetRotation` |

### 2.2 Brace & Indentation Style

- **Allman Braces**: Opening and closing braces (`{` and `}`) **must** be placed on their own line for namespaces, classes, structs, methods, properties, and all control flow statements (`if`, `else`, `for`, `foreach`, `while`, `switch`).
- **Indentation**: **4 spaces** per indentation level. Never use tab characters.
- **Line Width & Encoding**: Maximum **120 characters** per line, UTF-8 encoding, Unix LF (`\n`) line endings.
- **Always Use Braces**: Avoid single-line braceless `if` / `for` statements to prevent merge and debugging errors.

### 2.3 Unity-Specific Best Practices

1. **Encapsulation & Inspector Serialization**:
   - Prefer `[SerializeField] private` over `public` fields for values exposed in the Unity Inspector.
   - If external scripts need read access to an Inspector field, expose a read-only expression-bodied property:
     ```csharp
     [Header("Propulsion")]
     [Tooltip("Maximum forward speed in meters per second.")]
     [SerializeField] private float _maxForwardSpeed = 42.0f;

     public float MaxForwardSpeed => _maxForwardSpeed;
     ```
2. **Zero Per-Frame Heap Allocations (`Update`, `FixedUpdate`, `LateUpdate`)**:
   - **No `GetComponent<T>()` or `FindObjectOfType<T>()` in frame loops**: Cache all component references in `Awake()`.
   - **No LINQ (`System.Linq`) in runtime gameplay loops**: Use standard index-based `for` loops over arrays or `List<T>` to avoid iterator and closure allocations.
   - **No per-frame string concatenation for HUDs**: Only rebuild UI strings when the displayed integer or state actually changes.
   - **Cache Coroutine Yield Instructions**: Store `WaitForSeconds` instances in `readonly` fields rather than allocating `new WaitForSeconds(...)` inside loops.
   - **Use Non-Alloc Physics APIs**: Prefer `Physics.RaycastNonAlloc` / `Physics.OverlapSphereNonAlloc` (or preallocated buffers) over array-allocating queries.
3. **Lifecycle Separation**:
   - `Awake()`: Cache local component references (`GetComponent`) and initialize internal collections.
   - `OnEnable()` / `OnDisable()`: Subscribe and unsubscribe from C# events and Input System actions.
   - `Start()`: Cross-component initialization after all `Awake()` calls have completed.
   - `FixedUpdate()`: All `Rigidbody` forces, torques, wheel raycasts, and deterministic physics calculations.
   - `Update()`: Poll player input, update timers, and process non-physics state machines.
   - `LateUpdate()`: Smooth camera follow, cockpit head-bobbing, and HUD world-to-screen transforms.
4. **Component Safety & Inspector Organization**:
   - Add `[DisallowMultipleComponent]` and `[RequireComponent(typeof(...))]` on core `MonoBehaviour` classes.
   - Group serialized fields logically with `[Header("...")]` and document designer-facing values with `[Tooltip("...")]` and `[Range(min, max)]`.
5. **File Organization & Using Directives**:
   - Place **one** `MonoBehaviour` or `ScriptableObject` per `.cs` file. The filename **must** match the class name (`ArcadeKartController.cs` -> `public sealed class ArcadeKartController : MonoBehaviour`).
   - Place `using` directives at the top of the file, outside the namespace, sorted alphabetically with `System.*` namespaces first, followed by `UnityEngine.*`.

### 2.4 Reference Unity 6 C# Class Template

```csharp
using System;
using UnityEngine;

namespace MissionRacer.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrusterBoostController : MonoBehaviour
    {
        private const float MinBoostEnergy = 0.05f;

        [Header("Boost Configuration")]
        [Tooltip("Peak forward acceleration applied while turbo is active.")]
        [SerializeField] private float _boostAcceleration = 28.0f;

        [Tooltip("Energy drain rate per second while boosting.")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float _energyDrainRate = 0.25f;

        private Rigidbody _rigidbody;
        private float _currentEnergy = 1.0f;
        private bool _isBoostRequested;

        public float CurrentEnergy => _currentEnergy;
        public bool IsBoosting => _isBoostRequested && _currentEnergy > MinBoostEnergy;

        public event Action<float> BoostEnergyChanged;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            UpdateEnergy(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (!IsBoosting)
            {
                return;
            }

            Vector3 boostForce = transform.forward * _boostAcceleration;
            _rigidbody.AddForce(boostForce, ForceMode.Acceleration);
        }

        public void SetBoostInput(bool isPressed)
        {
            _isBoostRequested = isPressed;
        }

        private void UpdateEnergy(float deltaTime)
        {
            float previousEnergy = _currentEnergy;

            if (IsBoosting)
            {
                _currentEnergy = Mathf.Max(0.0f, _currentEnergy - (_energyDrainRate * deltaTime));
            }
            else
            {
                _currentEnergy = Mathf.Min(1.0f, _currentEnergy + (_energyDrainRate * 0.5f * deltaTime));
            }

            if (!Mathf.Approximately(previousEnergy, _currentEnergy))
            {
                BoostEnergyChanged?.Invoke(_currentEnergy);
            }
        }
    }
}
```

---

## 3. WebGL (`Three.js`) & Native iOS/Android Guidelines

### 3.1 Formatting Standards
- **JavaScript / HTML / CSS / JSON / YAML**: **2-space indentation**, LF line endings, single quotes preferred in JS.
- **Swift (`ios/`) & Java (`android/`)**: **4-space indentation**, LF line endings, 120-character max line width.

### 3.2 Fire HD 8 & Mobile GPU Performance Budget (`IS_LITE_GPU`)
Mission Racer 3D runs at a smooth 60 FPS on entry-level quad-core tablets (such as the Amazon Fire HD 8 with Mali-G52 GPU) as well as modern iPhones:
- **Shared Geometries & Materials**: Never instantiate new `THREE.BoxGeometry`, `THREE.CylinderGeometry`, or `THREE.MeshStandardMaterial` per road segment or per frame. Allocate shared geometries once at startup and reuse them.
- **Lite GPU Guards**: When `IS_LITE_GPU` is true:
  - Cap renderer pixel ratio to `1.25` (`Math.min(window.devicePixelRatio, 1.25)`).
  - Disable real-time shadow maps and `MeshPhysicalMaterial` clearcoat shaders in favor of pre-lit or Lambert/Phong materials.
  - Keep HUD overlays opaque or simple alpha-blended without CSS `backdrop-filter: blur()`.

---

## 4. Pre-Release & Cloud OTA Checklist

Before opening a pull request or running `bash scripts/publish_ota_update.sh`:

- [ ] **JS Syntax Validation**: `node --check tools/mission-control-web/wan-multiplayer.js` and inline `index.html` script check pass with zero errors.
- [ ] **Bundle Sync**: `ios/MissionRacer3D/WebBundle/` matches `tools/mission-control-web/`.
- [ ] **Android APK Build**: `bash scripts/build_android_apk.sh` succeeds and produces `build/MissionRacer3D.apk`.
- [ ] **Multiplayer & Intercom Verification**: Room code matching (`MARS`) and voice intercom bursts work across both Wi-Fi LAN and 4G/5G cellular connections.
- [ ] **OTA Version Increment**: `tools/mission-control-web/ota-manifest.json` version integer is incremented when publishing an OTA update.
