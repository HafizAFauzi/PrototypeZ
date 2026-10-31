using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.UI;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class SlotRevolverTests
    {
        private GameObject _revolverGo;
        private SlotRevolverController _controller;

        private GameObject _playerHealthGo;
        private PlayerHealth _playerHealth;

        private GameObject _experimentManagerGo;
        private ExperimentManager _experimentManager;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            EventBus.ClearAll();

            _experimentManagerGo = new GameObject("TestExperimentManager");
            _experimentManager = _experimentManagerGo.AddComponent<ExperimentManager>();

            _revolverGo = new GameObject("TestSlotRevolver");
            _controller = _revolverGo.AddComponent<SlotRevolverController>();

            _playerHealthGo = new GameObject("TestPlayerHealth");
            _playerHealth = _playerHealthGo.AddComponent<PlayerHealth>();
            _playerHealth.SubscribeEvents();
        }

        [TearDown]
        public void TearDown()
        {
            if (_revolverGo != null) Object.DestroyImmediate(_revolverGo);
            if (_playerHealthGo != null) Object.DestroyImmediate(_playerHealthGo);
            if (_experimentManagerGo != null) Object.DestroyImmediate(_experimentManagerGo);

            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region Probability & RNG Distribution Tests

        [Test]
        public void SlotProbability_DefaultWeights_SumTo100Percent()
        {
            float attackProb = _controller.GetSymbolProbability(SlotSymbol.ATTACK);
            float markProb = _controller.GetSymbolProbability(SlotSymbol.MARK);
            float healProb = _controller.GetSymbolProbability(SlotSymbol.HEAL);
            float counterProb = _controller.GetSymbolProbability(SlotSymbol.COUNTER_SHARPEN);
            float wildProb = _controller.GetSymbolProbability(SlotSymbol.WILD_JOKER);

            Assert.AreEqual(0.35f, attackProb, 0.001f);
            Assert.AreEqual(0.20f, markProb, 0.001f);
            Assert.AreEqual(0.20f, healProb, 0.001f);
            Assert.AreEqual(0.15f, counterProb, 0.001f);
            Assert.AreEqual(0.10f, wildProb, 0.001f);

            float total = attackProb + markProb + healProb + counterProb + wildProb;
            Assert.AreEqual(1.0f, total, 0.001f);
        }

        [Test]
        public void SlotProbability_StatisticalDistribution_10000Rolls()
        {
            // Seeded RNG for reproducible statistical testing
            _controller.SetRandomProvider(new System.Random(42));

            int rolls = 10000;
            int attackCount = 0;
            int markCount = 0;
            int healCount = 0;
            int counterCount = 0;
            int wildCount = 0;

            for (int i = 0; i < rolls; i++)
            {
                SlotSymbol sym = _controller.RollSymbol();
                switch (sym)
                {
                    case SlotSymbol.ATTACK: attackCount++; break;
                    case SlotSymbol.MARK: markCount++; break;
                    case SlotSymbol.HEAL: healCount++; break;
                    case SlotSymbol.COUNTER_SHARPEN: counterCount++; break;
                    case SlotSymbol.WILD_JOKER: wildCount++; break;
                }
            }

            // Margin of error ~2.5% for 10,000 rolls at 99% confidence
            Assert.AreEqual(0.35f, (float)attackCount / rolls, 0.025f, "ATTACK should be ~35%");
            Assert.AreEqual(0.20f, (float)markCount / rolls, 0.020f, "MARK should be ~20%");
            Assert.AreEqual(0.20f, (float)healCount / rolls, 0.020f, "HEAL should be ~20%");
            Assert.AreEqual(0.15f, (float)counterCount / rolls, 0.020f, "COUNTER should be ~15%");
            Assert.AreEqual(0.10f, (float)wildCount / rolls, 0.020f, "WILD_JOKER should be ~10%");
        }

        #endregion

        #region 3-Reel Spinner Mechanics

        [Test]
        public void ReelSpinner_InstantSpin_ProducesThreeSymbolsAndEvents()
        {
            bool startedEvent = false;
            int reelsStoppedCount = 0;
            bool completedEvent = false;

            EventBus.Subscribe<SlotSpinStartedEvent>(evt => startedEvent = true);
            EventBus.Subscribe<SlotReelStoppedEvent>(evt => reelsStoppedCount++);
            EventBus.Subscribe<SlotSpinCompletedEvent>(evt => completedEvent = true);

            _controller.SpinCylinderInstant();

            Assert.IsTrue(startedEvent);
            Assert.AreEqual(3, reelsStoppedCount);
            Assert.IsTrue(completedEvent);
            Assert.IsTrue(_controller.HasSpunThisTurn);
            Assert.IsFalse(_controller.IsSpinning);
            Assert.AreEqual(3, _controller.CurrentReelSymbols.Length);
        }

        #endregion

        #region 2 Hold Slots Inventory Tests

        [Test]
        public void HoldSlots_CanHoldUpToTwoSymbols()
        {
            _controller.SpinCylinderInstant();

            Assert.AreEqual(0, _controller.HeldCount);
            Assert.IsTrue(_controller.CanHoldMore);

            // Hold Reel 0
            bool held0 = _controller.TryHoldReel(0);
            Assert.IsTrue(held0);
            Assert.AreEqual(1, _controller.HeldCount);
            Assert.IsTrue(_controller.IsReelHeld(0));

            // Hold Reel 1
            bool held1 = _controller.TryHoldReel(1);
            Assert.IsTrue(held1);
            Assert.AreEqual(2, _controller.HeldCount);
            Assert.IsTrue(_controller.IsReelHeld(1));
            Assert.IsFalse(_controller.CanHoldMore);
        }

        [Test]
        public void HoldSlots_RuleMinimal1SymbolExecuted_BlocksHoldingAllThree()
        {
            _controller.SpinCylinderInstant();

            _controller.TryHoldReel(0);
            _controller.TryHoldReel(1);

            // Holding the 3rd reel would leave 0 symbols to execute this turn!
            // Must be rejected by rule: "Minimal 1 simbol harus dieksekusi pada giliran aktif"
            bool held2 = _controller.TryHoldReel(2);
            Assert.IsFalse(held2, "Cannot hold 3rd reel: leaves 0 executable symbols and exceeds max 2 slots");
            Assert.AreEqual(2, _controller.HeldCount);
            Assert.IsFalse(_controller.IsReelHeld(2));
        }

        [Test]
        public void HoldSlots_ReleaseHoldSlot_FreesSlotAndUnmarksReel()
        {
            _controller.SpinCylinderInstant();
            _controller.TryHoldReel(0);

            Assert.AreEqual(1, _controller.HeldCount);
            Assert.IsTrue(_controller.IsReelHeld(0));

            bool released = _controller.TryReleaseHold(0);
            Assert.IsTrue(released);
            Assert.AreEqual(0, _controller.HeldCount);
            Assert.IsFalse(_controller.IsReelHeld(0));
            Assert.IsNull(_controller.GetHoldSlot(0));
        }

        [Test]
        public void HoldSlots_ToggleReel_SwitchesStateCleanly()
        {
            _controller.SpinCylinderInstant();

            // Toggle on
            _controller.ToggleHoldReel(1);
            Assert.IsTrue(_controller.IsReelHeld(1));
            Assert.AreEqual(1, _controller.HeldCount);

            // Toggle off
            _controller.ToggleHoldReel(1);
            Assert.IsFalse(_controller.IsReelHeld(1));
            Assert.AreEqual(0, _controller.HeldCount);
        }

        [Test]
        public void HoldSlots_PreservedAcrossTurnsOnCommit()
        {
            _controller.SpinCylinderInstant();
            SlotSymbol heldSymbol = _controller.CurrentReelSymbols[0];

            _controller.TryHoldReel(0);
            Assert.AreEqual(heldSymbol, _controller.GetHoldSlot(0));

            // Commit turn
            bool committed = _controller.CommitTurn(out List<SlotSymbol> actions);
            Assert.IsTrue(committed);
            Assert.AreEqual(2, actions.Count, "2 unheld reels executed");

            // Verify held symbol is preserved for next turn
            Assert.AreEqual(1, _controller.HeldCount);
            Assert.AreEqual(heldSymbol, _controller.GetHoldSlot(0));
            Assert.AreEqual(2, _controller.TurnNumber);
            Assert.IsFalse(_controller.HasSpunThisTurn);
        }

        #endregion

        #region Single Reel Re-Spin & Feature Gating Tests

        [Test]
        public void RespinReel_GroupA_AllowsOneRespinPerTurn()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            _controller.SpinCylinderInstant();

            Assert.IsTrue(_controller.CanRespinReel(0));
            Assert.IsFalse(_controller.HasUsedRespinThisTurn);

            bool respun = _controller.RespinReel(0, isAdWatched: true);
            Assert.IsTrue(respun);
            Assert.IsTrue(_controller.HasUsedRespinThisTurn);

            // Second respin in same turn must be rejected
            Assert.IsFalse(_controller.CanRespinReel(1));
            bool secondRespin = _controller.RespinReel(1, isAdWatched: true);
            Assert.IsFalse(secondRespin);
        }

        [Test]
        public void RespinReel_GroupB_DisallowsRespinViaAd()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
            _controller.SpinCylinderInstant();

            // Group B has ads disabled, so CanRespinReel must return false
            Assert.IsFalse(_controller.CanRespinReel(0));
            bool attempted = _controller.RespinReel(0, isAdWatched: true);
            Assert.IsFalse(attempted);
        }

        [Test]
        public void RespinReel_CannotRespinHeldReel()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            _controller.SpinCylinderInstant();

            _controller.TryHoldReel(0);
            Assert.IsTrue(_controller.IsReelHeld(0));

            // Cannot respin a reel that is held
            Assert.IsFalse(_controller.CanRespinReel(0));
        }

        #endregion

        #region Wild Joker Resolution Tests

        [Test]
        public void WildJoker_ResolveToTargetSymbol()
        {
            _controller.SpinCylinderInstant();

            // Set all weights to 0 except Wild
            _controller.SetWeights(0, 0, 0, 0, 100);
            _controller.RespinReel(0, isAdWatched: false);

            Assert.AreEqual(SlotSymbol.WILD_JOKER, _controller.CurrentReelSymbols[0]);
            Assert.IsTrue(_controller.HasPendingWildJoker(out int reelIdx));
            Assert.AreEqual(0, reelIdx);

            // Resolve to HEAL
            bool resolved = _controller.ResolveWildJoker(0, SlotSymbol.HEAL);
            Assert.IsTrue(resolved);
            Assert.AreEqual(SlotSymbol.HEAL, _controller.CurrentReelSymbols[0]);

            // Cannot resolve to WILD_JOKER
            bool invalidResolve = _controller.ResolveWildJoker(0, SlotSymbol.WILD_JOKER);
            Assert.IsFalse(invalidResolve);
        }

        #endregion

        #region Player Health & Heal Symbol Integration Tests

        [Test]
        public void PlayerHealth_StartsAt100_TakesDamageAndHeals()
        {
            Assert.AreEqual(100, _playerHealth.CurrentHealth);
            Assert.AreEqual(100, _playerHealth.MaxHealth);

            _playerHealth.TakeDamage(30, "EnemyBullet");
            Assert.AreEqual(70, _playerHealth.CurrentHealth);

            _playerHealth.Heal(20, "NanoMedkit");
            Assert.AreEqual(90, _playerHealth.CurrentHealth);

            // Does not exceed max health
            _playerHealth.Heal(50, "Overheal");
            Assert.AreEqual(100, _playerHealth.CurrentHealth);
        }

        [Test]
        public void PlayerHealth_Revive_Sets50PercentHp()
        {
            _playerHealth.TakeDamage(100, "LethalDamage");
            Assert.AreEqual(0, _playerHealth.CurrentHealth);

            EventBus.Publish(new PlayerRevivedEvent(1, 0.5f));
            Assert.AreEqual(50, _playerHealth.CurrentHealth);
        }

        [Test]
        public void PlayerHealth_TurnCommittedWithHeal_Restores20HpPerHealSymbol()
        {
            _playerHealth.TakeDamage(50, "BattleDamage");
            Assert.AreEqual(50, _playerHealth.CurrentHealth);

            // Commit a turn containing 2 HEAL symbols
            var symbols = new List<SlotSymbol> { SlotSymbol.HEAL, SlotSymbol.HEAL, SlotSymbol.ATTACK };
            EventBus.Publish(new TurnCommittedEvent(1, symbols, new SlotSymbol?[2]));

            // 50 + 20*2 = 90
            Assert.AreEqual(90, _playerHealth.CurrentHealth);
        }

        #endregion

        #region Combat HUD View Tests

        [Test]
        public void CombatHUDView_InstantiatesAndUpdatesHealthCorrectly()
        {
            var hudGo = new GameObject("TestCombatHUD");
            var hudView = hudGo.AddComponent<CombatHUDView>();

            // View should update health slider and text without exception
            hudView.UpdateHealthUI(80, 100);

            // Re-spin button active on Group A
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            hudView.RefreshExperimentGatedUI();

            // Re-spin button inactive on Group B
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
            hudView.RefreshExperimentGatedUI();

            Object.DestroyImmediate(hudGo);
        }

        #endregion
    }
}
