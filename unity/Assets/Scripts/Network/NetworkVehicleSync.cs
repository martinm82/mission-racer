using UnityEngine;

namespace MissionRacer.Network
{
    public class NetworkVehicleSync : MonoBehaviour
    {
        [Header("Interpolation Settings (Cellular / Wi-Fi Smoothness)")]
        [SerializeField] private float positionLerpRate = 18f;
        [SerializeField] private float rotationLerpRate = 16f;

        [Header("Visual Meshes & Customization")]
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Light neonUnderglowLight;
        [SerializeField] private GameObject[] nitroFlameObjects;
        [SerializeField] private AudioSource hornAudioSource;
        [SerializeField] private AudioClip[] sillyHornClips;

        public void SetCosmetics(string paintHex, string neonHex)
        {
            ApplyPaintColor(paintHex);
            ApplyNeonColor(neonHex);
        }

        private void ApplyPaintColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || bodyRenderer == null) return;
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                bodyRenderer.material.color = color;
            }
        }

        private void ApplyNeonColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || neonUnderglowLight == null) return;
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                neonUnderglowLight.color = color;
                neonUnderglowLight.enabled = true;
            }
        }

        public void ToggleNitroFlames(bool active)
        {
            if (nitroFlameObjects == null) return;
            foreach (var obj in nitroFlameObjects)
            {
                if (obj != null) obj.SetActive(active);
            }
        }

        public void TriggerSillyHorn(int hornIndex = 0)
        {
            if (hornAudioSource != null && sillyHornClips != null && hornIndex < sillyHornClips.Length)
            {
                hornAudioSource.PlayOneShot(sillyHornClips[hornIndex]);
            }
        }
    }
}
