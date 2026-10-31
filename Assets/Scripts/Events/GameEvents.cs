using System;
using LuckyDuelist.Core;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Dispatched when the global game state transitions.
    /// </summary>
    public struct GameStateChangedEvent
    {
        public GameState PreviousState;
        public GameState NewState;

        public GameStateChangedEvent(GameState previousState, GameState newState)
        {
            PreviousState = previousState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Dispatched when an experimental group is assigned or re-assigned (e.g. at startup or via researcher override).
    /// </summary>
    public struct ExperimentGroupAssignedEvent
    {
        public ExperimentGroup Group;
        public string GroupId;
        public string GroupName;
        public string UserId;
        public bool IsResearcherOverride;

        public ExperimentGroupAssignedEvent(ExperimentGroup group, string groupId, string groupName, string userId, bool isResearcherOverride)
        {
            Group = group;
            GroupId = groupId;
            GroupName = groupName;
            UserId = userId;
            IsResearcherOverride = isResearcherOverride;
        }
    }

    /// <summary>
    /// Dispatched when the secret researcher 5-tap override is activated.
    /// </summary>
    public struct SecretOverrideTriggeredEvent
    {
        public ExperimentGroup PreviousGroup;
        public ExperimentGroup NewGroup;
        public int TapCount;

        public SecretOverrideTriggeredEvent(ExperimentGroup previousGroup, ExperimentGroup newGroup, int tapCount)
        {
            PreviousGroup = previousGroup;
            NewGroup = newGroup;
            TapCount = tapCount;
        }
    }

    /// <summary>
    /// Dispatched when a combat stage begins (Stage 1, 2, or 3).
    /// </summary>
    public struct StageStartedEvent
    {
        public int StageNumber;
        public string StageName;
        public int BossMaxHp;

        public StageStartedEvent(int stageNumber, string stageName, int bossMaxHp)
        {
            StageNumber = stageNumber;
            StageName = stageName;
            BossMaxHp = bossMaxHp;
        }
    }

    /// <summary>
    /// Dispatched when a combat stage finishes (either Win or Loss).
    /// </summary>
    public struct StageCompletedEvent
    {
        public int StageNumber;
        public bool IsVictory;
        public int TurnsTaken;
        public int PlayerFinalHp;
        public int BotFinalHp;
        public int GoldEarned;

        public StageCompletedEvent(int stageNumber, bool isVictory, int turnsTaken, int playerFinalHp, int botFinalHp, int goldEarned)
        {
            StageNumber = stageNumber;
            IsVictory = isVictory;
            TurnsTaken = turnsTaken;
            PlayerFinalHp = playerFinalHp;
            BotFinalHp = botFinalHp;
            GoldEarned = goldEarned;
        }
    }

    /// <summary>
    /// Dispatched when player HP drops to 0 during a combat stage.
    /// </summary>
    public struct PlayerDefeatedEvent
    {
        public int StageNumber;
        public bool CanOfferRevive;

        public PlayerDefeatedEvent(int stageNumber, bool canOfferRevive)
        {
            StageNumber = stageNumber;
            CanOfferRevive = canOfferRevive;
        }
    }

    /// <summary>
    /// Dispatched when player revives (e.g. via Rewarded Ad in Group A/C).
    /// </summary>
    public struct PlayerRevivedEvent
    {
        public int StageNumber;
        public float RestoredHpPercent;

        public PlayerRevivedEvent(int stageNumber, float restoredHpPercent)
        {
            StageNumber = stageNumber;
            RestoredHpPercent = restoredHpPercent;
        }
    }

    /// <summary>
    /// Dispatched when the match settlement screen is shown for a stage.
    /// </summary>
    public struct MatchSettlementEvent
    {
        public int StageNumber;
        public bool IsVictory;
        public int BaseGold;
        public bool CanDoubleGoldWithAd;
        public bool IsFinalStage;

        public MatchSettlementEvent(int stageNumber, bool isVictory, int baseGold, bool canDoubleGoldWithAd, bool isFinalStage)
        {
            StageNumber = stageNumber;
            IsVictory = isVictory;
            BaseGold = baseGold;
            CanDoubleGoldWithAd = canDoubleGoldWithAd;
            IsFinalStage = isFinalStage;
        }
    }

    /// <summary>
    /// Dispatched when all 3 stages are cleared or run concludes (Final Settlement).
    /// </summary>
    public struct FinalSettlementEvent
    {
        public bool CompletedAllStages;
        public int StagesCompleted;
        public int MatchesWon;
        public int TotalGoldEarned;
        public float SessionDurationSeconds;
        public string Outcome;

        public FinalSettlementEvent(bool completedAllStages, int stagesCompleted, int matchesWon, int totalGoldEarned, float sessionDurationSeconds, string outcome)
        {
            CompletedAllStages = completedAllStages;
            StagesCompleted = stagesCompleted;
            MatchesWon = matchesWon;
            TotalGoldEarned = totalGoldEarned;
            SessionDurationSeconds = sessionDurationSeconds;
            Outcome = outcome;
        }
    }

    /// <summary>
    /// Dispatched when the user transitions to the survey evaluation stage.
    /// </summary>
    public struct SurveyRequestedEvent
    {
        public string SurveyUrl;
        public string UserId;
        public string GroupId;

        public SurveyRequestedEvent(string surveyUrl, string userId, string groupId)
        {
            SurveyUrl = surveyUrl;
            UserId = userId;
            GroupId = groupId;
        }
    }

    /// <summary>
    /// Dispatched when player soft currency (gold) or virtual IAP balance changes.
    /// </summary>
    public struct PlayerWalletUpdatedEvent
    {
        public int SoftCurrencyGold;
        public float VirtualIapBalance;

        public PlayerWalletUpdatedEvent(int softCurrencyGold, float virtualIapBalance)
        {
            SoftCurrencyGold = softCurrencyGold;
            VirtualIapBalance = virtualIapBalance;
        }
    }
}
