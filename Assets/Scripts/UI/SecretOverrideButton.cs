using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LuckyDuelist.Core;
using LuckyDuelist.Experiment;
using LuckyDuelist.Monetization;

namespace LuckyDuelist.UI
{
    /// <summary>
    /// Researcher Secret Override Trigger & Debug Helper.
    /// Can be attached to any UI element (e.g. Version label, Logo) to detect 5 taps.
    /// Also provides an OnGUI debug panel for researchers during playtests.
    /// </summary>
    public class SecretOverrideButton : MonoBehaviour, IPointerClickHandler
    {
        [Header("Secret Tap Settings")]
        [Tooltip("If true, automatically hooks into standard UI Button on this GameObject")]
        [SerializeField] private bool hookToButtonComponent = true;

        [Header("Researcher Debug Overlay")]
        [SerializeField] private bool enableDebugOverlay = true;
        [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F12;

        private bool _isOverlayVisible = false;

        private void Awake()
        {
            if (hookToButtonComponent && TryGetComponent<Button>(out var button))
            {
                button.onClick.AddListener(OnTapRegistered);
            }
        }

        private void Update()
        {
            if (enableDebugOverlay && Input.GetKeyDown(toggleOverlayKey))
            {
                _isOverlayVisible = !_isOverlayVisible;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnTapRegistered();
        }

        /// <summary>
        /// Forwards tap event to ExperimentManager's 5-tap tracker.
        /// </summary>
        public void OnTapRegistered()
        {
            if (ExperimentManager.Instance != null)
            {
                ExperimentManager.Instance.RegisterTap();
            }
            else
            {
                Debug.LogWarning("[SecretOverrideButton] ExperimentManager.Instance is null.");
            }
        }

        private void OnGUI()
        {
            if (!enableDebugOverlay || !_isOverlayVisible) return;

            // Researcher OnGUI HUD
            int boxWidth = 340;
            int boxHeight = 560;
            int margin = 10;
            Rect panelRect = new Rect(Screen.width - boxWidth - margin, margin, boxWidth, boxHeight);

            GUI.Box(panelRect, "<b>RESEARCHER DEBUG OVERLAY (F12)</b>");
            GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 25, boxWidth - 20, boxHeight - 35));

            if (ExperimentManager.Instance != null)
            {
                GUILayout.Label($"<b>User ID:</b> {ExperimentManager.Instance.UserId}");
                GUILayout.Label($"<b>Group:</b> <color=yellow>{ExperimentManager.Instance.CurrentConfig.GroupId}</color> ({ExperimentManager.Instance.CurrentConfig.GroupName})");
                GUILayout.Label($"<b>Store IAP:</b> {(ExperimentManager.Instance.IsIAPStoreEnabled ? "OPEN" : "HIDDEN")}");
                GUILayout.Label($"<b>Rewarded Ads:</b> {(ExperimentManager.Instance.IsAdsEnabled ? "ACTIVE" : "DISABLED")}");
                GUILayout.Label($"<b>Wallet Balance:</b> Rp {ExperimentManager.Instance.CurrentWalletBalance:N0}");

                GUILayout.Space(5);
                GUILayout.Label("<b>Force Switch Group:</b>");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Group A (Ads)"))
                {
                    ExperimentManager.Instance.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
                }
                if (GUILayout.Button("Group B (IAP)"))
                {
                    ExperimentManager.Instance.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
                }
                if (GUILayout.Button("Group C (Hybrid)"))
                {
                    ExperimentManager.Instance.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Cycle Group (5-Tap Emulation)"))
                {
                    ExperimentManager.Instance.TriggerSecretOverrideCycle();
                }

                if (GUILayout.Button("Reset Participant Data (Fresh Session)"))
                {
                    ExperimentManager.Instance.ResetExperimentData();
                }
            }

            GUILayout.Space(10);
            if (GameManager.Instance != null)
            {
                GUILayout.Label($"<b>Game State:</b> <color=cyan>{GameManager.Instance.CurrentState}</color>");
                GUILayout.Label($"<b>Current Stage:</b> {GameManager.Instance.CurrentStageIndex}/3");
                GUILayout.Label($"<b>Soft Gold:</b> {GameManager.Instance.SoftCurrencyGold}");
                GUILayout.Label($"<b>Matches Completed:</b> {GameManager.Instance.MatchesCompleted} (Won: {GameManager.Instance.MatchesWon})");

                GUILayout.Space(5);
                GUILayout.Label("<b>State Flow Shortcuts:</b>");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Lobby"))
                {
                    GameManager.Instance.ReturnToLobby();
                }
                if (GUILayout.Button("Store"))
                {
                    GameManager.Instance.OpenStore();
                }
                if (GUILayout.Button("Start Run"))
                {
                    GameManager.Instance.StartNewRun();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Simulate Win"))
                {
                    GameManager.Instance.OnStageCompleted(isVictory: true, turnsTaken: 4, playerRemainingHp: 80, botRemainingHp: 0);
                }
                if (GUILayout.Button("Simulate Loss"))
                {
                    GameManager.Instance.OnPlayerDefeated();
                }
                if (GUILayout.Button("Survey"))
                {
                    GameManager.Instance.OpenSurveyForm();
                }
                GUILayout.EndHorizontal();

                if (GameManager.Instance.CurrentState == GameState.MatchSettlement)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Claim 1x Gold"))
                    {
                        GameManager.Instance.ClaimSettlementReward(doubleRewardWithAd: false);
                    }
                    if (GUILayout.Button("Claim 2x Gold (Ad)"))
                    {
                        GameManager.Instance.ClaimSettlementReward(doubleRewardWithAd: true);
                    }
                    if (GUILayout.Button("Next Stage"))
                    {
                        GameManager.Instance.ProceedToNextStage();
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Monetization & Ads (Chat 5):</b>");
            if (DummyAdPlayer.Instance != null)
            {
                GUILayout.Label($"<b>Ad Impressions:</b> {DummyAdPlayer.Instance.TotalAdImpressions} (Rp {DummyAdPlayer.Instance.TotalAdRevenue:N0})");
                if (DummyAdPlayer.Instance.IsPlayingAd)
                {
                    GUILayout.Label($"<color=yellow>Ad Playing: {DummyAdPlayer.Instance.RemainingCountdown:F1}s left</color>");
                    if (GUILayout.Button("Fast-Forward / Complete Ad Now"))
                    {
                        DummyAdPlayer.Instance.CompleteAdInstantlyForTesting();
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Test Revive Ad"))
                    {
                        DummyAdPlayer.Instance.PlayReviveAd();
                    }
                    if (GUILayout.Button("Test 2x Gold Ad"))
                    {
                        DummyAdPlayer.Instance.PlayDoubleGoldAd();
                    }
                    GUILayout.EndHorizontal();
                }
            }

            if (VirtualIAPStore.Instance != null)
            {
                GUILayout.Label($"<b>IAP Store Revenue:</b> Rp {VirtualIAPStore.Instance.TotalIAPRevenue:N0} (Tx: {VirtualIAPStore.Instance.TotalTransactions})");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(VirtualIAPStore.Instance.IsStoreOpen ? "Close Store" : "Open IAP Store"))
                {
                    if (VirtualIAPStore.Instance.IsStoreOpen)
                        VirtualIAPStore.Instance.CloseStore();
                    else
                        VirtualIAPStore.Instance.OpenStore();
                }
                if (GUILayout.Button("Reset Store Data"))
                {
                    VirtualIAPStore.Instance.ResetStoreDataForTesting();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }
    }
}
