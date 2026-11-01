using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Telemetry
{
    /// <summary>
    /// Telemetry & Cloud Synchronization Service for Lucky Duelist: Cyber Roulette.
    /// Collects session gameplay metrics, FPS reflex combat performance, and monetization data,
    /// then sends payload JSON via UnityWebRequest HTTP POST to Firebase Firestore / Supabase REST API
    /// with automatic local offline fallback to Application.persistentDataPath/pending_logs.json.
    /// Also manages automatic survey redirection at session conclusion.
    /// </summary>
    public class TelemetryService : MonoBehaviour
    {
        public static TelemetryService Instance { get; private set; }

        [Header("Cloud Backend Configuration")]
        [SerializeField] private CloudBackendType targetBackend = CloudBackendType.SupabaseRest;
        [Tooltip("REST API endpoint URL for telemetry ingestion.")]
        [SerializeField] private string endpointUrl = "https://your-project.supabase.co/rest/v1/telemetry_sessions";
        [Tooltip("API Key / Authorization Token (e.g. Supabase anon key or Firebase Auth token).")]
        [SerializeField] private string apiKey = "";
        [Tooltip("HTTP request timeout in seconds.")]
        [SerializeField] private float requestTimeoutSeconds = 10.0f;

        [Header("Offline Local Fallback")]
        [SerializeField] private bool offlineFallbackEnabled = true;
        [Tooltip("Auto-flush queued pending offline logs on service startup if network is available.")]
        [SerializeField] private bool autoFlushPendingOnStart = true;

        [Header("Survey Questionnaire Redirection")]
        [Tooltip("Automatically open Google Form evaluation questionnaire after session settlement.")]
        [SerializeField] private bool autoRedirectToSurvey = true;
        [Tooltip("Delay in seconds before automatic redirection opens the browser.")]
        [SerializeField] private float autoRedirectDelaySeconds = 5.0f;

        // Active Session Data
        public TelemetrySessionPayload CurrentPayload { get; private set; }
        public MatchDetailRecord CurrentStageDetail { get; private set; }
        public bool IsSyncing { get; private set; } = false;
        public bool LastSyncSuccessful { get; private set; } = false;
        public string LastSyncMessage { get; private set; } = string.Empty;

        // Custom path override for testing
        private string _customOfflineFilePath = null;
        private Coroutine _autoRedirectCoroutine = null;
        private bool _isSubscribed = false;

        public const string DEFAULT_PENDING_LOGS_FILENAME = "pending_logs.json";

        // Public Properties for convenient inspector & script access
        public CloudBackendType TargetBackend
        {
            get => targetBackend;
            set => targetBackend = value;
        }

        public string EndpointUrl
        {
            get => endpointUrl;
            set => endpointUrl = value;
        }

        public string ApiKey
        {
            get => apiKey;
            set => apiKey = value;
        }

        public float RequestTimeoutSeconds
        {
            get => requestTimeoutSeconds;
            set => requestTimeoutSeconds = value;
        }

        public bool OfflineFallbackEnabled
        {
            get => offlineFallbackEnabled;
            set => offlineFallbackEnabled = value;
        }

        public bool AutoRedirectToSurvey
        {
            get => autoRedirectToSurvey;
            set => autoRedirectToSurvey = value;
        }

        public float AutoRedirectDelaySeconds
        {
            get => autoRedirectDelaySeconds;
            set => autoRedirectDelaySeconds = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeNewSession();
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
            Debug.Log($"<color=cyan>[TelemetryService] Offline Fallback File Path: {GetOfflineFilePath()}</color>");

            if (autoFlushPendingOnStart && GetPendingLogsCount() > 0)
            {
                StartCoroutine(FlushPendingLogsRoutine());
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        #region Session Lifecycle & Initialization

        /// <summary>
        /// Initializes a fresh telemetry session payload, resolving User ID and Group from ExperimentManager.
        /// </summary>
        public void InitializeNewSession()
        {
            CurrentPayload = new TelemetrySessionPayload();

            string userId = "UNKNOWN_USER";
            string groupId = "GROUP_A";
            float initialBudget = 20000.0f;
            float currentWallet = 20000.0f;

            if (ExperimentManager.Instance != null)
            {
                userId = ExperimentManager.Instance.UserId;
                if (ExperimentManager.Instance.CurrentConfig != null)
                {
                    groupId = ExperimentManager.Instance.CurrentConfig.GroupId;
                    initialBudget = ExperimentManager.Instance.CurrentConfig.InitialWalletBudget;
                }
                currentWallet = ExperimentManager.Instance.CurrentWalletBalance;
            }

            CurrentPayload.user_id = userId;
            CurrentPayload.group_id = groupId;
            CurrentPayload.monetization.initial_budget = initialBudget;
            CurrentPayload.monetization.remaining_budget = currentWallet;

            CurrentStageDetail = new MatchDetailRecord(1);
            CurrentPayload.match_details.Add(CurrentStageDetail);

            Debug.Log($"[TelemetryService] Session initialized: User={userId}, Group={groupId}, SessionId={CurrentPayload.session.session_id}");
        }

        /// <summary>
        /// Resets all metrics and state to clean defaults (useful for testing or starting a new run).
        /// </summary>
        public void ResetSessionForTesting()
        {
            CancelAutoRedirect();
            InitializeNewSession();
            SubscribeEvents();
            LastSyncSuccessful = false;
            LastSyncMessage = string.Empty;
            IsSyncing = false;
        }

        #endregion

        #region Event Subscriptions & Data Collection

        public void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;

            EventBus.Subscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Subscribe<IAPTransactionCompletedEvent>(OnIAPTransactionCompleted);
            EventBus.Subscribe<AdImpressionCompletedEvent>(OnAdImpressionCompleted);
            EventBus.Subscribe<SlotRespinTriggeredEvent>(OnSlotRespinTriggered);
            EventBus.Subscribe<AimShotFiredEvent>(OnAimShotFired);
            EventBus.Subscribe<DefenseResolvedEvent>(OnDefenseResolved);
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Subscribe<StageStartedEvent>(OnStageStarted);
            EventBus.Subscribe<StageCompletedEvent>(OnStageCompleted);
            EventBus.Subscribe<FinalSettlementEvent>(OnFinalSettlement);
        }

        public void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;

            EventBus.Unsubscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
            EventBus.Unsubscribe<IAPTransactionCompletedEvent>(OnIAPTransactionCompleted);
            EventBus.Unsubscribe<AdImpressionCompletedEvent>(OnAdImpressionCompleted);
            EventBus.Unsubscribe<SlotRespinTriggeredEvent>(OnSlotRespinTriggered);
            EventBus.Unsubscribe<AimShotFiredEvent>(OnAimShotFired);
            EventBus.Unsubscribe<DefenseResolvedEvent>(OnDefenseResolved);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
            EventBus.Unsubscribe<StageCompletedEvent>(OnStageCompleted);
            EventBus.Unsubscribe<FinalSettlementEvent>(OnFinalSettlement);
        }

        private void OnExperimentGroupAssigned(ExperimentGroupAssignedEvent evt)
        {
            if (CurrentPayload == null) return;
            CurrentPayload.user_id = evt.UserId;
            CurrentPayload.group_id = evt.GroupId;
            if (ExperimentManager.Instance != null)
            {
                CurrentPayload.monetization.remaining_budget = ExperimentManager.Instance.CurrentWalletBalance;
            }
            Debug.Log($"[TelemetryService] User & Group updated: {evt.UserId} ({evt.GroupId})");
        }

        private void OnIAPTransactionCompleted(IAPTransactionCompletedEvent evt)
        {
            if (CurrentPayload == null) return;

            var record = new PurchasedItemRecord(
                evt.ItemId,
                evt.ItemType.ToString().ToUpperInvariant(),
                evt.AmountPaid,
                evt.Timestamp
            );

            CurrentPayload.monetization.purchased_items.Add(record);
            CurrentPayload.monetization.remaining_budget = evt.RemainingWalletBalance;
            CurrentPayload.monetization.RecalculateTotals();

            Debug.Log($"[TelemetryService] Recorded IAP Transaction: {evt.ItemId} (Rp {evt.AmountPaid:N0}). Total IAP: Rp {CurrentPayload.monetization.total_iap_revenue:N0}");
        }

        private void OnAdImpressionCompleted(AdImpressionCompletedEvent evt)
        {
            if (CurrentPayload == null) return;

            CurrentPayload.monetization.ad_impressions_count++;
            CurrentPayload.monetization.total_ad_revenue += evt.RevenueEarned;

            switch (evt.Placement)
            {
                case AdPlacementType.RespinSlot:
                    CurrentPayload.monetization.ad_impressions_breakdown.respin_slot++;
                    if (CurrentStageDetail != null) CurrentStageDetail.ad_respin_count++;
                    break;

                case AdPlacementType.ReviveMatch:
                    CurrentPayload.monetization.ad_impressions_breakdown.revive_match++;
                    if (CurrentStageDetail != null) CurrentStageDetail.ad_revive_used = true;
                    break;

                case AdPlacementType.DoubleGold:
                    CurrentPayload.monetization.ad_impressions_breakdown.double_gold++;
                    break;
            }

            CurrentPayload.monetization.RecalculateTotals();

            Debug.Log($"[TelemetryService] Recorded Ad Impression: {evt.Placement} (+Rp {evt.RevenueEarned:N0}). Total Ad Rev: Rp {CurrentPayload.monetization.total_ad_revenue:N0}, ARPU: Rp {CurrentPayload.monetization.total_user_arpu:N0}");
        }

        private void OnSlotRespinTriggered(SlotRespinTriggeredEvent evt)
        {
            if (evt.ViaRewardedAd && CurrentStageDetail != null)
            {
                // Handled in AdImpressionCompletedEvent if triggered via DummyAdPlayer
            }
        }

        private void OnAimShotFired(AimShotFiredEvent evt)
        {
            if (CurrentPayload == null) return;

            // In Lucky Duelist FPS mechanic, lock-on shot auto-fires upon 100% (1.0s) vital center lock
            CurrentPayload.fps_combat_performance.total_aim_shots_fired++;
            CurrentPayload.fps_combat_performance.total_aim_lockons_successful++;
        }

        private void OnDefenseResolved(DefenseResolvedEvent evt)
        {
            if (CurrentPayload == null) return;

            switch (evt.Result)
            {
                case DefenseResult.DodgeSuccess:
                    CurrentPayload.fps_combat_performance.total_red_bullets_dodged++;
                    if (CurrentStageDetail != null) CurrentStageDetail.dodge_count++;
                    break;

                case DefenseResult.ParrySuccess:
                    CurrentPayload.fps_combat_performance.total_yellow_bullets_parried++;
                    CurrentPayload.fps_combat_performance.total_damage_deflected += evt.DamageToEnemy;
                    if (CurrentStageDetail != null) CurrentStageDetail.parry_count++;
                    break;

                case DefenseResult.ParryFailedRed:
                    CurrentPayload.fps_combat_performance.total_unparryable_fails++;
                    if (CurrentStageDetail != null) CurrentStageDetail.unparryable_fails++;
                    break;
            }
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            if (CurrentStageDetail != null)
            {
                CurrentStageDetail.ad_revive_used = true;
            }
        }

        private void OnStageStarted(StageStartedEvent evt)
        {
            if (CurrentPayload == null) return;

            // Check if stage already has a record
            MatchDetailRecord existing = CurrentPayload.match_details.Find(m => m.stage == evt.StageNumber);
            if (existing != null)
            {
                CurrentStageDetail = existing;
            }
            else
            {
                CurrentStageDetail = new MatchDetailRecord(evt.StageNumber);
                CurrentPayload.match_details.Add(CurrentStageDetail);
            }

            Debug.Log($"[TelemetryService] Tracking Stage {evt.StageNumber} ({evt.StageName}).");
        }

        private void OnStageCompleted(StageCompletedEvent evt)
        {
            if (CurrentStageDetail != null)
            {
                CurrentStageDetail.outcome = evt.IsVictory ? "WIN" : "LOSS";
                CurrentStageDetail.turns = evt.TurnsTaken;
            }
        }

        private void OnFinalSettlement(FinalSettlementEvent evt)
        {
            if (CurrentPayload == null) return;

            // Finalize session metrics
            CurrentPayload.session.play_duration_sec = evt.SessionDurationSeconds;
            CurrentPayload.session.matches_completed = evt.StagesCompleted;
            CurrentPayload.session.matches_won = evt.MatchesWon;
            CurrentPayload.session.completed_stage_3 = evt.CompletedAllStages;
            CurrentPayload.session.soft_currency_gold = evt.TotalGoldEarned;

            // Ensure budget and ARPU are finalized
            if (ExperimentManager.Instance != null)
            {
                CurrentPayload.monetization.remaining_budget = ExperimentManager.Instance.CurrentWalletBalance;
            }
            CurrentPayload.monetization.RecalculateTotals();

            Debug.Log($"<color=yellow>[TelemetryService] Run Finished! ARPU: Rp {CurrentPayload.monetization.total_user_arpu:N0} (Ad: Rp {CurrentPayload.monetization.total_ad_revenue:N0}, IAP: Rp {CurrentPayload.monetization.total_iap_revenue:N0}). Initiating Cloud Sync...</color>");

            // Trigger cloud sync
            SyncCurrentSession((success, message) =>
            {
                Debug.Log($"[TelemetryService] Post-Settlement Cloud Sync finished. Success: {success}, Message: {message}");

                if (autoRedirectToSurvey)
                {
                    StartAutoRedirectCountdown(autoRedirectDelaySeconds);
                }
            });
        }

        #endregion

        #region Cloud Synchronization & Networking

        /// <summary>
        /// Sends the current session payload to the configured cloud backend.
        /// If offline or if network request fails, automatically stores payload in offline local fallback.
        /// </summary>
        public void SyncCurrentSession(Action<bool, string> onComplete = null)
        {
            if (CurrentPayload == null)
            {
                onComplete?.Invoke(false, "No active payload to synchronize.");
                return;
            }

            // Check if endpoint is placeholder or unconfigured
            bool isPlaceholder = string.IsNullOrEmpty(endpointUrl) || endpointUrl.Contains("your-project.supabase.co") || endpointUrl.Contains("YOUR_PROJECT");

            if (isPlaceholder)
            {
                string sessionId = CurrentPayload.session != null ? CurrentPayload.session.session_id : Guid.NewGuid().ToString();
                EventBus.Publish(new TelemetrySyncStartedEvent(sessionId, endpointUrl, false));

                Debug.LogWarning("[TelemetryService] Endpoint URL is placeholder. Preserving payload in offline local fallback.");
                bool saved = false;
                if (offlineFallbackEnabled)
                {
                    saved = SaveToOfflineFallback(CurrentPayload);
                }

                IsSyncing = false;
                LastSyncSuccessful = false;
                LastSyncMessage = "Endpoint is unconfigured placeholder. Saved locally.";

                EventBus.Publish(new TelemetrySyncCompletedEvent(
                    sessionId,
                    isSuccess: false,
                    isSavedToLocalFallback: saved,
                    responseCode: 0,
                    message: LastSyncMessage,
                    payloadJson: CurrentPayload.ToJson(true)
                ));

                onComplete?.Invoke(false, LastSyncMessage);
                return;
            }

            StartCoroutine(SendPayloadRoutine(CurrentPayload, (success, msg) =>
            {
                LastSyncSuccessful = success;
                LastSyncMessage = msg;
                onComplete?.Invoke(success, msg);
            }));
        }

        private IEnumerator SendPayloadRoutine(TelemetrySessionPayload payload, Action<bool, string> onComplete)
        {
            IsSyncing = true;
            string sessionId = payload.session != null ? payload.session.session_id : Guid.NewGuid().ToString();
            string bodyJson = TelemetryPayloadAdapter.FormatPayload(payload, targetBackend);

            EventBus.Publish(new TelemetrySyncStartedEvent(sessionId, endpointUrl, false));
            Debug.Log($"[TelemetryService] Sending HTTP POST to {endpointUrl} ({targetBackend})...\nPayload preview: {payload.user_id} / ARPU: {payload.monetization.total_user_arpu}");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);

            using (UnityWebRequest request = new UnityWebRequest(endpointUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.RoundToInt(requestTimeoutSeconds);

                // Set headers based on backend provider
                request.SetRequestHeader("Content-Type", "application/json");

                if (targetBackend == CloudBackendType.SupabaseRest)
                {
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("apikey", apiKey);
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    request.SetRequestHeader("Prefer", "return=minimal");
                }
                else if (targetBackend == CloudBackendType.FirestoreRest)
                {
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                }

                yield return request.SendWebRequest();

                bool isSuccess = false;
                string message = string.Empty;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    isSuccess = true;
                    message = $"Cloud Sync Success! HTTP {request.responseCode}";
                    Debug.Log($"<color=green>[TelemetryService] {message}</color>");
                }
                else
                {
                    isSuccess = false;
                    message = $"Cloud Sync Failed: {request.error} (HTTP {request.responseCode})";
                    Debug.LogWarning($"[TelemetryService] {message}. Routing to local fallback...");

                    if (offlineFallbackEnabled)
                    {
                        SaveToOfflineFallback(payload);
                    }
                }

                IsSyncing = false;
                LastSyncSuccessful = isSuccess;
                LastSyncMessage = message;

                EventBus.Publish(new TelemetrySyncCompletedEvent(
                    sessionId,
                    isSuccess: isSuccess,
                    isSavedToLocalFallback: !isSuccess && offlineFallbackEnabled,
                    responseCode: request.responseCode,
                    message: message,
                    payloadJson: payload.ToJson(true)
                ));

                onComplete?.Invoke(isSuccess, message);
            }
        }

        #endregion

        #region Offline Local Fallback Storage

        /// <summary>
        /// Gets the resolved filepath for pending offline logs (Application.persistentDataPath/pending_logs.json).
        /// </summary>
        public string GetOfflineFilePath()
        {
            if (!string.IsNullOrEmpty(_customOfflineFilePath))
            {
                return _customOfflineFilePath;
            }

            return Path.Combine(Application.persistentDataPath, DEFAULT_PENDING_LOGS_FILENAME);
        }

        /// <summary>
        /// Sets a custom offline file path (used in unit tests to avoid touching persistent user data).
        /// </summary>
        public void SetOfflineFilePathForTesting(string path)
        {
            _customOfflineFilePath = path;
        }

        /// <summary>
        /// Saves a telemetry session payload to the local offline fallback queue file.
        /// </summary>
        public bool SaveToOfflineFallback(TelemetrySessionPayload payload)
        {
            if (payload == null) return false;

            try
            {
                string path = GetOfflineFilePath();
                PendingTelemetryQueue queue = LoadPendingLogs();
                queue.pendingSessions.Add(payload);

                string json = queue.ToJson(prettyPrint: true);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json, Encoding.UTF8);
                Debug.Log($"[TelemetryService] Successfully saved session to offline local fallback: {path} (Queue Count: {queue.pendingSessions.Count})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryService] Failed to save to offline local fallback: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads all queued offline pending sessions from disk.
        /// </summary>
        public PendingTelemetryQueue LoadPendingLogs()
        {
            string path = GetOfflineFilePath();
            if (!File.Exists(path))
            {
                return new PendingTelemetryQueue();
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return PendingTelemetryQueue.FromJson(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryService] Error reading pending logs from {path}: {ex.Message}");
                return new PendingTelemetryQueue();
            }
        }

        /// <summary>
        /// Returns the count of pending offline sessions waiting for cloud sync.
        /// </summary>
        public int GetPendingLogsCount()
        {
            PendingTelemetryQueue queue = LoadPendingLogs();
            return queue != null && queue.pendingSessions != null ? queue.pendingSessions.Count : 0;
        }

        /// <summary>
        /// Clears all pending offline logs from disk.
        /// </summary>
        public void ClearPendingLogs()
        {
            try
            {
                string path = GetOfflineFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[TelemetryService] Cleared offline pending logs file: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryService] Failed to clear pending logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine that attempts to flush and upload all pending offline sessions sequentially.
        /// </summary>
        public IEnumerator FlushPendingLogsRoutine(Action<int, int> onComplete = null)
        {
            PendingTelemetryQueue queue = LoadPendingLogs();
            if (queue == null || queue.pendingSessions.Count == 0)
            {
                onComplete?.Invoke(0, 0);
                yield break;
            }

            int initialCount = queue.pendingSessions.Count;
            int uploadedCount = 0;
            List<TelemetrySessionPayload> remaining = new List<TelemetrySessionPayload>(queue.pendingSessions);

            Debug.Log($"[TelemetryService] Flushing {initialCount} pending offline sessions to Cloud Backend...");

            for (int i = 0; i < remaining.Count; i++)
            {
                var payload = remaining[i];
                bool syncDone = false;
                bool syncSuccess = false;

                yield return StartCoroutine(SendPayloadRoutine(payload, (success, msg) =>
                {
                    syncDone = true;
                    syncSuccess = success;
                }));

                while (!syncDone) yield return null;

                if (syncSuccess)
                {
                    uploadedCount++;
                    remaining.RemoveAt(i);
                    i--;

                    queue.pendingSessions = remaining;
                    string path = GetOfflineFilePath();
                    if (remaining.Count > 0)
                    {
                        File.WriteAllText(path, queue.ToJson(true), Encoding.UTF8);
                    }
                    else
                    {
                        ClearPendingLogs();
                    }
                }
                else
                {
                    Debug.LogWarning("[TelemetryService] Pausing offline queue flush due to network interruption.");
                    break;
                }
            }

            Debug.Log($"[TelemetryService] Flush finished: {uploadedCount}/{initialCount} sessions uploaded.");
            onComplete?.Invoke(uploadedCount, remaining.Count);
        }

        /// <summary>
        /// Context menu helper to open the local persistent data folder in Windows Explorer.
        /// </summary>
        [ContextMenu("Open Persistent Data Folder (Explorer)")]
        public void OpenPersistentDataFolder()
        {
            string folder = Application.persistentDataPath;
            Debug.Log($"<color=cyan>[TelemetryService] Opening Persistent Data Folder: {folder}</color>");
            Application.OpenURL("file://" + folder);
        }

        /// <summary>
        /// Context menu helper to immediately test saving the current session payload to offline fallback.
        /// </summary>
        [ContextMenu("Test Save Current Session to Offline Fallback")]
        public void TestSaveCurrentSessionToOfflineFallback()
        {
            if (CurrentPayload == null) InitializeNewSession();
            bool saved = SaveToOfflineFallback(CurrentPayload);
            Debug.Log($"<color=yellow>[TelemetryService] Test save result: {saved}. Total pending logs count: {GetPendingLogsCount()} at {GetOfflineFilePath()}</color>");
        }

        #endregion

        #region Survey Redirection

        /// <summary>
        /// Immediately triggers browser redirect to Google Form PAM & UX survey questionnaire.
        /// Automatically populates participant User ID and Group ID in query parameters.
        /// </summary>
        public void TriggerSurveyRedirect()
        {
            CancelAutoRedirect();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OpenSurveyForm();
            }
            else
            {
                string userId = CurrentPayload != null ? CurrentPayload.user_id : "UNKNOWN_USER";
                string groupId = CurrentPayload != null ? CurrentPayload.group_id : "GROUP_A";
                string fallbackUrl = $"https://docs.google.com/forms/d/e/1FAIpQLSeAguN6wn9iFWJWSneScYFAChTlFlVmcKC7GCOO9yXqQjny3Q/viewform?usp=pp_url&entry.1419689152={UnityWebRequest.EscapeURL(userId)}&entry.1387777410={UnityWebRequest.EscapeURL(groupId)}";
                Application.OpenURL(fallbackUrl);
                EventBus.Publish(new SurveyRequestedEvent(fallbackUrl, userId, groupId));
            }
        }

        /// <summary>
        /// Starts a countdown timer that will automatically redirect to the Google Form questionnaire.
        /// </summary>
        public void StartAutoRedirectCountdown(float delaySeconds, Action<float> onTick = null)
        {
            CancelAutoRedirect();
            _autoRedirectCoroutine = StartCoroutine(AutoRedirectRoutine(delaySeconds, onTick));
        }

        /// <summary>
        /// Cancels any active auto-redirect countdown.
        /// </summary>
        public void CancelAutoRedirect()
        {
            if (_autoRedirectCoroutine != null)
            {
                StopCoroutine(_autoRedirectCoroutine);
                _autoRedirectCoroutine = null;
            }
        }

        private IEnumerator AutoRedirectRoutine(float delaySeconds, Action<float> onTick)
        {
            float elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                float remaining = Mathf.Max(0f, delaySeconds - elapsed);
                onTick?.Invoke(remaining);
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            onTick?.Invoke(0f);
            Debug.Log("[TelemetryService] Auto-redirect countdown expired. Redirecting to evaluation questionnaire...");
            TriggerSurveyRedirect();
        }

        #endregion
    }
}
