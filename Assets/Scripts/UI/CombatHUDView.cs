using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.UI
{
    /// <summary>
    /// Manages the First-Person Bottom-Left Minimalist HUD Canvas.
    /// Includes:
    /// 1. Health Bar (Slider + Dynamic TMP_Text "HP: 100/100" with low HP color shifting).
    /// 2. 3-Reel Cylinder Display (Cards showing symbol icon, name, neon color).
    /// 3. Main Action Button ("PUTAR SILINDER" / "EKSEKUSI GILIRAN").
    /// 4. Full 3-Reel Re-spin via Rewarded Ad Button (Gated by ExperimentGroup: Group A & C visible, Group B hidden).
    /// 5. Defense Prompt Badges ([Q: HINDAR] | [E: PARRY PISAU]).
    /// 6. Wild Joker Selection Modal.
    ///
    /// Supports TextMeshPro (TMP_Text) for crisp UI and drag-and-drop from the Inspector.
    /// </summary>
    public class CombatHUDView : MonoBehaviour
    {
        [Header("Canvas & Root Anchor")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private RectTransform bottomLeftHudRoot;

        [Header("Health Bar Elements")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Image healthFillImage;

        [Header("3 Cylinder Reels")]
        [SerializeField] private TMP_Text[] reelSymbolTexts = new TMP_Text[3];
        [SerializeField] private Image[] reelCardBorders = new Image[3];

        [Header("Primary Action Buttons")]
        [SerializeField] private Button spinOrCommitButton;
        [SerializeField] private TMP_Text spinOrCommitText;
        [SerializeField] private Button respinAdButton;
        [SerializeField] private TMP_Text respinAdText;

        [Header("Defense Prompt")]
        [SerializeField] private GameObject defensePromptRoot;
        [SerializeField] private TMP_Text defensePromptText;

        [Header("Wild Joker Modal")]
        [SerializeField] private GameObject wildJokerModal;
        [SerializeField] private Button wildAttackBtn;
        [SerializeField] private Button wildMarkBtn;
        [SerializeField] private Button wildHealBtn;
        [SerializeField] private Button wildCounterBtn;

        private int _pendingWildReelIndex = -1;

        [Header("Procedural Fallback Settings")]
        [Tooltip("If true, automatically creates UI GameObjects at runtime. Set to FALSE if you design your own UI in the Hierarchy.")]
        [SerializeField] private bool useProceduralUIFallback = false;

        private bool _isInitialized = false;

        private void Awake()
        {
            EnsureHUDConstructed();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventBus.Subscribe<SlotSpinStartedEvent>(OnSlotSpinStarted);
            EventBus.Subscribe<SlotReelStoppedEvent>(OnSlotReelStopped);
            EventBus.Subscribe<SlotSpinCompletedEvent>(OnSlotSpinCompleted);
            EventBus.Subscribe<SlotRespinTriggeredEvent>(OnSlotRespinTriggered);
            EventBus.Subscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Subscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Subscribe<WildJokerResolvedEvent>(OnWildJokerResolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventBus.Unsubscribe<SlotSpinStartedEvent>(OnSlotSpinStarted);
            EventBus.Unsubscribe<SlotReelStoppedEvent>(OnSlotReelStopped);
            EventBus.Unsubscribe<SlotSpinCompletedEvent>(OnSlotSpinCompleted);
            EventBus.Unsubscribe<SlotRespinTriggeredEvent>(OnSlotRespinTriggered);
            EventBus.Unsubscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Unsubscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Unsubscribe<WildJokerResolvedEvent>(OnWildJokerResolved);
        }

        private void Start()
        {
            RefreshExperimentGatedUI();
            RefreshHUD();
        }

        #region Event Handlers

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            UpdateHealthUI(evt.CurrentHp, evt.MaxHp);
        }

        private void OnSlotSpinStarted(SlotSpinStartedEvent evt)
        {
            if (spinOrCommitButton != null) spinOrCommitButton.interactable = false;
            if (spinOrCommitText != null) spinOrCommitText.text = "MEMUTAR SILINDER...";
            if (respinAdButton != null) respinAdButton.interactable = false;

            for (int i = 0; i < 3; i++)
            {
                if (reelSymbolTexts != null && i < reelSymbolTexts.Length && reelSymbolTexts[i] != null)
                {
                    reelSymbolTexts[i].text = "🎰\n...";
                }
            }
        }

        private void OnSlotReelStopped(SlotReelStoppedEvent evt)
        {
            int index = evt.ReelIndex;
            if (index >= 0 && index < 3)
            {
                string icon = SlotSymbolMetadata.GetIcon(evt.Symbol);
                string name = SlotSymbolMetadata.GetName(evt.Symbol);
                Color color = SlotSymbolMetadata.GetColor(evt.Symbol);

                if (reelSymbolTexts != null && index < reelSymbolTexts.Length && reelSymbolTexts[index] != null)
                {
                    reelSymbolTexts[index].text = $"{icon}\n{name}";
                    reelSymbolTexts[index].color = color;
                }

                if (reelCardBorders != null && index < reelCardBorders.Length && reelCardBorders[index] != null)
                {
                    reelCardBorders[index].color = color;
                }
            }
        }

        private void OnSlotSpinCompleted(SlotSpinCompletedEvent evt)
        {
            RefreshHUD();
            CheckAndOpenWildJokerModal();
        }

        private void OnSlotRespinTriggered(SlotRespinTriggeredEvent evt)
        {
            RefreshHUD();
            CheckAndOpenWildJokerModal();
        }

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            if (wildJokerModal != null) wildJokerModal.SetActive(false);
            RefreshHUD();
        }

        private void OnExperimentGroupAssigned(ExperimentGroupAssignedEvent evt)
        {
            RefreshExperimentGatedUI();
        }

        private void OnWildJokerResolved(WildJokerResolvedEvent evt)
        {
            if (wildJokerModal != null) wildJokerModal.SetActive(false);
            _pendingWildReelIndex = -1;
            RefreshHUD();
        }

        #endregion

        #region UI Refresh & Health Logic

        public void UpdateHealthUI(int currentHp, int maxHp)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHp;
                healthSlider.value = currentHp;
            }

            if (healthText != null)
            {
                healthText.text = $"HP: {currentHp} / {maxHp}";
            }

            if (healthFillImage != null)
            {
                float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
                if (ratio > 0.5f)
                {
                    healthFillImage.color = new Color(0.2f, 0.95f, 0.45f); // Neon emerald green
                }
                else if (ratio > 0.25f)
                {
                    healthFillImage.color = new Color(1.0f, 0.85f, 0.2f); // Neon amber
                }
                else
                {
                    healthFillImage.color = new Color(1.0f, 0.25f, 0.25f); // Neon red
                }
            }
        }

        private void UpdateActionButtonsState()
        {
            var controller = SlotRevolverController.Instance;
            if (controller == null) return;

            if (controller.IsSpinning)
            {
                if (spinOrCommitButton != null) spinOrCommitButton.interactable = false;
                if (spinOrCommitText != null) spinOrCommitText.text = "MEMUTAR...";
                if (respinAdButton != null) respinAdButton.interactable = false;
                return;
            }

            if (!controller.HasSpunThisTurn)
            {
                // Ready to spin
                if (spinOrCommitButton != null) spinOrCommitButton.interactable = true;
                if (spinOrCommitText != null) spinOrCommitText.text = "PUTAR SILINDER";
            }
            else
            {
                // Has spun -> Ready to commit all 3 reels for turn actions
                bool canCommit = controller.CanCommitTurn();
                if (spinOrCommitButton != null) spinOrCommitButton.interactable = canCommit;
                if (spinOrCommitText != null) spinOrCommitText.text = "EKSEKUSI GILIRAN";
            }

            // Update Full 3-Reel Respin button
            bool isAdsEnabled = ExperimentManager.Instance == null || ExperimentManager.Instance.IsRespinAdEnabled;
            bool canRespin = controller.CanRespin();

            if (respinAdButton != null)
            {
                respinAdButton.gameObject.SetActive(isAdsEnabled);
                respinAdButton.interactable = canRespin;
                if (respinAdText != null)
                {
                    respinAdText.text = controller.HasUsedRespinThisTurn ? "[ 🎬 RE-SPIN TERPAKAI ]" : "[ 🎬 RE-SPIN VIA ADS ]";
                }
            }
        }

        public void RefreshHUD()
        {
            var controller = SlotRevolverController.Instance;
            if (controller != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    SlotSymbol sym = controller.CurrentReelSymbols[i];
                    string icon = SlotSymbolMetadata.GetIcon(sym);
                    string name = SlotSymbolMetadata.GetName(sym);
                    Color col = SlotSymbolMetadata.GetColor(sym);

                    if (reelSymbolTexts != null && i < reelSymbolTexts.Length && reelSymbolTexts[i] != null)
                    {
                        reelSymbolTexts[i].text = $"{icon}\n{name}";
                        reelSymbolTexts[i].color = col;
                    }

                    if (reelCardBorders != null && i < reelCardBorders.Length && reelCardBorders[i] != null)
                    {
                        reelCardBorders[i].color = col;
                    }
                }
            }

            var playerHp = PlayerHealth.Instance;
            if (playerHp != null)
            {
                UpdateHealthUI(playerHp.CurrentHealth, playerHp.MaxHealth);
            }
            else
            {
                UpdateHealthUI(100, 100);
            }

            UpdateActionButtonsState();
        }

        public void RefreshExperimentGatedUI()
        {
            // Re-spin Ad button is ONLY shown on Group A & Group C. Hidden completely on Group B.
            bool allowRespinAd = ExperimentManager.Instance == null || ExperimentManager.Instance.IsRespinAdEnabled;

            if (respinAdButton != null)
            {
                respinAdButton.gameObject.SetActive(allowRespinAd);
            }
        }

        #endregion

        #region Button Click Actions

        public void OnPrimaryActionButtonClicked()
        {
            var controller = SlotRevolverController.Instance;
            if (controller == null) return;

            if (!controller.HasSpunThisTurn)
            {
                // Initiate spin
                controller.SpinCylinder();
            }
            else
            {
                // Commit turn directly with all 3 reels
                if (controller.CommitTurn(out List<SlotSymbol> actions))
                {
                    Debug.Log($"[CombatHUDView] Turn committed with {actions.Count} actions!");
                }
            }
        }

        /// <summary>
        /// Triggered when the main orange [ RE-SPIN VIA ADS ] button is clicked.
        /// Re-spins all 3 reels on the cylinder.
        /// </summary>
        public void OnMainRespinAdButtonClicked()
        {
            var controller = SlotRevolverController.Instance;
            if (controller == null) return;

            if (controller.CanRespin())
            {
                controller.RespinAllReels(isAdWatched: true);
            }
        }

        #endregion

        #region Wild Joker Modal

        private void CheckAndOpenWildJokerModal()
        {
            var controller = SlotRevolverController.Instance;
            if (controller == null) return;

            if (controller.HasPendingWildJoker(out int reelIndex))
            {
                _pendingWildReelIndex = reelIndex;
                if (wildJokerModal != null)
                {
                    wildJokerModal.SetActive(true);
                }
            }
        }

        public void SelectWildChoice(SlotSymbol chosenSymbol)
        {
            if (_pendingWildReelIndex >= 0 && SlotRevolverController.Instance != null)
            {
                SlotRevolverController.Instance.ResolveWildJoker(_pendingWildReelIndex, chosenSymbol);
            }
        }

        #endregion

        #region Procedural UI Fallback Builder

        /// <summary>
        /// Automatically generates a cyberpunk-themed HUD layout if not set up in Inspector.
        /// </summary>
        private void EnsureHUDConstructed()
        {
            if (_isInitialized) return;
            if (!useProceduralUIFallback)
            {
                _isInitialized = true;
                return;
            }

            if (hudCanvas == null)
            {
                hudCanvas = GetComponentInParent<Canvas>();
                if (hudCanvas == null)
                {
                    var canvasGo = new GameObject("CombatHUD_Canvas");
                    hudCanvas = canvasGo.AddComponent<Canvas>();
                    hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    var scaler = canvasGo.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    canvasGo.AddComponent<GraphicRaycaster>();
                    transform.SetParent(canvasGo.transform, false);
                }
            }

            if (bottomLeftHudRoot == null)
            {
                var hudRootGo = new GameObject("BottomLeft_HUD_Root");
                bottomLeftHudRoot = hudRootGo.AddComponent<RectTransform>();
                bottomLeftHudRoot.SetParent(hudCanvas.transform, false);

                // Anchor Bottom-Left (Min 0,0, Max 0,0, Pivot 0,0)
                bottomLeftHudRoot.anchorMin = new Vector2(0f, 0f);
                bottomLeftHudRoot.anchorMax = new Vector2(0f, 0f);
                bottomLeftHudRoot.pivot = new Vector2(0f, 0f);
                bottomLeftHudRoot.anchoredPosition = new Vector2(30f, 30f);
                bottomLeftHudRoot.sizeDelta = new Vector2(460f, 320f);

                // Background panel
                var bgImage = hudRootGo.AddComponent<Image>();
                bgImage.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);

                // Outline styling
                var outline = hudRootGo.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0.8f, 1f, 0.5f);
                outline.effectDistance = new Vector2(2f, -2f);

                BuildProceduralHUDChildren(bottomLeftHudRoot);
            }

            _isInitialized = true;
        }

        private void BuildProceduralHUDChildren(RectTransform root)
        {
            // 1. Health Bar (Top)
            var hpGo = new GameObject("HealthBar_Slider");
            var hpRect = hpGo.AddComponent<RectTransform>();
            hpRect.SetParent(root, false);
            hpRect.anchorMin = new Vector2(0.05f, 0.86f);
            hpRect.anchorMax = new Vector2(0.95f, 0.96f);
            hpRect.offsetMin = Vector2.zero;
            hpRect.offsetMax = Vector2.zero;

            healthSlider = hpGo.AddComponent<Slider>();
            var hpBg = hpGo.AddComponent<Image>();
            hpBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(hpRect, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.sizeDelta = Vector2.zero;

            var fillGo = new GameObject("Fill");
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.SetParent(fillArea, false);
            fillRect.sizeDelta = Vector2.zero;
            healthFillImage = fillGo.AddComponent<Image>();
            healthFillImage.color = new Color(0.2f, 0.95f, 0.45f);

            healthSlider.fillRect = fillRect;
            healthSlider.minValue = 0;
            healthSlider.maxValue = 100;
            healthSlider.value = 100;

            var hpTextGo = new GameObject("Health_Text");
            var hpTextRect = hpTextGo.AddComponent<RectTransform>();
            hpTextRect.SetParent(hpRect, false);
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;
            var hpTmp = hpTextGo.AddComponent<TextMeshProUGUI>();
            hpTmp.fontSize = 18;
            hpTmp.fontStyle = FontStyles.Bold;
            hpTmp.alignment = TextAlignmentOptions.Center;
            hpTmp.color = Color.white;
            hpTmp.text = "HP: 100 / 100";
            healthText = hpTmp;

            // 2. 3 Cylinder Reel Displays (Reels 0, 1, 2)
            var reelsContainerGo = new GameObject("Reels_Container");
            var reelsRect = reelsContainerGo.AddComponent<RectTransform>();
            reelsRect.SetParent(root, false);
            reelsRect.anchorMin = new Vector2(0.05f, 0.42f);
            reelsRect.anchorMax = new Vector2(0.95f, 0.82f);
            reelsRect.offsetMin = Vector2.zero;
            reelsRect.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject($"ReelCard_{i}");
                var cardRect = cardGo.AddComponent<RectTransform>();
                cardRect.SetParent(reelsRect, false);
                cardRect.anchorMin = new Vector2(i * 0.34f, 0f);
                cardRect.anchorMax = new Vector2(i * 0.34f + 0.31f, 1f);
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;

                reelCardBorders[i] = cardGo.AddComponent<Image>();
                reelCardBorders[i].color = new Color(0.2f, 0.25f, 0.35f, 1f);

                var txtGo = new GameObject("SymbolText");
                var txtRect = txtGo.AddComponent<RectTransform>();
                txtRect.SetParent(cardRect, false);
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.offsetMin = Vector2.zero;
                txtRect.offsetMax = Vector2.zero;

                var rTmp = txtGo.AddComponent<TextMeshProUGUI>();
                rTmp.fontSize = 16;
                rTmp.fontStyle = FontStyles.Bold;
                rTmp.alignment = TextAlignmentOptions.Center;
                rTmp.text = "\u2694\nATTACK";
                rTmp.color = SlotSymbolMetadata.GetColor(SlotSymbol.ATTACK);
                reelSymbolTexts[i] = rTmp;
            }

            // 3. Primary Spin / Commit Button
            var spinBtnGo = new GameObject("Spin_Commit_Button");
            var spinRect = spinBtnGo.AddComponent<RectTransform>();
            spinRect.SetParent(root, false);
            spinRect.anchorMin = new Vector2(0.05f, 0.18f);
            spinRect.anchorMax = new Vector2(0.55f, 0.36f);
            spinRect.offsetMin = Vector2.zero;
            spinRect.offsetMax = Vector2.zero;

            var spinBg = spinBtnGo.AddComponent<Image>();
            spinBg.color = new Color(0.1f, 0.65f, 0.95f, 1f);
            spinOrCommitButton = spinBtnGo.AddComponent<Button>();
            spinOrCommitButton.onClick.AddListener(OnPrimaryActionButtonClicked);

            var spinTxtGo = new GameObject("Text");
            var spinTxtRect = spinTxtGo.AddComponent<RectTransform>();
            spinTxtRect.SetParent(spinRect, false);
            spinTxtRect.anchorMin = Vector2.zero;
            spinTxtRect.anchorMax = Vector2.one;
            var spinTmp = spinTxtGo.AddComponent<TextMeshProUGUI>();
            spinTmp.fontSize = 15;
            spinTmp.fontStyle = FontStyles.Bold;
            spinTmp.alignment = TextAlignmentOptions.Center;
            spinTmp.color = Color.white;
            spinTmp.text = "PUTAR SILINDER";
            spinOrCommitText = spinTmp;

            // 4. Re-spin Ad Button (Group A & C only)
            var adBtnGo = new GameObject("Respin_Ad_Button");
            var adRect = adBtnGo.AddComponent<RectTransform>();
            adRect.SetParent(root, false);
            adRect.anchorMin = new Vector2(0.58f, 0.18f);
            adRect.anchorMax = new Vector2(0.95f, 0.36f);
            adRect.offsetMin = Vector2.zero;
            adRect.offsetMax = Vector2.zero;

            var adBg = adBtnGo.AddComponent<Image>();
            adBg.color = new Color(0.85f, 0.55f, 0.1f, 1f);
            respinAdButton = adBtnGo.AddComponent<Button>();
            respinAdButton.onClick.AddListener(OnMainRespinAdButtonClicked);

            var adTxtGo = new GameObject("Text");
            var adTxtRect = adTxtGo.AddComponent<RectTransform>();
            adTxtRect.SetParent(adRect, false);
            adTxtRect.anchorMin = Vector2.zero;
            adTxtRect.anchorMax = Vector2.one;
            var adTmp = adTxtGo.AddComponent<TextMeshProUGUI>();
            adTmp.fontSize = 12;
            adTmp.fontStyle = FontStyles.Bold;
            adTmp.alignment = TextAlignmentOptions.Center;
            adTmp.color = Color.black;
            adTmp.text = "[ 🎬 RE-SPIN ]";
            respinAdText = adTmp;

            // 5. Defense Prompt Bar (Bottom)
            defensePromptRoot = new GameObject("Defense_Prompt_Container");
            var defRect = defensePromptRoot.AddComponent<RectTransform>();
            defRect.SetParent(root, false);
            defRect.anchorMin = new Vector2(0.05f, 0.03f);
            defRect.anchorMax = new Vector2(0.95f, 0.14f);
            defRect.offsetMin = Vector2.zero;
            defRect.offsetMax = Vector2.zero;

            var defBg = defensePromptRoot.AddComponent<Image>();
            defBg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

            var defTxtGo = new GameObject("Text");
            var defTxtRect = defTxtGo.AddComponent<RectTransform>();
            defTxtRect.SetParent(defRect, false);
            defTxtRect.anchorMin = Vector2.zero;
            defTxtRect.anchorMax = Vector2.one;
            var defTmp = defTxtGo.AddComponent<TextMeshProUGUI>();
            defTmp.fontSize = 12;
            defTmp.alignment = TextAlignmentOptions.Center;
            defTmp.color = new Color(0f, 0.85f, 1f);
            defTmp.text = "[Q] HINDAR (Merah)   |   [E] PARRY (Kuning)";
            defensePromptText = defTmp;

            // 6. Wild Joker Selection Modal
            BuildWildJokerModal(root);
        }

        private void BuildWildJokerModal(RectTransform parent)
        {
            wildJokerModal = new GameObject("WildJoker_Modal");
            var modalRect = wildJokerModal.AddComponent<RectTransform>();
            modalRect.SetParent(parent, false);
            modalRect.anchorMin = new Vector2(0.1f, 0.25f);
            modalRect.anchorMax = new Vector2(0.9f, 0.75f);
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            var modalBg = wildJokerModal.AddComponent<Image>();
            modalBg.color = new Color(0.04f, 0.06f, 0.12f, 0.96f);
            var modalOutline = wildJokerModal.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0.95f, 0.35f, 1f);
            modalOutline.effectDistance = new Vector2(2f, -2f);

            var titleGo = new GameObject("Title");
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.SetParent(modalRect, false);
            titleRect.anchorMin = new Vector2(0.05f, 0.75f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.fontSize = 15;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(0.95f, 0.35f, 1f);
            titleTmp.text = "⭐ WILD JOKER AKTIF ⭐\nPilih Simbol:";

            // 4 Option Buttons (Attack, Mark, Heal, Counter)
            SlotSymbol[] choices = new[] { SlotSymbol.ATTACK, SlotSymbol.MARK, SlotSymbol.HEAL, SlotSymbol.COUNTER_SHARPEN };
            for (int i = 0; i < 4; i++)
            {
                SlotSymbol sym = choices[i];
                var btnGo = new GameObject($"Btn_{sym}");
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.SetParent(modalRect, false);
                btnRect.anchorMin = new Vector2(0.08f + (i % 2) * 0.44f, i < 2 ? 0.42f : 0.12f);
                btnRect.anchorMax = new Vector2(0.48f + (i % 2) * 0.44f, i < 2 ? 0.68f : 0.38f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = SlotSymbolMetadata.GetColor(sym) * 0.85f;
                var btn = btnGo.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectWildChoice(sym));

                var bTmp = new GameObject("Text").AddComponent<TextMeshProUGUI>();
                bTmp.transform.SetParent(btnRect, false);
                bTmp.rectTransform.anchorMin = Vector2.zero;
                bTmp.rectTransform.anchorMax = Vector2.one;
                bTmp.fontSize = 13;
                bTmp.fontStyle = FontStyles.Bold;
                bTmp.alignment = TextAlignmentOptions.Center;
                bTmp.color = Color.black;
                bTmp.text = $"{SlotSymbolMetadata.GetIcon(sym)} {SlotSymbolMetadata.GetName(sym)}";

                if (i == 0) wildAttackBtn = btn;
                else if (i == 1) wildMarkBtn = btn;
                else if (i == 2) wildHealBtn = btn;
                else if (i == 3) wildCounterBtn = btn;
            }

            wildJokerModal.SetActive(false);
        }

        #endregion
    }
}
