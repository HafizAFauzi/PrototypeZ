using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.Monetization;
using LuckyDuelist.Telemetry;
using LuckyDuelist.UI;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class TelemetryTests
    {
        private GameObject _telemetryServiceGo;
        private GameObject _experimentManagerGo;
        private GameObject _gameManagerGo;
        private GameObject _settlementViewGo;

        private TelemetryService _telemetryService;
        private ExperimentManager _experimentManager;
        private GameManager _gameManager;
        private SettlementSurveyView _settlementView;

        private string _testOfflineFilePath;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            EventBus.ClearAll();

            _testOfflineFilePath = Path.Combine(Application.temporaryCachePath, "test_pending_logs.json");
            if (File.Exists(_testOfflineFilePath))
            {
                File.Delete(_testOfflineFilePath);
            }

            // 1. Experiment Manager
            _experimentManagerGo = new GameObject("Test_ExperimentManager");
            _experimentManager = _experimentManagerGo.AddComponent<ExperimentManager>();

            // 2. Game Manager
            _gameManagerGo = new GameObject("Test_GameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();

            // 3. Telemetry Service
            _telemetryServiceGo = new GameObject("Test_TelemetryService");
            _telemetryService = _telemetryServiceGo.AddComponent<TelemetryService>();
            _telemetryService.SetOfflineFilePathForTesting(_testOfflineFilePath);
            _telemetryService.ResetSessionForTesting();

            // 4. Settlement Survey View
            _settlementViewGo = new GameObject("Test_SettlementSurveyView");
            _settlementView = _settlementViewGo.AddComponent<SettlementSurveyView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testOfflineFilePath))
            {
                File.Delete(_testOfflineFilePath);
            }

            if (_settlementViewGo != null) UnityEngine.Object.DestroyImmediate(_settlementViewGo);
            if (_telemetryServiceGo != null) UnityEngine.Object.DestroyImmediate(_telemetryServiceGo);
            if (_gameManagerGo != null) UnityEngine.Object.DestroyImmediate(_gameManagerGo);
            if (_experimentManagerGo != null) UnityEngine.Object.DestroyImmediate(_experimentManagerGo);

            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region 1. Session Initialization & Participant Assignment

        [Test]
        public void TelemetryService_InitializesWithActiveParticipantAndGroup()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);

            _telemetryService.InitializeNewSession();
            var payload = _telemetryService.CurrentPayload;

            Assert.IsNotNull(payload);
            Assert.AreEqual(_experimentManager.UserId, payload.user_id);
            Assert.AreEqual("GROUP_C", payload.group_id);
            Assert.IsFalse(string.IsNullOrEmpty(payload.session.session_id));
            Assert.AreEqual(20000.0f, payload.monetization.initial_budget);
            Assert.AreEqual(20000.0f, payload.monetization.remaining_budget);
        }

        #endregion

        #region 2. Ad Revenue & Impressions Breakdown

        [Test]
        public void TelemetryService_AccumulatesAdImpressionsAndRevenueCorrectly()
        {
            _telemetryService.ResetSessionForTesting();

            // Simulate 1x Respin Ad, 1x Revive Ad, 1x Double Gold Ad (Rp 250 each)
            EventBus.Publish(new AdImpressionCompletedEvent(
                adId: "AD-01",
                userId: "TEST-USER",
                placement: AdPlacementType.RespinSlot,
                durationSeconds: 10f,
                ecpmRateIdr: 250f,
                revenueEarned: 250f,
                timestamp: DateTime.UtcNow
            ));

            EventBus.Publish(new AdImpressionCompletedEvent(
                adId: "AD-02",
                userId: "TEST-USER",
                placement: AdPlacementType.ReviveMatch,
                durationSeconds: 10f,
                ecpmRateIdr: 250f,
                revenueEarned: 250f,
                timestamp: DateTime.UtcNow
            ));

            EventBus.Publish(new AdImpressionCompletedEvent(
                adId: "AD-03",
                userId: "TEST-USER",
                placement: AdPlacementType.DoubleGold,
                durationSeconds: 10f,
                ecpmRateIdr: 250f,
                revenueEarned: 250f,
                timestamp: DateTime.UtcNow
            ));

            var mon = _telemetryService.CurrentPayload.monetization;

            Assert.AreEqual(3, mon.ad_impressions_count);
            Assert.AreEqual(750.0f, mon.total_ad_revenue);
            Assert.AreEqual(1, mon.ad_impressions_breakdown.respin_slot);
            Assert.AreEqual(1, mon.ad_impressions_breakdown.revive_match);
            Assert.AreEqual(1, mon.ad_impressions_breakdown.double_gold);
            Assert.AreEqual(750.0f, mon.total_user_arpu);
        }

        #endregion

        #region 3. IAP Purchases & Wallet Balance Reduction

        [Test]
        public void TelemetryService_AccumulatesIAPTransactionsAndReducesBudget()
        {
            _telemetryService.ResetSessionForTesting();

            // Simulate buying 2 skins (Rp 10.000 each)
            EventBus.Publish(new IAPTransactionCompletedEvent(
                transactionId: "TX-01",
                userId: "TEST-USER",
                itemId: "SKIN_REVOLVER_DAMASCUS",
                itemName: "Revolver Damascus",
                itemType: CosmeticItemType.WeaponRevolver,
                amountPaid: 10000.0f,
                remainingWalletBalance: 10000.0f,
                timestamp: DateTime.UtcNow
            ));

            EventBus.Publish(new IAPTransactionCompletedEvent(
                transactionId: "TX-02",
                userId: "TEST-USER",
                itemId: "SKIN_KNIFE_TANTO",
                itemName: "Tanto Combat Knife",
                itemType: CosmeticItemType.WeaponKnife,
                amountPaid: 10000.0f,
                remainingWalletBalance: 0.0f,
                timestamp: DateTime.UtcNow
            ));

            var mon = _telemetryService.CurrentPayload.monetization;

            Assert.AreEqual(2, mon.purchased_items.Count);
            Assert.AreEqual(20000.0f, mon.total_iap_revenue);
            Assert.AreEqual(0.0f, mon.remaining_budget);
            Assert.AreEqual("SKIN_REVOLVER_DAMASCUS", mon.purchased_items[0].item_id);
            Assert.AreEqual("SKIN_KNIFE_TANTO", mon.purchased_items[1].item_id);
        }

        #endregion

        #region 4. ARPU Formula Calculation (One-Way ANOVA Base)

        [Test]
        public void TelemetryService_CalculatesTotalARPU_MatchingResearchFormula()
        {
            _telemetryService.ResetSessionForTesting();

            // Add Ad impressions: 3x @ Rp 250 = Rp 750
            for (int i = 0; i < 3; i++)
            {
                EventBus.Publish(new AdImpressionCompletedEvent(
                    adId: $"AD-{i}",
                    userId: "TEST-USER",
                    placement: AdPlacementType.RespinSlot,
                    durationSeconds: 10f,
                    ecpmRateIdr: 250f,
                    revenueEarned: 250f,
                    timestamp: DateTime.UtcNow
                ));
            }

            // Add IAP purchase: Rp 20.000
            EventBus.Publish(new IAPTransactionCompletedEvent(
                transactionId: "TX-01",
                userId: "TEST-USER",
                itemId: "SKIN_REVOLVER_GOLD",
                itemName: "Cyber Gold Revolver",
                itemType: CosmeticItemType.WeaponRevolver,
                amountPaid: 20000.0f,
                remainingWalletBalance: 0.0f,
                timestamp: DateTime.UtcNow
            ));

            var mon = _telemetryService.CurrentPayload.monetization;

            // ARPU_Total = ARPU_Ads + ARPU_IAP = 750 + 20000 = 20750
            Assert.AreEqual(750.0f, mon.total_ad_revenue);
            Assert.AreEqual(20000.0f, mon.total_iap_revenue);
            Assert.AreEqual(20750.0f, mon.total_user_arpu);
        }

        #endregion

        #region 5. FPS Reflex & Timing Performance Tracking

        [Test]
        public void TelemetryService_RecordsCombatReflexMetricsCorrectly()
        {
            _telemetryService.ResetSessionForTesting();

            // Aim shots fired (lock-on full)
            EventBus.Publish(new AimShotFiredEvent(damageDealt: 20, isCritical: false, burstIndex: 0, totalBursts: 2, remainingEnemyHp: 80));
            EventBus.Publish(new AimShotFiredEvent(damageDealt: 40, isCritical: true, burstIndex: 1, totalBursts: 2, remainingEnemyHp: 40));

            // Successful Yellow Bullet Parry with knife [E] (deflected damage)
            EventBus.Publish(new DefenseResolvedEvent(
                result: DefenseResult.ParrySuccess,
                attackType: EnemyAttackType.PARRYABLE_YELLOW,
                damageToPlayer: 0,
                damageToEnemy: 50,
                isCriticalParry: true,
                burstIndex: 0,
                totalBursts: 1
            ));

            // Successful Red Bullet Dodge [Q]
            EventBus.Publish(new DefenseResolvedEvent(
                result: DefenseResult.DodgeSuccess,
                attackType: EnemyAttackType.UNPARRYABLE_RED,
                damageToPlayer: 0,
                damageToEnemy: 0,
                isCriticalParry: false,
                burstIndex: 0,
                totalBursts: 1
            ));

            // Failed parry on red bullet (unparryable fail)
            EventBus.Publish(new DefenseResolvedEvent(
                result: DefenseResult.ParryFailedRed,
                attackType: EnemyAttackType.UNPARRYABLE_RED,
                damageToPlayer: 25,
                damageToEnemy: 0,
                isCriticalParry: false,
                burstIndex: 0,
                totalBursts: 1
            ));

            var fps = _telemetryService.CurrentPayload.fps_combat_performance;

            Assert.AreEqual(2, fps.total_aim_shots_fired);
            Assert.AreEqual(2, fps.total_aim_lockons_successful);
            Assert.AreEqual(1, fps.total_yellow_bullets_parried);
            Assert.AreEqual(50, fps.total_damage_deflected);
            Assert.AreEqual(1, fps.total_red_bullets_dodged);
            Assert.AreEqual(1, fps.total_unparryable_fails);
        }

        #endregion

        #region 6. Match Details Per Stage

        [Test]
        public void TelemetryService_LogsMatchDetailsAcrossStages()
        {
            _telemetryService.ResetSessionForTesting();

            // Stage 1
            EventBus.Publish(new StageStartedEvent(stageNumber: 1, stageName: "Stage 1: Neon Rookie", bossMaxHp: 70));
            EventBus.Publish(new DefenseResolvedEvent(DefenseResult.ParrySuccess, EnemyAttackType.PARRYABLE_YELLOW, 0, 30, false, 0, 1));
            EventBus.Publish(new StageCompletedEvent(stageNumber: 1, isVictory: true, turnsTaken: 3, playerFinalHp: 80, botFinalHp: 0, goldEarned: 100));

            // Stage 2
            EventBus.Publish(new StageStartedEvent(stageNumber: 2, stageName: "Stage 2: Chrome Gambler", bossMaxHp: 90));
            EventBus.Publish(new DefenseResolvedEvent(DefenseResult.DodgeSuccess, EnemyAttackType.UNPARRYABLE_RED, 0, 0, false, 0, 1));
            EventBus.Publish(new DefenseResolvedEvent(DefenseResult.ParryFailedRed, EnemyAttackType.UNPARRYABLE_RED, 25, 0, false, 0, 1));
            EventBus.Publish(new StageCompletedEvent(stageNumber: 2, isVictory: true, turnsTaken: 5, playerFinalHp: 55, botFinalHp: 0, goldEarned: 150));

            var matches = _telemetryService.CurrentPayload.match_details;

            Assert.GreaterOrEqual(matches.Count, 2);

            var s1 = matches.Find(m => m.stage == 1);
            Assert.IsNotNull(s1);
            Assert.AreEqual("WIN", s1.outcome);
            Assert.AreEqual(3, s1.turns);
            Assert.AreEqual(1, s1.parry_count);

            var s2 = matches.Find(m => m.stage == 2);
            Assert.IsNotNull(s2);
            Assert.AreEqual("WIN", s2.outcome);
            Assert.AreEqual(5, s2.turns);
            Assert.AreEqual(1, s2.dodge_count);
            Assert.AreEqual(1, s2.unparryable_fails);
        }

        #endregion

        #region 7. JSON Payload Schema Validation (Matching ERD.md Section 4)

        [Test]
        public void TelemetryPayload_SerializesToValidJson_MatchingERDSchema()
        {
            var p = new TelemetrySessionPayload
            {
                user_id = "RES-C-029",
                group_id = "GROUP_C",
                device_model = "Xiaomi Redmi Note 12 / Android 13",
                created_at = "2026-11-01T17:10:00Z"
            };

            p.session.session_id = "c1f7b942-8e12-42da-921a-99d8e411b033";
            p.session.play_duration_sec = 395.4f;
            p.session.matches_completed = 3;
            p.session.matches_won = 3;
            p.session.completed_stage_3 = true;
            p.session.soft_currency_gold = 550;

            p.monetization.initial_budget = 20000.0f;
            p.monetization.remaining_budget = 0.0f;
            p.monetization.total_iap_revenue = 20000.0f;
            p.monetization.ad_impressions_count = 3;
            p.monetization.ad_impressions_breakdown.respin_slot = 1;
            p.monetization.ad_impressions_breakdown.revive_match = 1;
            p.monetization.ad_impressions_breakdown.double_gold = 1;
            p.monetization.total_ad_revenue = 750.0f;
            p.monetization.total_user_arpu = 20750.0f;

            p.fps_combat_performance.total_aim_shots_fired = 11;
            p.fps_combat_performance.total_aim_lockons_successful = 11;
            p.fps_combat_performance.total_red_bullets_dodged = 4;
            p.fps_combat_performance.total_yellow_bullets_parried = 3;
            p.fps_combat_performance.total_unparryable_fails = 1;
            p.fps_combat_performance.total_damage_deflected = 120;

            string json = p.ToJson(prettyPrint: true);

            // Assert JSON contains key field names from ERD Section 4
            StringAssert.Contains("\"user_id\": \"RES-C-029\"", json);
            StringAssert.Contains("\"group_id\": \"GROUP_C\"", json);
            StringAssert.Contains("\"session_id\": \"c1f7b942-8e12-42da-921a-99d8e411b033\"", json);
            StringAssert.Contains("\"total_user_arpu\": 20750.0", json);
            StringAssert.Contains("\"total_damage_deflected\": 120", json);

            // Round-trip deserialization verification
            var roundTrip = TelemetrySessionPayload.FromJson(json);
            Assert.IsNotNull(roundTrip);
            Assert.AreEqual("RES-C-029", roundTrip.user_id);
            Assert.AreEqual("GROUP_C", roundTrip.group_id);
            Assert.AreEqual(20750.0f, roundTrip.monetization.total_user_arpu);
            Assert.AreEqual(120, roundTrip.fps_combat_performance.total_damage_deflected);
        }

        #endregion

        #region 8. Firestore REST Document Adapter

        [Test]
        public void TelemetryPayloadAdapter_BuildsValidFirestoreDocumentFormat()
        {
            var p = new TelemetrySessionPayload
            {
                user_id = "RES-A-010",
                group_id = "GROUP_A"
            };
            p.session.completed_stage_3 = true;
            p.monetization.total_user_arpu = 1250.0f;

            string firestoreJson = TelemetryPayloadAdapter.BuildFirestoreDocumentJson(p);

            StringAssert.Contains("\"fields\":{", firestoreJson);
            StringAssert.Contains("\"user_id\":{\"stringValue\":\"RES-A-010\"}", firestoreJson);
            StringAssert.Contains("\"group_id\":{\"stringValue\":\"GROUP_A\"}", firestoreJson);
            StringAssert.Contains("\"completed_stage_3\":{\"booleanValue\":true}", firestoreJson);
            StringAssert.Contains("\"total_user_arpu\":{\"doubleValue\":1250.00}", firestoreJson);
        }

        #endregion

        #region 9. Offline Local Fallback Persistence

        [Test]
        public void TelemetryService_SavesAndLoads_PendingLogsOfflineQueue()
        {
            var p1 = new TelemetrySessionPayload { user_id = "OFFLINE-01", group_id = "GROUP_A" };
            p1.monetization.total_user_arpu = 500f;

            var p2 = new TelemetrySessionPayload { user_id = "OFFLINE-02", group_id = "GROUP_B" };
            p2.monetization.total_user_arpu = 10000f;

            // Save both sessions to fallback file
            bool saved1 = _telemetryService.SaveToOfflineFallback(p1);
            bool saved2 = _telemetryService.SaveToOfflineFallback(p2);

            Assert.IsTrue(saved1);
            Assert.IsTrue(saved2);
            Assert.IsTrue(File.Exists(_testOfflineFilePath));

            // Load and verify queue
            var queue = _telemetryService.LoadPendingLogs();
            Assert.IsNotNull(queue);
            Assert.AreEqual(2, queue.pendingSessions.Count);
            Assert.AreEqual("OFFLINE-01", queue.pendingSessions[0].user_id);
            Assert.AreEqual(500f, queue.pendingSessions[0].monetization.total_user_arpu);
            Assert.AreEqual("OFFLINE-02", queue.pendingSessions[1].user_id);
            Assert.AreEqual(10000f, queue.pendingSessions[1].monetization.total_user_arpu);

            // Test clearing queue
            _telemetryService.ClearPendingLogs();
            Assert.IsFalse(File.Exists(_testOfflineFilePath));
            Assert.AreEqual(0, _telemetryService.GetPendingLogsCount());
        }

        #endregion

        #region 10. Survey URL Query Parameter Injection

        [Test]
        public void SurveyRedirection_ReplacesUserIdAndGroupIdParametersCorrectly()
        {
            string template = "https://docs.google.com/forms/d/e/FORM_123/viewform?usp=pp_url&entry.101={USER_ID}&entry.202={GROUP_ID}";
            string userId = "RES C-045#";
            string groupId = "GROUP_C";

            string escapedUserId = UnityEngine.Networking.UnityWebRequest.EscapeURL(userId);
            string escapedGroupId = UnityEngine.Networking.UnityWebRequest.EscapeURL(groupId);

            string finalUrl = template
                .Replace("{USER_ID}", escapedUserId)
                .Replace("{GROUP_ID}", escapedGroupId);

            StringAssert.Contains("entry.101=RES+C-045%23", finalUrl);
            StringAssert.Contains("entry.202=GROUP_C", finalUrl);
        }

        #endregion

        #region 11. Final Settlement Triggers Auto Cloud Sync Event

        [Test]
        public void FinalSettlement_PublishesTelemetrySyncStarted_AndSavesOfflineWhenEndpointPlaceholder()
        {
            _telemetryService.ResetSessionForTesting();
            _telemetryService.EndpointUrl = "https://your-project.supabase.co/rest/v1/telemetry_sessions"; // Placeholder

            bool syncCompletedFired = false;
            bool savedLocally = false;

            EventBus.Subscribe<TelemetrySyncCompletedEvent>(evt =>
            {
                syncCompletedFired = true;
                savedLocally = evt.IsSavedToLocalFallback;
            });

            // Conclude run
            EventBus.Publish(new FinalSettlementEvent(
                completedAllStages: true,
                stagesCompleted: 3,
                matchesWon: 3,
                totalGoldEarned: 500,
                sessionDurationSeconds: 240f,
                outcome: "WIN"
            ));

            Assert.IsTrue(syncCompletedFired);
            Assert.IsTrue(savedLocally);
            Assert.AreEqual(1, _telemetryService.GetPendingLogsCount());
        }

        #endregion
    }
}
