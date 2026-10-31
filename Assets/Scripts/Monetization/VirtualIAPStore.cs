using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.Core;

namespace LuckyDuelist.Monetization
{
    /// <summary>
    /// Virtual In-App Purchase (IAP) Store for First-Person Premium Cosmetics.
    /// Implements GDD Section 4.2, TSD Section 2.5, and ERD COSMETIC_ITEMS / IAP_TRANSACTIONS:
    /// 1. Catalog of First-Person cosmetic items: Revolver Skins, Cylinder Spinner Skins, Knife Skins.
    /// 2. Experiment participant quota wallet management (Rp 20.000,00 initial balance).
    /// 3. Purchase validation, persistence, and equipping logic.
    /// 4. Dynamic feature flagging: Locked/Hidden in Group A; Open in Group B and Group C.
    /// 5. Fullscreen interactive UI overlay with category filters, dynamic cards, and feedback.
    /// </summary>
    public class VirtualIAPStore : MonoBehaviour
    {
        public static VirtualIAPStore Instance { get; private set; }

        public const string PREF_KEY_OWNED_ITEMS = "LuckyDuelist_OwnedCosmetics";
        public const string PREF_KEY_EQUIPPED_REVOLVER = "LuckyDuelist_EquippedRevolver";
        public const string PREF_KEY_EQUIPPED_SPINNER = "LuckyDuelist_EquippedSpinner";
        public const string PREF_KEY_EQUIPPED_KNIFE = "LuckyDuelist_EquippedKnife";

        public const string DEFAULT_REVOLVER_ID = "SKIN_REVOLVER_DEFAULT";
        public const string DEFAULT_SPINNER_ID = "SKIN_SPINNER_DEFAULT";
        public const string DEFAULT_KNIFE_ID = "SKIN_KNIFE_DEFAULT";

        [Header("Catalog Items")]
        private readonly List<CosmeticCatalogItem> _catalog = new List<CosmeticCatalogItem>();
        private readonly HashSet<string> _ownedItemIds = new HashSet<string>();

        // Currently Equipped Skin IDs
        private string _equippedRevolverId = DEFAULT_REVOLVER_ID;
        private string _equippedSpinnerId = DEFAULT_SPINNER_ID;
        private string _equippedKnifeId = DEFAULT_KNIFE_ID;

        // Session Tracking
        private float _totalIAPRevenue = 0f;
        private int _totalTransactions = 0;

        // Store UI State
        private bool _isStoreOpen = false;
        private CosmeticItemType? _currentCategoryFilter = null; // null = All

        // UI References (Self-constructed)
        private Canvas _storeCanvas;
        private GameObject _storeOverlayRoot;
        private Text _walletBalanceText;
        private Text _statusFeedbackText;
        private Transform _itemsContainer;
        private readonly List<GameObject> _spawnedItemCards = new List<GameObject>();

        #region Public Properties
        public bool IsStoreOpen => _isStoreOpen;
        public float TotalIAPRevenue => _totalIAPRevenue;
        public int TotalTransactions => _totalTransactions;
        public string EquippedRevolverId => _equippedRevolverId;
        public string EquippedSpinnerId => _equippedSpinnerId;
        public string EquippedKnifeId => _equippedKnifeId;
        public IReadOnlyList<CosmeticCatalogItem> Catalog => _catalog;
        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCatalog();
            LoadOwnedAndEquippedItems();
            EnsureUIConstructed();
        }

