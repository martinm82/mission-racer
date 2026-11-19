using UnityEngine;

namespace MissionRacer.Tracks
{
    public enum TrackEnvironment
    {
        NeonCityNight,   // High-speed asphalt, skyscrapers, neon reflections
        CanyonRedRock,   // Desert canyon, dirt shortcuts for trucks, big jumps
        StuntParkArena   // Multi-level arena, loop-de-loops, boost pads, Cops & Robbers mode
    }

    [CreateAssetMenu(fileName = "NewTrack", menuName = "MissionRacer/Track Definition")]
    public class TrackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string trackId;
        public string displayName;
        public TrackEnvironment environment;
        [TextArea] public string description;

        [Header("Race Rules")]
        public int defaultLaps = 3;
        public float parLapTimeSeconds = 45f;
        public bool hasDirtShortcuts = false;

        [Header("Lighting & Visuals")]
        public Material skyboxMaterial;
        public Color ambientLightColor = Color.white;
        public Color fogColor = new Color(0.05f, 0.08f, 0.15f);
        public float fogDensity = 0.008f;
    }
}
