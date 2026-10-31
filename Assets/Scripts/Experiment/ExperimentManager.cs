using System;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Experiment
{
    /// <summary>
    /// Manages A/B/C testing group assignment, balancing (1:1:1 ratio),
    /// feature gating, and researcher secret override (5-tap detection).
    /// </summary>
    public class ExperimentManager : MonoBehaviour
    {
        public static ExperimentManager Instance { get; private set; }

        public enum BalancingMode
        {
            /// <summary>
            /// Strict sequential interleaving (A -> B -> C -> A...) guaranteeing exact 1:1:1 quota.
            /// </summary>
            StrictRoundRobin,

            /// <summary>
            /// Uniform pseudo-random distribution (33.3% probability per group).
            /// </summary>
            UniformRandom
        }

        [Header("Experiment Allocation Settings")]
        [SerializeField] private BalancingMode balancingMode = BalancingMode.StrictRoundRobin;
        [SerializeField] private bool forceManualGroup = false;
        [SerializeField] private ExperimentGroup manualGroup = ExperimentGroup.GroupA_DominantAds;

        [Header("Secret Researcher Override Settings")]
        [Tooltip("Max seconds between taps before tap counter resets")]
        [SerializeField] private float tapWindowSeconds = 2.5f;
        [Tooltip("Number of taps required to trigger secret researcher override")]
        [SerializeField] private int requiredTapCount = 5;

        // PlayerPrefs Keys
        private const string PREF_KEY_USER_ID = "LuckyDuelist_UserId";
        private const string PREF_KEY_ASSIGNED_GROUP = "LuckyDuelist_AssignedGroup";
        private const string PREF_KEY_ROUND_ROBIN_COUNTER = "LuckyDuelist_RoundRobinCounter";
        private const string PREF_KEY_WALLET_BALANCE = "LuckyDuelist_WalletBalance";

        // State
        public string UserId { get; private set; }
        public ExperimentGroup CurrentGroup { get; private set; }
        public ExperimentConfig CurrentConfig { get; private set; }
        public float CurrentWalletBalance { get; private set; }

        // Tap Detection
        private int _currentTapCount = 0;
        private float _lastTapTime = -100f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeExperiment();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE
            HandleResearcherKeyboardShortcuts();
#endif
        }

        /// <summary>
        /// Initializes the participant session, restoring or assigning group and User ID.
        /// </summary>
        public void InitializeExperiment()
        {
            // 1. Resolve User ID
            if (PlayerPrefs.HasKey(PREF_KEY_USER_ID))
            {
                UserId = PlayerPrefs.GetString(PREF_KEY_USER_ID);
            }
            else
            {
                UserId = GenerateUniqueUserId();
                PlayerPrefs.SetString(PREF_KEY_USER_ID, UserId);
            }

            // 2. Resolve Group Assignment
            if (forceManualGroup)
            {
                CurrentGroup = manualGroup;
            }
            else if (PlayerPrefs.HasKey(PREF_KEY_ASSIGNED_GROUP))
            {
                int savedGroupInt = PlayerPrefs.GetInt(PREF_KEY_ASSIGNED_GROUP);
                CurrentGroup = (ExperimentGroup)Mathf.Clamp(savedGroupInt, 0, 2);
            }
            else
            {
                CurrentGroup = BalanceNewGroupAssignment();
                PlayerPrefs.SetInt(PREF_KEY_ASSIGNED_GROUP, (int)CurrentGroup);
            }

            // 3. Apply Config
            ApplyConfig(CurrentGroup, isOverride: false);

            // 4. Resolve Wallet Balance
            if (PlayerPrefs.HasKey(PREF_KEY_WALLET_BALANCE))
            {
                CurrentWalletBalance = PlayerPrefs.GetFloat(PREF_KEY_WALLET_BALANCE);
            }
            else
            {
                CurrentWalletBalance = CurrentConfig.InitialWalletBudget;
                PlayerPrefs.SetFloat(PREF_KEY_WALLET_BALANCE, CurrentWalletBalance);
            }

            PlayerPrefs.Save();

            Debug.Log($"[ExperimentManager] Initialized. User: {UserId} | Group: {CurrentConfig.GroupId} ({CurrentConfig.GroupName}) | Initial Wallet: Rp {CurrentWalletBalance:N0}");
        }

        /// <summary>
        /// Balanced allocation logic ensuring 1:1:1 distribution across respondents.
        /// </summary>
        private ExperimentGroup BalanceNewGroupAssignment()
        {
            if (balancingMode == BalancingMode.StrictRoundRobin)
            {
                int counter = PlayerPrefs.GetInt(PREF_KEY_ROUND_ROBIN_COUNTER, 0);
                int groupIndex = counter % 3;

                // Advance counter for next tester session
                PlayerPrefs.SetInt(PREF_KEY_ROUND_ROBIN_COUNTER, counter + 1);

                return (ExperimentGroup)groupIndex;
            }
            else
            {
                // Uniform pseudo-random (1:1:1 probability)
                int randomIndex = UnityEngine.Random.Range(0, 3);
                return (ExperimentGroup)randomIndex;
            }
        }

        private string GenerateUniqueUserId()
        {
            string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            return $"RES-{shortGuid}";
        }

        private void ApplyConfig(ExperimentGroup group, bool isOverride)
        {
            CurrentGroup = group;
            CurrentConfig = ExperimentConfig.CreateForGroup(group);

            // Broadcast assignment to other modules via EventBus
            EventBus.Publish(new ExperimentGroupAssignedEvent(
                CurrentGroup,
                CurrentConfig.GroupId,
                CurrentConfig.GroupName,
                UserId,
                isOverride
            ));
        }

        /// <summary>
        /// Registers a tap from the researcher secret override trigger button.
        /// If 5 taps occur within the tap window, the group switches to the next group.
        /// </summary>
        public void RegisterTap()
        {
            float currentTime = Time.unscaledTime;

            if (currentTime - _lastTapTime <= tapWindowSeconds)
            {
                _currentTapCount++;
            }
            else
            {
                _currentTapCount = 1;
            }

            _lastTapTime = currentTime;

            Debug.Log($"[ExperimentManager] Researcher secret tap: {_currentTapCount}/{requiredTapCount}");

            if (_currentTapCount >= requiredTapCount)
            {
                _currentTapCount = 0;
                TriggerSecretOverrideCycle();
            }
        }

        /// <summary>
        /// Cycles to the next experimental group: Group A -> Group B -> Group C -> Group A.
        /// </summary>
        public void TriggerSecretOverrideCycle()
        {
            ExperimentGroup previousGroup = CurrentGroup;
            ExperimentGroup nextGroup = (ExperimentGroup)(((int)CurrentGroup + 1) % 3);

            SetGroupOverride(nextGroup);

            EventBus.Publish(new SecretOverrideTriggeredEvent(previousGroup, nextGroup, requiredTapCount));
            Debug.LogWarning($"<color=yellow>[ExperimentManager] SECRET OVERRIDE TRIGGERED! Switched from {previousGroup} to {nextGroup}.</color>");
        }

        /// <summary>
        /// Explicitly overrides the active group for researcher testing.
        /// </summary>
        public void SetGroupOverride(ExperimentGroup newGroup)
        {
            CurrentGroup = newGroup;
            PlayerPrefs.SetInt(PREF_KEY_ASSIGNED_GROUP, (int)newGroup);

            // When switching to Group B or C, grant initial budget if not present
            if (newGroup != ExperimentGroup.GroupA_DominantAds && CurrentWalletBalance <= 0)
            {
                CurrentWalletBalance = ExperimentConfig.StandardInitialBudget;
                PlayerPrefs.SetFloat(PREF_KEY_WALLET_BALANCE, CurrentWalletBalance);
            }

            PlayerPrefs.Save();
            ApplyConfig(newGroup, isOverride: true);
        }

        /// <summary>
        /// Deducts amount from the virtual IAP wallet if sufficient funds exist.
        /// </summary>
        public bool TryDeductWalletBalance(float amount)
        {
            if (CurrentWalletBalance >= amount)
            {
                CurrentWalletBalance -= amount;
                PlayerPrefs.SetFloat(PREF_KEY_WALLET_BALANCE, CurrentWalletBalance);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resets all local experiment data to simulate a fresh participant installation.
        /// </summary>
        public void ResetExperimentData()
        {
            PlayerPrefs.DeleteKey(PREF_KEY_USER_ID);
            PlayerPrefs.DeleteKey(PREF_KEY_ASSIGNED_GROUP);
            PlayerPrefs.DeleteKey(PREF_KEY_WALLET_BALANCE);
            PlayerPrefs.Save();

            _currentTapCount = 0;
            _lastTapTime = -100f;

            InitializeExperiment();
            Debug.Log("[ExperimentManager] Local experiment data reset. New participant created.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE
        private void HandleResearcherKeyboardShortcuts()
        {
            // Keyboard shortcuts for researcher testing convenience:
            // F8 -> Register 1 Secret Tap
            if (Input.GetKeyDown(KeyCode.F8))
            {
                RegisterTap();
            }

            // LeftShift + 1 -> Force Group A
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            }
            // LeftShift + 2 -> Force Group B
            else if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
            }
            // LeftShift + 3 -> Force Group C
            else if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetGroupOverride(ExperimentGroup.GroupC_Hybrid);
            }
        }
#endif

        #region Feature Gate Query Properties
        public bool IsAdsEnabled => CurrentConfig != null && CurrentConfig.IsAdsEnabled;
        public bool IsIAPStoreEnabled => CurrentConfig != null && CurrentConfig.IsIAPStoreEnabled;
        public bool IsRespinAdEnabled => CurrentConfig != null && CurrentConfig.IsRespinAdEnabled;
        public bool IsReviveAdEnabled => CurrentConfig != null && CurrentConfig.IsReviveAdEnabled;
        public bool IsDoubleGoldAdEnabled => CurrentConfig != null && CurrentConfig.IsDoubleGoldAdEnabled;
        public float AdRevenuePerImpression => CurrentConfig != null ? CurrentConfig.AdRevenuePerImpression : 250f;
        #endregion
    }
}
