using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class DefenseQTETests
    {
        private GameObject _camGo;
        private FPSCameraController _cameraController;

        private GameObject _knifeGo;
        private KnifeParryController _knifeController;

        private GameObject _defenseGo;
        private DefenseQTEController _defenseController;

        private GameObject _botAiGo;
        private BotGunnerAI _botAi;

        private GameObject _playerHealthGo;
        private PlayerHealth _playerHealth;

        private GameObject _enemyHealthGo;
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

            _playerHealthGo = new GameObject("TestPlayerHealth");
            _playerHealth = _playerHealthGo.AddComponent<PlayerHealth>();

            _enemyHealthGo = new GameObject("TestEnemyHealth");
            _enemyHealth = _enemyHealthGo.AddComponent<EnemyHealth>();
            _enemyHealth.ResetForStage(1, "Neon Rookie", 70);

            _camGo = new GameObject("TestCameraRig");
            _camGo.tag = "MainCamera";
            _camGo.AddComponent<Camera>();
            _cameraController = _camGo.AddComponent<FPSCameraController>();

            _knifeGo = new GameObject("TestKnifeRig");
            _knifeController = _knifeGo.AddComponent<KnifeParryController>();

            _botAiGo = new GameObject("TestBotGunnerAI");
            _botAi = _botAiGo.AddComponent<BotGunnerAI>();

            _defenseGo = new GameObject("TestDefenseQTE");
            _defenseController = _defenseGo.AddComponent<DefenseQTEController>();
            _defenseController.SubscribeEvents();
        }

        [TearDown]
        public void TearDown()
        {
            if (_defenseController != null) _defenseController.UnsubscribeEvents();
            if (_defenseGo != null) Object.DestroyImmediate(_defenseGo);
            if (_botAiGo != null) Object.DestroyImmediate(_botAiGo);
            if (_knifeGo != null) Object.DestroyImmediate(_knifeGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_enemyHealthGo != null) Object.DestroyImmediate(_enemyHealthGo);
            if (_playerHealthGo != null) Object.DestroyImmediate(_playerHealthGo);
            if (_gameManagerGo != null) Object.DestroyImmediate(_gameManagerGo);

            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region 1. FPSCameraController Tests (Sway / Tilt / Recoil / Breathing)

        [Test]
        public void DodgeOffset_BellCurveFormula_PeaksAtMidpointAndReturnsToZero()
        {
            float maxDist = 1.2f;

            // Start of dodge (t = 0.0): offset is 0
            Vector3 startOffset = FPSCameraController.CalculateDodgeOffset(0f, maxDist, DodgeDirection.Left);
            Assert.AreEqual(0f, startOffset.x, 0.001f);

            // Midpoint of dodge (t = 0.5): offset peaks at -1.2 units (left)
            Vector3 midOffsetLeft = FPSCameraController.CalculateDodgeOffset(0.5f, maxDist, DodgeDirection.Left);
            Assert.AreEqual(-1.2f, midOffsetLeft.x, 0.001f);

            // Midpoint for right dodge (t = 0.5): offset peaks at +1.2 units (right)
            Vector3 midOffsetRight = FPSCameraController.CalculateDodgeOffset(0.5f, maxDist, DodgeDirection.Right);
            Assert.AreEqual(1.2f, midOffsetRight.x, 0.001f);

            // End of dodge (t = 1.0): returns to 0
            Vector3 endOffset = FPSCameraController.CalculateDodgeOffset(1.0f, maxDist, DodgeDirection.Left);
            Assert.AreEqual(0f, endOffset.x, 0.001f);
        }

        [Test]
        public void DodgeRoll_BellCurveFormula_PeaksAt15Degrees()
        {
            float maxRoll = 15.0f;

            // Start: 0 deg
            float rollStart = FPSCameraController.CalculateDodgeRoll(0f, maxRoll, DodgeDirection.Left);
            Assert.AreEqual(0f, rollStart, 0.001f);

            // Midpoint (left): -15 deg roll
            float rollMidLeft = FPSCameraController.CalculateDodgeRoll(0.5f, maxRoll, DodgeDirection.Left);
            Assert.AreEqual(-15.0f, rollMidLeft, 0.001f);

            // Midpoint (right): +15 deg roll
            float rollMidRight = FPSCameraController.CalculateDodgeRoll(0.5f, maxRoll, DodgeDirection.Right);
            Assert.AreEqual(15.0f, rollMidRight, 0.001f);

            // End: 0 deg
            float rollEnd = FPSCameraController.CalculateDodgeRoll(1.0f, maxRoll, DodgeDirection.Left);
            Assert.AreEqual(0f, rollEnd, 0.001f);
        }

        [Test]
        public void TriggerDodge_SetsIsDodging_AndPublishesEvent()
        {
            bool eventFired = false;
            DodgeDirection directionReceived = DodgeDirection.Right;
            float durationReceived = 0f;

            EventBus.Subscribe<PlayerDodgeTriggeredEvent>(evt =>
            {
                eventFired = true;
                directionReceived = evt.Direction;
                durationReceived = evt.Duration;
            });

            bool success = _cameraController.TriggerDodge(DodgeDirection.Left);
            Assert.IsTrue(success);
            Assert.IsTrue(_cameraController.IsDodging);
            Assert.AreEqual(DodgeDirection.Left, _cameraController.CurrentDodgeDirection);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(DodgeDirection.Left, directionReceived);
            Assert.AreEqual(0.4f, durationReceived, 0.001f);

            // Cannot re-trigger while currently dodging
            bool retrigger = _cameraController.TriggerDodge(DodgeDirection.Right);
            Assert.IsFalse(retrigger);
        }

        [Test]
        public void FireRecoil_IncreasesPitch()
        {
            Assert.AreEqual(0f, _cameraController.CurrentRecoilPitch, 0.001f);
            _cameraController.TriggerFireRecoil(3.0f);
            Assert.AreEqual(3.0f, _cameraController.CurrentRecoilPitch, 0.001f);
        }

        [Test]
        public void IdleBreathing_CalculatesHarmonicOffset()
        {
            Vector3 b0 = FPSCameraController.CalculateBreathingOffset(0f, 1.4f, 0.025f);
            Assert.AreEqual(0f, b0.y, 0.001f); // sin(0) = 0
            Assert.AreEqual(0.010f, b0.x, 0.001f); // cos(0) * 0.4 * 0.025 = 0.010
        }

        #endregion

        #region 2. KnifeParryController Tests (Precision Window & Slashes)

        [Test]
        public void ParryWindow_ActivePrecisionTimingDetection()
        {
            float start = 0.06f;
            float end = 0.28f;

            Assert.IsFalse(KnifeParryController.IsInActiveParryWindow(0.02f, start, end), "Too early before knife reaches center");
            Assert.IsTrue(KnifeParryController.IsInActiveParryWindow(0.06f, start, end), "Exact window start");
            Assert.IsTrue(KnifeParryController.IsInActiveParryWindow(0.18f, start, end), "Sweet spot midpoint");
            Assert.IsTrue(KnifeParryController.IsInActiveParryWindow(0.28f, start, end), "Exact window end");
            Assert.IsFalse(KnifeParryController.IsInActiveParryWindow(0.35f, start, end), "Too late during knife recovery");
        }

        [Test]
        public void TriggerParry_SetsIsParrying_AndPublishesEvent()
        {
            bool eventFired = false;
            float windowReceived = 0f;

            EventBus.Subscribe<PlayerParryTriggeredEvent>(evt =>
            {
                eventFired = true;
                windowReceived = evt.ActiveWindowDuration;
            });

            bool success = _knifeController.TriggerParry();
            Assert.IsTrue(success);
            Assert.IsTrue(_knifeController.IsParrying);
            Assert.IsTrue(eventFired);
            Assert.AreEqual(0.22f, windowReceived, 0.01f);

            // Cannot re-trigger while currently parrying
            bool retrigger = _knifeController.TriggerParry();
            Assert.IsFalse(retrigger);
        }

        [Test]
        public void KnifePosition_InterpolatesFromIdleToCenterAndRecovers()
        {
            Vector3 idle = new Vector3(-0.35f, -0.28f, 0.45f);
            Vector3 center = new Vector3(0.02f, -0.06f, 0.38f);

            Vector3 atStart = KnifeParryController.CalculateKnifePosition(0f, idle, center);
            Assert.AreEqual(idle.x, atStart.x, 0.001f);

            Vector3 atPeak = KnifeParryController.CalculateKnifePosition(0.4f, idle, center);
            Assert.AreEqual(center.x, atPeak.x, 0.001f);

            Vector3 atEnd = KnifeParryController.CalculateKnifePosition(1.0f, idle, center);
            Assert.AreEqual(idle.x, atEnd.x, 0.001f);
        }

        [Test]
        public void KnifeCosmeticSkins_ApplyCorrectly()
        {
            _knifeController.ApplySkin(KnifeParryController.KnifeSkin.CyberTanto);
            Assert.AreEqual(KnifeParryController.KnifeSkin.CyberTanto, _knifeController.CurrentSkin);

            _knifeController.ApplySkin(KnifeParryController.KnifeSkin.ViperDagger);
            Assert.AreEqual(KnifeParryController.KnifeSkin.ViperDagger, _knifeController.CurrentSkin);
        }

        #endregion

        #region 3. BotGunnerAI Tests (3-Stage Progression)

        [Test]
        public void Stage1_NeonRookie_Has100PercentSlowYellowBullets()
        {
            List<EnemyShotData> pattern = BotGunnerAI.GenerateAttackPattern(stageNumber: 1, turnNumber: 1);

            Assert.AreEqual(1, pattern.Count, "Stage 1 should have 1 shot per turn");
            Assert.AreEqual(EnemyAttackType.PARRYABLE_YELLOW, pattern[0].AttackType, "Stage 1 must be 100% Yellow");
            Assert.AreEqual(1.25f, pattern[0].TelegraphDuration, 0.001f, "Slow telegraph for rookie");
            Assert.AreEqual(0.90f, pattern[0].BulletTravelTime, 0.001f, "Slow bullet travel");
        }

        [Test]
        public void Stage2_ChromeGambler_MixesRedAndYellowBullets()
        {
            // Turn 1: Yellow
            List<EnemyShotData> turn1 = BotGunnerAI.GenerateAttackPattern(stageNumber: 2, turnNumber: 1);
            Assert.AreEqual(1, turn1.Count);
            Assert.AreEqual(EnemyAttackType.PARRYABLE_YELLOW, turn1[0].AttackType);

            // Turn 2: Red
            List<EnemyShotData> turn2 = BotGunnerAI.GenerateAttackPattern(stageNumber: 2, turnNumber: 2);
            Assert.AreEqual(1, turn2.Count);
            Assert.AreEqual(EnemyAttackType.UNPARRYABLE_RED, turn2[0].AttackType);

            // Turn 3: Yellow
            List<EnemyShotData> turn3 = BotGunnerAI.GenerateAttackPattern(stageNumber: 2, turnNumber: 3);
            Assert.AreEqual(EnemyAttackType.PARRYABLE_YELLOW, turn3[0].AttackType);

            Assert.AreEqual(0.95f, turn1[0].TelegraphDuration, 0.001f, "Medium telegraph");
            Assert.AreEqual(0.65f, turn1[0].BulletTravelTime, 0.001f, "Medium travel");
        }

        [Test]
        public void Stage3_CyberBaron_RapidMultiShotAlternatingBursts()
        {
            List<EnemyShotData> bossPattern = BotGunnerAI.GenerateAttackPattern(stageNumber: 3, turnNumber: 1);

            Assert.AreEqual(2, bossPattern.Count, "Boss should fire rapid multi-shot burst");
            Assert.AreEqual(EnemyAttackType.UNPARRYABLE_RED, bossPattern[0].AttackType, "First shot is Red (Must Dodge)");
            Assert.AreEqual(EnemyAttackType.PARRYABLE_YELLOW, bossPattern[1].AttackType, "Second shot is rapid Yellow (Must Parry)");

            Assert.AreEqual(0.65f, bossPattern[0].TelegraphDuration, 0.001f, "Fast boss telegraph");
            Assert.AreEqual(0.45f, bossPattern[0].BulletTravelTime, 0.001f, "Fast boss bullet travel");
            Assert.AreEqual(0.32f, bossPattern[0].PauseAfterShot, 0.001f, "Short pause between bursts");
        }

        [Test]
        public void BotGunnerAI_IsParryableAndIsUnparryable_Helpers()
        {
            Assert.IsTrue(BotGunnerAI.IsParryable(EnemyAttackType.PARRYABLE_YELLOW));
            Assert.IsFalse(BotGunnerAI.IsParryable(EnemyAttackType.UNPARRYABLE_RED));

            Assert.IsTrue(BotGunnerAI.IsUnparryable(EnemyAttackType.UNPARRYABLE_RED));
            Assert.IsFalse(BotGunnerAI.IsUnparryable(EnemyAttackType.PARRYABLE_YELLOW));
        }

        #endregion

        #region 4. DefenseQTEController Resolution Logic Tests

        [Test]
        public void RedBullet_Dodged_ResultsInZeroDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.UNPARRYABLE_RED,
                isDodging: true,
                isParryingInWindow: false,
                isParryingAttempted: false,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.DodgeSuccess, res.Result);
            Assert.AreEqual(0, res.DamageToPlayer);
            Assert.AreEqual(0, res.DamageToEnemy);
            Assert.IsFalse(res.IsCriticalParry);
        }

        [Test]
        public void RedBullet_ParryAttempted_FailsWithFullDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.UNPARRYABLE_RED,
                isDodging: false,
                isParryingInWindow: true, // Even if parrying inside window, RED cannot be parried!
                isParryingAttempted: true,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.ParryFailedRed, res.Result);
            Assert.AreEqual(25, res.DamageToPlayer);
            Assert.AreEqual(0, res.DamageToEnemy);
        }

        [Test]
        public void YellowBullet_ParriedInPrecisionWindow_DealsCriticalDeflectionDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                isDodging: false,
                isParryingInWindow: true,
                isParryingAttempted: true,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.ParrySuccess, res.Result);
            Assert.AreEqual(0, res.DamageToPlayer);
            Assert.AreEqual(40, res.DamageToEnemy);
            Assert.IsTrue(res.IsCriticalParry);
        }

        [Test]
        public void YellowBullet_ParriedWithCounterBuff_Adds15TrueDamageBonus()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                isDodging: false,
                isParryingInWindow: true,
                isParryingAttempted: true,
                isCounterActive: true,
                baseDeflectionDamage: 40,
                counterBonusDamage: 15
            );

            Assert.AreEqual(DefenseResult.ParrySuccess, res.Result);
            Assert.AreEqual(0, res.DamageToPlayer);
            Assert.AreEqual(55, res.DamageToEnemy, "40 base + 15 Counter True Damage bonus = 55");
            Assert.IsTrue(res.IsCriticalParry);
        }

        [Test]
        public void YellowBullet_Dodged_ResultsInSafeZeroDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                isDodging: true,
                isParryingInWindow: false,
                isParryingAttempted: false,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.DodgeSuccess, res.Result);
            Assert.AreEqual(0, res.DamageToPlayer);
            Assert.AreEqual(0, res.DamageToEnemy);
        }

        [Test]
        public void YellowBullet_MissedTimingParry_TakesDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                isDodging: false,
                isParryingInWindow: false, // Late or early
                isParryingAttempted: true,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.ParryFailedTiming, res.Result);
            Assert.AreEqual(25, res.DamageToPlayer);
        }

        [Test]
        public void NoInput_TakesFullEnemyGunshotDamage()
        {
            var res = DefenseQTEController.ResolveDefenseOutcome(
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                isDodging: false,
                isParryingInWindow: false,
                isParryingAttempted: false,
                isCounterActive: false,
                baseDeflectionDamage: 40,
                counterBonusDamage: 0
            );

            Assert.AreEqual(DefenseResult.HitTaken, res.Result);
            Assert.AreEqual(25, res.DamageToPlayer);
        }

        #endregion

        #region 5. Counter / Sharpen Sinergy & Event Bus Integration

        [Test]
        public void TurnCommitted_WithCounterSharpen_ActivatesCounterBuff()
        {
            Assert.IsFalse(_defenseController.IsCounterBuffActive);

            var symbols = new List<SlotSymbol> { SlotSymbol.COUNTER_SHARPEN, SlotSymbol.ATTACK };
            EventBus.Publish(new TurnCommittedEvent(1, symbols, new SlotSymbol?[2]));

            Assert.IsTrue(_defenseController.IsCounterBuffActive);
        }

        [Test]
        public void StageStarted_ResetsTelemetryCounters()
        {
            // Simulate prior combat data
            _defenseController.ActivateCounterBuff();
            Assert.IsTrue(_defenseController.IsCounterBuffActive);

            EventBus.Publish(new StageStartedEvent(2, "Stage 2: Chrome Gambler", 90));

            Assert.AreEqual(0, _defenseController.DodgeRedCount);
            Assert.AreEqual(0, _defenseController.ParryYellowCount);
            Assert.AreEqual(0, _defenseController.UnparryableFailCount);
            Assert.AreEqual(0, _defenseController.DamageDeflected);
            Assert.IsFalse(_defenseController.IsCounterBuffActive);
        }

        [Test]
        public void ParryDeflectionDamage_EliminatingEnemy_TriggersVictory()
        {
            // Set enemy to 20 HP
            _enemyHealth.TakeDamage(50, isCritical: false, reason: "Setup");
            Assert.AreEqual(20, _enemyHealth.CurrentHealth);

            // Reflected parry deals 40 damage
            _enemyHealth.TakeDamage(40, isCritical: true, reason: "CriticalDeflectionSlash");

            Assert.AreEqual(0, _enemyHealth.CurrentHealth);
            Assert.IsFalse(_enemyHealth.IsAlive);
        }

        [Test]
        public void StageTransition_EnemyHealthResetsForStage2AndStage3()
        {
            // Stage 1 -> 70 HP
            _enemyHealth.ResetForStage(1, "Neon Rookie", 70);
            Assert.AreEqual(70, _enemyHealth.MaxHealth);
            Assert.AreEqual(70, _enemyHealth.CurrentHealth);

            // Stage 2 -> 90 HP
            _enemyHealth.ResetForStage(2, "Chrome Gambler", 90);
            Assert.AreEqual(90, _enemyHealth.MaxHealth);
            Assert.AreEqual(90, _enemyHealth.CurrentHealth);

            // Stage 3 -> 120 HP
            _enemyHealth.ResetForStage(3, "The Cyber Baron", 120);
            Assert.AreEqual(120, _enemyHealth.MaxHealth);
            Assert.AreEqual(120, _enemyHealth.CurrentHealth);
        }

        #endregion
    }
}
