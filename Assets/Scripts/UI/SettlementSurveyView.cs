using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.Telemetry;

namespace LuckyDuelist.UI
{
    /// <summary>
    /// Post-session Settlement & Survey Evaluation View.
    /// Presents final run statistics (ARPU, Rewarded Ads, IAP, FPS Parry/Dodge performance),
    /// displays real-time cloud synchronization status,
    /// and provides immediate or automatic redirection to the Google Form PAM & UX questionnaire.
    /// </summary>
    public class SettlementSurveyView : MonoBehaviour
    {
        public static SettlementSurveyView Instance { get; private set; }

        [Header("UI Canvas")]
        [SerializeField] private Canvas settlementCanvas;
        [SerializeField] private GameObject settlementPanelRoot;

        [Header("Text Displays")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text respondentInfoText;
        [SerializeField] private Text arpuSummaryText;
        [SerializeField] private Text combatSummaryText;
        [SerializeField] private Text cloudSyncStatusText;
        [SerializeField] private Text countdownPromptText;

        [Header("Buttons")]
        [SerializeField] private Button surveyRedirectButton;
        [SerializeField] private Text surveyButtonLabel;
        [SerializeField] private Button returnToLobbyButton;
        [SerializeField] private Button cancelAutoRedirectButton;

        private float _remainingCountdown = -1f;
        private bool _isSubscribed = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureUIHierarchy();
            SetUIVisible(false);
            SubscribeEvents();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        public void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;

            EventBus.Subscribe<FinalSettlementEvent>(OnFinalSettlement);
            EventBus.Subscribe<TelemetrySyncStartedEvent>(OnTelemetrySyncStarted);
            EventBus.Subscribe<TelemetrySyncCompletedEvent>(OnTelemetrySyncCompleted);
            EventBus.Subscribe<SurveyRequestedEvent>(OnSurveyRequested);
        }

        public void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;

            EventBus.Unsubscribe<FinalSettlementEvent>(OnFinalSettlement);
            EventBus.Unsubscribe<TelemetrySyncStartedEvent>(OnTelemetrySyncStarted);
            EventBus.Unsubscribe<TelemetrySyncCompletedEvent>(OnTelemetrySyncCompleted);
            EventBus.Unsubscribe<SurveyRequestedEvent>(OnSurveyRequested);
        }

        #region UI Construction & Lifecycle

        private void EnsureUIHierarchy()
        {
            if (settlementPanelRoot != null) return;

            // 1. Create Canvas
            var canvasGo = new GameObject("[SettlementSurvey_Canvas]");
            canvasGo.transform.SetParent(transform, false);

            settlementCanvas = canvasGo.AddComponent<Canvas>();
            settlementCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            settlementCanvas.sortingOrder = 500; // Above HUD and game views

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 16);

