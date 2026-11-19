using System;
using UnityEngine;

#if ENABLE_VIVOX
using Unity.Services.Vivox;
using VivoxUnity;
#endif

namespace MissionRacer.Voice
{
    public enum MicMode
    {
        OpenMic,     // Hands-free talking while steering
        PushToTalk   // Hold on-screen radio button to talk like race engineer
    }

    /// <summary>
    /// Pit Radio (In-Game Voice Chat) powered by Unity Vivox.
    /// Free tier includes 5,000 MAU. Provides low-latency team voice chat
    /// so Dad and the boys can talk and laugh during the race.
    /// </summary>
    public class PitRadioVoiceManager : MonoBehaviour
    {
        public static PitRadioVoiceManager Instance { get; private set; }

        [Header("Radio Settings")]
        [SerializeField] private MicMode activeMicMode = MicMode.OpenMic;
        [SerializeField] private bool isMuted = false;
        [SerializeField] private AudioSource radioSfxSource;
        [SerializeField] private AudioClip radioChirpClip; // Classic walkie-talkie beep

        public MicMode ActiveMicMode => activeMicMode;
        public bool IsMuted => isMuted;

        public event Action<bool> OnMuteStateChanged;
        public event Action<string, bool> OnPlayerTalkingStateChanged; // ParticipantName, isTalking

        private string _activeChannelName = "";

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
        /// Automatically joins the private family voice channel using the Relay Room Code
        /// </summary>
        public async void JoinFamilyRadioChannel(string relayRoomCode)
        {
            _activeChannelName = $"family_radio_{relayRoomCode}";
            Debug.Log($"[PitRadio] Connecting to voice channel: {_activeChannelName}");

#if ENABLE_VIVOX
            try
            {
                await VivoxService.Instance.InitializeAsync();
                await VivoxService.Instance.LoginAsync();

                var channelOptions = new ChannelOptions
                {
                    MakeActiveChannelUponJoining = true
                };

                await VivoxService.Instance.JoinGroupChannelAsync(_activeChannelName, ChatCapability.AudioOnly, channelOptions);
                Debug.Log("[PitRadio] Successfully joined family voice channel!");

                ApplyMicMode();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PitRadio] Failed to connect to voice channel: {ex.Message}");
            }
#else
            Debug.Log($"[PitRadio] Voice Channel Active: {_activeChannelName} (Ready for Vivox SDK)");
#endif
        }

        public void LeaveRadioChannel()
        {
#if ENABLE_VIVOX
            if (!string.IsNullOrEmpty(_activeChannelName))
            {
                VivoxService.Instance.LeaveChannelAsync(_activeChannelName);
            }
#endif
        }

        #region Mic Control (Open Mic vs Push-To-Talk)

        public void SetMicMode(MicMode mode)
        {
            activeMicMode = mode;
            ApplyMicMode();
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyMicMode();
            OnMuteStateChanged?.Invoke(isMuted);
        }

        public void SetPushToTalkActive(bool isPressed)
        {
            if (activeMicMode != MicMode.PushToTalk) return;

            if (isPressed)
            {
                PlayRadioChirp();
                SetHardwareMicMuted(false);
            }
            else
            {
                SetHardwareMicMuted(true);
            }
        }

        private void ApplyMicMode()
        {
            if (isMuted)
            {
                SetHardwareMicMuted(true);
            }
            else if (activeMicMode == MicMode.OpenMic)
            {
                SetHardwareMicMuted(false);
            }
            else if (activeMicMode == MicMode.PushToTalk)
            {
                SetHardwareMicMuted(true); // Wait for push
            }
        }

        private void SetHardwareMicMuted(bool mute)
        {
#if ENABLE_VIVOX
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                if (mute) VivoxService.Instance.MuteInputDevice();
                else VivoxService.Instance.UnmuteInputDevice();
            }
#endif
        }

        private void PlayRadioChirp()
        {
            if (radioSfxSource != null && radioChirpClip != null)
            {
                radioSfxSource.PlayOneShot(radioChirpClip, 0.4f);
            }
        }

        #endregion
    }
}
