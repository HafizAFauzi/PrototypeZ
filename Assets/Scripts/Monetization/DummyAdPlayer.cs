using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;

namespace LuckyDuelist.Monetization
{
    /// <summary>
    /// Simulated Rewarded Video Ad Player for Lucky Duelist: Cyber Roulette.
    /// Implements GDD Section 4.1 & TSD Section 2.5:
    /// 1. Fullscreen overlay simulating a video ad (10 seconds playback).
    /// 2. Live countdown timer (10s down to 0s) and progress bar.
    /// 3. Close/Claim button unlocked upon countdown completion.
    /// 4. Standardized eCPM revenue calculation: Rp 250,00 per completed impression.
    /// 5. Reward callbacks: Tactical Cylinder Re-spin, 50% HP Revive, 2x Gold multiplier.
    /// 6. Strict feature gating: Enforced via ExperimentManager (Group A & C enabled, Group B disabled).
    /// </summary>
    public class DummyAdPlayer : MonoBehaviour
    {
        public static DummyAdPlayer Instance { get; private set; }

        public const float StandardDurationSeconds = 10.0f;
        public const float StandardEcpmRewardIdr = 250.0f; // Rp 250,00 per impression

        [Header("Ad Player Configuration")]
        [SerializeField] private float adDurationSeconds = StandardDurationSeconds;
        [SerializeField] private float ecpmRateIdr = StandardEcpmRewardIdr;
        [SerializeField] private bool autoConstructUI = true;

        [Header("Runtime State")]
        [SerializeField] private bool isPlayingAd = false;
        [SerializeField] private float remainingCountdown = 0f;
        [SerializeField] private int totalAdImpressions = 0;
        [SerializeField] private float totalAdRevenue = 0f;

        [Header("Placement Counters")]
        [SerializeField] private int respinAdCount = 0;
        [SerializeField] private int reviveAdCount = 0;
        [SerializeField] private int doubleGoldAdCount = 0;

        // UI References (dynamically constructed if not bound)
        private Canvas _adCanvas;
        private GameObject _overlayRoot;
        private Text _sponsorTitleText;
        private Text _countdownText;
        private Text _adBodyText;
        private Slider _progressBar;
        private Button _claimButton;
        private Text _claimButtonText;
        private Image _videoViewportBg;

        // Callbacks & Routine
        private Coroutine _adPlaybackCoroutine;
        private Action _pendingRewardCallback;
        private Action _pendingDismissCallback;
        private AdPlacementType _currentPlacement;
        private string _currentAdId;

        // Ad Creative Mock Messages
        private readonly string[] _mockAdBanners = new string[]
        {
            "<b>[ VOLT-SURGE NEURAL ENERGY ]</b>\n<i>Overclock your reflexes +300% with cyber-caffeine!</i>\n'Drink Volt. Never miss a parry.'",
            "<b>[ ARASAKA CYBERNETIC OPTICS ]</b>\n<i>Mk. IV Smart-Aim Reticles: Instant lock-on precision.</i>\n'See the future of dueling before they fire.'",
            "<b>[ NEON CASINO ROYALE ]</b>\n<i>Spin to win high-roller cyber gold and legendary revolver barrels!</i>\n'Fortune favors the daring duelist.'",
            "<b>[ SHADOW-BLADE NANO-ALLOY ]</b>\n<i>Tanto knives forged with monomolecular cutting edges.</i>\n'Clang with perfection. Deflect any bullet.'"
        };

        #region Public Properties
        public bool IsPlayingAd => isPlayingAd;
        public float RemainingCountdown => remainingCountdown;
        public float AdDurationSeconds => adDurationSeconds;
        public float EcpmRateIdr => ecpmRateIdr;
        public int TotalAdImpressions => totalAdImpressions;
        public float TotalAdRevenue => totalAdRevenue;
        public int RespinAdCount => respinAdCount;
        public int ReviveAdCount => reviveAdCount;
        public int DoubleGoldAdCount => doubleGoldAdCount;
        public AdPlacementType CurrentPlacement => _currentPlacement;
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

            if (autoConstructUI)
            {
                EnsureOverlayConstructed();
            }
        }

