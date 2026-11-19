using UnityEngine;

namespace MissionRacer.Haptics
{
    public enum HapticImpactType
    {
        Light,   // Curb rumble, light tire slide
        Medium,  // Nitro ignition, drifting initiate
        Heavy,   // Barrier collision, vehicle bump
        Success  // Mission stamped, race podium finish
    }

    /// <summary>
    /// Provides rich mobile haptic sensations for iOS Taptic Engine and Android Haptics.
    /// </summary>
    public class HapticFeedbackManager : MonoBehaviour
    {
        public static HapticFeedbackManager Instance { get; private set; }

        public bool hapticsEnabled = true;

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

        public void PlayHaptic(HapticImpactType impact)
        {
            if (!hapticsEnabled) return;

#if UNITY_IOS && !UNITY_EDITOR
            switch (impact)
            {
                case HapticImpactType.Light:
                    _TriggerIosImpact(0); // UIImpactFeedbackStyleLight
                    break;
                case HapticImpactType.Medium:
                    _TriggerIosImpact(1); // UIImpactFeedbackStyleMedium
                    break;
                case HapticImpactType.Heavy:
                    _TriggerIosImpact(2); // UIImpactFeedbackStyleHeavy
                    break;
                case HapticImpactType.Success:
                    _TriggerIosNotification(0); // UINotificationFeedbackTypeSuccess
                    break;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#else
            // Editor Debug
            // Debug.Log($"[Haptics] Pulse: {impact}");
#endif
        }

        public void TriggerCollisionBump() => PlayHaptic(HapticImpactType.Heavy);
        public void TriggerNitroIgnition() => PlayHaptic(HapticImpactType.Medium);
        public void TriggerCurbRumble() => PlayHaptic(HapticImpactType.Light);
        public void TriggerPitStopStamp() => PlayHaptic(HapticImpactType.Success);

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerIosImpact(int style);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerIosNotification(int type);
#endif
    }
}