        private void Start()
        {
            EnsureUIConstructed();
            HideStoreUI();

            EventBus.Subscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        #region Catalog Initialization

        private void InitializeCatalog()
        {
            _catalog.Clear();

            // 1. REVOLVER SKINS (GDD 4.2)
            _catalog.Add(new CosmeticCatalogItem(
                DEFAULT_REVOLVER_ID,
                CosmeticItemType.WeaponRevolver,
                "Standard Chrome Revolver",
                priceIdr: 0.0f,
                assetPrefabKey: "Weapon_Revolver_Default",
                rarity: CosmeticRarity.Common,
                description: "Pistol silinder standar polisi cyberpunk dengan laras baja krom.",
                accentColor: new Color(0.75f, 0.75f, 0.75f),
                isDefaultOwned: true
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_REVOLVER_DAMASCUS",
                CosmeticItemType.WeaponRevolver,
                "Cyber Damascus Revolver",
                priceIdr: 10000.0f, // Rp 10.000
                assetPrefabKey: "Weapon_Revolver_Damascus",
                rarity: CosmeticRarity.Rare,
                description: "Ukiran sirkuit bercahaya neon, reticle plasma ungu cerah, dan audio tembakan railgun bertenaga tinggi.",
                accentColor: new Color(0.6f, 0.2f, 1.0f), // Neon Purple
                isDefaultOwned: false
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_REVOLVER_GOLD",
                CosmeticItemType.WeaponRevolver,
                "High-Roller Gold Revolver",
                priceIdr: 15000.0f, // Rp 15.000
                assetPrefabKey: "Weapon_Revolver_Gold",
                rarity: CosmeticRarity.Epic,
                description: "Bingkai emas murni berkilau, kilatan cahaya saat lock-on, dan efek audio denting koin emas meletus.",
                accentColor: new Color(1.0f, 0.85f, 0.1f), // Pure Gold
                isDefaultOwned: false
            ));

            // 2. SPINNER (SLOT CYLINDER) SKINS (GDD 4.2)
            _catalog.Add(new CosmeticCatalogItem(
                DEFAULT_SPINNER_ID,
                CosmeticItemType.CylinderSpinner,
                "Standard Revolver Cylinder",
                priceIdr: 0.0f,
                assetPrefabKey: "Spinner_Cylinder_Default",
                rarity: CosmeticRarity.Common,
                description: "Silinder revolver 3-reel standar mekanis heksagonal.",
                accentColor: new Color(0.5f, 0.55f, 0.6f),
                isDefaultOwned: true
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_SPINNER_CASINO",
                CosmeticItemType.CylinderSpinner,
                "Neon Casino Reels",
                priceIdr: 5000.0f, // Rp 5.000
                assetPrefabKey: "Spinner_Cylinder_Casino",
                rarity: CosmeticRarity.Common,
                description: "Desain gulungan slot holografis pada silinder senjata dengan efek lampu jackpot menyala.",
                accentColor: new Color(0.0f, 1.0f, 0.8f), // Neon Cyan
                isDefaultOwned: false
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_SPINNER_RETRO",
                CosmeticItemType.CylinderSpinner,
                "Retro Arcade 1984 Spinner",
                priceIdr: 5000.0f, // Rp 5.000
                assetPrefabKey: "Spinner_Cylinder_Retro",
                rarity: CosmeticRarity.Common,
                description: "Silinder bertema mesin arkade 80-an klasik dengan font piksel menyala dan aksen CRT.",
                accentColor: new Color(1.0f, 0.3f, 0.6f), // Hot Pink
                isDefaultOwned: false
            ));

            // 3. KNIFE PARRY SKINS (GDD 4.2)
            _catalog.Add(new CosmeticCatalogItem(
                DEFAULT_KNIFE_ID,
                CosmeticItemType.WeaponKnife,
                "Standard Combat Knife",
                priceIdr: 0.0f,
                assetPrefabKey: "Weapon_Knife_Default",
                rarity: CosmeticRarity.Common,
                description: "Pisau taktis belati militer tangan kiri untuk menangkis tembakan.",
                accentColor: new Color(0.7f, 0.7f, 0.7f),
                isDefaultOwned: true
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_KNIFE_TANTO",
                CosmeticItemType.WeaponKnife,
                "Cyber Tantō Knife",
                priceIdr: 10000.0f, // Rp 10.000
                assetPrefabKey: "Weapon_Knife_Tanto",
                rarity: CosmeticRarity.Rare,
                description: "Pisau belati bergaya samurai modern; percikan tangkisan 'CLANG!' berwarna cyan neon bercahaya.",
                accentColor: new Color(0.0f, 1.0f, 0.9f), // Bright Cyan
                isDefaultOwned: false
            ));

            _catalog.Add(new CosmeticCatalogItem(
                "SKIN_KNIFE_VIPER",
                CosmeticItemType.WeaponKnife,
                "Viper Serrated Dagger",
                priceIdr: 15000.0f, // Rp 15.000
                assetPrefabKey: "Weapon_Knife_Viper",
                rarity: CosmeticRarity.Epic,
                description: "Pisau bergerigi hitam legam; memberikan kilatan hijau beracun saat memantulkan peluru kuning.",
                accentColor: new Color(0.2f, 1.0f, 0.2f), // Toxic Green
                isDefaultOwned: false
            ));
        }

        private void LoadOwnedAndEquippedItems()
        {
            _ownedItemIds.Clear();

            // Always own defaults
            _ownedItemIds.Add(DEFAULT_REVOLVER_ID);
            _ownedItemIds.Add(DEFAULT_SPINNER_ID);
            _ownedItemIds.Add(DEFAULT_KNIFE_ID);

            // Load saved owned items
            if (PlayerPrefs.HasKey(PREF_KEY_OWNED_ITEMS))
            {
                string raw = PlayerPrefs.GetString(PREF_KEY_OWNED_ITEMS);
                string[] parts = raw.Split(',');
                foreach (string item in parts)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        _ownedItemIds.Add(item.Trim());
                    }
                }
            }

