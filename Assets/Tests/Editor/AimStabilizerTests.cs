using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class AimStabilizerTests
    {
        private GameObject _aimGo;
        private AimStabilizerController _aimController;

        private GameObject _enemyGo;
        private EnemyHealth _enemyHealth;

        private GameObject _gameManagerGo;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            EventBus.ClearAll();

            _gameManagerGo = new GameObject("TestGameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();

            _enemyGo = new GameObject("TestEnemyHealth");
            _enemyHealth = _enemyGo.AddComponent<EnemyHealth>();
            _enemyHealth.ResetForStage(1, "Neon Rookie", 70);

            _aimGo = new GameObject("TestAimStabilizer");
            _aimController = _aimGo.AddComponent<AimStabilizerController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_aimGo != null) Object.DestroyImmediate(_aimGo);
            if (_enemyGo != null) Object.DestroyImmediate(_enemyGo);
            if (_gameManagerGo != null) Object.DestroyImmediate(_gameManagerGo);
            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region Perlin Drift & Vector Dynamics Tests

        [Test]
        public void DriftVelocity_MatchesTSDPerlinFormula()
        {
            float t = 1.5f;
            float f = 2.0f;
            float speed = 320f;
            float seedX = 42f;
            float seedY = 88f;

            Vector2 drift = AimStabilizerController.CalculateDriftVelocity(t, f, speed, seedX, seedY);

            float expectedX = (Mathf.PerlinNoise(t * f + seedX, 0f) - 0.5f) * speed;
            float expectedY = (Mathf.PerlinNoise(0f, t * f + seedY) - 0.5f) * speed;

            Assert.AreEqual(expectedX, drift.x, 0.001f);
            Assert.AreEqual(expectedY, drift.y, 0.001f);
        }

        [Test]
        public void DriftVelocity_ScalesLinearlyWithSpeed()
        {
            float t = 0.8f;
            float f = 1.5f;
            Vector2 drift100 = AimStabilizerController.CalculateDriftVelocity(t, f, 100f, 10f, 20f);
            Vector2 drift300 = AimStabilizerController.CalculateDriftVelocity(t, f, 300f, 10f, 20f);

            Assert.AreEqual(drift100.x * 3f, drift300.x, 0.001f);
            Assert.AreEqual(drift100.y * 3f, drift300.y, 0.001f);
        }

        [Test]
        public void PlayerVelocity_ScalesWithDragSensitivity()
        {
            Vector2 dragDelta = new Vector2(20f, -15f);
            float sensitivity = 1.5f;

            Vector2 playerVel = AimStabilizerController.CalculatePlayerVelocity(dragDelta, sensitivity, invert: false);

            Assert.AreEqual(30f, playerVel.x, 0.001f);
            Assert.AreEqual(-22.5f, playerVel.y, 0.001f);
        }

        [Test]
        public void ReticlePosition_IntegratesMotionCorrectly()
        {
            Vector2 current = new Vector2(10f, 20f);
            Vector2 drift = new Vector2(50f, -30f);
            Vector2 player = new Vector2(-20f, 40f);
            float dt = 0.1f;
            Vector2 boxBounds = new Vector2(400f, 400f);

            // (10 + (50 - 20) * 0.1) = 13, (20 + (-30 + 40) * 0.1) = 21
            Vector2 newPos = AimStabilizerController.CalculateReticlePosition(current, drift, player, dt, boxBounds, 0f);

            Assert.AreEqual(13f, newPos.x, 0.001f);
            Assert.AreEqual(21f, newPos.y, 0.001f);
        }

        [Test]
        public void ReticlePosition_StrictlyClampedWithinTargetBoxBounds()
        {
            Vector2 current = Vector2.zero;
            Vector2 hugeDrift = new Vector2(5000f, -5000f);
            Vector2 boxBounds = new Vector2(360f, 360f);
            float reticleRadius = 20f;

            // Max bounds: halfWidth - reticleRadius = 180 - 20 = 160
            Vector2 newPos = AimStabilizerController.CalculateReticlePosition(current, hugeDrift, Vector2.zero, 1f, boxBounds, reticleRadius);

            Assert.AreEqual(160f, newPos.x, 0.001f);
            Assert.AreEqual(-160f, newPos.y, 0.001f);
        }

        #endregion

        #region Vital Circle & [MARK] Debuff Synergy Tests

        [Test]
        public void VitalCircle_DetectsInsideAndOutsidePoints()
        {
            Vector2 vitalCenter = new Vector2(0f, 40f);
            float radius = 50f;

            // Center of circle -> inside
            Assert.IsTrue(AimStabilizerController.IsInsideVitalCircle(vitalCenter, vitalCenter, radius));

            // Within radius (distance = 30) -> inside
            Assert.IsTrue(AimStabilizerController.IsInsideVitalCircle(new Vector2(0f, 70f), vitalCenter, radius));

            // Exact boundary (distance = 50) -> inside
            Assert.IsTrue(AimStabilizerController.IsInsideVitalCircle(new Vector2(50f, 40f), vitalCenter, radius));

            // Outside boundary (distance = 51) -> outside
            Assert.IsFalse(AimStabilizerController.IsInsideVitalCircle(new Vector2(51f, 40f), vitalCenter, radius));

            // Far point -> outside
            Assert.IsFalse(AimStabilizerController.IsInsideVitalCircle(new Vector2(100f, 100f), vitalCenter, radius));
        }

        [Test]
        public void VitalRadius_ExpandsByMultiplierWhenTargetMarked()
        {
            float baseRadius = 50f;
            float multiplier = 1.5f;

            float normalRadius = AimStabilizerController.CalculateVitalRadius(baseRadius, isMarked: false, multiplier);
            float markedRadius = AimStabilizerController.CalculateVitalRadius(baseRadius, isMarked: true, multiplier);

            Assert.AreEqual(50f, normalRadius, 0.001f);
            Assert.AreEqual(75f, markedRadius, 0.001f);

            // A point at distance 65 is outside normal vital circle, but inside marked vital circle!
            Vector2 center = Vector2.zero;
            Vector2 testPoint = new Vector2(65f, 0f);

            Assert.IsFalse(AimStabilizerController.IsInsideVitalCircle(testPoint, center, normalRadius));
            Assert.IsTrue(AimStabilizerController.IsInsideVitalCircle(testPoint, center, markedRadius));
        }

        [Test]
        public void DamageCalculation_DealsDoubleCritDamageWhenMarked()
        {
            int baseDamage = 15;
            float critMultiplier = 2.0f;

            int normalDmg = AimStabilizerController.CalculateDamage(baseDamage, isMarked: false, critMultiplier);
            int critDmg = AimStabilizerController.CalculateDamage(baseDamage, isMarked: true, critMultiplier);

            Assert.AreEqual(15, normalDmg);
            Assert.AreEqual(30, critDmg);
        }

        #endregion

        #region Lock-On Accumulation & Decay Tests

        [Test]
        public void LockOn_AccumulatesLinearlyInsideVitalCircle()
        {
            float timer = 0f;
            float dt = 0.25f;

            timer = AimStabilizerController.AccumulateLockOn(timer, isInsideVital: true, dt, maxDuration: 1.0f);
            Assert.AreEqual(0.25f, timer, 0.001f);

            timer = AimStabilizerController.AccumulateLockOn(timer, isInsideVital: true, 0.5f, maxDuration: 1.0f);
            Assert.AreEqual(0.75f, timer, 0.001f);

            // Reaches and clamps at 1.0s max threshold
            timer = AimStabilizerController.AccumulateLockOn(timer, isInsideVital: true, 0.5f, maxDuration: 1.0f);
            Assert.AreEqual(1.0f, timer, 0.001f);
        }

        [Test]
        public void LockOn_DecaysSmoothlyWhenOutsideVitalCircle()
        {
            float timer = 0.6f;
            float decayRate = 0.6f;
            float dt = 0.5f;

            // 0.6 - (0.6 * 0.5) = 0.3
            timer = AimStabilizerController.AccumulateLockOn(timer, isInsideVital: false, dt, maxDuration: 1.0f, decayRate: decayRate);
            Assert.AreEqual(0.3f, timer, 0.001f);

            // Cannot drop below 0.0
            timer = AimStabilizerController.AccumulateLockOn(timer, isInsideVital: false, 1.0f, maxDuration: 1.0f, decayRate: decayRate);
            Assert.AreEqual(0.0f, timer, 0.001f);
        }

        #endregion

        #region EnemyHealth & Mark Status Tests

        [Test]
        public void EnemyHealth_TakeDamage_ReducesHpAndDispatchesEvent()
        {
            bool eventFired = false;
            int receivedHp = 0;
            int receivedDelta = 0;

            EventBus.Subscribe<EnemyHealthChangedEvent>(evt =>
            {
                eventFired = true;
                receivedHp = evt.CurrentHp;
                receivedDelta = evt.Delta;
            });

            _enemyHealth.TakeDamage(15, isCritical: false, "Shot");

            Assert.IsTrue(eventFired);
            Assert.AreEqual(55, _enemyHealth.CurrentHealth);
            Assert.AreEqual(55, receivedHp);
            Assert.AreEqual(-15, receivedDelta);
        }

        [Test]
        public void EnemyHealth_ApplyMark_SetsIsMarkedAndDispatchesEvent()
        {
            bool markEventFired = false;
            bool isMarkedStatus = false;

            EventBus.Subscribe<EnemyMarkStatusChangedEvent>(evt =>
            {
                markEventFired = true;
                isMarkedStatus = evt.IsMarked;
            });

            Assert.IsFalse(_enemyHealth.IsMarked);
            _enemyHealth.ApplyMark("TestMark");

            Assert.IsTrue(markEventFired);
            Assert.IsTrue(isMarkedStatus);
            Assert.IsTrue(_enemyHealth.IsMarked);

            // Clear mark
            _enemyHealth.ClearMark("TestClear");
            Assert.IsFalse(_enemyHealth.IsMarked);
        }

        [Test]
        public void EnemyHealth_HpReachesZero_TriggersDefeat()
        {
            _enemyHealth.SetHealth(10);
            _enemyHealth.TakeDamage(15, isCritical: false, "FatalShot");

            Assert.AreEqual(0, _enemyHealth.CurrentHealth);
            Assert.IsFalse(_enemyHealth.IsAlive);
        }

        #endregion

        #region Session, Turn Commitment & Burst Chaining Tests

        [Test]
        public void TurnCommitted_WithAttackSymbols_StartsAimStabilizerSession()
        {
            bool sessionStarted = false;
            int totalBursts = 0;

            EventBus.Subscribe<AimStabilizerStartedEvent>(evt =>
            {
                sessionStarted = true;
                totalBursts = evt.TotalBursts;
            });

            // Simulate committing 2x Attack
            var symbols = new List<SlotSymbol> { SlotSymbol.ATTACK, SlotSymbol.ATTACK, SlotSymbol.HEAL };
            EventBus.Publish(new TurnCommittedEvent(1, symbols, new SlotSymbol?[2]));

            Assert.IsTrue(sessionStarted);
            Assert.AreEqual(2, totalBursts);
            Assert.IsTrue(_aimController.IsAiming);
            Assert.AreEqual(2, _aimController.TotalBursts);
        }

        [Test]
        public void TurnCommitted_WithMarkAndAttack_AppliesMarkAndStartsSession()
        {
            var symbols = new List<SlotSymbol> { SlotSymbol.MARK, SlotSymbol.ATTACK };
            EventBus.Publish(new TurnCommittedEvent(1, symbols, new SlotSymbol?[2]));

            Assert.IsTrue(_enemyHealth.IsMarked);
            Assert.IsTrue(_aimController.IsAiming);
            Assert.AreEqual(1, _aimController.TotalBursts);
            Assert.AreEqual(75f, _aimController.CurrentVitalRadius);
        }

        [Test]
        public void TurnCommitted_WithoutAttack_DoesNotStartAimStabilizer()
        {
            var symbols = new List<SlotSymbol> { SlotSymbol.HEAL, SlotSymbol.COUNTER_SHARPEN };
            EventBus.Publish(new TurnCommittedEvent(1, symbols, new SlotSymbol?[2]));

            Assert.IsFalse(_aimController.IsAiming);
            Assert.AreEqual(AimStabilizerController.AimState.Inactive, _aimController.CurrentState);
        }

        [Test]
        public void BurstChaining_TracksBurstCountAcrossMultipleShots()
        {
            _aimController.BeginAimSession(burstCount: 3);

            Assert.AreEqual(3, _aimController.TotalBursts);
            Assert.AreEqual(0, _aimController.CurrentBurstIndex);
            Assert.IsTrue(_aimController.IsAiming);
        }

        #endregion
    }
}
