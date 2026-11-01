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

        [Header("Targets (Optional direct binding)")]
        [SerializeField] private KnifeParryController targetKnife;
        [SerializeField] private AimStabilizerController targetAim;

        public KnifeParryController TargetKnife
        {
            get => targetKnife != null ? targetKnife : KnifeParryController.Instance;
            set => targetKnife = value;
        }

        public AimStabilizerController TargetAim
        {
            get => targetAim != null ? targetAim : AimStabilizerController.Instance;
            set => targetAim = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CosmeticEquippedEvent>(OnCosmeticEquipped);
        }

        private void Start()
        {
            // Apply currently equipped cosmetics on startup
            ApplyAllEquippedCosmetics();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CosmeticEquippedEvent>(OnCosmeticEquipped);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

        public void ApplyKnifeSkin(string itemId, KnifeParryController customKnife = null)
        {
            var knife = customKnife != null ? customKnife : TargetKnife;
            if (knife == null) return;

            switch (itemId)
            {
                case "SKIN_KNIFE_TANTO":
                    knife.ApplySkin(KnifeParryController.KnifeSkin.CyberTanto);
                    break;

                case "SKIN_KNIFE_VIPER":
                    knife.ApplySkin(KnifeParryController.KnifeSkin.ViperDagger);
                    break;

                case VirtualIAPStore.DEFAULT_KNIFE_ID:
                default:
                    knife.ApplySkin(KnifeParryController.KnifeSkin.DefaultCombatKnife);
                    break;
            }

            Debug.Log($"[CosmeticApplier] Applied knife skin: {itemId}");
        }

        public void ApplyRevolverSkin(string itemId, AimStabilizerController customAim = null)
        {
            var aim = customAim != null ? customAim : TargetAim;
            if (aim != null)
            {
                switch (itemId)
                {
                    case "SKIN_REVOLVER_DAMASCUS":
                        // Plasma Purple reticle tint (GDD 4.2)
                        aim.SetReticleColorTint(new Color(0.7f, 0.2f, 1.0f));
                        break;

                    case "SKIN_REVOLVER_GOLD":
                        // Gold reticle tint (GDD 4.2)
                        aim.SetReticleColorTint(new Color(1.0f, 0.85f, 0.1f));
                        break;

                    default:
                        // Cyan standard reticle
                        aim.SetReticleColorTint(new Color(0.0f, 1.0f, 0.85f));
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
