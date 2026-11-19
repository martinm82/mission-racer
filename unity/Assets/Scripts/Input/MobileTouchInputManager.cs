using System;
using UnityEngine;
using MissionRacer.Vehicle;

namespace MissionRacer.InputHandling
{
    public enum SteeringControlStyle
    {
        TouchArrows,       // Left / Right touch buttons (Easiest for Age 7)
        VirtualThumbstick, // Floating analog stick (Best for Age 10)
        TiltSteering       // Gyroscope tilt
    }

    public class MobileTouchInputManager : MonoBehaviour
    {
        public static MobileTouchInputManager Instance { get; private set; }

        [Header("Target Vehicle")]
        [SerializeField] private ArcadeVehicleController targetVehicle;

        [Header("Control Preferences")]
        public SteeringControlStyle controlStyle = SteeringControlStyle.TouchArrows;
        public float tiltSensitivity = 2.2f;

        // Active Input Values
        private float _throttleInput;
        private float _steerInput;
        private bool _isDrifting;
        private bool _isBoosting;

        public float ThrottleInput => _throttleInput;
        public float SteerInput => _steerInput;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetTargetVehicle(ArcadeVehicleController vehicle)
        {
            targetVehicle = vehicle;
        }

        private void Update()
        {
            // Hardware Gamepad / Keyboard override (if connected)
            float hardwareHorizontal = UnityEngine.Input.GetAxis("Horizontal");
            float hardwareVertical = UnityEngine.Input.GetAxis("Vertical");

            if (Mathf.Abs(hardwareHorizontal) > 0.05f || Mathf.Abs(hardwareVertical) > 0.05f)
            {
                _steerInput = hardwareHorizontal;
                _throttleInput = hardwareVertical;
            }
            else if (controlStyle == SteeringControlStyle.TiltSteering)
            {
                // Accelerometer tilt along X axis
                _steerInput = Mathf.Clamp(UnityEngine.Input.acceleration.x * tiltSensitivity, -1f, 1f);
            }

            if (targetVehicle != null)
            {
                targetVehicle.SetInputs(_throttleInput, _steerInput, _isDrifting, _isBoosting);
            }

            // Reset transient flags
            _isBoosting = false;
        }

        #region On-Screen UI Button Events

        public void SetSteerLeft(bool isHeld)
        {
            if (isHeld) _steerInput = -1f;
            else if (_steerInput < 0f) _steerInput = 0f;
        }

        public void SetSteerRight(bool isHeld)
        {
            if (isHeld) _steerInput = 1f;
            else if (_steerInput > 0f) _steerInput = 0f;
        }

        public void SetThrottle(bool isHeld)
        {
            _throttleInput = isHeld ? 1f : 0f;
        }

        public void SetBrakeReverse(bool isHeld)
        {
            _throttleInput = isHeld ? -1f : 0f;
        }

        public void SetDrift(bool isHeld)
        {
            _isDrifting = isHeld;
        }

        public void TriggerNitro()
        {
            _isBoosting = true;
        }

        public void SetJoystickSteer(float normalizedX)
        {
            if (controlStyle == SteeringControlStyle.VirtualThumbstick)
            {
                _steerInput = Mathf.Clamp(normalizedX, -1f, 1f);
            }
        }

        #endregion
    }
}
