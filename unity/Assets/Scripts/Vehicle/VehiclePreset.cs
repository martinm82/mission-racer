using UnityEngine;

namespace MissionRacer.Vehicle
{
    public enum VehicleType
    {
        MuscleCar,
        SportsCar,
        OffroadTruck,
        Motorbike
    }

    [CreateAssetMenu(fileName = "NewVehiclePreset", menuName = "MissionRacer/Vehicle Preset")]
    public class VehiclePreset : ScriptableObject
    {
        [Header("Identity")]
        public string codeName;
        public string displayName;
        public VehicleType vehicleType;
        [TextArea] public string description;

        [Header("Physical Attributes")]
        public GameObject vehiclePrefab;
        public float baseWeightKg = 1400f;
        public float baseTopSpeedKmh = 90f;
        public float baseAcceleration = 24f;
        public float baseHandlingRating = 70f;

        [Header("Garage Economy")]
        public int creditPrice = 0;
        public bool isStarterVehicle = false;
        public int minDriverLevel = 1;

        [Header("Visual Mount Points")]
        public Vector3 spoilerMountOffset;
        public Vector3[] wheelOffsets;
    }
}
