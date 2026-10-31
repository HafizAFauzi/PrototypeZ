using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LuckyDuelist.Combat;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;
using LuckyDuelist.Monetization;

namespace LuckyDuelist.Tests
{
    [TestFixture]
    public class MonetizationTests
    {
        private GameObject _experimentManagerGo;
        private GameObject _gameManagerGo;
        private GameObject _adPlayerGo;
        private GameObject _storeGo;
        private GameObject _cosmeticApplierGo;
        private GameObject _knifeGo;
        private GameObject _aimGo;
        private GameObject _slotGo;
        private GameObject _playerHpGo;

        private ExperimentManager _experimentManager;
        private GameManager _gameManager;
        private DummyAdPlayer _adPlayer;
        private VirtualIAPStore _store;
        private CosmeticApplier _cosmeticApplier;
        private KnifeParryController _knife;
        private AimStabilizerController _aim;
        private SlotRevolverController _slot;
        private PlayerHealth _playerHp;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            EventBus.ClearAll();

            // Core Managers
            _experimentManagerGo = new GameObject("Test_ExperimentManager");
            _experimentManager = _experimentManagerGo.AddComponent<ExperimentManager>();

            _gameManagerGo = new GameObject("Test_GameManager");
            _gameManager = _gameManagerGo.AddComponent<GameManager>();

            // Combat & Entities
            _playerHpGo = new GameObject("Test_PlayerHp");
            _playerHp = _playerHpGo.AddComponent<PlayerHealth>();

            _slotGo = new GameObject("Test_SlotController");
            _slot = _slotGo.AddComponent<SlotRevolverController>();

            _aimGo = new GameObject("Test_AimController");
            _aim = _aimGo.AddComponent<AimStabilizerController>();

            _knifeGo = new GameObject("Test_KnifeController");
            _knife = _knifeGo.AddComponent<KnifeParryController>();

            // Monetization Systems
            _adPlayerGo = new GameObject("Test_DummyAdPlayer");
            _adPlayer = _adPlayerGo.AddComponent<DummyAdPlayer>();
            _adPlayer.ResetStatsForTesting();

            _storeGo = new GameObject("Test_VirtualIAPStore");
            _store = _storeGo.AddComponent<VirtualIAPStore>();
            _store.ResetStoreDataForTesting();

            _cosmeticApplierGo = new GameObject("Test_CosmeticApplier");
            _cosmeticApplier = _cosmeticApplierGo.AddComponent<CosmeticApplier>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cosmeticApplierGo != null) UnityEngine.Object.DestroyImmediate(_cosmeticApplierGo);
            if (_storeGo != null) UnityEngine.Object.DestroyImmediate(_storeGo);
            if (_adPlayerGo != null) UnityEngine.Object.DestroyImmediate(_adPlayerGo);
            if (_knifeGo != null) UnityEngine.Object.DestroyImmediate(_knifeGo);
            if (_aimGo != null) UnityEngine.Object.DestroyImmediate(_aimGo);
            if (_slotGo != null) UnityEngine.Object.DestroyImmediate(_slotGo);
            if (_playerHpGo != null) UnityEngine.Object.DestroyImmediate(_playerHpGo);
            if (_gameManagerGo != null) UnityEngine.Object.DestroyImmediate(_gameManagerGo);
            if (_experimentManagerGo != null) UnityEngine.Object.DestroyImmediate(_experimentManagerGo);

