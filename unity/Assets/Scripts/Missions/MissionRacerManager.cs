using System;
using UnityEngine;
using MissionRacer.Vehicle;

namespace MissionRacer.Missions
{
    public class MissionRacerManager : MonoBehaviour
    {
        public static MissionRacerManager Instance { get; private set; }

        [Header("Active Driver State")]
        [SerializeField] private string driverCallsign = "Lightning 7";
        [SerializeField] private int driverAge = 7;
        [SerializeField] private int creditsBalance = 500;
        [SerializeField] private int currentFuel = 3;
        [SerializeField] private int maxFuel = 5;
        [SerializeField] private string linkedLicenseUid = "04:8E:7F:A3:12:80";

        private bool showMissionDashboard = false;
        private string lastBannerNotification = "Welcome to Monaco GP 3D! Press [TAB] for Mission Dashboard.";
        private float bannerTimer = 6f;
        private ArcadeVehicleController playerVehicle;

        // Events
        public event Action<int> OnFuelChanged;
        public event Action<int> OnCreditsChanged;
        public event Action<string> OnLicenseStamped;

        public int CurrentFuel => currentFuel;
        public int MaxFuel => maxFuel;
        public int CreditsBalance => creditsBalance;
        public string DriverCallsign => driverCallsign;
        public int DriverAge => driverAge;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            GameObject playerObj = GameObject.Find("Player_Supercar_Lightning7");
            if (playerObj != null)
            {
                playerVehicle = playerObj.GetComponent<ArcadeVehicleController>();
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab) || UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                showMissionDashboard = !showMissionDashboard;
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
            }
        }

        public bool TryConsumeFuelForRace()
        {
            if (currentFuel <= 0)
            {
                ShowNotification("Out of Fuel! Complete a Mission in the Dashboard [TAB] first.");
                return false;
            }

            currentFuel--;
            OnFuelChanged?.Invoke(currentFuel);
            return true;
        }

        public void AddRewards(int credits, int fuel, string missionName = "Pit Stop Mission")
        {
            creditsBalance += credits;
            currentFuel = Mathf.Clamp(currentFuel + fuel, 0, maxFuel);

            OnCreditsChanged?.Invoke(creditsBalance);
            OnFuelChanged?.Invoke(currentFuel);
            ShowNotification($"STAMPED: {missionName} (+{credits} Coins, +{fuel} Fuel!)");
        }

        public void SelectDriverProfile(string callsign, int age, int defaultCredits, int defaultFuel)
        {
            driverCallsign = callsign;
            driverAge = age;
            creditsBalance = defaultCredits;
            currentFuel = defaultFuel;
            ShowNotification($"Switched Active Driver to {callsign}");
        }

        private void ShowNotification(string message)
        {
            lastBannerNotification = message;
            bannerTimer = 4.5f;
        }

        public void ProcessNfcLicenseScan(string scannedCardUid, bool isDadPitStop)
        {
            Debug.Log($"[MissionRacer] NFC Card Scanned: UID {scannedCardUid} (PitStop: {isDadPitStop})");

            if (scannedCardUid == linkedLicenseUid)
            {
                if (isDadPitStop)
                {
                    AddRewards(300, 2, "NFC Dad Pit Stop Stamp");
                    OnLicenseStamped?.Invoke("Official Stamp from Dad! +300 CR & +2 Fuel Cans!");
                }
                else
                {
                    ShowNotification($"NFC Driver Verified: Welcome back, {driverCallsign}!");
                    OnLicenseStamped?.Invoke($"Driver Verified: Welcome back, {driverCallsign}!");
                }
            }
        }

        private void OnGUI()
        {
            GUIStyle cardStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(14, 14, 10, 10)
            };
            cardStyle.normal.textColor = Color.white;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 6, 6)
            };

            // 1. Top-Left Live Telemetry & Driver Bar
            GUI.backgroundColor = new Color(0.05f, 0.08f, 0.16f, 0.92f);
            float speedKmh = playerVehicle != null ? Mathf.RoundToInt(playerVehicle.CurrentSpeedKmh) : 0f;
            GUI.Box(new Rect(16, 16, 330, 86),
                $"🏎️ DRIVER: {driverCallsign} (Age {driverAge})\n" +
                $"⛽ Fuel: {currentFuel}/{maxFuel}   |   🪙 Coins: {creditsBalance}\n" +
                $"⚡ Speed: {speedKmh:0} KM/H   |   Controls: WASD + Space (Nitro)",
                cardStyle);

            // 2. Top-Right Mission Dashboard Toggle Button
            GUI.backgroundColor = showMissionDashboard ? new Color(0.95f, 0.22f, 0.32f, 0.95f) : new Color(0.05f, 0.78f, 0.95f, 0.95f);
            if (GUI.Button(new Rect(Screen.width - 265, 16, 249, 44),
                showMissionDashboard ? "✕ Close Mission Dashboard (TAB)" : "🎯 Open Mission Dashboard (TAB)", btnStyle))
            {
                showMissionDashboard = !showMissionDashboard;
            }

            // 3. Toast Notification Banner
            if (bannerTimer > 0f && !string.IsNullOrEmpty(lastBannerNotification))
            {
                GUI.backgroundColor = new Color(0.06f, 0.55f, 0.38f, 0.95f);
                GUI.Box(new Rect(Screen.width * 0.5f - 230, 18, 460, 38), $"✨ {lastBannerNotification}", cardStyle);
            }

            // 4. Full Interactive Mission Control Dashboard Modal in Unity Play Mode
            if (showMissionDashboard)
            {
                float mw = 620f;
                float mh = 410f;
                float mx = (Screen.width - mw) * 0.5f;
                float my = (Screen.height - mh) * 0.5f;

                GUI.backgroundColor = new Color(0.04f, 0.07f, 0.14f, 0.97f);
                GUI.Box(new Rect(mx, my, mw, mh), "🎯 MISSION RACER 3D — DAD'S PIT BOSS MISSION DASHBOARD", cardStyle);

                // Driver Switcher Row
                GUI.Label(new Rect(mx + 20, my + 42, 200, 24), "Select Active Squad Driver:");
                GUI.backgroundColor = new Color(0.15f, 0.75f, 0.45f, 0.95f);
                if (GUI.Button(new Rect(mx + 20, my + 66, 180, 34), "⚡ Lightning 7 (Age 7)", btnStyle))
                    SelectDriverProfile("Lightning 7", 7, 500, 3);
                if (GUI.Button(new Rect(mx + 215, my + 66, 180, 34), "🔥 Thunder 10 (Age 10)", btnStyle))
                    SelectDriverProfile("Thunder 10", 10, 1200, 4);
                if (GUI.Button(new Rect(mx + 410, my + 66, 185, 34), "👑 Pit Boss Dad", btnStyle))
                    SelectDriverProfile("Pit Boss Dad", 40, 2500, 5);

                // Dad's Pit Stop Missions
                GUI.Label(new Rect(mx + 20, my + 115, 420, 24), "Stamp Real-World Missions to Award Fuel & Coins:");

                GUI.backgroundColor = new Color(0.12f, 0.52f, 0.88f, 0.95f);
                GUI.Box(new Rect(mx + 20, my + 142, mw - 40, 54), "📐 Math & Homework Mission (20 min)\nRewards: +300 Garage Coins & +1 Racing Fuel", cardStyle);
                if (GUI.Button(new Rect(mx + mw - 190, my + 150, 155, 38), "🔨 Stamp (+300 CR)", btnStyle))
                    AddRewards(300, 1, "Math & Homework Mission");

                GUI.Box(new Rect(mx + 20, my + 206, mw - 40, 54), "📖 Book Reading Mission (20 min)\nRewards: +250 Garage Coins & +1 Racing Fuel", cardStyle);
                if (GUI.Button(new Rect(mx + mw - 190, my + 214, 155, 38), "🔨 Stamp (+250 CR)", btnStyle))
                    AddRewards(250, 1, "Book Reading Mission");

                GUI.Box(new Rect(mx + 20, my + 270, mw - 40, 54), "🧹 Clean Bedroom & Study Desk\nRewards: +200 Garage Coins & +1 Racing Fuel", cardStyle);
                if (GUI.Button(new Rect(mx + mw - 190, my + 278, 155, 38), "🔨 Stamp (+200 CR)", btnStyle))
                    AddRewards(200, 1, "Clean Bedroom Mission");

                // Bottom Actions: Simulate NFC Scan & Open Web/Phone Dashboard
                GUI.backgroundColor = new Color(0.85f, 0.55f, 0.1f, 0.95f);
                if (GUI.Button(new Rect(mx + 20, my + 344, 280, 42), "🪪 Simulate NFC License Stamp (+2 Fuel)", btnStyle))
                    ProcessNfcLicenseScan(linkedLicenseUid, true);

                GUI.backgroundColor = new Color(0.2f, 0.65f, 0.95f, 0.95f);
                if (GUI.Button(new Rect(mx + 315, my + 344, 285, 42), "🌐 Open Web & Phone Dashboard (:3000)", btnStyle))
                    Application.OpenURL("http://localhost:3000");
            }
        }
    }
}
