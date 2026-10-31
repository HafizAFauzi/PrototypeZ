using System;
using System.Collections;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// FPS Camera Controller as specified in GDD Section 1.1, 2.5 and TSD Section 2.1.
    /// Manages the First-Person camera rig:
    /// 1. Idle Breathing: Subtle sinusoidal oscillation on camera position.
    /// 2. Dodge Sway ([Q] Key): Kamera bergerak cepat miring ke kiri/kanan sebesar -15 deg (roll)
    ///    dan bergeser lateral sejauh 1.2 unit selama 0.4 detik sebelum kembali ke posisi awal.
    /// 3. Fire Recoil: Kamera menghentak ke atas sebesar +3 deg (pitch) dan perlahan kembali (smooth damp).
    /// 4. Impact Camera Shake: Traumatic shake upon taking hits or parrying high-caliber shots.
    /// </summary>
    public class FPSCameraController : MonoBehaviour
    {
        public static FPSCameraController Instance { get; private set; }

        [Header("Camera & Rig References")]
        [Tooltip("The camera transform to manipulate. Defaults to Camera.main if null.")]
        [SerializeField] private Transform targetCameraTransform;

        [Header("Idle Breathing Dynamics")]
        [SerializeField] private bool enableBreathing = true;
        [SerializeField] private float breathingFrequency = 1.4f;
        [SerializeField] private float breathingAmplitude = 0.025f;

        [Header("Dodge Sway Settings (TSD Section 2.1)")]
        [Tooltip("Lateral displacement during dodge (default: 1.2 units)")]
        [SerializeField] private float dodgeDistance = 1.2f;
        [Tooltip("Roll tilt angle during dodge in degrees (default: -15 degrees)")]
        [SerializeField] private float dodgeRollAngle = 15.0f;
        [Tooltip("Total duration of dodge sway in seconds (default: 0.4s)")]
        [SerializeField] private float dodgeDuration = 0.4f;
        [Tooltip("Cooldown pause before another dodge can be initiated")]
        [SerializeField] private float dodgeCooldown = 0.15f;

        [Header("Recoil Dynamics")]
        [Tooltip("Upward pitch angle upon firing (default: +3 degrees)")]
        [SerializeField] private float recoilPitchAngle = 3.0f;
        [Tooltip("Speed of recoil decay recovery")]
        [SerializeField] private float recoilRecoverySpeed = 10.0f;

        // Runtime State
        private Vector3 _baseLocalPosition = Vector3.zero;
        private Quaternion _baseLocalRotation = Quaternion.identity;
        private bool _isDodging = false;
        private float _dodgeTimer = 0f;
        private float _dodgeCooldownTimer = 0f;
        private DodgeDirection _currentDodgeDirection = DodgeDirection.Left;
        private float _currentRecoilPitch = 0f;
        private float _shakeTimer = 0f;
        private float _shakeMagnitude = 0f;
        private float _timeAccumulator = 0f;

        // Public Properties
        public bool IsDodging => _isDodging;
        public float DodgeProgress => _isDodging && dodgeDuration > 0 ? Mathf.Clamp01(_dodgeTimer / dodgeDuration) : 0f;
        public DodgeDirection CurrentDodgeDirection => _currentDodgeDirection;
        public float DodgeDuration => dodgeDuration;
        public float DodgeDistance => dodgeDistance;
        public float DodgeRollAngle => dodgeRollAngle;
        public float CurrentRecoilPitch => _currentRecoilPitch;
        public Transform TargetCamera => targetCameraTransform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCameraAssigned();
        }

        private void Start()
        {
            EventBus.Subscribe<AimShotFiredEvent>(OnAimShotFired);

            if (targetCameraTransform != null)
            {
                _baseLocalPosition = targetCameraTransform.localPosition;
                _baseLocalRotation = targetCameraTransform.localRotation;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<AimShotFiredEvent>(OnAimShotFired);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timeAccumulator += dt;

            // Handle Cooldown
            if (_dodgeCooldownTimer > 0f)
            {
                _dodgeCooldownTimer -= dt;
            }

            // Keyboard shortcut [Q] for Dodge
            if (Input.GetKeyDown(KeyCode.Q))
            {
                TriggerDodge(DodgeDirection.Left);
            }

            // Update Dodge Timer
            if (_isDodging)
            {
                _dodgeTimer += dt;
                if (_dodgeTimer >= dodgeDuration)
                {
                    _isDodging = false;
                    _dodgeTimer = 0f;
                    _dodgeCooldownTimer = dodgeCooldown;
                }
            }

            // Update Recoil Pitch Recovery
            if (_currentRecoilPitch > 0.001f)
            {
                _currentRecoilPitch = Mathf.Lerp(_currentRecoilPitch, 0f, dt * recoilRecoverySpeed);
            }
            else
            {
                _currentRecoilPitch = 0f;
            }

            // Update Shake
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= dt;
                if (_shakeTimer <= 0f)
                {
                    _shakeMagnitude = 0f;
                }
            }

            ApplyCameraTransform(dt);
        }

        private void EnsureCameraAssigned()
        {
            if (targetCameraTransform == null)
            {
                if (Camera.main != null)
                {
                    targetCameraTransform = Camera.main.transform;
                }
                else
                {
                    var cam = FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        targetCameraTransform = cam.transform;
                    }
                    else
                    {
                        GameObject camGo = new GameObject("Main Camera");
                        camGo.tag = "MainCamera";
                        var c = camGo.AddComponent<Camera>();
                        targetCameraTransform = camGo.transform;
                    }
                }
            }
        }

        #region Public Actions

        /// <summary>
        /// Initiates a camera dodge sway maneuver to the left or right.
        /// Rotates camera roll (-15 deg) and shifts laterally (1.2 units) over 0.4s.
        /// </summary>
        public bool TriggerDodge(DodgeDirection direction = DodgeDirection.Left)
        {
            if (_isDodging || _dodgeCooldownTimer > 0f)
            {
                return false;
            }

            _isDodging = true;
            _dodgeTimer = 0f;
            _currentDodgeDirection = direction;

            Debug.Log($"<color=cyan>[FPSCamera]</color> Dodge Sway triggered ({direction})! Roll: -15°, Lateral: 1.2 units, Duration: {dodgeDuration}s");
            EventBus.Publish(new PlayerDodgeTriggeredEvent(direction, dodgeDuration));

            return true;
        }

        /// <summary>
        /// Triggers weapon fire recoil (+3 degrees upward pitch).
        /// </summary>
        public void TriggerFireRecoil(float pitchKick = -1f)
        {
            float kick = pitchKick > 0f ? pitchKick : recoilPitchAngle;
            _currentRecoilPitch = Mathf.Clamp(_currentRecoilPitch + kick, 0f, recoilPitchAngle * 2.2f);
        }

        /// <summary>
        /// Triggers an impulse camera shake (e.g. taking damage or parrying high caliber shot).
        /// </summary>
        public void TriggerShake(float magnitude = 0.15f, float duration = 0.25f)
        {
            _shakeMagnitude = magnitude;
            _shakeTimer = duration;
        }

        #endregion

        #region Transform Integration

        private void ApplyCameraTransform(float dt)
        {
            if (targetCameraTransform == null) return;

            // 1. Idle Breathing
            Vector3 breathingOffset = Vector3.zero;
            if (enableBreathing && !_isDodging)
            {
                breathingOffset = CalculateBreathingOffset(_timeAccumulator, breathingFrequency, breathingAmplitude);
            }

            // 2. Dodge Sway Offset & Roll
            Vector3 dodgeOffset = Vector3.zero;
            float dodgeRoll = 0f;
            if (_isDodging)
            {
                float progress = DodgeProgress;
                dodgeOffset = CalculateDodgeOffset(progress, dodgeDistance, _currentDodgeDirection);
                dodgeRoll = CalculateDodgeRoll(progress, dodgeRollAngle, _currentDodgeDirection);
            }

            // 3. Shake Offset
            Vector3 shakeOffset = Vector3.zero;
            if (_shakeTimer > 0f && _shakeMagnitude > 0f)
            {
                shakeOffset = new Vector3(
                    UnityEngine.Random.Range(-_shakeMagnitude, _shakeMagnitude),
                    UnityEngine.Random.Range(-_shakeMagnitude, _shakeMagnitude),
                    0f
                );
            }

            // Combine Position
            targetCameraTransform.localPosition = _baseLocalPosition + breathingOffset + dodgeOffset + shakeOffset;

            // Combine Rotation: Pitch (Recoil) + Roll (Dodge)
            Quaternion recoilRotation = Quaternion.Euler(-_currentRecoilPitch, 0f, 0f);
            Quaternion rollRotation = Quaternion.Euler(0f, 0f, dodgeRoll);
            targetCameraTransform.localRotation = _baseLocalRotation * recoilRotation * rollRotation;
        }

        #endregion

        #region Pure Calculation Helpers (Unit Testable)

        /// <summary>
        /// Calculates the lateral displacement vector for a dodge progress [0..1].
        /// Uses a smooth sinusoidal bell curve (sin(pi * t)) reaching peak at t=0.5.
        /// </summary>
        public static Vector3 CalculateDodgeOffset(float progress, float maxDistance, DodgeDirection direction)
        {
            float clamped = Mathf.Clamp01(progress);
            // Bell curve: starts at 0, peaks at 1 when progress = 0.5, returns to 0 at progress = 1.0
            float curve = Mathf.Sin(clamped * Mathf.PI);
            float sign = direction == DodgeDirection.Left ? -1f : 1f;
            return new Vector3(sign * maxDistance * curve, 0f, 0f);
        }

        /// <summary>
        /// Calculates the roll tilt angle in degrees for a dodge progress [0..1].
        /// Tilts -15 degrees (or +15 degrees for right) at peak.
        /// </summary>
        public static float CalculateDodgeRoll(float progress, float maxRollAngle, DodgeDirection direction)
        {
            float clamped = Mathf.Clamp01(progress);
            float curve = Mathf.Sin(clamped * Mathf.PI);
            // Left dodge rolls camera to left (-15 deg Z-rotation)
            float sign = direction == DodgeDirection.Left ? -1f : 1f;
            return sign * maxRollAngle * curve;
        }

        /// <summary>
        /// Calculates subtle sinusoidal idle breathing offset.
        /// </summary>
        public static Vector3 CalculateBreathingOffset(float time, float frequency, float amplitude)
        {
            float y = Mathf.Sin(time * frequency) * amplitude;
            float x = Mathf.Cos(time * frequency * 0.5f) * (amplitude * 0.4f);
            return new Vector3(x, y, 0f);
        }

        #endregion

        #region Event Callbacks

        private void OnAimShotFired(AimShotFiredEvent evt)
        {
            TriggerFireRecoil(recoilPitchAngle);
        }

        #endregion
    }
}
