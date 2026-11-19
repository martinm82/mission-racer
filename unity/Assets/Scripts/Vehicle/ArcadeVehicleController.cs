using System.Collections;
using UnityEngine;

namespace MissionRacer.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public class ArcadeVehicleController : MonoBehaviour
    {
        [Header("Vehicle Performance")]
        [SerializeField] private float maxSpeed = 38f;
        [SerializeField] private float accelerationRate = 28f;
        [SerializeField] private float reverseSpeed = 14f;
        [SerializeField] private float turnSensitivity = 95f;
        [SerializeField] private float brakeForce = 32f;
        [SerializeField] private float driftGrip = 0.85f;

        [Header("Player Input Mode")]
        [Tooltip("Enable direct keyboard/gamepad control (disable on AI cars)")]
        public bool useKeyboardInput = false;

        [Header("Boost / Nitro")]
        [SerializeField] private float boostMultiplier = 1.45f;
        [SerializeField] private float boostDuration = 2.5f;
        [SerializeField] private float boostCooldown = 5f;
        private float _currentBoostTimer = 0f;
        private float _boostCooldownTimer = 0f;
        public bool IsBoosting => _currentBoostTimer > 0f;

        [Header("Driver Assist (Ages 7 & 10)")]
        [Tooltip("Softly keeps car facing track velocity and eases oversteer for younger players")]
        public bool enableSteeringAssist = true;
        [Tooltip("Auto-reduces speed when entering sharp turns too fast")]
        public bool enableAutoBrakeAssist = false;
        [Tooltip("Auto-uprights vehicle if rolled upside down")]
        public bool autoResetIfFlipped = true;

        [Header("Ground & Physics References")]
        [SerializeField] private Transform[] groundRaycastPoints;
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float groundCheckDistance = 1.6f;
        [SerializeField] private Transform visualBody;

        private Rigidbody _rb;
        private float _moveInput;
        private float _steerInput;
        private bool _isGrounded;
        private bool _isDrifting;
        private float _flipTimer = 0f;

        public float CurrentSpeedKmh => _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;
        public float SpeedRatio => _rb != null ? Mathf.Clamp01(_rb.linearVelocity.magnitude / maxSpeed) : 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        }

        public void SetVisualBody(Transform body)
        {
            visualBody = body;
        }

        public void SetInputs(float move, float steer, bool drift = false, bool triggerBoost = false)
        {
            _moveInput = Mathf.Clamp(move, -1f, 1f);
            _steerInput = Mathf.Clamp(steer, -1f, 1f);
            _isDrifting = drift;

            if (triggerBoost && _boostCooldownTimer <= 0f && _currentBoostTimer <= 0f)
            {
                ActivateBoost();
            }
        }

        public void ActivateBoost()
        {
            _currentBoostTimer = boostDuration;
            _boostCooldownTimer = boostCooldown;
        }

        private void Update()
        {
            if (useKeyboardInput)
            {
                float v = UnityEngine.Input.GetAxis("Vertical");
                float h = UnityEngine.Input.GetAxis("Horizontal");
                bool drift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                bool boost = UnityEngine.Input.GetKeyDown(KeyCode.Space);
                SetInputs(v, h, drift, boost);
            }

            if (_currentBoostTimer > 0f) _currentBoostTimer -= Time.deltaTime;
            if (_boostCooldownTimer > 0f) _boostCooldownTimer -= Time.deltaTime;

            CheckAutoResetFlipped();
            AnimateVisualBodyTilt();
        }

        private void FixedUpdate()
        {
            _isGrounded = CheckGroundContact();

            if (_isGrounded)
            {
                ApplyAcceleration();
                ApplySteering();
                ApplyFriction();
            }
            else
            {
                _rb.AddForce(Physics.gravity * 2.2f, ForceMode.Acceleration);
            }
        }

        private bool CheckGroundContact()
        {
            if (groundRaycastPoints == null || groundRaycastPoints.Length == 0)
            {
                return Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, groundCheckDistance, groundLayer);
            }

            int groundedCount = 0;
            foreach (var pt in groundRaycastPoints)
            {
                if (pt != null && Physics.Raycast(pt.position, Vector3.down, groundCheckDistance, groundLayer))
                {
                    groundedCount++;
                }
            }
            return groundedCount >= 2;
        }

        private void ApplyAcceleration()
        {
            float targetMaxSpeed = IsBoosting ? maxSpeed * boostMultiplier : maxSpeed;
            float targetAccel = IsBoosting ? accelerationRate * 1.5f : accelerationRate;
            float effectiveMove = (IsBoosting && _moveInput >= -0.05f) ? 1f : _moveInput;

            if (effectiveMove > 0.05f)
            {
                if (_rb.linearVelocity.magnitude < targetMaxSpeed)
                {
                    Vector3 forwardForce = transform.forward * (effectiveMove * targetAccel);
                    _rb.AddForce(forwardForce, ForceMode.Acceleration);
                }
            }
            else if (effectiveMove < -0.05f)
            {
                float dot = Vector3.Dot(_rb.linearVelocity, transform.forward);
                if (dot > 1.0f)
                {
                    _rb.AddForce(-transform.forward * brakeForce, ForceMode.Acceleration);
                }
                else if (_rb.linearVelocity.magnitude < reverseSpeed)
                {
                    _rb.AddForce(transform.forward * (_moveInput * (accelerationRate * 0.6f)), ForceMode.Acceleration);
                }
            }

            if (enableAutoBrakeAssist && Mathf.Abs(_steerInput) > 0.7f && _rb.linearVelocity.magnitude > maxSpeed * 0.6f)
            {
                _rb.linearVelocity *= 0.985f;
            }
        }

        private void ApplySteering()
        {
            float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / 6f);
            float steerAngle = _steerInput * turnSensitivity * Time.fixedDeltaTime * speedFactor;

            if (Vector3.Dot(_rb.linearVelocity, transform.forward) < -0.2f)
            {
                steerAngle = -steerAngle;
            }

            if (enableSteeringAssist && !_isDrifting)
            {
                steerAngle *= 0.92f;
            }

            Quaternion deltaRotation = Quaternion.Euler(0f, steerAngle, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRotation);
        }

        private void ApplyFriction()
        {
            Vector3 forwardVelocity = transform.forward * Vector3.Dot(_rb.linearVelocity, transform.forward);
            Vector3 lateralVelocity = transform.right * Vector3.Dot(_rb.linearVelocity, transform.right);

            float lateralGrip = _isDrifting ? driftGrip : 0.15f;
            _rb.linearVelocity = forwardVelocity + (lateralVelocity * lateralGrip);
        }

        private void CheckAutoResetFlipped()
        {
            if (!autoResetIfFlipped) return;

            if (Vector3.Dot(transform.up, Vector3.up) < 0.25f)
            {
                _flipTimer += Time.deltaTime;
                if (_flipTimer >= 1.6f)
                {
                    ResetCarUpright();
                    _flipTimer = 0f;
                }
            }
            else
            {
                _flipTimer = 0f;
            }
        }

        public void ResetCarUpright()
        {
            transform.position += Vector3.up * 1.5f;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        private void AnimateVisualBodyTilt()
        {
            if (visualBody == null) return;

            float targetRoll = -_steerInput * (IsBoosting ? 6f : 4f);
            float targetPitch = _moveInput * 2.5f;

            Quaternion targetLocalRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
            visualBody.localRotation = Quaternion.Slerp(visualBody.localRotation, targetLocalRot, Time.deltaTime * 8f);
        }

        private void OnGUI()
        {
            if (!useKeyboardInput) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.fontSize = 18;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = Color.white;

            GUI.Box(new Rect(20, 20, 310, 95), "");
            GUI.Label(new Rect(35, 28, 280, 30), $"🏎️ SPEED: {Mathf.RoundToInt(CurrentSpeedKmh)} KM/H", boxStyle);
            GUI.Label(new Rect(35, 58, 280, 25), IsBoosting ? "⚡ NITRO BOOST: ACTIVE!" : "⚡ NITRO: READY (SPACEBAR)", boxStyle);
            GUI.Label(new Rect(35, 84, 280, 22), "Controls: W/A/S/D or Arrows | Shift: Drift");
        }
    }
}
