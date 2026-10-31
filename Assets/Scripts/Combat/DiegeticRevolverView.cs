using System.Collections;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Diegetic 3D/2.5D visual controller for the Tactical Revolver held in the player's right hand.
    /// Visualizes cylinder roll, reel stops, chamber neon emissive colors, and recoil shake.
    /// </summary>
    public class DiegeticRevolverView : MonoBehaviour
    {
        [Header("Transform Hooks")]
        [Tooltip("The rotating cylinder mesh/barrel containing the 3 reels")]
        [SerializeField] private Transform cylinderTransform;
        [Tooltip("Right hand weapon root for recoil kicks")]
        [SerializeField] private Transform weaponRoot;

        [Header("Spin Motion")]
        [SerializeField] private Vector3 cylinderRotationAxis = Vector3.forward;
        [SerializeField] private float spinRpm = 1200f;
        [SerializeField] private float smoothStopDamping = 8f;

        [Header("Recoil Animation")]
        [SerializeField] private float recoilKickAngle = -6f;
        [SerializeField] private float recoilKickDistance = -0.05f;
        [SerializeField] private float recoilRecoverySpeed = 12f;

        [Header("Chamber Lights (Optional)")]
        [SerializeField] private Light[] chamberLights;
        [SerializeField] private Renderer[] reelRenderers;

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
        /// Triggers a brief physical recoil kick when the weapon fires.
        /// </summary>
        public void TriggerRecoilKick()
        {
            if (weaponRoot == null) return;

            weaponRoot.localPosition = _originalWeaponPos + Vector3.back * Mathf.Abs(recoilKickDistance);
            weaponRoot.localRotation = _originalWeaponRot * Quaternion.Euler(recoilKickAngle, 0f, 0f);
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

            // Apply color to renderer material if assigned
            if (reelRenderers != null && evt.ReelIndex >= 0 && evt.ReelIndex < reelRenderers.Length)
            {
                if (reelRenderers[evt.ReelIndex] != null && reelRenderers[evt.ReelIndex].material != null)
                {
                    reelRenderers[evt.ReelIndex].material.color = neonColor;
                }
            }
        }

        private void OnSpinCompleted(SlotSpinCompletedEvent evt)
        {
            _isSpinning = false;
        }

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            // If attack symbol committed, slight kick preparation
            if (evt.ExecutedSymbols != null && evt.ExecutedSymbols.Contains(SlotSymbol.ATTACK))
            {
                TriggerRecoilKick();
            }
        }
    }
}
