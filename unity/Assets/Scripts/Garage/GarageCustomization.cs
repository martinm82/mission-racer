using System;
using UnityEngine;

namespace MissionRacer.Garage
{
    [Serializable]
    public class GarageCustomization
    {
        [Header("Colors")]
        public Color primaryBodyColor = new Color(0.9f, 0.15f, 0.15f);
        public Color secondaryStripeColor = new Color(0.1f, 0.1f, 0.1f);
        public Color neonUnderglowColor = new Color(0f, 0.9f, 1f);
        public bool isNeonEnabled = true;

        [Header("Cosmetic Attachments")]
        public int selectedSpoilerIndex = 0;
        public int selectedWheelIndex = 0;
        public string selectedHornId = "beep_classic";

        [Header("Performance Upgrades (0 to 3)")]
        [Range(0, 3)] public int engineLevel = 0;   // Boosts top speed & acceleration
        [Range(0, 3)] public int tireLevel = 0;     // Boosts grip & drift recovery
        [Range(0, 3)] public int handlingLevel = 0; // Boosts turn response

        public float GetUpgradedSpeed(float baseSpeed) => baseSpeed * (1f + (engineLevel * 0.08f));
        public float GetUpgradedAccel(float baseAccel) => baseAccel * (1f + (engineLevel * 0.10f));
        public float GetUpgradedHandling(float baseHandling) => baseHandling * (1f + (handlingLevel * 0.07f));
    }
}
