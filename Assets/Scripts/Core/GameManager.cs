using System;
using System.Collections;
using UnityEngine;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Core
{
    /// <summary>
    /// Master State Machine and Core Session Controller.
    /// Manages high-level lifecycle:
    /// Splash -> Lobby / Store -> CombatStage1 -> CombatStage2 -> CombatStage3 -> Settlement -> Survey.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State Machine")]
        [SerializeField] private GameState currentState = GameState.Splash;
        [SerializeField] private bool autoTransitionFromSplash = true;
        [SerializeField] private float splashDurationSeconds = 2.0f;

        [Header("Stage Configuration")]
        [SerializeField] private StageData[] stageConfigurations;

        [Header("Evaluation Survey Configuration")]
        [Tooltip("Google Form survey URL template. Placeholder {USER_ID} and {GROUP_ID} will be automatically replaced.")]
        [SerializeField] private string surveyBaseUrl = "https://docs.google.com/forms/d/e/YOUR_FORM_ID/viewform?usp=pp_url&entry.USER_ID_FIELD={USER_ID}&entry.GROUP_ID_FIELD={GROUP_ID}";

        // Session & Run Tracking
        public GameState CurrentState => currentState;
        public int CurrentStageIndex { get; private set; } = 1; // 1, 2, or 3
        public int SoftCurrencyGold { get; private set; } = 0;
        public int MatchesCompleted { get; private set; } = 0;
        public int MatchesWon { get; private set; } = 0;
        public bool CompletedStage3 { get; private set; } = false;
        public float SessionStartTime { get; private set; } = 0f;
        public bool ReviveUsedInCurrentMatch { get; private set; } = false;

        public const int MaxStages = 3;

        [Serializable]
        public struct StageData
        {
            public int stageNumber;
            public string stageName;
            public int bossMaxHp;
            public int baseGoldReward;
            [TextArea] public string stageDescription;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeDefaultStages();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            InitializeDefaultStages();
        }
#endif

        private void Start()
        {
            SessionStartTime = Time.realtimeSinceStartup;

            // Subscribe to Experiment changes
            EventBus.Subscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);

            // Start in Splash
            ChangeState(GameState.Splash);

            if (autoTransitionFromSplash)
            {
                StartCoroutine(AutoSplashTransitionRoutine());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            EventBus.Unsubscribe<ExperimentGroupAssignedEvent>(OnExperimentGroupAssigned);
        }

        private void InitializeDefaultStages()
        {
            if (stageConfigurations == null || stageConfigurations.Length == 0)
            {
                stageConfigurations = new StageData[3]
                {
                    new StageData
                    {
                        stageNumber = 1,
                        stageName = "Stage 1: Neon Rookie",
                        bossMaxHp = 70,
                        baseGoldReward = 100,
                        stageDescription = "Warm-up stage. 100% slow yellow parryable bullets. Practice knife parry [E]."
                    },
                    new StageData
                    {
                        stageNumber = 2,
                        stageName = "Stage 2: Chrome Gambler",
                        bossMaxHp = 90,
                        baseGoldReward = 150,
                        stageDescription = "Mid-tier duel. Mixed red unparryable bullets (dodge [Q]) and yellow parryable bullets (parry [E])."
                    },
                    new StageData
                    {
                        stageNumber = 3,
                        stageName = "Stage 3: The Cyber Baron",
                        bossMaxHp = 120,
                        baseGoldReward = 250,
                        stageDescription = "Final Boss. Rapid alternating bursts testing Counter and Mark slot combinations."
                    }
                };
            }
        }

        private IEnumerator AutoSplashTransitionRoutine()
        {
            yield return new WaitForSeconds(splashDurationSeconds);
            if (currentState == GameState.Splash)
            {
                ChangeState(GameState.Lobby);
            }
        }

        #region State Machine Management

        /// <summary>
        /// Transitions the global game state to a new state and triggers entry/exit routines.
        /// </summary>
        public bool ChangeState(GameState newState)
        {
            if (currentState == newState) return true;

            // Validation: Cannot enter Store if IAP Store is disabled for the participant's group
            if (newState == GameState.Store)
            {
                bool isStoreEnabled = ExperimentManager.Instance != null && ExperimentManager.Instance.IsIAPStoreEnabled;
                if (!isStoreEnabled)
                {
                    Debug.LogWarning("[GameManager] Access to Store rejected: Group does not have IAP enabled (Group A).");
                    return false;
                }
            }

            GameState oldState = currentState;
            OnExitState(oldState);

            currentState = newState;
            OnEnterState(newState);

            Debug.Log($"<color=cyan>[GameManager] State Transition: {oldState} ➔ {newState}</color>");
            EventBus.Publish(new GameStateChangedEvent(oldState, newState));
            return true;
        }

        private void OnEnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Splash:
                    // Subsystems initialization
                    break;

                case GameState.Lobby:
                    // Return to lobby, ready to start match or visit store
                    break;

                case GameState.Store:
                    // Virtual IAP Store view
                    break;

                case GameState.CombatStage1:
                    CurrentStageIndex = 1;
                    ReviveUsedInCurrentMatch = false;
                    BroadcastStageStart(1);
                    break;

                case GameState.CombatStage2:
                    CurrentStageIndex = 2;
                    ReviveUsedInCurrentMatch = false;
                    BroadcastStageStart(2);
                    break;

                case GameState.CombatStage3:
                    CurrentStageIndex = 3;
                    ReviveUsedInCurrentMatch = false;
                    BroadcastStageStart(3);
                    break;

                case GameState.MatchSettlement:
                    // Handled via OnStageCompleted()
                    break;

                case GameState.FinalSettlement:
                    BroadcastFinalSettlement();
                    break;

                case GameState.Survey:
                    // Post-session questionnaire
                    break;
            }
        }

        private void OnExitState(GameState state)
        {
            // Clean up state-specific timers or overlays if necessary
        }

        #endregion

        #region Flow & Match Control

        /// <summary>
        /// Starts a full 3-stage game run from Stage 1.
        /// </summary>
        public void StartNewRun()
        {
            CurrentStageIndex = 1;
            MatchesCompleted = 0;
            MatchesWon = 0;
            CompletedStage3 = false;
            ReviveUsedInCurrentMatch = false;

            ChangeState(GameState.CombatStage1);
        }

        /// <summary>
        /// Opens the Virtual IAP Store if allowed by experiment feature gates.
        /// </summary>
        public bool OpenStore()
        {
            return ChangeState(GameState.Store);
        }

        /// <summary>
        /// Closes the store and returns to the lobby.
        /// </summary>
        public void CloseStore()
        {
            ChangeState(GameState.Lobby);
        }

        /// <summary>
        /// Returns to the lobby from settlement or menu.
        /// </summary>
        public void ReturnToLobby()
        {
            ChangeState(GameState.Lobby);
        }

        /// <summary>
        /// Called when a combat stage concludes.
        /// </summary>
        public void OnStageCompleted(bool isVictory, int turnsTaken, int playerRemainingHp, int botRemainingHp)
        {
            MatchesCompleted++;

            StageData currentData = GetStageData(CurrentStageIndex);
            int goldEarned = isVictory ? currentData.baseGoldReward : 0;

            if (isVictory)
            {
                MatchesWon++;
                if (CurrentStageIndex == 3)
                {
                    CompletedStage3 = true;
                }
            }

            EventBus.Publish(new StageCompletedEvent(
                CurrentStageIndex,
                isVictory,
                turnsTaken,
                playerRemainingHp,
                botRemainingHp,
                goldEarned
            ));

            // Transition to match settlement
            ChangeState(GameState.MatchSettlement);

            bool canDouble = isVictory && (ExperimentManager.Instance != null && ExperimentManager.Instance.IsDoubleGoldAdEnabled);
            bool isFinal = (CurrentStageIndex >= MaxStages) || !isVictory;

            EventBus.Publish(new MatchSettlementEvent(
                CurrentStageIndex,
                isVictory,
                goldEarned,
                canDouble,
                isFinal
            ));
        }

        /// <summary>
        /// Called by CombatManager when player HP reaches 0.
        /// Checks if Rewarded Ad revive is available (Group A & C, max 1x per match).
        /// </summary>
        public void OnPlayerDefeated()
        {
            bool canRevive = !ReviveUsedInCurrentMatch &&
                             ExperimentManager.Instance != null &&
                             ExperimentManager.Instance.IsReviveAdEnabled;

            EventBus.Publish(new PlayerDefeatedEvent(CurrentStageIndex, canRevive));

            if (!canRevive)
            {
                // Conclude stage as loss
                OnStageCompleted(isVictory: false, turnsTaken: 0, playerRemainingHp: 0, botRemainingHp: 100);
            }
        }

        /// <summary>
        /// Executes player revive after watching a rewarded ad.
        /// </summary>
        public void RevivePlayer()
        {
            ReviveUsedInCurrentMatch = true;
            EventBus.Publish(new PlayerRevivedEvent(CurrentStageIndex, restoredHpPercent: 0.5f));
            Debug.Log($"[GameManager] Player revived in Stage {CurrentStageIndex} with 50% HP.");
        }

        /// <summary>
        /// Claims settlement reward for the current stage, optionally doubling it if Rewarded Ad was watched.
        /// </summary>
        public void ClaimSettlementReward(bool doubleRewardWithAd)
        {
            StageData stage = GetStageData(CurrentStageIndex);
            int baseGold = stage.baseGoldReward;
            int totalClaimed = baseGold;

            if (doubleRewardWithAd && ExperimentManager.Instance != null && ExperimentManager.Instance.IsDoubleGoldAdEnabled)
            {
                totalClaimed = baseGold * 2;
                Debug.Log($"[GameManager] Gold doubled via Rewarded Ad! Claimed {totalClaimed} Gold.");
            }
            else
            {
                Debug.Log($"[GameManager] Normal claim: {totalClaimed} Gold.");
            }

            SoftCurrencyGold += totalClaimed;
            EventBus.Publish(new PlayerWalletUpdatedEvent(SoftCurrencyGold, ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentWalletBalance : 0f));
        }

        /// <summary>
        /// Advances from MatchSettlement to the next stage or to FinalSettlement.
        /// </summary>
        public void ProceedToNextStage()
        {
            if (CurrentStageIndex < MaxStages)
            {
                CurrentStageIndex++;
                switch (CurrentStageIndex)
                {
                    case 2:
                        ChangeState(GameState.CombatStage2);
                        break;
                    case 3:
                        ChangeState(GameState.CombatStage3);
                        break;
                }
            }
            else
            {
                // Completed Stage 3 Boss!
                ChangeState(GameState.FinalSettlement);
            }
        }

        /// <summary>
        /// Ends the run and shows final settlement (e.g. after loss or completion).
        /// </summary>
        public void FinishRun()
        {
            ChangeState(GameState.FinalSettlement);
        }

        /// <summary>
        /// Opens post-session evaluation questionnaire in external web browser.
        /// Formats Google Form URL with user ID and experiment group query parameters.
        /// </summary>
        public void OpenSurveyForm()
        {
            ChangeState(GameState.Survey);

            string userId = ExperimentManager.Instance != null ? ExperimentManager.Instance.UserId : "UNKNOWN_USER";
            string groupId = ExperimentManager.Instance != null ? ExperimentManager.Instance.CurrentConfig.GroupId : "GROUP_A";

            string finalUrl = surveyBaseUrl
                .Replace("{USER_ID}", UnityEngine.Networking.UnityWebRequest.EscapeURL(userId))
                .Replace("{GROUP_ID}", UnityEngine.Networking.UnityWebRequest.EscapeURL(groupId));

            Debug.Log($"[GameManager] Opening Survey Questionnaire: {finalUrl}");
            Application.OpenURL(finalUrl);

            EventBus.Publish(new SurveyRequestedEvent(finalUrl, userId, groupId));
        }

        #endregion

        #region Helpers & Broadcasts

        private void BroadcastStageStart(int stageIndex)
        {
            StageData data = GetStageData(stageIndex);
            EventBus.Publish(new StageStartedEvent(data.stageNumber, data.stageName, data.bossMaxHp));
        }

        private void BroadcastFinalSettlement()
        {
            float duration = Time.realtimeSinceStartup - SessionStartTime;
            string outcome = CompletedStage3 ? "WIN" : "LOSS";

            EventBus.Publish(new FinalSettlementEvent(
                CompletedStage3,
                MatchesCompleted,
                MatchesWon,
                SoftCurrencyGold,
                duration,
                outcome
            ));
        }

        public StageData GetStageData(int stageNumber)
        {
            if (stageConfigurations != null)
            {
                for (int i = 0; i < stageConfigurations.Length; i++)
                {
                    if (stageConfigurations[i].stageNumber == stageNumber)
                    {
                        return stageConfigurations[i];
                    }
                }
            }

            return new StageData
            {
                stageNumber = stageNumber,
                stageName = $"Stage {stageNumber}",
                bossMaxHp = 100,
                baseGoldReward = 100,
                stageDescription = "Default Stage"
            };
        }

        private void OnExperimentGroupAssigned(ExperimentGroupAssignedEvent evt)
        {
            // If in Store and group changed to Group A (where store is disabled), exit to Lobby immediately
            if (currentState == GameState.Store && evt.Group == ExperimentGroup.GroupA_DominantAds)
            {
                Debug.LogWarning("[GameManager] Exiting Store because new group disables Store access.");
                ChangeState(GameState.Lobby);
            }
        }

        #endregion
    }
}