            EventBus.ClearAll();
            PlayerPrefs.DeleteAll();
        }

        #region 1. Feature Flagging Tests (Group A, B, C)

        [Test]
        public void FeatureFlagging_GroupA_EnforcesAdsOnlyAndLocksStore()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            Assert.IsTrue(_experimentManager.IsAdsEnabled, "Group A must enable Rewarded Ads.");
            Assert.IsFalse(_experimentManager.IsIAPStoreEnabled, "Group A must disable IAP Store.");
            Assert.AreEqual(0.0f, _experimentManager.CurrentWalletBalance, "Group A starts with 0 wallet budget.");

            // Attempting to open store must fail
            bool opened = _store.OpenStore();
            Assert.IsFalse(opened, "Store cannot open in Group A.");
            Assert.IsFalse(_store.IsStoreOpen);

            // Ad request must be accepted
            bool adTriggered = _adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null);
            Assert.IsTrue(adTriggered, "Group A must allow Rewarded Ads.");
            _adPlayer.DismissWithoutReward();
        }

        [Test]
        public void FeatureFlagging_GroupB_EnforcesIAPOnlyAndDisablesAds()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            Assert.IsFalse(_experimentManager.IsAdsEnabled, "Group B must disable Rewarded Ads.");
            Assert.IsTrue(_experimentManager.IsIAPStoreEnabled, "Group B must enable IAP Store.");
            Assert.AreEqual(20000.0f, _experimentManager.CurrentWalletBalance, "Group B has Rp 20.000 quota.");

            // Store must open
            bool opened = _store.OpenStore();
            Assert.IsTrue(opened, "Store must open in Group B.");
            Assert.IsTrue(_store.IsStoreOpen);
            _store.CloseStore();

            // Ad requests must be rejected
            bool adTriggered = _adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null);
            Assert.IsFalse(adTriggered, "Rewarded Ads must be rejected in Group B.");
            Assert.IsFalse(_adPlayer.IsPlayingAd);

            bool respinAd = _adPlayer.PlayRespinAd(0);
            Assert.IsFalse(respinAd);

            bool reviveAd = _adPlayer.PlayReviveAd();
            Assert.IsFalse(reviveAd);

            bool doubleGoldAd = _adPlayer.PlayDoubleGoldAd();
            Assert.IsFalse(doubleGoldAd);
        }

        [Test]
        public void FeatureFlagging_GroupC_EnablesBothStoreAndAds()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);

            Assert.IsTrue(_experimentManager.IsAdsEnabled, "Group C must enable Rewarded Ads.");
            Assert.IsTrue(_experimentManager.IsIAPStoreEnabled, "Group C must enable IAP Store.");
            Assert.AreEqual(20000.0f, _experimentManager.CurrentWalletBalance, "Group C has Rp 20.000 quota.");

            // Store can open
            bool opened = _store.OpenStore();
            Assert.IsTrue(opened, "Store must open in Group C.");
            _store.CloseStore();

            // Ad can trigger
            bool adTriggered = _adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null);
            Assert.IsTrue(adTriggered, "Rewarded Ads must trigger in Group C.");
            _adPlayer.DismissWithoutReward();
        }

        #endregion

        #region 2. DummyAdPlayer Tests (Countdown, Rewards, eCPM)

        [Test]
        public void DummyAdPlayer_CalculatesConstantEcpmRevenue_Rp250()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            Assert.AreEqual(0, _adPlayer.TotalAdImpressions);
            Assert.AreEqual(0.0f, _adPlayer.TotalAdRevenue);

            // Impression 1
            _adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null);
            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.AreEqual(1, _adPlayer.TotalAdImpressions);
            Assert.AreEqual(250.0f, _adPlayer.TotalAdRevenue, "Each impression earns constant Rp 250,00.");

            // Impression 2
            _adPlayer.ShowRewardedAd(AdPlacementType.ReviveMatch, null);
            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.AreEqual(2, _adPlayer.TotalAdImpressions);
            Assert.AreEqual(500.0f, _adPlayer.TotalAdRevenue);

            // Impression 3
            _adPlayer.ShowRewardedAd(AdPlacementType.DoubleGold, null);
            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.AreEqual(3, _adPlayer.TotalAdImpressions);
            Assert.AreEqual(750.0f, _adPlayer.TotalAdRevenue);
        }

        [Test]
        public void DummyAdPlayer_DispatchesAdImpressionCompletedEvent()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            bool eventFired = false;
            float recordedRevenue = 0f;
            AdPlacementType recordedPlacement = AdPlacementType.RespinSlot;

            EventBus.Subscribe<AdImpressionCompletedEvent>(evt =>
            {
                eventFired = true;
                recordedRevenue = evt.RevenueEarned;
                recordedPlacement = evt.Placement;
            });

            _adPlayer.ShowRewardedAd(AdPlacementType.DoubleGold, null);
            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.IsTrue(eventFired, "AdImpressionCompletedEvent must be published upon ad completion.");
            Assert.AreEqual(250.0f, recordedRevenue);
            Assert.AreEqual(AdPlacementType.DoubleGold, recordedPlacement);
        }

        [Test]
        public void DummyAdPlayer_RewardCallback_ExecutesOnCompletion()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            bool rewardReceived = false;
            _adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, onRewardGranted: () =>
            {
                rewardReceived = true;
            });

            Assert.IsFalse(rewardReceived, "Reward must not be granted before ad completes.");
            Assert.IsTrue(_adPlayer.IsPlayingAd);

            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.IsTrue(rewardReceived, "Reward callback must execute when ad completes.");
            Assert.IsFalse(_adPlayer.IsPlayingAd);
        }

        [Test]
        public void DummyAdPlayer_RespinAd_ExecutesReelRespin()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            _slot.SpinCylinderInstant();

            bool respinEventFired = false;
            EventBus.Subscribe<SlotRespinTriggeredEvent>(evt =>
            {
                respinEventFired = true;
                Assert.AreEqual(0, evt.ReelIndex);
                Assert.IsTrue(evt.ViaRewardedAd);
            });

            bool adStarted = _adPlayer.PlayRespinAd(0);
            Assert.IsTrue(adStarted);

            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.IsTrue(respinEventFired, "Respin event must fire after completing the ad.");
            Assert.IsTrue(_slot.HasUsedRespinThisTurn);
        }

        [Test]
        public void DummyAdPlayer_ReviveAd_RestoresFiftyPercentHp()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            _playerHp.TakeDamage(100, "LethalShot");
            Assert.AreEqual(0, _playerHp.CurrentHealth);

            bool reviveEventFired = false;
            EventBus.Subscribe<PlayerRevivedEvent>(evt =>
            {
                reviveEventFired = true;
                Assert.AreEqual(0.5f, evt.RestoredHpPercent);
            });

            bool adStarted = _adPlayer.PlayReviveAd();
            Assert.IsTrue(adStarted);

            _adPlayer.CompleteAdInstantlyForTesting();

            Assert.IsTrue(reviveEventFired, "PlayerRevivedEvent must fire upon ad completion.");
            Assert.AreEqual(50, _playerHp.CurrentHealth, "Player health must be restored to 50% (50/100).");
            Assert.IsTrue(_gameManager.ReviveUsedInCurrentMatch);
        }

        [Test]
        public void DummyAdPlayer_DoubleGoldAd_DoublesSettlementReward()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);

            int initialGold = _gameManager.SoftCurrencyGold;

            bool adStarted = _adPlayer.PlayDoubleGoldAd();
            Assert.IsTrue(adStarted);

            _adPlayer.CompleteAdInstantlyForTesting();

            // Stage 1 base reward is 100 -> Doubled = 200
            int expectedGold = initialGold + 200;
            Assert.AreEqual(expectedGold, _gameManager.SoftCurrencyGold, "Double Gold Ad must award 2x base stage gold.");
        }

        #endregion

        #region 3. VirtualIAPStore Tests (Catalog, Quota, Purchasing, Equipping)

        [Test]
        public void VirtualIAPStore_InitialWalletBalance_IsTwentyThousandIdr()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            Assert.AreEqual(20000.0f, _experimentManager.CurrentWalletBalance);
            Assert.AreEqual(0.0f, _store.TotalIAPRevenue);
            Assert.AreEqual(0, _store.TotalTransactions);
        }

        [Test]
        public void VirtualIAPStore_CatalogContainsNineItemsWithCorrectPrices()
        {
            var catalog = _store.Catalog;
            Assert.AreEqual(9, catalog.Count, "Catalog must contain exactly 9 items (3 Revolver, 3 Spinner, 3 Knife).");

            // Revolver Skins
            var defaultRevolver = _store.GetCatalogItem("SKIN_REVOLVER_DEFAULT");
            var damascusRevolver = _store.GetCatalogItem("SKIN_REVOLVER_DAMASCUS");
            var goldRevolver = _store.GetCatalogItem("SKIN_REVOLVER_GOLD");

            Assert.IsNotNull(defaultRevolver);
            Assert.AreEqual(0f, defaultRevolver.PriceIdr);
            Assert.AreEqual(CosmeticRarity.Common, defaultRevolver.Rarity);

            Assert.IsNotNull(damascusRevolver);
            Assert.AreEqual(10000f, damascusRevolver.PriceIdr);
            Assert.AreEqual(CosmeticRarity.Rare, damascusRevolver.Rarity);

            Assert.IsNotNull(goldRevolver);
            Assert.AreEqual(15000f, goldRevolver.PriceIdr);
            Assert.AreEqual(CosmeticRarity.Epic, goldRevolver.Rarity);

            // Spinner Skins
            var defaultSpinner = _store.GetCatalogItem("SKIN_SPINNER_DEFAULT");
            var casinoSpinner = _store.GetCatalogItem("SKIN_SPINNER_CASINO");
            var retroSpinner = _store.GetCatalogItem("SKIN_SPINNER_RETRO");

            Assert.IsNotNull(defaultSpinner);
            Assert.AreEqual(0f, defaultSpinner.PriceIdr);

            Assert.IsNotNull(casinoSpinner);
            Assert.AreEqual(5000f, casinoSpinner.PriceIdr);

            Assert.IsNotNull(retroSpinner);
            Assert.AreEqual(5000f, retroSpinner.PriceIdr);

            // Knife Skins
            var defaultKnife = _store.GetCatalogItem("SKIN_KNIFE_DEFAULT");
            var tantoKnife = _store.GetCatalogItem("SKIN_KNIFE_TANTO");
            var viperKnife = _store.GetCatalogItem("SKIN_KNIFE_VIPER");

            Assert.IsNotNull(defaultKnife);
            Assert.AreEqual(0f, defaultKnife.PriceIdr);

            Assert.IsNotNull(tantoKnife);
            Assert.AreEqual(10000f, tantoKnife.PriceIdr);
            Assert.AreEqual(CosmeticRarity.Rare, tantoKnife.Rarity);

            Assert.IsNotNull(viperKnife);
            Assert.AreEqual(15000f, viperKnife.PriceIdr);
            Assert.AreEqual(CosmeticRarity.Epic, viperKnife.Rarity);

            // Check Defaults Owned
            Assert.IsTrue(_store.IsItemOwned("SKIN_REVOLVER_DEFAULT"));
            Assert.IsTrue(_store.IsItemOwned("SKIN_SPINNER_DEFAULT"));
            Assert.IsTrue(_store.IsItemOwned("SKIN_KNIFE_DEFAULT"));
        }

        [Test]
        public void VirtualIAPStore_PurchaseDeductsWalletBalanceAndGrantsOwnership()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            bool purchaseSuccess = _store.TryPurchaseItem("SKIN_SPINNER_CASINO", out string reason);
            Assert.IsTrue(purchaseSuccess, $"Purchase failed: {reason}");

            Assert.AreEqual(15000.0f, _experimentManager.CurrentWalletBalance, "Rp 20.000 - Rp 5.000 = Rp 15.000 remaining.");
            Assert.IsTrue(_store.IsItemOwned("SKIN_SPINNER_CASINO"));
            Assert.IsTrue(_store.IsItemEquipped("SKIN_SPINNER_CASINO"), "Purchased item should be auto-equipped.");
            Assert.AreEqual(5000.0f, _store.TotalIAPRevenue);
            Assert.AreEqual(1, _store.TotalTransactions);
        }

        [Test]
        public void VirtualIAPStore_PurchaseWithInsufficientFunds_Fails()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            // Buy Damascus Revolver (Rp 10.000) -> Balance becomes Rp 10.000
            bool firstBuy = _store.TryPurchaseItem("SKIN_REVOLVER_DAMASCUS", out string _);
            Assert.IsTrue(firstBuy);
            Assert.AreEqual(10000.0f, _experimentManager.CurrentWalletBalance);

            // Attempt to buy Gold Revolver (Rp 15.000) with only Rp 10.000 remaining -> Must Fail!
            bool secondBuy = _store.TryPurchaseItem("SKIN_REVOLVER_GOLD", out string failureReason);
            Assert.IsFalse(secondBuy, "Purchase must fail when balance is insufficient.");
            Assert.IsNotEmpty(failureReason);
            Assert.AreEqual(10000.0f, _experimentManager.CurrentWalletBalance, "Wallet balance must remain unchanged.");
            Assert.IsFalse(_store.IsItemOwned("SKIN_REVOLVER_GOLD"));
        }

        [Test]
        public void VirtualIAPStore_CannotRepurchaseAlreadyOwnedItem()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            // Default item is already owned
            bool buyDefault = _store.TryPurchaseItem("SKIN_KNIFE_DEFAULT", out string reason);
            Assert.IsFalse(buyDefault);
            Assert.AreEqual("Item ini sudah Anda miliki.", reason);

            // Buy an item once
            _store.TryPurchaseItem("SKIN_KNIFE_TANTO", out _);
            Assert.IsTrue(_store.IsItemOwned("SKIN_KNIFE_TANTO"));

            // Attempt to buy again
            bool buyAgain = _store.TryPurchaseItem("SKIN_KNIFE_TANTO", out string reasonAgain);
            Assert.IsFalse(buyAgain);
            Assert.AreEqual("Item ini sudah Anda miliki.", reasonAgain);
        }

        [Test]
        public void VirtualIAPStore_EquipItem_UpdatesEquippedCosmeticAndDispatchesEvent()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);

            // Cannot equip unowned item
            bool equipUnowned = _store.EquipItem("SKIN_KNIFE_VIPER", out string unownedReason);
            Assert.IsFalse(equipUnowned);

            // Buy Viper Knife (Rp 15.000)
            _store.TryPurchaseItem("SKIN_KNIFE_VIPER", out _);

            // Equip back default
            _store.EquipItem("SKIN_KNIFE_DEFAULT", out _);
            Assert.AreEqual("SKIN_KNIFE_DEFAULT", _store.EquippedKnifeId);

            // Equip Viper Knife
            bool eventFired = false;
            EventBus.Subscribe<CosmeticEquippedEvent>(evt =>
            {
                if (evt.ItemId == "SKIN_KNIFE_VIPER") eventFired = true;
            });

            bool equipViper = _store.EquipItem("SKIN_KNIFE_VIPER", out _);
            Assert.IsTrue(equipViper);
            Assert.IsTrue(eventFired);
            Assert.AreEqual("SKIN_KNIFE_VIPER", _store.EquippedKnifeId);
        }

        #endregion

        #region 4. CosmeticApplier Tests

        [Test]
        public void CosmeticApplier_AppliesEquippedCosmeticsToKnifeAndRevolver()
        {
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);

            // Buy & Equip Cyber Tanto Knife
            _store.TryPurchaseItem("SKIN_KNIFE_TANTO", out _);
            _cosmeticApplier.ApplyKnifeSkin("SKIN_KNIFE_TANTO");

            Assert.AreEqual(KnifeParryController.KnifeSkin.CyberTanto, _knife.CurrentSkin);

            // Buy & Equip High-Roller Gold Revolver
            _cosmeticApplier.ApplyRevolverSkin("SKIN_REVOLVER_GOLD");
            Assert.AreEqual(new Color(1.0f, 0.85f, 0.1f), _aim.ReticleBaseColor, "Gold revolver must tint reticle to cyber gold.");
        }

        #endregion

        #region 5. Dynamic Researcher Override Switching

        [Test]
        public void SecretOverride_DynamicSwitching_ImmediatelyAdaptsMonetizationAccess()
        {
            // Start in Group A (Ads Only)
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            Assert.IsFalse(_store.OpenStore(), "Store must be blocked in Group A.");
            Assert.IsTrue(_adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null), "Ads must work in Group A.");
            _adPlayer.DismissWithoutReward();

            // Switch to Group B (IAP Only)
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupB_DominantIAP);
            Assert.IsTrue(_store.OpenStore(), "Store must open in Group B.");
            Assert.IsFalse(_adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null), "Ads must be blocked in Group B.");
            _store.CloseStore();

            // Switch to Group C (Hybrid)
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);
            Assert.IsTrue(_store.OpenStore(), "Store must open in Group C.");
            Assert.IsTrue(_adPlayer.ShowRewardedAd(AdPlacementType.RespinSlot, null), "Ads must work in Group C.");
            _adPlayer.DismissWithoutReward();
            _store.CloseStore();

            // Switch back to Group A while store is open: Store must automatically close!
            _experimentManager.SetGroupOverride(ExperimentGroup.GroupC_Hybrid);
            _store.OpenStore();
            Assert.IsTrue(_store.IsStoreOpen);

            _experimentManager.SetGroupOverride(ExperimentGroup.GroupA_DominantAds);
            Assert.IsFalse(_store.IsStoreOpen, "Store must automatically close when switching to Group A.");
        }

        #endregion
    }
}
