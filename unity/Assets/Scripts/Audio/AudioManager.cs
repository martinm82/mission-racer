using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace MissionRacer.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer & Groups")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource engineSource;
        [SerializeField] private AudioSource tireScreechSource;
        [SerializeField] private AudioSource nitroSfxSource;
        [SerializeField] private AudioSource ambientWindSource;

        [Header("Engine Audio Tuning")]
        [SerializeField] private float idlePitch = 0.75f;
        [SerializeField] private float maxPitch = 2.4f;
        [SerializeField] private float gearRatioCount = 5f;
        [SerializeField] private float topSpeedForGears = 180f;

        [Header("Music Tracks")]
        [SerializeField] private AudioClip[] raceMusicTracks;
        [SerializeField] private AudioClip garageThemeTrack;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip nitroIgnitionClip;
        [SerializeField] private AudioClip gearShiftPopClip;
        [SerializeField] private AudioClip collisionThudClip;
        [SerializeField] private AudioClip countdownBeepClip;
        [SerializeField] private AudioClip countdownGoClip;

        private float _currentEnginePitch = 0.75f;

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
            PlayGarageMusic();
        }

        #region Dynamic Engine Audio

        /// <summary>
        /// Updates vehicle engine RPM pitch dynamically with virtual automatic gear shifts
        /// </summary>
        public void UpdateVehicleAudio(float speedKmh, float throttleInput, bool isDrifting, bool isBoosting)
        {
            if (engineSource == null) return;

            // 1. Calculate virtual gear and RPM
            float normalizedSpeed = Mathf.Clamp01(speedKmh / topSpeedForGears);
            float gearFraction = (normalizedSpeed * gearRatioCount) % 1.0f;
            if (normalizedSpeed >= 0.98f) gearFraction = 1.0f;

            float targetPitch = Mathf.Lerp(idlePitch, maxPitch, gearFraction);
            if (isBoosting) targetPitch *= 1.25f;

            _currentEnginePitch = Mathf.Lerp(_currentEnginePitch, targetPitch, Time.deltaTime * 6f);
            engineSource.pitch = _currentEnginePitch;
            engineSource.volume = Mathf.Lerp(0.35f, 0.95f, Mathf.Abs(throttleInput) + (speedKmh / topSpeedForGears) * 0.5f);

            // 2. Tire Screech on Drift
            if (tireScreechSource != null)
            {
                float targetScreechVol = (isDrifting && speedKmh > 25f) ? 0.75f : 0f;
                tireScreechSource.volume = Mathf.Lerp(tireScreechSource.volume, targetScreechVol, Time.deltaTime * 12f);
            }

            // 3. High Speed Wind Rush
            if (ambientWindSource != null)
            {
                ambientWindSource.volume = Mathf.Clamp01((speedKmh - 60f) / 120f) * 0.45f;
            }
        }

        public void PlayNitroIgnition()
        {
            if (nitroSfxSource != null && nitroIgnitionClip != null)
            {
                nitroSfxSource.PlayOneShot(nitroIgnitionClip, 0.9f);
            }
        }

        public void PlayCollisionImpact(float impactVelocity)
        {
            if (collisionThudClip != null)
            {
                float vol = Mathf.Clamp01(impactVelocity / 20f);
                AudioSource.PlayClipAtPoint(collisionThudClip, Camera.main.transform.position, vol);
            }
        }

        #endregion

        #region Music Playback

        public void PlayRaceMusic()
        {
            if (raceMusicTracks != null && raceMusicTracks.Length > 0 && musicSource != null)
            {
                int trackIdx = Random.Range(0, raceMusicTracks.Length);
                StartCoroutine(CrossfadeMusic(raceMusicTracks[trackIdx]));
            }
        }

        public void PlayGarageMusic()
        {
            if (garageThemeTrack != null && musicSource != null)
            {
                StartCoroutine(CrossfadeMusic(garageThemeTrack));
            }
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            float duration = 1.0f;
            float startVol = musicSource.volume;

            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }

            musicSource.clip = newClip;
            musicSource.Play();

            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                musicSource.volume = Mathf.Lerp(0f, 0.65f, t / duration);
                yield return null;
            }
            musicSource.volume = 0.65f;
        }

        #endregion
    }
}
