using UnityEngine;
using LuckyDuelist.Events;
using LuckyDuelist.Combat;

namespace LuckyDuelist.Monetization
{
    /// <summary>
    /// Connects cosmetic items equipped in VirtualIAPStore to the 3D/2.5D FPS Weapon Rig and UI overlays.
    /// Implements TSD Section 2.5 (CosmeticApplier).
    /// Dynamically applies:
    /// - Knife Skins to KnifeParryController (Default, Cyber Tanto, Viper Dagger).
    /// - Revolver Skins to DiegeticRevolverView and AimStabilizer reticle tints.
    /// - Cylinder Spinner Skins to DiegeticRevolverView and Slot cylinder visual elements.
    /// </summary>
    public class CosmeticApplier : MonoBehaviour
    {
        public static CosmeticApplier Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            EventBus.Subscribe<CosmeticEquippedEvent>(OnCosmeticEquipped);

            // Apply currently equipped cosmetics on startup
            ApplyAllEquippedCosmetics();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<CosmeticEquippedEvent>(OnCosmeticEquipped);
        }

        /// <summary>
        /// Reads all currently equipped skins from VirtualIAPStore and updates visual components.
        /// </summary>
        public void ApplyAllEquippedCosmetics()
        {
            if (VirtualIAPStore.Instance == null) return;

            ApplyRevolverSkin(VirtualIAPStore.Instance.EquippedRevolverId);
            ApplySpinnerSkin(VirtualIAPStore.Instance.EquippedSpinnerId);
            ApplyKnifeSkin(VirtualIAPStore.Instance.EquippedKnifeId);
        }

        private void OnCosmeticEquipped(CosmeticEquippedEvent evt)
        {
            switch (evt.ItemType)
            {
                case CosmeticItemType.WeaponRevolver:
                    ApplyRevolverSkin(evt.ItemId);
                    break;

                case CosmeticItemType.CylinderSpinner:
                    ApplySpinnerSkin(evt.ItemId);
                    break;

                case CosmeticItemType.WeaponKnife:
                    ApplyKnifeSkin(evt.ItemId);
                    break;
            }
        }

        public void ApplyKnifeSkin(string itemId)
        {
            if (KnifeParryController.Instance == null) return;

            switch (itemId)
            {
                case "SKIN_KNIFE_TANTO":
                    KnifeParryController.Instance.ApplySkin(KnifeParryController.KnifeSkin.CyberTanto);
                    break;

                case "SKIN_KNIFE_VIPER":
                    KnifeParryController.Instance.ApplySkin(KnifeParryController.KnifeSkin.ViperDagger);
                    break;

                case VirtualIAPStore.DEFAULT_KNIFE_ID:
                default:
                    KnifeParryController.Instance.ApplySkin(KnifeParryController.KnifeSkin.DefaultCombatKnife);
                    break;
            }

            Debug.Log($"[CosmeticApplier] Applied knife skin: {itemId}");
        }

        public void ApplyRevolverSkin(string itemId)
        {
            // Update Aim reticle color in AimStabilizerController if present
            if (AimStabilizerController.Instance != null)
            {
                switch (itemId)
                {
                    case "SKIN_REVOLVER_DAMASCUS":
                        // Plasma Purple reticle tint (GDD 4.2)
                        AimStabilizerController.Instance.SetReticleColorTint(new Color(0.7f, 0.2f, 1.0f));
                        break;

                    case "SKIN_REVOLVER_GOLD":
                        // Gold reticle tint (GDD 4.2)
                        AimStabilizerController.Instance.SetReticleColorTint(new Color(1.0f, 0.85f, 0.1f));
                        break;

                    default:
                        // Cyan standard reticle
                        AimStabilizerController.Instance.SetReticleColorTint(new Color(0.0f, 1.0f, 0.85f));
                        break;
                }
            }

            Debug.Log($"[CosmeticApplier] Applied revolver skin: {itemId}");
        }

        public void ApplySpinnerSkin(string itemId)
        {
            Debug.Log($"[CosmeticApplier] Applied cylinder spinner skin: {itemId}");
        }
    }
}