            // Load equipped items
            _equippedRevolverId = PlayerPrefs.GetString(PREF_KEY_EQUIPPED_REVOLVER, DEFAULT_REVOLVER_ID);
            _equippedSpinnerId = PlayerPrefs.GetString(PREF_KEY_EQUIPPED_SPINNER, DEFAULT_SPINNER_ID);
            _equippedKnifeId = PlayerPrefs.GetString(PREF_KEY_EQUIPPED_KNIFE, DEFAULT_KNIFE_ID);

            // Fallback validation
            if (!_ownedItemIds.Contains(_equippedRevolverId)) _equippedRevolverId = DEFAULT_REVOLVER_ID;
            if (!_ownedItemIds.Contains(_equippedSpinnerId)) _equippedSpinnerId = DEFAULT_SPINNER_ID;
            if (!_ownedItemIds.Contains(_equippedKnifeId)) _equippedKnifeId = DEFAULT_KNIFE_ID;
        }

        private void SaveOwnedItems()
        {
            string serialized = string.Join(",", _ownedItemIds);
            PlayerPrefs.SetString(PREF_KEY_OWNED_ITEMS, serialized);
            PlayerPrefs.Save();
        }

        #endregion

        #region Public Store Operations & Feature Gating

        /// <summary>
        /// Attempts to open the Virtual IAP Store.
        /// Gated by ExperimentManager: Returns false if IAP Store is disabled (Group A).
        /// </summary>
        public bool OpenStore()
        {
            if (ExperimentManager.Instance != null && !ExperimentManager.Instance.IsIAPStoreEnabled)
            {
                Debug.LogWarning("[VirtualIAPStore] Cannot open store: IAP Store is disabled for the current experiment group (Group A).");
                return false;
            }

            EnsureUIConstructed();
            _isStoreOpen = true;

            float wallet = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f;
            EventBus.Publish(new IAPStoreStateChangedEvent(true, wallet));

            ShowStoreUI();
            RefreshStoreUI();

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Store)
            {
                GameManager.Instance.ChangeState(GameState.Store);
            }

            return true;
        }

        /// <summary>
        /// Closes the store UI and returns to Lobby state.
        /// </summary>
        public void CloseStore()
        {
            _isStoreOpen = false;
            HideStoreUI();

            float wallet = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f;
            EventBus.Publish(new IAPStoreStateChangedEvent(false, wallet));

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Store)
            {
                GameManager.Instance.ChangeState(GameState.Lobby);
            }
        }

        /// <summary>
        /// Attempts to purchase a cosmetic item using the participant's quota balance.
        /// </summary>
        public bool TryPurchaseItem(string itemId, out string failureReason)
        {
            failureReason = string.Empty;

            // 1. Feature flag check
            if (ExperimentManager.Instance != null && !ExperimentManager.Instance.IsIAPStoreEnabled)
            {
                failureReason = "Fitur Toko IAP tidak aktif untuk grup eksperimen ini.";
                return false;
            }

            // 2. Catalog check
            CosmeticCatalogItem item = GetCatalogItem(itemId);
            if (item == null)
            {
                failureReason = "Item tidak ditemukan dalam katalog.";
                return false;
            }

            // 3. Ownership check
            if (IsItemOwned(itemId))
            {
                failureReason = "Item ini sudah Anda miliki.";
                return false;
            }

            // 4. Quota Wallet balance check & deduction via ExperimentManager
            float currentWallet = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f;
            if (currentWallet < item.PriceIdr)
            {
                failureReason = $"Saldo kuota tidak mencukupi (Harga: Rp {item.PriceIdr:N0}, Saldo: Rp {currentWallet:N0}).";
                return false;
            }

            bool deducted = ExperimentManager.Instance != null && ExperimentManager.Instance.TryDeductWalletBalance(item.PriceIdr);
            if (!deducted)
            {
                failureReason = "Gagal memotong saldo kuota.";
                return false;
            }

            // 5. Grant Ownership
            _ownedItemIds.Add(itemId);
            SaveOwnedItems();

            _totalIAPRevenue += item.PriceIdr;
            _totalTransactions++;

            // 6. Auto-equip newly purchased item
            EquipItem(itemId, out _);

            // 7. Dispatch Transaction Event (ERD IAP_TRANSACTIONS)
            string transactionId = "TX-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string userId = ExperimentManager.Instance != null ? ExperimentManager.Instance.UserId : "UNKNOWN_TESTER";
            float remaining = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f;

            EventBus.Publish(new IAPTransactionCompletedEvent(
                transactionId,
                userId,
                item.ItemId,
                item.ItemName,
                item.ItemType,
                item.PriceIdr,
                remaining,
                DateTime.UtcNow
            ));

            Debug.Log($"<color=cyan>[VirtualIAPStore] Purchased {item.ItemName} for Rp {item.PriceIdr:N0}! Remaining wallet: Rp {remaining:N0}</color>");

            ShowFeedback($"Berhasil membeli <b>{item.ItemName}</b> seharga Rp {item.PriceIdr:N0}!");
            RefreshStoreUI();
            return true;
        }

        /// <summary>
        /// Equips an owned cosmetic item.
        /// </summary>
        public bool EquipItem(string itemId, out string failureReason)
        {
            failureReason = string.Empty;

            CosmeticCatalogItem item = GetCatalogItem(itemId);
            if (item == null)
            {
                failureReason = "Item tidak terdaftar.";
                return false;
            }

            if (!IsItemOwned(itemId))
            {
                failureReason = "Item harus dibeli terlebih dahulu sebelum digunakan.";
                return false;
            }

            switch (item.ItemType)
            {
                case CosmeticItemType.WeaponRevolver:
                    _equippedRevolverId = itemId;
                    PlayerPrefs.SetString(PREF_KEY_EQUIPPED_REVOLVER, itemId);
                    break;

                case CosmeticItemType.CylinderSpinner:
                    _equippedSpinnerId = itemId;
                    PlayerPrefs.SetString(PREF_KEY_EQUIPPED_SPINNER, itemId);
                    break;

                case CosmeticItemType.WeaponKnife:
                    _equippedKnifeId = itemId;
                    PlayerPrefs.SetString(PREF_KEY_EQUIPPED_KNIFE, itemId);
                    break;
            }

            PlayerPrefs.Save();

            EventBus.Publish(new CosmeticEquippedEvent(item.ItemType, item.ItemId, item.ItemName));
            Debug.Log($"[VirtualIAPStore] Equipped {item.ItemName} ({item.ItemType}).");

            ShowFeedback($"Menggunakan <b>{item.ItemName}</b>.");
            RefreshStoreUI();
            return true;
        }

        public bool IsItemOwned(string itemId)
        {
            return _ownedItemIds.Contains(itemId);
        }

        public bool IsItemEquipped(string itemId)
        {
            return itemId == _equippedRevolverId || itemId == _equippedSpinnerId || itemId == _equippedKnifeId;
        }

        public string GetEquippedSkin(CosmeticItemType type)
        {
            switch (type)
            {
                case CosmeticItemType.WeaponRevolver: return _equippedRevolverId;
                case CosmeticItemType.CylinderSpinner: return _equippedSpinnerId;
                case CosmeticItemType.WeaponKnife: return _equippedKnifeId;
                default: return string.Empty;
            }
        }

        public CosmeticCatalogItem GetCatalogItem(string itemId)
        {
            for (int i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].ItemId == itemId) return _catalog[i];
            }
            return null;
        }

        /// <summary>
        /// Resets all purchases and equips default skins (used in test suites).
        /// </summary>
        public void ResetStoreDataForTesting()
        {
            PlayerPrefs.DeleteKey(PREF_KEY_OWNED_ITEMS);
            PlayerPrefs.DeleteKey(PREF_KEY_EQUIPPED_REVOLVER);
            PlayerPrefs.DeleteKey(PREF_KEY_EQUIPPED_SPINNER);
            PlayerPrefs.DeleteKey(PREF_KEY_EQUIPPED_KNIFE);
            PlayerPrefs.Save();

            _totalIAPRevenue = 0f;
            _totalTransactions = 0;
            LoadOwnedAndEquippedItems();
        }

        #endregion

        #region Event Handlers

        private void OnExperimentGroupAssigned(ExperimentGroupAssignedEvent evt)
        {
            // If in Group A (where store is disabled) and store was open, close it immediately!
            if (evt.Group == ExperimentGroup.GroupA_DominantAds && _isStoreOpen)
            {
                CloseStore();
            }
            else
            {
                RefreshStoreUI();
            }
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == GameState.Store && !_isStoreOpen)
            {
                OpenStore();
            }
            else if (evt.PreviousState == GameState.Store && _isStoreOpen && evt.NewState != GameState.Store)
            {
                _isStoreOpen = false;
                HideStoreUI();
            }
        }

        #endregion

        #region Store UI Construction & Management

        public void EnsureUIConstructed()
        {
            if (_storeOverlayRoot != null) return;

            var canvasGo = new GameObject("[VirtualIAPStore_Canvas]");
            canvasGo.transform.SetParent(transform, false);

            _storeCanvas = canvasGo.AddComponent<Canvas>();
            _storeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _storeCanvas.sortingOrder = 400;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            // 1. Fullscreen Background Panel
            _storeOverlayRoot = new GameObject("Store_Overlay_Panel");
            var rootRect = _storeOverlayRoot.AddComponent<RectTransform>();
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootBg = _storeOverlayRoot.AddComponent<Image>();
            rootBg.color = new Color(0.06f, 0.07f, 0.10f, 0.98f); // Cyber Dark Gray

            // 2. Top Header Bar
            var topBar = new GameObject("Top_Header_Bar");
            var topBarRect = topBar.AddComponent<RectTransform>();
            topBarRect.SetParent(rootRect, false);
            topBarRect.anchorMin = new Vector2(0f, 0.88f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;

            var topBarBg = topBar.AddComponent<Image>();
            topBarBg.color = new Color(0.10f, 0.13f, 0.20f, 0.95f);

            // Title
            var titleGo = new GameObject("Title_Text");
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.SetParent(topBarRect, false);
            titleRect.anchorMin = new Vector2(0.04f, 0f);
            titleRect.anchorMax = new Vector2(0.55f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var titleText = titleGo.AddComponent<Text>();
            titleText.font = defaultFont;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = new Color(0.0f, 1.0f, 0.85f);
            titleText.text = "<b>? CYBER MATRIX - VIRTUAL IAP COSMETICS</b>";

            // Wallet Quota Balance Badge
            var walletGo = new GameObject("Wallet_Badge");
            var walletRect = walletGo.AddComponent<RectTransform>();
            walletRect.SetParent(topBarRect, false);
            walletRect.anchorMin = new Vector2(0.55f, 0.15f);
            walletRect.anchorMax = new Vector2(0.85f, 0.85f);
            walletRect.offsetMin = Vector2.zero;
            walletRect.offsetMax = Vector2.zero;

            var walletBg = walletGo.AddComponent<Image>();
            walletBg.color = new Color(0.05f, 0.25f, 0.20f, 0.9f);

            var walletTextGo = new GameObject("Text");
            var walletTextRect = walletTextGo.AddComponent<RectTransform>();
            walletTextRect.SetParent(walletRect, false);
            walletTextRect.anchorMin = Vector2.zero;
            walletTextRect.anchorMax = Vector2.one;
            walletTextRect.offsetMin = Vector2.zero;
            walletTextRect.offsetMax = Vector2.zero;

            _walletBalanceText = walletTextGo.AddComponent<Text>();
            _walletBalanceText.font = defaultFont;
            _walletBalanceText.fontSize = 18;
            _walletBalanceText.fontStyle = FontStyle.Bold;
            _walletBalanceText.alignment = TextAnchor.MiddleCenter;
            _walletBalanceText.color = new Color(0.1f, 1.0f, 0.6f);
            _walletBalanceText.text = "SALDO KUOTA: Rp 20.000";

            // Close Button in Top Bar
            var closeBtnGo = new GameObject("Close_Button");
            var closeBtnRect = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRect.SetParent(topBarRect, false);
            closeBtnRect.anchorMin = new Vector2(0.88f, 0.15f);
            closeBtnRect.anchorMax = new Vector2(0.97f, 0.85f);
            closeBtnRect.offsetMin = Vector2.zero;
            closeBtnRect.offsetMax = Vector2.zero;

            var closeBtnBg = closeBtnGo.AddComponent<Image>();
            closeBtnBg.color = new Color(0.7f, 0.15f, 0.2f, 1f);

            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseStore);

            var closeTxt = new GameObject("Text").AddComponent<Text>();
            closeTxt.transform.SetParent(closeBtnRect, false);
            closeTxt.rectTransform.anchorMin = Vector2.zero;
            closeTxt.rectTransform.anchorMax = Vector2.one;
            closeTxt.rectTransform.offsetMin = Vector2.zero;
            closeTxt.rectTransform.offsetMax = Vector2.zero;
            closeTxt.font = defaultFont;
            closeTxt.fontSize = 16;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.text = "[X] TUTUP";

            // 3. Category Filter Tabs Bar
            var filterBar = new GameObject("Filter_Tabs_Bar");
            var filterBarRect = filterBar.AddComponent<RectTransform>();
            filterBarRect.SetParent(rootRect, false);
            filterBarRect.anchorMin = new Vector2(0.04f, 0.81f);
            filterBarRect.anchorMax = new Vector2(0.96f, 0.87f);
            filterBarRect.offsetMin = Vector2.zero;
            filterBarRect.offsetMax = Vector2.zero;

            CreateFilterTab(filterBarRect, "SEMUA (ALL)", null, 0);
            CreateFilterTab(filterBarRect, "SKIN REVOLVER", CosmeticItemType.WeaponRevolver, 1);
            CreateFilterTab(filterBarRect, "SKIN SPINNER", CosmeticItemType.CylinderSpinner, 2);
            CreateFilterTab(filterBarRect, "SKIN PISAU", CosmeticItemType.WeaponKnife, 3);

            // 4. Status Toast Feedback
            var feedbackGo = new GameObject("Feedback_Text");
            var feedbackRect = feedbackGo.AddComponent<RectTransform>();
            feedbackRect.SetParent(rootRect, false);
            feedbackRect.anchorMin = new Vector2(0.05f, 0.76f);
            feedbackRect.anchorMax = new Vector2(0.95f, 0.80f);
            feedbackRect.offsetMin = Vector2.zero;
            feedbackRect.offsetMax = Vector2.zero;

            _statusFeedbackText = feedbackGo.AddComponent<Text>();
            _statusFeedbackText.font = defaultFont;
            _statusFeedbackText.fontSize = 15;
            _statusFeedbackText.fontStyle = FontStyle.Italic;
            _statusFeedbackText.alignment = TextAnchor.MiddleCenter;
            _statusFeedbackText.color = new Color(0.9f, 0.9f, 0.4f);
            _statusFeedbackText.text = "Pilih kosmetik untuk melengkapi senjata sudut pandang orang pertama Anda.";

            // 5. Items Scroll / Container View
            var scrollGo = new GameObject("Items_Scroll_Container");
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.SetParent(rootRect, false);
            scrollRect.anchorMin = new Vector2(0.04f, 0.04f);
            scrollRect.anchorMax = new Vector2(0.96f, 0.74f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0.04f, 0.05f, 0.07f, 0.7f);

            var gridGo = new GameObject("Grid_Content");
            var gridRect = gridGo.AddComponent<RectTransform>();
            gridRect.SetParent(scrollRect, false);
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = new Vector2(10, 10);
            gridRect.offsetMax = new Vector2(-10, -10);

            var gridLayout = gridGo.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(580, 210);
            gridLayout.spacing = new Vector2(15, 15);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            _itemsContainer = gridRect;
        }

        private void CreateFilterTab(RectTransform parent, string label, CosmeticItemType? category, int index)
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            var tabGo = new GameObject($"Tab_{label}");
            var tabRect = tabGo.AddComponent<RectTransform>();
            tabRect.SetParent(parent, false);
            tabRect.anchorMin = new Vector2(index * 0.25f, 0f);
            tabRect.anchorMax = new Vector2((index + 1) * 0.25f - 0.01f, 1f);
            tabRect.offsetMin = Vector2.zero;
            tabRect.offsetMax = Vector2.zero;

            var tabBg = tabGo.AddComponent<Image>();
            tabBg.color = new Color(0.15f, 0.20f, 0.30f, 0.9f);

            var btn = tabGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                _currentCategoryFilter = category;
                RefreshStoreUI();
            });

            var txt = new GameObject("Text").AddComponent<Text>();
            txt.transform.SetParent(tabRect, false);
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
            txt.font = defaultFont;
            txt.fontSize = 15;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
        }

        public void RefreshStoreUI()
        {
            if (_storeOverlayRoot == null) return;

            // 1. Update Wallet Balance
            float walletBalance = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f;
            if (_walletBalanceText != null)
            {
                _walletBalanceText.text = $"SALDO KUOTA: <b>Rp {walletBalance:N0}</b>";
            }

            // 2. Clear old item cards
            for (int i = 0; i < _spawnedItemCards.Count; i++)
            {
                if (_spawnedItemCards[i] != null) Destroy(_spawnedItemCards[i]);
            }
            _spawnedItemCards.Clear();

            // 3. Build filtered item cards
            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            for (int i = 0; i < _catalog.Count; i++)
            {
                CosmeticCatalogItem item = _catalog[i];

                if (_currentCategoryFilter.HasValue && item.ItemType != _currentCategoryFilter.Value)
                {
                    continue;
                }

                GameObject cardGo = CreateItemCard(item, defaultFont, walletBalance);
                _spawnedItemCards.Add(cardGo);
            }
        }

        private GameObject CreateItemCard(CosmeticCatalogItem item, Font font, float walletBalance)
        {
            var cardGo = new GameObject($"Card_{item.ItemId}");
            cardGo.transform.SetParent(_itemsContainer, false);

            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(580, 210);

            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = new Color(0.10f, 0.12f, 0.17f, 0.95f);

            var outline = cardGo.AddComponent<Outline>();
            bool isEquipped = IsItemEquipped(item.ItemId);
            bool isOwned = IsItemOwned(item.ItemId);

            outline.effectColor = isEquipped ? new Color(0.1f, 1.0f, 0.5f, 0.9f) : item.GetRarityColor() * 0.7f;
            outline.effectDistance = new Vector2(2, -2);

            // Left Color Accent Bar
            var accentGo = new GameObject("Accent_Bar");
            var accentRect = accentGo.AddComponent<RectTransform>();
            accentRect.SetParent(cardRect, false);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0.02f, 1f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = Vector2.zero;

            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = item.AccentColor;

            // Content Area
            var contentGo = new GameObject("Content");
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.SetParent(cardRect, false);
            contentRect.anchorMin = new Vector2(0.04f, 0.05f);
            contentRect.anchorMax = new Vector2(0.96f, 0.95f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Header line: Item Name & Rarity badge
            var headerGo = new GameObject("Header_Text");
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.SetParent(contentRect, false);
            headerRect.anchorMin = new Vector2(0f, 0.72f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            var headerText = headerGo.AddComponent<Text>();
            headerText.font = font;
            headerText.fontSize = 17;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.color = item.GetRarityColor();
            headerText.text = $"{item.ItemName}  <color=#888888>[{item.GetRarityLabel()} - {item.GetCategoryLabel()}]</color>";

            // Description
            var descGo = new GameObject("Desc_Text");
            var descRect = descGo.AddComponent<RectTransform>();
            descRect.SetParent(contentRect, false);
            descRect.anchorMin = new Vector2(0f, 0.28f);
            descRect.anchorMax = new Vector2(0.68f, 0.70f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;

            var descText = descGo.AddComponent<Text>();
            descText.font = font;
            descText.fontSize = 13;
            descText.alignment = TextAnchor.UpperLeft;
            descText.color = new Color(0.85f, 0.85f, 0.90f);
            descText.text = item.Description;

            // Price Badge
            var priceGo = new GameObject("Price_Text");
            var priceRect = priceGo.AddComponent<RectTransform>();
            priceRect.SetParent(contentRect, false);
            priceRect.anchorMin = new Vector2(0f, 0f);
            priceRect.anchorMax = new Vector2(0.40f, 0.25f);
            priceRect.offsetMin = Vector2.zero;
            priceRect.offsetMax = Vector2.zero;

            var priceText = priceGo.AddComponent<Text>();
            priceText.font = font;
            priceText.fontSize = 16;
            priceText.fontStyle = FontStyle.Bold;
            priceText.alignment = TextAnchor.MiddleLeft;
            priceText.color = item.PriceIdr > 0 ? new Color(1.0f, 0.85f, 0.2f) : Color.gray;
            priceText.text = item.PriceIdr > 0 ? $"Harga: Rp {item.PriceIdr:N0}" : "GRATIS / BAWAAN";

            // Action Button (Equip / Buy)
            var btnGo = new GameObject("Action_Button");
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.SetParent(contentRect, false);
            btnRect.anchorMin = new Vector2(0.70f, 0.05f);
            btnRect.anchorMax = new Vector2(1f, 0.45f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnBg = btnGo.AddComponent<Image>();
            var btn = btnGo.AddComponent<Button>();

            var btnTxt = new GameObject("Text").AddComponent<Text>();
            btnTxt.transform.SetParent(btnRect, false);
            btnTxt.rectTransform.anchorMin = Vector2.zero;
            btnTxt.rectTransform.anchorMax = Vector2.one;
            btnTxt.rectTransform.offsetMin = Vector2.zero;
            btnTxt.rectTransform.offsetMax = Vector2.zero;
            btnTxt.font = font;
            btnTxt.fontSize = 14;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;

            if (isEquipped)
            {
                btnBg.color = new Color(0.1f, 0.6f, 0.3f, 0.9f);
                btnTxt.color = Color.white;
                btnTxt.text = "[ ? DIPAKAI ]";
                btn.interactable = false;
            }
            else if (isOwned)
            {
                btnBg.color = new Color(0.15f, 0.45f, 0.75f, 1f);
                btnTxt.color = Color.white;
                btnTxt.text = "[ GUNAKAN ]";
                btn.interactable = true;
                string itId = item.ItemId;
                btn.onClick.AddListener(() => EquipItem(itId, out _));
            }
            else
            {
                // Unowned -> Can Buy?
                bool canAfford = walletBalance >= item.PriceIdr;
                if (canAfford)
                {
                    btnBg.color = new Color(0.85f, 0.55f, 0.1f, 1f); // Cyber Amber
                    btnTxt.color = Color.black;
                    btnTxt.text = $"[ BELI Rp {item.PriceIdr:N0} ]";
                    btn.interactable = true;
                    string itId = item.ItemId;
                    btn.onClick.AddListener(() => TryPurchaseItem(itId, out _));
                }
                else
                {
                    btnBg.color = new Color(0.3f, 0.3f, 0.35f, 0.6f);
                    btnTxt.color = new Color(0.7f, 0.7f, 0.7f);
                    btnTxt.text = "[ SALDO KURANG ]";
                    btn.interactable = false;
                }
            }

            return cardGo;
        }

        public void ShowFeedback(string message)
        {
            if (_statusFeedbackText != null)
            {
                _statusFeedbackText.text = message;
            }
        }

        private void ShowStoreUI()
        {
            if (_storeOverlayRoot != null)
            {
                _storeOverlayRoot.SetActive(true);
            }
        }

        private void HideStoreUI()
        {
            if (_storeOverlayRoot != null)
            {
                _storeOverlayRoot.SetActive(false);
            }
        }

        #endregion
    }
}
