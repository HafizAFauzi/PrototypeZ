using System.Collections;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Diegetic 2D/2.5D visual controller for the Tactical Revolver held in the player's right hand.
    /// Supports both 2D Sprite mode (Orthographic) and 3D Mesh mode (Perspective).
    /// Visualizes 2D/3D cylinder roll, reel stops, chamber neon colors, and recoil kick.
    /// </summary>
    public class DiegeticRevolverView : MonoBehaviour
    {
        [Header("Transform Hooks")]
        [Tooltip("The rotating cylinder sprite or mesh containing the reels")]
        [SerializeField] private Transform cylinderTransform;
        [Tooltip("Right hand weapon root for recoil kicks")]
        [SerializeField] private Transform weaponRoot;

        [Header("Mode Configuration")]
        [Tooltip("When true, recoil kicks and rotation are optimized for 2D Orthographic view")]
        [SerializeField] private bool is2DMode = true;

        [Header("Spin Motion")]
        [SerializeField] private Vector3 cylinderRotationAxis = Vector3.forward;
        [SerializeField] private float spinRpm = 1200f;
        [SerializeField] private float smoothStopDamping = 8f;

        [Header("Recoil Animation")]
        [SerializeField] private float recoilKickAngle = -8f;
        [SerializeField] private float recoilKickDistance = 0.2f;
        [SerializeField] private float recoilRecoverySpeed = 12f;

        [Header("Chamber Lights & Renderers (Optional)")]
        [SerializeField] private Light[] chamberLights;
        [SerializeField] private Renderer[] reelRenderers;
        [SerializeField] private SpriteRenderer[] reelSpriteRenderers;

        private bool _isSpinning = false;
        private float _currentSpinSpeed = 0f;
        private Vector3 _originalWeaponPos;
        private Quaternion _originalWeaponRot;

        private void Awake()
        {
            if (weaponRoot != null)
            {
                _originalWeaponPos = weaponRoot.localPosition;
                _originalWeaponRot = weaponRoot.localRotation;
            }
        }

        private void Start()
        {
            EventBus.Subscribe<SlotSpinStartedEvent>(OnSpinStarted);
            EventBus.Subscribe<SlotReelStoppedEvent>(OnReelStopped);
            EventBus.Subscribe<SlotSpinCompletedEvent>(OnSpinCompleted);
            EventBus.Subscribe<TurnCommittedEvent>(OnTurnCommitted);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SlotSpinStartedEvent>(OnSpinStarted);
            EventBus.Unsubscribe<SlotReelStoppedEvent>(OnReelStopped);
            EventBus.Unsubscribe<SlotSpinCompletedEvent>(OnSpinCompleted);
            EventBus.Unsubscribe<TurnCommittedEvent>(OnTurnCommitted);
        }

        private void Update()
        {
            // Cylinder rotation
            if (cylinderTransform != null)
            {
                if (_isSpinning)
                {
                    _currentSpinSpeed = Mathf.Lerp(_currentSpinSpeed, spinRpm, Time.deltaTime * 6f);
                }
                else
                {
                    _currentSpinSpeed = Mathf.Lerp(_currentSpinSpeed, 0f, Time.deltaTime * smoothStopDamping);
                }

                if (_currentSpinSpeed > 0.1f)
                {
                    cylinderTransform.Rotate(cylinderRotationAxis, _currentSpinSpeed * Time.deltaTime, Space.Self);
                }
            }

            // Recoil recovery
            if (weaponRoot != null)
            {
                weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, _originalWeaponPos, Time.deltaTime * recoilRecoverySpeed);
                weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, _originalWeaponRot, Time.deltaTime * recoilRecoverySpeed);
            }
        }

        /// <summary>
        /// Triggers a brief physical recoil kick when the weapon fires (supports both 2D and 3D).
        /// </summary>
        public void TriggerRecoilKick()
        {
            if (weaponRoot == null) return;

            if (is2DMode)
            {
                // In 2D: kick downward-right on the screen plane and rotate around Z axis
                weaponRoot.localPosition = _originalWeaponPos + new Vector3(Mathf.Abs(recoilKickDistance) * 0.35f, -Mathf.Abs(recoilKickDistance), 0f);
                weaponRoot.localRotation = _originalWeaponRot * Quaternion.Euler(0f, 0f, recoilKickAngle);
            }
            else
            {
                // In 3D: kick backward along Z and pitch up
                weaponRoot.localPosition = _originalWeaponPos + Vector3.back * Mathf.Abs(recoilKickDistance);
                weaponRoot.localRotation = _originalWeaponRot * Quaternion.Euler(recoilKickAngle, 0f, 0f);
            }
        }

        private void OnSpinStarted(SlotSpinStartedEvent evt)
        {
            _isSpinning = true;

            // Dim chamber lights during spin
            if (chamberLights != null)
            {
                for (int i = 0; i < chamberLights.Length; i++)
                {
                    if (chamberLights[i] != null) chamberLights[i].intensity = 0.3f;
                }
            }
        }

        private void OnReelStopped(SlotReelStoppedEvent evt)
        {
            Color neonColor = SlotSymbolMetadata.GetColor(evt.Symbol);

            // Light up chamber light corresponding to reel
            if (chamberLights != null && evt.ReelIndex >= 0 && evt.ReelIndex < chamberLights.Length)
            {
                if (chamberLights[evt.ReelIndex] != null)
                {
                    chamberLights[evt.ReelIndex].color = neonColor;
                    chamberLights[evt.ReelIndex].intensity = 1.8f;
                }
            }

            // Apply color to 3D renderer material if assigned
            if (reelRenderers != null && evt.ReelIndex >= 0 && evt.ReelIndex < reelRenderers.Length)
            {
                if (reelRenderers[evt.ReelIndex] != null && reelRenderers[evt.ReelIndex].material != null)
                {
                    reelRenderers[evt.ReelIndex].material.color = neonColor;
                }
            }

            // Apply color to 2D SpriteRenderer if assigned
            if (reelSpriteRenderers != null && evt.ReelIndex >= 0 && evt.ReelIndex < reelSpriteRenderers.Length)
            {
                if (reelSpriteRenderers[evt.ReelIndex] != null)
                {
                    reelSpriteRenderers[evt.ReelIndex].color = neonColor;
                }
            }
        }

        private void OnSpinCompleted(SlotSpinCompletedEvent evt)
        {
            _isSpinning = false;
        }

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            // If attack symbol committed, trigger recoil kick
            if (evt.ExecutedSymbols != null && evt.ExecutedSymbols.Contains(SlotSymbol.ATTACK))
            {
                TriggerRecoilKick();
            }
        }
    }
}
