using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class CoreFrameworkTests
    {
        private GameObject _gameManagerGo;
        private GameObject _experimentManagerGo;
        private GameManager _gameManager;
        private ExperimentManager _experimentManager;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            EventBus.ClearAll();

            _experimentManagerGo = new GameObject("TestExperimentManager");
            _experimentManager = _experimentManagerGo.AddComponent<ExperimentManager>();

            _gameManagerGo = new GameObject("TestGameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameManagerGo != null) Object.DestroyImmediate(_gameManagerGo);
            if (_experimentManagerGo != null) Object.DestroyImmediate(_experimentManagerGo);
            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region EventBus Tests

        [Test]
        public void EventBus_SubscribeAndPublish_InvokesListener()
        {
            bool eventReceived = false;
            GameState previous = GameState.Splash;
            GameState next = GameState.Lobby;

            EventBus.Subscribe<GameStateChangedEvent>(evt =>
            {
                eventReceived = true;
                previous = evt.PreviousState;
                next = evt.NewState;
            });

            EventBus.Publish(new GameStateChangedEvent(GameState.Splash, GameState.Lobby));

            Assert.IsTrue(eventReceived);
            Assert.AreEqual(GameState.Splash, previous);
            Assert.AreEqual(GameState.Lobby, next);
        }

        [Test]
        public void EventBus_Unsubscribe_StopsInvokingListener()
        {
            int callCount = 0;
            System.Action<GameStateChangedEvent> listener = evt => callCount++;

            EventBus.Subscribe(listener);
            EventBus.Publish(new GameStateChangedEvent(GameState.Splash, GameState.Lobby));
            Assert.AreEqual(1, callCount);

            EventBus.Unsubscribe(listener);
            EventBus.Publish(new GameStateChangedEvent(GameState.Lobby, GameState.Store));
            Assert.AreEqual(1, callCount);
        }

        #endregion

        #region ExperimentManager Tests

        [Test]
        public void ExperimentConfig_GroupA_HasCorrectFeatureGates()
        {
            var config = ExperimentConfig.CreateForGroup(ExperimentGroup.GroupA_DominantAds);

            Assert.AreEqual("GROUP_A", config.GroupId);
            Assert.IsTrue(config.IsAdsEnabled);
            Assert.IsFalse(config.IsIAPStoreEnabled);
            Assert.AreEqual(0.0f, config.InitialWalletBudget);
            Assert.IsTrue(config.IsRespinAdEnabled);
            Assert.IsTrue(config.IsReviveAdEnabled);
            Assert.IsTrue(config.IsDoubleGoldAdEnabled);
        }

        [Test]
        public void ExperimentConfig_GroupB_HasCorrectFeatureGates()
        {
            var config = ExperimentConfig.CreateForGroup(ExperimentGroup.GroupB_DominantIAP);

            Assert.AreEqual("GROUP_B", config.GroupId);
            Assert.IsFalse(config.IsAdsEnabled);
            Assert.IsTrue(config.IsIAPStoreEnabled);
            Assert.AreEqual(20000.0f, config.InitialWalletBudget);
            Assert.IsFalse(config.IsRespinAdEnabled);
            Assert.IsFalse(config.IsReviveAdEnabled);
            Assert.IsFalse(config.IsDoubleGoldAdEnabled);
        }

        [Test]
        public void ExperimentConfig_GroupC_HasCorrectFeatureGates()
        {
            var config = ExperimentConfig.CreateForGroup(ExperimentGroup.GroupC_Hybrid);

            Assert.AreEqual("GROUP_C", config.GroupId);
            Assert.IsTrue(config.IsAdsEnabled);
            Assert.IsTrue(config.IsIAPStoreEnabled);
            Assert.AreEqual(20000.0f, config.InitialWalletBudget);
            Assert.IsTrue(config.IsRespinAdEnabled);
            Assert.IsTrue(config.IsReviveAdEnabled);
            Assert.IsTrue(config.IsDoubleGoldAdEnabled);
        }

        [Test]
        public void ExperimentManager_SecretOverride_5TapsCyclesGroup()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            Assert.AreEqual(ExperimentGroup.GroupA_DominantAds, _experimentManager.CurrentGroup);

            // Tap 5 times
            for (int i = 0; i < 5; i++)
            {
                _experimentManager.RegisterTap();
            }

            // Should cycle to Group B
            Assert.AreEqual(ExperimentGroup.GroupB_DominantIAP, _experimentManager.CurrentGroup);

            // Tap 5 more times
            for (int i = 0; i < 5; i++)
            {
                _experimentManager.RegisterTap();
            }

            // Should cycle to Group C
            Assert.AreEqual(ExperimentGroup.GroupC_Hybrid, _experimentManager.CurrentGroup);
        }

        #endregion

        #region GameManager Tests

        [Test]
        public void GameManager_StateTransition_FlowsCorrectly()
        {
            _gameManager.ChangeState(GameState.Lobby);
            Assert.AreEqual(GameState.Lobby, _gameManager.CurrentState);

            _gameManager.StartNewRun();
            Assert.AreEqual(GameState.CombatStage1, _gameManager.CurrentState);
            Assert.AreEqual(1, _gameManager.CurrentStageIndex);

            // Finish Stage 1 as victory
            _gameManager.OnStageCompleted(isVictory: true, turnsTaken: 3, playerRemainingHp: 100, botRemainingHp: 0);
            Assert.AreEqual(GameState.MatchSettlement, _gameManager.CurrentState);

            // Claim settlement and proceed to next stage
            _gameManager.ClaimSettlementReward(doubleRewardWithAd: false);
            _gameManager.ProceedToNextStage();
            Assert.AreEqual(GameState.CombatStage2, _gameManager.CurrentState);
            Assert.AreEqual(2, _gameManager.CurrentStageIndex);

            // Finish Stage 2 as victory
            _gameManager.OnStageCompleted(isVictory: true, turnsTaken: 4, playerRemainingHp: 75, botRemainingHp: 0);
            _gameManager.ProceedToNextStage();
            Assert.AreEqual(GameState.CombatStage3, _gameManager.CurrentState);
            Assert.AreEqual(3, _gameManager.CurrentStageIndex);

            // Finish Stage 3 as victory
            _gameManager.OnStageCompleted(isVictory: true, turnsTaken: 6, playerRemainingHp: 50, botRemainingHp: 0);
            _gameManager.ProceedToNextStage();
            Assert.AreEqual(GameState.FinalSettlement, _gameManager.CurrentState);
            Assert.IsTrue(_gameManager.CompletedStage3);
        }

        [Test]
        public void GameManager_GroupA_CannotEnterStore()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            _gameManager.ChangeState(GameState.Lobby);

            bool entered = _gameManager.OpenStore();
            Assert.IsFalse(entered);
            Assert.AreEqual(GameState.Lobby, _gameManager.CurrentState);
        }

        [Test]
        public void GameManager_GroupB_CanEnterStore()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
            _gameManager.ChangeState(GameState.Lobby);

            bool entered = _gameManager.OpenStore();
            Assert.IsTrue(entered);
            Assert.AreEqual(GameState.Store, _gameManager.CurrentState);
        }

        #endregion
    }
}
