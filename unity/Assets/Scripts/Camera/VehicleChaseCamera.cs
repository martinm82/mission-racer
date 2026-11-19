using UnityEngine;

namespace MissionRacer.CameraSystem
{
    /// <summary>
    /// Need for Speed style dynamic 3rd-person chase camera.
    /// Features:
    /// - Low bumper follow angle with smooth yaw damping
    /// - Dynamic FOV speed-warp when boosting / reaching high speeds
    /// - Acceleration squat & braking dive
    /// - Lateral drift offset so powerslides look cinematic
    /// </summary>
    public class VehicleChaseCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform targetVehicle;
        [SerializeField] private Rigidbody targetRigidbody;

        [Header("Chase Offsets (Need for Speed Style)")]
        [SerializeField] private float height = 1.9f;             // Low angle behind rear window/bumper
        [SerializeField] private float distanceBehind = 4.8f;     // Tight distance for high sense of speed
        [SerializeField] private float lookAheadDistance = 6.0f;  // Points camera down the road
        [SerializeField] private float pitchAngle = 10f;          // Slight downward tilt

        [Header("Dynamic Response")]
        [SerializeField] private float positionDamping = 0.08f;   // Responsive follow
        [SerializeField] private float rotationDamping = 6.5f;     // Camera swings smoothly around drifts
        [SerializeField] private float driftSwingFactor = 0.4f;   // How much camera swings sideways in slides

        [Header("FOV Speed Warp")]
        [SerializeField] private float normalFov = 62f;
        [SerializeField] private float boostFov = 82f;
        [SerializeField] private float maxFovSpeedKmh = 120f;

        private Camera _cam;
        private float _smoothedYaw;
        private bool _yawInitialized = false;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = normalFov;
        }

        public void SetTarget(Transform target, Rigidbody rb = null)
        {
            targetVehicle = target;
            targetRigidbody = rb != null ? rb : target.GetComponent<Rigidbody>();
            if (targetVehicle != null)
            {
                _smoothedYaw = targetVehicle.eulerAngles.y;
                _yawInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (targetVehicle == null) return;

            if (!_yawInitialized)
            {
                _smoothedYaw = targetVehicle.eulerAngles.y;
                _yawInitialized = true;
            }

            // 1. Smoothly damp camera yaw angle while anchoring distance directly to vehicle position (zero forward/backward jitter)
            _smoothedYaw = Mathf.LerpAngle(_smoothedYaw, targetVehicle.eulerAngles.y, 1f - Mathf.Exp(-rotationDamping * Time.deltaTime));
            Quaternion camYawRot = Quaternion.Euler(0f, _smoothedYaw, 0f);
            Vector3 camForward = camYawRot * Vector3.forward;

            Vector3 velocity = targetRigidbody != null ? targetRigidbody.linearVelocity : targetVehicle.forward * 10f;
            float speedKmh = velocity.magnitude * 3.6f;

            Vector3 targetPos = targetVehicle.position;
            transform.position = targetPos - (camForward * distanceBehind) + (Vector3.up * height);

            // 2. Look ahead along the road
            Vector3 lookTarget = targetPos + (camForward * lookAheadDistance) + (Vector3.up * (height * 0.45f));
            transform.LookAt(lookTarget, Vector3.up);

            // 3. Dynamic FOV speed warp
            if (_cam != null)
            {
                float speedRatio = Mathf.Clamp01(speedKmh / maxFovSpeedKmh);
                float desiredFov = Mathf.Lerp(normalFov, boostFov, speedRatio);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, desiredFov, Time.deltaTime * 5.0f);
            }
        }
    }
}
