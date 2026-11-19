using System;
using UnityEngine;

namespace MissionRacer.NFC
{
    /// <summary>
    /// Cross-platform C# bridge for reading physical NTAG215/213 Racing Licenses
    /// Supports iOS (CoreNFC) and Android (NfcAdapter).
    /// </summary>
    public class NativeNfcManager : MonoBehaviour
    {
        public static NativeNfcManager Instance { get; private set; }

        public event Action<string, string> OnLicenseCardRead; // (cardUid, licensePayload)
        public event Action<string> OnNfcError;

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

        /// <summary>
        /// Prompts the user to hold their physical Racing License card to the phone
        /// </summary>
        public void StartScanningLicenseCard(string promptMessage = "Hold your Racing License to the top of your phone")
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Calls native iOS CoreNFC framework
            _StartIosNfcSession(promptMessage);
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Calls Android NfcAdapter foreground dispatch
            _StartAndroidNfcSession();
#else
            Debug.Log($"[NativeNFC] (Editor Simulation) Prompt: {promptMessage}");
            // Simulate read in Unity Editor after 1 second
            Invoke(nameof(SimulateEditorNfcRead), 1.0f);
#endif
        }

        private void SimulateEditorNfcRead()
        {
            HandleNfcCardDetected("04:8E:7F:A3:12:80", "mr://license/MR-07-ROOKIE");
        }

        /// <summary>
        /// Callback invoked by iOS Objective-C / Android Java bridge when NFC tag is scanned
        /// </summary>
        public void HandleNfcCardDetected(string cardUid, string payload)
        {
            Debug.Log($"[NativeNFC] Card Detected! UID: {cardUid}, Payload: {payload}");

            // Trigger satisfying mobile haptic feedback
            TriggerHapticSuccess();

            OnLicenseCardRead?.Invoke(cardUid, payload);

            // Forward to MissionRacerManager for fuel/credits/stamping
            if (MissionRacer.Missions.MissionRacerManager.Instance != null)
            {
                bool isDadHost = true; // In Pit Stop mode, Dad is stamping
                MissionRacer.Missions.MissionRacerManager.Instance.ProcessNfcLicenseScan(cardUid, isDadHost);
            }
        }

        public void HandleNfcError(string errorReason)
        {
            Debug.LogWarning($"[NativeNFC] Scan error: {errorReason}");
            OnNfcError?.Invoke(errorReason);
        }

        private void TriggerHapticSuccess()
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _StartIosNfcSession(string alertMessage);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void _StartAndroidNfcSession()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var nfcAdapterClass = new AndroidJavaClass("android.nfc.NfcAdapter"))
                {
                    var nfcAdapter = nfcAdapterClass.CallStatic<AndroidJavaObject>("getDefaultAdapter", currentActivity);
                    if (nfcAdapter == null || !nfcAdapter.Call<bool>("isEnabled"))
                    {
                        HandleNfcError("NFC is not supported or disabled on this Android device.");
                    }
                }
            }
            catch (Exception ex)
            {
                HandleNfcError("Android NFC Error: " + ex.Message);
            }
        }
#endif
    }
}