        private void Start()
        {
            EnsureOverlayConstructed();
            HideOverlayImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Ad Playback API

        /// <summary>
        /// Requests to display a fullscreen simulated Rewarded Video Ad.
        /// Feature gates are checked automatically: returns false if ads are disabled in current experiment group.
        /// </summary>
        /// <param name="placement">Placement type (RespinSlot, ReviveMatch, DoubleGold)</param>
        /// <param name="onRewardGranted">Callback executed when player completes the ad and claims reward</param>
        /// <param name="onAdDismissed">Callback executed if ad is closed or cancelled</param>
        public bool ShowRewardedAd(AdPlacementType placement, Action onRewardGranted, Action onAdDismissed = null)
        {
            if (isPlayingAd)
            {
                Debug.LogWarning("[DummyAdPlayer] An ad is already actively playing.");
                return false;
            }

            // 1. Verify Feature Flagging via ExperimentManager
            if (!IsPlacementAllowedByExperiment(placement))
            {
                string groupName = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentConfig.GroupId : "UNKNOWN";
                Debug.LogWarning($"[DummyAdPlayer] Rewarded Ad rejected: Placement {placement} is disabled for group {groupName}.");
                onAdDismissed?.Invoke();
                return false;
            }

            // 2. Setup Ad Session
            _currentPlacement = placement;
            _pendingRewardCallback = onRewardGranted;
            _pendingDismissCallback = onAdDismissed;
            _currentAdId = "AD-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            EnsureOverlayConstructed();
            ShowOverlay();

            // 3. Dispatch Start Event
            EventBus.Publish(new AdImpressionStartedEvent(_currentAdId, placement, adDurationSeconds));
            Debug.Log($"[DummyAdPlayer] Started Rewarded Ad playback [{_currentAdId}] for {placement} (Duration: {adDurationSeconds}s).");

            // 4. Begin Countdown Routine
            if (_adPlaybackCoroutine != null) StopCoroutine(_adPlaybackCoroutine);
            _adPlaybackCoroutine = StartCoroutine(AdPlaybackRoutine());

            return true;
        }

        /// <summary>
        /// Convenience method: Displays ad for tactical 1-reel cylinder re-spin.
        /// </summary>
        public bool PlayRespinAd(int reelIndex, Action onSuccess = null)
        {
            var controller = SlotRevolverController.Instance;
            if (controller == null || !controller.CanRespinReel(reelIndex))
            {
                Debug.LogWarning($"[DummyAdPlayer] Cannot play respin ad: Reel {reelIndex} cannot be respun.");
                return false;
            }

            return ShowRewardedAd(
                AdPlacementType.RespinSlot,
                onRewardGranted: () =>
                {
                    if (SlotRevolverController.Instance != null)
                    {
                        SlotRevolverController.Instance.RespinReel(reelIndex, isAdWatched: true);
                    }
                    onSuccess?.Invoke();
                }
            );
        }

        /// <summary>
        /// Convenience method: Displays ad for second-chance 50% HP revive.
        /// </summary>
        public bool PlayReviveAd(Action onSuccess = null)
        {
            return ShowRewardedAd(
                AdPlacementType.ReviveMatch,
                onRewardGranted: () =>
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.RevivePlayer();
                    }
                    onSuccess?.Invoke();
                }
            );
        }

        /// <summary>
        /// Convenience method: Displays ad for double gold victory multiplier.
        /// </summary>
        public bool PlayDoubleGoldAd(Action onSuccess = null)
        {
            return ShowRewardedAd(
                AdPlacementType.DoubleGold,
                onRewardGranted: () =>
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.ClaimSettlementReward(doubleRewardWithAd: true);
                    }
                    onSuccess?.Invoke();
                }
            );
        }

        /// <summary>
        /// Instantly completes the currently playing ad and triggers reward.
        /// Dedicated for unit testing, automated verification, and researcher shortcut.
        /// </summary>
        public void CompleteAdInstantlyForTesting()
        {
            if (!isPlayingAd) return;

            if (_adPlaybackCoroutine != null)
            {
                StopCoroutine(_adPlaybackCoroutine);
                _adPlaybackCoroutine = null;
            }

            remainingCountdown = 0f;
            FinalizeAdImpression();
            ClaimRewardAndDismiss();
        }

        /// <summary>
        /// Resets impression and revenue counters (used in test fixtures).
        /// </summary>
        public void ResetStatsForTesting()
        {
            totalAdImpressions = 0;
            totalAdRevenue = 0f;
            respinAdCount = 0;
            reviveAdCount = 0;
            doubleGoldAdCount = 0;
            isPlayingAd = false;
            remainingCountdown = 0f;
        }

        #endregion

        #region Internal Playback Logic & Routine

        private IEnumerator AdPlaybackRoutine()
        {
            isPlayingAd = true;
            remainingCountdown = adDurationSeconds;

            // Pick mock creative based on placement
            int bannerIndex = Mathf.Abs((int)_currentPlacement) % _mockAdBanners.Length;
            if (_adBodyText != null)
            {
                _adBodyText.text = _mockAdBanners[bannerIndex];
            }

            if (_claimButton != null)
            {
                _claimButton.interactable = false;
                if (_claimButtonText != null)
                {
                    _claimButtonText.text = "WATCHING AD VIDEO...";
                }
            }

            // 10-second countdown loop (unscaled time to support paused gameplay)
            while (remainingCountdown > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                remainingCountdown = Mathf.Max(0f, remainingCountdown - dt);

                UpdateAdDisplay(remainingCountdown);

                // Slight subtle color pulsing for simulated video player
                if (_videoViewportBg != null)
                {
                    float pulse = 0.15f + Mathf.PingPong(Time.unscaledTime * 0.8f, 0.10f);
                    _videoViewportBg.color = new Color(pulse * 0.5f, pulse, pulse * 1.5f, 0.95f);
                }

                yield return null;
            }

            // Ad countdown completed!
            FinalizeAdImpression();

            // Unlock Claim Button
            if (_claimButton != null)
            {
                _claimButton.interactable = true;
                if (_claimButtonText != null)
                {
                    _claimButtonText.text = "<b>[ ? CLAIM REWARD ]</b>";
                }
            }

            if (_countdownText != null)
            {
                _countdownText.text = "<b><color=#00FF99>AD COMPLETED! REWARD UNLOCKED</color></b>";
            }
        }

        private void UpdateAdDisplay(float timeLeft)
        {
            int secondsLeft = Mathf.CeilToInt(timeLeft);

            if (_countdownText != null)
            {
                _countdownText.text = $"Reward in: <b>{secondsLeft}s</b>";
            }

            if (_progressBar != null && adDurationSeconds > 0f)
            {
                float progress = Mathf.Clamp01((adDurationSeconds - timeLeft) / adDurationSeconds);
                _progressBar.value = progress;
            }
        }

        private void FinalizeAdImpression()
        {
            totalAdImpressions++;
            totalAdRevenue += ecpmRateIdr;

            switch (_currentPlacement)
            {
                case AdPlacementType.RespinSlot:
                    respinAdCount++;
                    break;
                case AdPlacementType.ReviveMatch:
                    reviveAdCount++;
                    break;
                case AdPlacementType.DoubleGold:
                    doubleGoldAdCount++;
                    break;
            }

            string userId = ExperimentManager.Instance != null ? ExperimentManager.Instance.UserId : "UNKNOWN_TESTER";

            EventBus.Publish(new AdImpressionCompletedEvent(
                _currentAdId,
                userId,
                _currentPlacement,
                adDurationSeconds,
                ecpmRateIdr,
                ecpmRateIdr,
                DateTime.UtcNow
            ));

            Debug.Log($"<color=green>[DummyAdPlayer] Ad impression finalized! Total impressions: {totalAdImpressions} | Total Ad Revenue: Rp {totalAdRevenue:N0}</color>");
        }

        public void ClaimRewardAndDismiss()
        {
            Action callback = _pendingRewardCallback;
            _pendingRewardCallback = null;
            _pendingDismissCallback = null;

            isPlayingAd = false;
            HideOverlayImmediate();

            callback?.Invoke();
        }

        public void DismissWithoutReward()
        {
            if (_adPlaybackCoroutine != null)
            {
                StopCoroutine(_adPlaybackCoroutine);
                _adPlaybackCoroutine = null;
            }

            Action dismissCallback = _pendingDismissCallback;
            _pendingRewardCallback = null;
            _pendingDismissCallback = null;

            isPlayingAd = false;
            HideOverlayImmediate();

            dismissCallback?.Invoke();
        }

        private bool IsPlacementAllowedByExperiment(AdPlacementType placement)
        {
            if (ExperimentManager.Instance == null) return true;

            if (!ExperimentManager.Instance.IsAdsEnabled) return false;

            switch (placement)
            {
                case AdPlacementType.RespinSlot:
                    return ExperimentManager.Instance.IsRespinAdEnabled;
                case AdPlacementType.ReviveMatch:
                    return ExperimentManager.Instance.IsReviveAdEnabled;
                case AdPlacementType.DoubleGold:
                    return ExperimentManager.Instance.IsDoubleGoldAdEnabled;
                default:
                    return true;
            }
        }

        #endregion

        #region Fullscreen Overlay Canvas Construction

        public void EnsureOverlayConstructed()
        {
            if (_overlayRoot != null) return;

            // 1. Root Canvas (High Sorting Order 500)
            var canvasGo = new GameObject("[DummyAdPlayer_Canvas]");
            canvasGo.transform.SetParent(transform, false);

            _adCanvas = canvasGo.AddComponent<Canvas>();
            _adCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _adCanvas.sortingOrder = 500;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            // 2. Fullscreen Blackout Dimmer
            _overlayRoot = new GameObject("Ad_Overlay_Panel");
            var rootRect = _overlayRoot.AddComponent<RectTransform>();
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootBg = _overlayRoot.AddComponent<Image>();
            rootBg.color = new Color(0.04f, 0.05f, 0.08f, 0.98f); // Deep cyber black

            // 3. Top Cyberpunk Ad Header Bar
            var topBar = new GameObject("Top_Header_Bar");
            var topBarRect = topBar.AddComponent<RectTransform>();
            topBarRect.SetParent(rootRect, false);
            topBarRect.anchorMin = new Vector2(0f, 0.90f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;

            var topBarBg = topBar.AddComponent<Image>();
            topBarBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);

            var titleGo = new GameObject("Title_Text");
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.SetParent(topBarRect, false);
            titleRect.anchorMin = new Vector2(0.05f, 0f);
            titleRect.anchorMax = new Vector2(0.65f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _sponsorTitleText = titleGo.AddComponent<Text>();
            _sponsorTitleText.font = defaultFont;
            _sponsorTitleText.fontSize = 22;
            _sponsorTitleText.fontStyle = FontStyle.Bold;
            _sponsorTitleText.alignment = TextAnchor.MiddleLeft;
            _sponsorTitleText.color = new Color(0.0f, 1.0f, 0.85f); // Bright Cyan
            _sponsorTitleText.text = "? NEO-NET SPONSORED STREAM  |  REWARDED BROADCAST";

            // Countdown in Top Bar
            var countdownGo = new GameObject("Countdown_Text");
            var countdownRect = countdownGo.AddComponent<RectTransform>();
            countdownRect.SetParent(topBarRect, false);
            countdownRect.anchorMin = new Vector2(0.70f, 0f);
            countdownRect.anchorMax = new Vector2(0.95f, 1f);
            countdownRect.offsetMin = Vector2.zero;
            countdownRect.offsetMax = Vector2.zero;

            _countdownText = countdownGo.AddComponent<Text>();
            _countdownText.font = defaultFont;
            _countdownText.fontSize = 20;
            _countdownText.fontStyle = FontStyle.Bold;
            _countdownText.alignment = TextAnchor.MiddleRight;
            _countdownText.color = new Color(1.0f, 0.85f, 0.2f); // Cyber Amber
            _countdownText.text = "Reward in: 10s";

            // 4. Central Simulated Video Viewport
            var videoContainer = new GameObject("Video_Viewport");
            var videoRect = videoContainer.AddComponent<RectTransform>();
            videoRect.SetParent(rootRect, false);
            videoRect.anchorMin = new Vector2(0.15f, 0.20f);
            videoRect.anchorMax = new Vector2(0.85f, 0.85f);
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;

            _videoViewportBg = videoContainer.AddComponent<Image>();
            _videoViewportBg.color = new Color(0.08f, 0.15f, 0.25f, 0.95f);

            var outline = videoContainer.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 1f, 0.85f, 0.5f);
            outline.effectDistance = new Vector2(3, -3);

            // Watermark / Video Header in Viewport
            var liveBadgeGo = new GameObject("Live_Badge");
            var liveBadgeRect = liveBadgeGo.AddComponent<RectTransform>();
            liveBadgeRect.SetParent(videoRect, false);
            liveBadgeRect.anchorMin = new Vector2(0.03f, 0.90f);
            liveBadgeRect.anchorMax = new Vector2(0.20f, 0.97f);
            liveBadgeRect.offsetMin = Vector2.zero;
            liveBadgeRect.offsetMax = Vector2.zero;

            var liveBadgeBg = liveBadgeGo.AddComponent<Image>();
            liveBadgeBg.color = new Color(0.9f, 0.15f, 0.2f, 0.9f); // Red LIVE badge

            var liveText = new GameObject("Text").AddComponent<Text>();
            liveText.transform.SetParent(liveBadgeRect, false);
            liveText.rectTransform.anchorMin = Vector2.zero;
            liveText.rectTransform.anchorMax = Vector2.one;
            liveText.rectTransform.offsetMin = Vector2.zero;
            liveText.rectTransform.offsetMax = Vector2.zero;
            liveText.font = defaultFont;
            liveText.fontSize = 14;
            liveText.fontStyle = FontStyle.Bold;
            liveText.alignment = TextAnchor.MiddleCenter;
            liveText.color = Color.white;
            liveText.text = "? LIVE AD SIMULATION";

            // Ad Mock Body / Creative Content
            var bodyGo = new GameObject("Ad_Body_Text");
            var bodyRect = bodyGo.AddComponent<RectTransform>();
            bodyRect.SetParent(videoRect, false);
            bodyRect.anchorMin = new Vector2(0.08f, 0.25f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.80f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            _adBodyText = bodyGo.AddComponent<Text>();
            _adBodyText.font = defaultFont;
            _adBodyText.fontSize = 24;
            _adBodyText.alignment = TextAnchor.MiddleCenter;
            _adBodyText.color = Color.white;
            _adBodyText.text = _mockAdBanners[0];

            // 5. Video Progress Bar Slider
            var sliderGo = new GameObject("Ad_Progress_Slider");
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.SetParent(videoRect, false);
            sliderRect.anchorMin = new Vector2(0.05f, 0.08f);
            sliderRect.anchorMax = new Vector2(0.95f, 0.14f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            _progressBar = sliderGo.AddComponent<Slider>();
            _progressBar.minValue = 0f;
            _progressBar.maxValue = 1f;
            _progressBar.value = 0f;
            _progressBar.interactable = false;

            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            var fillArea = new GameObject("Fill Area");
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.SetParent(sliderRect, false);
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill");
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0f, 1f, 0.85f); // Bright Cyan
            _progressBar.fillRect = fillRect;

            // 6. Bottom Action Bar: Claim / Close Button
            var bottomBar = new GameObject("Bottom_Action_Bar");
            var bottomBarRect = bottomBar.AddComponent<RectTransform>();
            bottomBarRect.SetParent(rootRect, false);
            bottomBarRect.anchorMin = new Vector2(0.30f, 0.04f);
            bottomBarRect.anchorMax = new Vector2(0.70f, 0.15f);
            bottomBarRect.offsetMin = Vector2.zero;
            bottomBarRect.offsetMax = Vector2.zero;

            var claimBtnGo = new GameObject("Claim_Reward_Button");
            var claimBtnRect = claimBtnGo.AddComponent<RectTransform>();
            claimBtnRect.SetParent(bottomBarRect, false);
            claimBtnRect.anchorMin = Vector2.zero;
            claimBtnRect.anchorMax = Vector2.one;
            claimBtnRect.offsetMin = Vector2.zero;
            claimBtnRect.offsetMax = Vector2.zero;

            var claimBtnBg = claimBtnGo.AddComponent<Image>();
            claimBtnBg.color = new Color(0.1f, 0.75f, 0.4f, 1f); // Vibrant Emerald Green

            _claimButton = claimBtnGo.AddComponent<Button>();
            _claimButton.onClick.AddListener(ClaimRewardAndDismiss);

            var claimTxtGo = new GameObject("Text");
            var claimTxtRect = claimTxtGo.AddComponent<RectTransform>();
            claimTxtRect.SetParent(claimBtnRect, false);
            claimTxtRect.anchorMin = Vector2.zero;
            claimTxtRect.anchorMax = Vector2.one;
            claimTxtRect.offsetMin = Vector2.zero;
            claimTxtRect.offsetMax = Vector2.zero;

            _claimButtonText = claimTxtGo.AddComponent<Text>();
            _claimButtonText.font = defaultFont;
            _claimButtonText.fontSize = 20;
            _claimButtonText.fontStyle = FontStyle.Bold;
            _claimButtonText.alignment = TextAnchor.MiddleCenter;
            _claimButtonText.color = Color.black;
            _claimButtonText.text = "WATCHING AD VIDEO...";
        }

        private void ShowOverlay()
        {
            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(true);
            }
        }

        private void HideOverlayImmediate()
        {
            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(false);
            }
        }

        #endregion
    }
}
