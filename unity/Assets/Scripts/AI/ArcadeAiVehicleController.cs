using UnityEngine;
using MissionRacer.Vehicle;

namespace MissionRacer.AI
{
    public enum AiPersonality
    {
        FriendlyRookie, // TurboPup: Forgiving, makes occasional mistakes, lets 7yo pass
        CompetitivePro  // RoboRacer / SpeedyFox: Takes racing line, drifts corners, uses nitro
    }

    [RequireComponent(typeof(ArcadeVehicleController))]
    public class ArcadeAiVehicleController : MonoBehaviour
    {
        [Header("AI Identity")]
        public string botName = "TurboPup";
        public AiPersonality personality = AiPersonality.FriendlyRookie;

        [Header("Waypoint Navigation")]
        [SerializeField] private Tracks.WaypointPath waypointPath;
        [SerializeField] private float waypointReachThreshold = 6f;
        [SerializeField] private float steeringSmoothDamp = 0.15f;

        [Header("Family Rubber-Banding")]
        [Tooltip("Slows down slightly if children are trailing behind to keep the family pack tight")]
        public bool enableFamilyRubberBand = true;
        [SerializeField] private Transform trailingPlayerTransform;

        private ArcadeVehicleController _vehicle;
        private int _currentWaypointIndex = 0;
        private float _currentSteer = 0f;
        private float _steerVelocity = 0f;

        private void Awake()
        {
            _vehicle = GetComponent<ArcadeVehicleController>();
        }

        public void SetWaypointPath(Tracks.WaypointPath path)
        {
            waypointPath = path;
            _currentWaypointIndex = 0;
        }

        public void SetTrailingPlayer(Transform player)
        {
            trailingPlayerTransform = player;
        }

        private void Update()
        {
            if (waypointPath == null || waypointPath.WaypointCount == 0) return;

            Vector3 targetWaypoint = waypointPath.GetWaypointPosition(_currentWaypointIndex);
            Vector3 toTarget = targetWaypoint - transform.position;
            toTarget.y = 0f;

            // Advance waypoint when reached
            if (toTarget.magnitude < waypointReachThreshold)
            {
                _currentWaypointIndex = (int)Mathf.Repeat(_currentWaypointIndex + 1, waypointPath.WaypointCount);
                targetWaypoint = waypointPath.GetWaypointPosition(_currentWaypointIndex);
                toTarget = (targetWaypoint - transform.position);
                toTarget.y = 0f;
            }

            // Calculate steering angle towards target waypoint
            float targetAngle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
            float desiredSteer = Mathf.Clamp(targetAngle / 45f, -1f, 1f);
            _currentSteer = Mathf.SmoothDamp(_currentSteer, desiredSteer, ref _steerVelocity, steeringSmoothDamp);

            // Throttle & Rubber-Band calculation
            float throttle = 1.0f;
            bool shouldDrift = Mathf.Abs(targetAngle) > 35f;

            // Rubber-banding: If family player is trailing, ease off the gas slightly
            if (enableFamilyRubberBand && trailingPlayerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, trailingPlayerTransform.position);
                Vector3 toPlayer = trailingPlayerTransform.position - transform.position;
                bool isAheadOfPlayer = Vector3.Dot(transform.forward, toPlayer) < 0f;

                if (isAheadOfPlayer && distanceToPlayer > 30f)
                {
                    // Ease off to let the young player catch up
                    throttle = personality == AiPersonality.FriendlyRookie ? 0.65f : 0.85f;
                }
            }

            // Fire Nitro on straight sections when facing straight ahead
            bool triggerNitro = Mathf.Abs(targetAngle) < 8f && throttle > 0.9f && Random.value < 0.05f;

            // Pass inputs to vehicle controller
            _vehicle.SetInputs(throttle, _currentSteer, shouldDrift, triggerNitro);
        }
    }
}
