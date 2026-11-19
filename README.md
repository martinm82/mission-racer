# 🏎️ Mission Racer

> **A family multiplayer racing game connecting Dad and his two boys (ages 7 and 10) across distance, turning everyday schoolwork, reading, and chores into high-octane racing fuel.**

---

## 🏁 The Core Vision

**Mission Racer** combines three things:
1. **3D "Need for Speed" Arcade Racing**: Dynamic behind-the-car chase camera with speed-warp FOV, body roll into turns, glowing neon underglow, twin exhaust nitro flame bursts, and tailored driving assists for ages 7 and 10.
2. **The Physical NFC Racing License**: Each boy carries their own real PVC NFC card or keychain fob.
   * **Together ("The Pit Stop")**: Kids tap their physical license to Dad's phone. Dad's app acts as **Mission Control**, stamping their card with Racing Fuel and Customization Credits.
   * **Separated ("Remote Grand Prix")**: Boys complete missions at home. Dad approves them remotely, instantly crediting their license over the cloud.
3. **Screen-Time & Habit Balance**: 3 free daily races (energy-capped at 5). Long marathon binges are prevented naturally; more race fuel is earned through real-world learning and missions.

---

## 🗂️ Project Structure

```
mission-racer/
├── backend/
│   └── supabase/
│       └── migrations/
│           └── 20261111_001_initial_schema.sql    # Complete PostgreSQL database schema & seed data
├── unity/
│   └── Assets/
│       └── Scripts/
│           ├── Vehicle/
│           │   ├── ArcadeVehicleController.cs    # 3D Arcade physics, drift, boost, age 7 assists
│           │   └── VehiclePreset.cs              # ScriptableObject vehicle presets (Muscle, Sport, Truck, Bike)
│           ├── Camera/
│           │   └── TopDownFollowCamera.cs        # Smooth top-down isometric cam (with 3rd-person toggle)
│           ├── Garage/
│           │   └── GarageCustomization.cs        # Paint colors, spoilers, wheels, neon underglow
│           └── Missions/
│               └── MissionRacerManager.cs        # Fuel tank rules, credits, NFC license events
└── tools/
    └── mission-control-web/
        ├── index.html                            # Interactive Mission Control, License & Track simulator
        └── serve.js                              # Zero-dependency local web server
```

---

## 🚀 Running the Interactive Mission Control Simulator

We built an interactive web simulator where you can test the **Racing License**, **Dad's Pit Stop mission stamping**, **Garage customization**, and **Top-Down driving** directly in your browser or on your phone!

### 1. Launch the Server
```bash
node tools/mission-control-web/serve.js
```

### 2. Open in Browser
* **On your Mac**: Visit [http://localhost:3000](http://localhost:3000)
* **On your iPhone or Android Tablet**: Connect to the same Wi-Fi and open the local IP shown in your terminal (e.g., `http://192.168.0.100:3000`).

---

## 🪪 The Physical NFC Racing License (Hardware Specs)

To create the real cards for the boys:
* **Chip Type**: **NTAG215** or **NTAG213** (standard NFC chips, fully compatible with both iOS CoreNFC and Android NFC).
* **Format**:
  * Credit card sized white PVC cards (printable with home badge printers or custom printed via sticker overlay).
  * Or epoxy keychain fobs (durable for backpacks and pockets).
* **Printed Front**:
  * League Title: `MISSION RACER: OFFICIAL DRIVER LICENSE`
  * Callsign: `Lightning 7` (Boy 1) or `Thunder 10` (Boy 2)
  * Code: `MR-07-ROOKIE` / `MR-10-PRO`
* **Printed Back**:
  * Mission Stamp Grid (boxes for physical stickers or Dad stamps).

---

## 🎮 Vehicle Catalog

| Vehicle | Category | Personality | Base Top Speed |
| :--- | :--- | :--- | :--- |
| **V8 Thundercat** | Muscle Car | Roaring V8, heavy body, glorious drift slides | 88 km/h |
| **Apex GT** | Sports Coupe | Ultra-sharp cornering, active aero, rapid acceleration | 94 km/h |
| **Mud Crusher 4x4** | Monster Truck | Heavy 4x4, plows through barriers and dirt shortcuts | 76 km/h |
| **Trail Blazer 250** | Motocross Bike | Agile, tight turning, jumps, and wheelie boost | 90 km/h |
