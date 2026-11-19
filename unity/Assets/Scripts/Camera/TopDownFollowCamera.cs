using UnityEngine;

namespace MissionRacer.CameraSystem
{
    public enum CameraPerspective
    {
        TopDownIsometric, // Default: best for mobile, age 7 & 10 situational awareness
        ThirdPersonChase  // Future 3D mode: behind the bumper
    }

    public class TopDownFollowCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform targetVehicle;
        [SerializeField] private Rigidbody targetRigidbody;

        [Header("Camera Mode")]
        [SerializeField] private CameraPerspective activePerspective = CameraPerspective.TopDownIsometric;

        [Header("Top-Down Isometric Preset")]
        [SerializeField] private float topDownHeight = 16f;
        [SerializeField] private float topDownDistanceBehind = 9f;
        [SerializeField] private float topDownPitchAngle = 62f;
        [SerializeField] private float topDownLookAheadFactor = 0.35f;

        [Header("3rd-Person Chase Cam Preset (Future Mode)")]
        [SerializeField] private float chaseHeight = 2.4f;
        [SerializeField] private float chaseDistanceBehind = 5.8f;
        [SerializeField] private float chasePitchAngle = 12f;

        [Header("Damping & Dynamic FOV")]
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSmoothSpeed = 5.5f;
        [SerializeField] private float normalFov = 55f;
        [SerializeField] private float boostFov = 68f;

        private Camera _cam;
        private Vector3 _currentVelocity;

        public CameraPerspective ActivePerspective => activePerspective;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null) _cam.fieldOfView = normalFov;
        }

        public void SetTarget(Transform target, Rigidbody rb = null)
        {
            targetVehicle = target;
            targetRigidbody = rb != null ? rb : target.GetComponent<Rigidbody>();
        }

        public void SetPerspective(CameraPerspective newPerspective)
        {
            activePerspective = newPerspective;
        }

        public void TogglePerspective()
        {
            activePerspective = (activePerspective == CameraPerspective.TopDownIsometric)
                ? CameraPerspective.ThirdPersonChase
                : CameraPerspective.TopDownIsometric;
        }

        private void LateUpdate()
        {
            if (targetVehicle == null) return;

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (activePerspective == CameraPerspective.TopDownIsometric)
            {
                // Top-Down: Smooth lookahead based on vehicle velocity
                Vector3 velocityOffset = Vector3.zero;
                if (targetRigidbody != null)
                {
                    velocityOffset = targetRigidbody.linearVelocity * topDownLookAheadFactor;
                }

                targetPosition = targetVehicle.position
                                 - (targetVehicle.forward * topDownDistanceBehind)
                                 + (Vector3.up * topDownHeight)
                                 + velocityOffset;

                Vector3 lookTarget = targetVehicle.position + velocityOffset;
                targetRotation = Quaternion.Euler(topDownPitchAngle, targetVehicle.eulerAngles.y, 0f);
            }
            else
            {
                // 3rd-Person Chase: Behind the car bumper
                targetPosition = targetVehicle.position
                                 - (targetVehicle.forward * chaseDistanceBehind)
                                 + (Vector3.up * chaseHeight);

                Vector3 lookTarget = targetVehicle.position + (targetVehicle.forward * 4f) + Vector3.up * 1.2f;
                targetRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            }

            // Smooth position movement
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, positionSmoothTime);

            // Smooth rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);

            // Dynamic FOV speed warp
            if (_cam != null && targetRigidbody != null)
            {
                float speedKmh = targetRigidbody.linearVelocity.magnitude * 3.6f;
                float targetFov = Mathf.Lerp(normalFov, boostFov, Mathf.Clamp01(speedKmh / 110f));
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * 3.5f);
            }
        }
    }
}