            // 2. Fullscreen Background Panel
            settlementPanelRoot = new GameObject("Settlement_Overlay_Panel");
            var rootRect = settlementPanelRoot.AddComponent<RectTransform>();
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImage = settlementPanelRoot.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.05f, 0.09f, 0.95f); // Deep dark cyberpunk slate

            // 3. Central Content Card
            var cardGo = new GameObject("Card_Container");
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.SetParent(rootRect, false);
            cardRect.anchorMin = new Vector2(0.15f, 0.08f);
            cardRect.anchorMax = new Vector2(0.85f, 0.92f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            var cardImage = cardGo.AddComponent<Image>();
            cardImage.color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            var outline = cardGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.95f, 1.0f, 0.7f); // Neon Cyan outline
            outline.effectDistance = new Vector2(2, -2);

            // 4. Header Title
            var titleGo = new GameObject("Title_Text");
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.SetParent(cardRect, false);
            titleRect.anchorMin = new Vector2(0.05f, 0.86f);
            titleRect.anchorMax = new Vector2(0.95f, 0.97f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            titleText = titleGo.AddComponent<Text>();
            titleText.font = defaultFont;
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1.0f, 0.85f, 0.2f); // Neon Gold
            titleText.text = "â˜… DUEL SELESAI / SESSION SETTLEMENT â˜…";

            // 5. Subtitle & Result
            var subtitleGo = new GameObject("Subtitle_Text");
            var subtitleRect = subtitleGo.AddComponent<RectTransform>();
            subtitleRect.SetParent(cardRect, false);
            subtitleRect.anchorMin = new Vector2(0.05f, 0.80f);
            subtitleRect.anchorMax = new Vector2(0.95f, 0.86f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            subtitleText = subtitleGo.AddComponent<Text>();
            subtitleText.font = defaultFont;
            subtitleText.fontSize = 20;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = Color.white;
            subtitleText.text = "HASIL PERTANDINGAN: VICTORY";

            // 6. Respondent Badge
            var badgeGo = new GameObject("Respondent_Badge");
            var badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.SetParent(cardRect, false);
            badgeRect.anchorMin = new Vector2(0.05f, 0.73f);
            badgeRect.anchorMax = new Vector2(0.95f, 0.79f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;

            respondentInfoText = badgeGo.AddComponent<Text>();
            respondentInfoText.font = defaultFont;
            respondentInfoText.fontSize = 17;
            respondentInfoText.alignment = TextAnchor.MiddleCenter;
            respondentInfoText.color = new Color(0.4f, 0.8f, 1.0f); // Soft Cyan
            respondentInfoText.text = "Tester ID: RES-C-001 | Kelompok: GROUP_C (Hybrid)";

            // 7. ARPU & Economic Breakdown Box
            var arpuGo = new GameObject("ARPU_Summary_Box");
            var arpuRect = arpuGo.AddComponent<RectTransform>();
            arpuRect.SetParent(cardRect, false);
            arpuRect.anchorMin = new Vector2(0.05f, 0.47f);
            arpuRect.anchorMax = new Vector2(0.48f, 0.71f);
            arpuRect.offsetMin = Vector2.zero;
            arpuRect.offsetMax = Vector2.zero;

            var arpuBg = arpuGo.AddComponent<Image>();
            arpuBg.color = new Color(0.12f, 0.16f, 0.25f, 0.9f);

            var arpuTextGo = new GameObject("ARPU_Text");
            var arpuTextRect = arpuTextGo.AddComponent<RectTransform>();
            arpuTextRect.SetParent(arpuRect, false);
            arpuTextRect.anchorMin = new Vector2(0.05f, 0.05f);
            arpuTextRect.anchorMax = new Vector2(0.95f, 0.95f);
            arpuTextRect.offsetMin = Vector2.zero;
            arpuTextRect.offsetMax = Vector2.zero;

            arpuSummaryText = arpuTextGo.AddComponent<Text>();
            arpuSummaryText.font = defaultFont;
            arpuSummaryText.fontSize = 16;
            arpuSummaryText.alignment = TextAnchor.UpperLeft;
            arpuSummaryText.color = Color.white;
            arpuSummaryText.text = "ðŸ’° ANALISIS ARPU EKONOMI:\n- Rewarded Ads: Rp 0\n- IAP Cosmetics: Rp 0\n- Total ARPU: Rp 0\n- Sisa Kuota Budget: Rp 20.000";

            // 8. FPS Reflex Combat Stats Box
            var combatGo = new GameObject("Combat_Summary_Box");
            var combatRect = combatGo.AddComponent<RectTransform>();
            combatRect.SetParent(cardRect, false);
            combatRect.anchorMin = new Vector2(0.52f, 0.47f);
            combatRect.anchorMax = new Vector2(0.95f, 0.71f);
            combatRect.offsetMin = Vector2.zero;
            combatRect.offsetMax = Vector2.zero;

            var combatBg = combatGo.AddComponent<Image>();
            combatBg.color = new Color(0.12f, 0.16f, 0.25f, 0.9f);

            var combatTextGo = new GameObject("Combat_Text");
            var combatTextRect = combatTextGo.AddComponent<RectTransform>();
            combatTextRect.SetParent(combatRect, false);
            combatTextRect.anchorMin = new Vector2(0.05f, 0.05f);
            combatTextRect.anchorMax = new Vector2(0.95f, 0.95f);
            combatTextRect.offsetMin = Vector2.zero;
            combatTextRect.offsetMax = Vector2.zero;

            combatSummaryText = combatTextGo.AddComponent<Text>();
            combatSummaryText.font = defaultFont;
            combatSummaryText.fontSize = 16;
            combatSummaryText.alignment = TextAnchor.UpperLeft;
            combatSummaryText.color = Color.white;
            combatSummaryText.text = "âš” STATISTIK REFLEKS COMBAT:\n- Parry Kuning [E]: 0x Sukses\n- Dodge Merah [Q]: 0x Sukses\n- Salah Tekan E: 0x\n- Damage Deflected: 0 DMG\n- Akurasi Lock-on: 100%";

            // 9. Cloud Telemetry Status Box
            var cloudStatusGo = new GameObject("Cloud_Status_Box");
            var cloudStatusRect = cloudStatusGo.AddComponent<RectTransform>();
            cloudStatusRect.SetParent(cardRect, false);
            cloudStatusRect.anchorMin = new Vector2(0.05f, 0.33f);
            cloudStatusRect.anchorMax = new Vector2(0.95f, 0.44f);
            cloudStatusRect.offsetMin = Vector2.zero;
            cloudStatusRect.offsetMax = Vector2.zero;

            var cloudBg = cloudStatusGo.AddComponent<Image>();
            cloudBg.color = new Color(0.10f, 0.14f, 0.22f, 0.9f);

            var cloudTextGo = new GameObject("Cloud_Text");
            var cloudTextRect = cloudTextGo.AddComponent<RectTransform>();
            cloudTextRect.SetParent(cloudStatusRect, false);
            cloudTextRect.anchorMin = new Vector2(0.04f, 0.1f);
            cloudTextRect.anchorMax = new Vector2(0.96f, 0.9f);
            cloudTextRect.offsetMin = Vector2.zero;
            cloudTextRect.offsetMax = Vector2.zero;

            cloudSyncStatusText = cloudTextGo.AddComponent<Text>();
            cloudSyncStatusText.font = defaultFont;
            cloudSyncStatusText.fontSize = 16;
            cloudSyncStatusText.fontStyle = FontStyle.Bold;
            cloudSyncStatusText.alignment = TextAnchor.MiddleCenter;
            cloudSyncStatusText.color = new Color(1.0f, 0.85f, 0.2f);
            cloudSyncStatusText.text = "â³ Menghubungkan ke Cloud Backend (Supabase/Firestore)...";

            // 10. Auto-redirect Countdown Notice
            var countdownGo = new GameObject("Countdown_Notice");
            var countdownRect = countdownGo.AddComponent<RectTransform>();
            countdownRect.SetParent(cardRect, false);
            countdownRect.anchorMin = new Vector2(0.05f, 0.26f);
            countdownRect.anchorMax = new Vector2(0.95f, 0.31f);
            countdownRect.offsetMin = Vector2.zero;
            countdownRect.offsetMax = Vector2.zero;

            countdownPromptText = countdownGo.AddComponent<Text>();
            countdownPromptText.font = defaultFont;
            countdownPromptText.fontSize = 14;
            countdownPromptText.alignment = TextAnchor.MiddleCenter;
            countdownPromptText.color = new Color(0.8f, 0.8f, 0.8f);
            countdownPromptText.text = "Membuka Google Form kuesioner evaluasi secara otomatis...";

            // 11. Prominent Redirect Button (Primary Action)
            var btnRedirectGo = new GameObject("Button_SurveyRedirect");
            var btnRedirectRect = btnRedirectGo.AddComponent<RectTransform>();
            btnRedirectRect.SetParent(cardRect, false);
            btnRedirectRect.anchorMin = new Vector2(0.12f, 0.12f);
            btnRedirectRect.anchorMax = new Vector2(0.88f, 0.23f);
            btnRedirectRect.offsetMin = Vector2.zero;
            btnRedirectRect.offsetMax = Vector2.zero;

            var btnRedirectImg = btnRedirectGo.AddComponent<Image>();
            btnRedirectImg.color = new Color(0.0f, 0.8f, 0.6f); // Vivid Emerald / Cyan
            surveyRedirectButton = btnRedirectGo.AddComponent<Button>();
            surveyRedirectButton.targetGraphic = btnRedirectImg;
            surveyRedirectButton.onClick.AddListener(OnSurveyButtonClicked);

            var btnRedirectLabelGo = new GameObject("Label");
            var btnRedirectLabelRect = btnRedirectLabelGo.AddComponent<RectTransform>();
            btnRedirectLabelRect.SetParent(btnRedirectRect, false);
            btnRedirectLabelRect.anchorMin = Vector2.zero;
            btnRedirectLabelRect.anchorMax = Vector2.one;
            btnRedirectLabelRect.offsetMin = Vector2.zero;
            btnRedirectLabelRect.offsetMax = Vector2.zero;

            surveyButtonLabel = btnRedirectLabelGo.AddComponent<Text>();
            surveyButtonLabel.font = defaultFont;
            surveyButtonLabel.fontSize = 20;
            surveyButtonLabel.fontStyle = FontStyle.Bold;
            surveyButtonLabel.alignment = TextAnchor.MiddleCenter;
            surveyButtonLabel.color = Color.black;
            surveyButtonLabel.text = "âž” LANJUT KE KUESIONER EVALUASI (GOOGLE FORM)";

            // 12. Bottom Controls (Cancel countdown or return to lobby)
            var cancelBtnGo = new GameObject("Button_CancelCountdown");
            var cancelBtnRect = cancelBtnGo.AddComponent<RectTransform>();
            cancelBtnRect.SetParent(cardRect, false);
            cancelBtnRect.anchorMin = new Vector2(0.12f, 0.03f);
            cancelBtnRect.anchorMax = new Vector2(0.48f, 0.09f);
            cancelBtnRect.offsetMin = Vector2.zero;
            cancelBtnRect.offsetMax = Vector2.zero;

            var cancelImg = cancelBtnGo.AddComponent<Image>();
            cancelImg.color = new Color(0.2f, 0.25f, 0.35f);
            cancelAutoRedirectButton = cancelBtnGo.AddComponent<Button>();
            cancelAutoRedirectButton.targetGraphic = cancelImg;
            cancelAutoRedirectButton.onClick.AddListener(OnCancelCountdownClicked);

            var cancelLabelGo = new GameObject("Label");
            var cancelLabelRect = cancelLabelGo.AddComponent<RectTransform>();
            cancelLabelRect.SetParent(cancelBtnRect, false);
            cancelLabelRect.anchorMin = Vector2.zero;
            cancelLabelRect.anchorMax = Vector2.one;
            cancelLabelRect.offsetMin = Vector2.zero;
            cancelLabelRect.offsetMax = Vector2.zero;

            var cancelLabel = cancelLabelGo.AddComponent<Text>();
            cancelLabel.font = defaultFont;
            cancelLabel.fontSize = 14;
            cancelLabel.alignment = TextAnchor.MiddleCenter;
            cancelLabel.color = Color.white;
            cancelLabel.text = "BATALKAN TIMER OTOMATIS";

            var lobbyBtnGo = new GameObject("Button_ReturnLobby");
            var lobbyBtnRect = lobbyBtnGo.AddComponent<RectTransform>();
            lobbyBtnRect.SetParent(cardRect, false);
            lobbyBtnRect.anchorMin = new Vector2(0.52f, 0.03f);
            lobbyBtnRect.anchorMax = new Vector2(0.88f, 0.09f);
            lobbyBtnRect.offsetMin = Vector2.zero;
            lobbyBtnRect.offsetMax = Vector2.zero;

            var lobbyImg = lobbyBtnGo.AddComponent<Image>();
            lobbyImg.color = new Color(0.2f, 0.25f, 0.35f);
            returnToLobbyButton = lobbyBtnGo.AddComponent<Button>();
            returnToLobbyButton.targetGraphic = lobbyImg;
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);

            var lobbyLabelGo = new GameObject("Label");
            var lobbyLabelRect = lobbyLabelGo.AddComponent<RectTransform>();
            lobbyLabelRect.SetParent(lobbyBtnRect, false);
            lobbyLabelRect.anchorMin = Vector2.zero;
            lobbyLabelRect.anchorMax = Vector2.one;
            lobbyLabelRect.offsetMin = Vector2.zero;
            lobbyLabelRect.offsetMax = Vector2.zero;

            var lobbyLabel = lobbyLabelGo.AddComponent<Text>();
            lobbyLabel.font = defaultFont;
            lobbyLabel.fontSize = 14;
            lobbyLabel.alignment = TextAnchor.MiddleCenter;
            lobbyLabel.color = Color.white;
            lobbyLabel.text = "KEMBALI KE LOBBY";
        }

        public void SetUIVisible(bool visible)
        {
            if (settlementPanelRoot != null)
            {
                settlementPanelRoot.SetActive(visible);
            }
        }

        #endregion

        #region Event Handlers & View Update

        private void OnFinalSettlement(FinalSettlementEvent evt)
        {
            EnsureUIHierarchy();
            PopulateSettlementData(evt);
            SetUIVisible(true);

            // Handle Auto-redirect timer display
            if (TelemetryService.Instance != null && TelemetryService.Instance.AutoRedirectToSurvey)
            {
                StartCoroutine(RunCountdownRoutine(TelemetryService.Instance.AutoRedirectDelaySeconds));
            }
            else
            {
                if (countdownPromptText != null)
                {
                    countdownPromptText.text = "Silakan klik tombol di bawah untuk mengisi kuesioner evaluasi PAM & UX.";
                }
            }
        }

        private void PopulateSettlementData(FinalSettlementEvent evt)
        {
            string userId = "UNKNOWN";
            string groupId = "GROUP_A";
            string groupName = "Kelompok Eksperimen";

            if (ExperimentManager.Instance != null)
            {
                userId = ExperimentManager.Instance.UserId;
                if (ExperimentManager.Instance.CurrentConfig != null)
                {
                    groupId = ExperimentManager.Instance.CurrentConfig.GroupId;
                    groupName = ExperimentManager.Instance.CurrentConfig.GroupName;
                }
            }

            // Title & Subtitle
            if (titleText != null)
            {
                titleText.text = evt.CompletedAllStages
                    ? "â˜… SELAMAT! SEMUA STAGE TELAH DITUNTASKAN â˜…"
                    : "âš” PERTARUNGAN BERAKHIR / SESSION SETTLEMENT âš”";
                titleText.color = evt.CompletedAllStages ? new Color(1.0f, 0.85f, 0.2f) : new Color(1.0f, 0.4f, 0.4f);
            }

            if (subtitleText != null)
            {
                subtitleText.text = evt.CompletedAllStages
                    ? "Status: VICTORY - Cyber Baron Dikalahkan!"
                    : $"Status: SELESAI ({evt.MatchesWon}/{evt.StagesCompleted} Stage Menang, Koin Emas: {evt.TotalGoldEarned})";
            }

            if (respondentInfoText != null)
            {
                respondentInfoText.text = $"ID Responden: {userId} | Kelompok: {groupId} ({groupName}) | Waktu Bermain: {evt.SessionDurationSeconds:F0}s";
            }

            // ARPU Summary
            float adRev = 0f;
            float iapRev = 0f;
            float arpu = 0f;
            float remainingBudget = 20000.0f;

            if (TelemetryService.Instance != null && TelemetryService.Instance.CurrentPayload != null)
            {
                var mon = TelemetryService.Instance.CurrentPayload.monetization;
                adRev = mon.total_ad_revenue;
                iapRev = mon.total_iap_revenue;
                arpu = mon.total_user_arpu;
                remainingBudget = mon.remaining_budget;
            }

            if (arpuSummaryText != null)
            {
                arpuSummaryText.text =
                    $"ðŸ’° ANALISIS ARPU EKONOMI:\n" +
                    $"â€¢ Pendapatan Ads: Rp {adRev:N0}\n" +
                    $"â€¢ Transaksi IAP: Rp {iapRev:N0}\n" +
                    $"â€¢ TOTAL ARPU: Rp {arpu:N0}\n" +
                    $"â€¢ Sisa Kuota Budget: Rp {remainingBudget:N0}";
            }

            // Combat Reflex Summary
            int parried = 0;
            int dodged = 0;
            int failedParry = 0;
            int deflectedDmg = 0;
            int shotsFired = 0;

            if (TelemetryService.Instance != null && TelemetryService.Instance.CurrentPayload != null)
            {
                var combat = TelemetryService.Instance.CurrentPayload.fps_combat_performance;
                parried = combat.total_yellow_bullets_parried;
                dodged = combat.total_red_bullets_dodged;
                failedParry = combat.total_unparryable_fails;
                deflectedDmg = combat.total_damage_deflected;
                shotsFired = combat.total_aim_shots_fired;
            }

            if (combatSummaryText != null)
            {
                combatSummaryText.text =
                    $"âš” STATISTIK REFLEKS COMBAT:\n" +
                    $"â€¢ Tangkisan Parry [E]: {parried}x Berhasil\n" +
                    $"â€¢ Hindaran Dodge [Q]: {dodged}x Berhasil\n" +
                    $"â€¢ Salah Tekan E saat Red: {failedParry}x\n" +
                    $"â€¢ Deflected Damage: {deflectedDmg} DMG\n" +
                    $"â€¢ Tembakan Auto Lock-on: {shotsFired}x";
            }
        }

        private void OnTelemetrySyncStarted(TelemetrySyncStartedEvent evt)
        {
            if (cloudSyncStatusText != null)
            {
                cloudSyncStatusText.text = "â³ Menyinkronkan data sesi ke Cloud Backend (Supabase / Firestore)...";
                cloudSyncStatusText.color = new Color(1.0f, 0.85f, 0.2f); // Yellow
            }
        }

        private void OnTelemetrySyncCompleted(TelemetrySyncCompletedEvent evt)
        {
            if (cloudSyncStatusText == null) return;

            if (evt.IsSuccess)
            {
                cloudSyncStatusText.text = "âœ“ Telemetri Berhasil Tersinkronisasi ke Cloud Backend!";
                cloudSyncStatusText.color = new Color(0.2f, 1.0f, 0.5f); // Neon Green
            }
            else if (evt.IsSavedToLocalFallback)
            {
                cloudSyncStatusText.text = "âš  Mode Offline: Data sesi berhasil diamankan di pending_logs.json";
                cloudSyncStatusText.color = new Color(1.0f, 0.7f, 0.2f); // Orange Gold
            }
            else
            {
                cloudSyncStatusText.text = $"âœ• Gagal Sinkronisasi: {evt.Message}";
                cloudSyncStatusText.color = new Color(1.0f, 0.4f, 0.4f); // Red
            }
        }

        private void OnSurveyRequested(SurveyRequestedEvent evt)
        {
            if (countdownPromptText != null)
            {
                countdownPromptText.text = "âœ“ Browser kuesioner evaluasi telah dibuka di jendela baru!";
                countdownPromptText.color = new Color(0.4f, 1.0f, 0.6f);
            }
        }

        #endregion

        #region Countdown & Button Actions

        private IEnumerator RunCountdownRoutine(float delaySeconds)
        {
            _remainingCountdown = delaySeconds;

            while (_remainingCountdown > 0f)
            {
                if (countdownPromptText != null)
                {
                    countdownPromptText.text = $"Membuka Google Form secara otomatis dalam {_remainingCountdown:F0} detik...";
                }

                if (surveyButtonLabel != null)
                {
                    surveyButtonLabel.text = $"âž” LANJUT KE KUESIONER EVALUASI ({_remainingCountdown:F0}s)";
                }

                yield return new WaitForSeconds(1.0f);
                _remainingCountdown -= 1.0f;
            }

            if (surveyButtonLabel != null)
            {
                surveyButtonLabel.text = "âž” BUKA KUESIONER EVALUASI (GOOGLE FORM)";
            }

            Debug.Log("[SettlementSurveyView] Auto-redirect countdown expired. Executing survey redirection.");
            if (TelemetryService.Instance != null)
            {
                TelemetryService.Instance.TriggerSurveyRedirect();
            }
        }

        private void OnSurveyButtonClicked()
        {
            StopAllCoroutines();

            if (countdownPromptText != null)
            {
                countdownPromptText.text = "Membuka browser kuesioner evaluasi...";
            }

            if (TelemetryService.Instance != null)
            {
                TelemetryService.Instance.TriggerSurveyRedirect();
            }
        }

        private void OnCancelCountdownClicked()
        {
            StopAllCoroutines();

            if (TelemetryService.Instance != null)
            {
                TelemetryService.Instance.CancelAutoRedirect();
            }

            if (countdownPromptText != null)
            {
                countdownPromptText.text = "Timer otomatis dibatalkan. Silakan klik tombol saat Anda siap mengisi kuesioner.";
            }

            if (surveyButtonLabel != null)
            {
                surveyButtonLabel.text = "âž” LANJUT KE KUESIONER EVALUASI (GOOGLE FORM)";
            }
        }

        private void OnReturnToLobbyClicked()
        {
            StopAllCoroutines();
            SetUIVisible(false);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToLobby();
            }
        }

        #endregion
    }
}
