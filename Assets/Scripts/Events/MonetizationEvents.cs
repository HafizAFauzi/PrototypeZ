using System;
using UnityEngine;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Supported Rewarded Ad placement types according to GDD Section 4.1 & TSD Section 2.5.
    /// </summary>
    public enum AdPlacementType
    {
        /// <summary>
        /// Tactical re-spin of 1 slot reel in the revolver cylinder (max 1x per turn).
        /// </summary>
        RespinSlot = 0,

        /// <summary>
        /// Second-chance revive with 50% HP upon defeat (max 1x per match).
        /// </summary>
        ReviveMatch = 1,

        /// <summary>
        /// Double soft currency gold multiplier claimed upon victory settlement.
        /// </summary>
        DoubleGold = 2
    }

    /// <summary>
    /// Cosmetic equipment categories for First-Person view according to GDD Section 4.2.
    /// </summary>
    public enum CosmeticItemType
    {
        WeaponRevolver = 0,
        CylinderSpinner = 1,
        WeaponKnife = 2
    }

    /// <summary>
    /// Rarity tiers for cosmetic items.
    /// </summary>
    public enum CosmeticRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2
    }

    /// <summary>
    /// Dispatched when a Rewarded Ad impression begins playback.
    /// </summary>
    public struct AdImpressionStartedEvent
    {
        public string AdId;
        public AdPlacementType Placement;
        public float DurationSeconds;

        public AdImpressionStartedEvent(string adId, AdPlacementType placement, float durationSeconds)
        {
            AdId = adId;
            Placement = placement;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// Dispatched when a Rewarded Ad completes its full 10-second duration and unlocks reward.
    /// Matches the AD_IMPRESSIONS table in ERD.md.
    /// </summary>
    public struct AdImpressionCompletedEvent
    {
        public string AdId;
        public string UserId;
        public AdPlacementType Placement;
        public float DurationSeconds;
        public float EcpmRateIdr;
        public float RevenueEarned;
        public DateTime Timestamp;

        public AdImpressionCompletedEvent(
            string adId,
            string userId,
            AdPlacementType placement,
            float durationSeconds,
            float ecpmRateIdr,
            float revenueEarned,
            DateTime timestamp)
        {
            AdId = adId;
            UserId = userId;
            Placement = placement;
            DurationSeconds = durationSeconds;
            EcpmRateIdr = ecpmRateIdr;
            RevenueEarned = revenueEarned;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Dispatched when player purchases a cosmetic item from the Virtual IAP Store.
    /// Matches the IAP_TRANSACTIONS table in ERD.md.
    /// </summary>
    public struct IAPTransactionCompletedEvent
    {
        public string TransactionId;
        public string UserId;
        public string ItemId;
        public string ItemName;
        public CosmeticItemType ItemType;
        public float AmountPaid;
        public float RemainingWalletBalance;
        public DateTime Timestamp;

        public IAPTransactionCompletedEvent(
            string transactionId,
            string userId,
            string itemId,
            string itemName,
            CosmeticItemType itemType,
            float amountPaid,
            float remainingWalletBalance,
            DateTime timestamp)
        {
            TransactionId = transactionId;
            UserId = userId;
            ItemId = itemId;
            ItemName = itemName;
            ItemType = itemType;
            AmountPaid = amountPaid;
            RemainingWalletBalance = remainingWalletBalance;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Dispatched when player equips or changes an equipped cosmetic item.
    /// </summary>
    public struct CosmeticEquippedEvent
    {
        public CosmeticItemType ItemType;
        public string ItemId;
        public string ItemName;

        public CosmeticEquippedEvent(CosmeticItemType itemType, string itemId, string itemName)
        {
            ItemType = itemType;
            ItemId = itemId;
            ItemName = itemName;
        }
    }

    /// <summary>
    /// Dispatched when Virtual IAP Store state opens or closes.
    /// </summary>
    public struct IAPStoreStateChangedEvent
    {
        public bool IsOpen;
        public float CurrentWalletBalance;

        public IAPStoreStateChangedEvent(bool isOpen, float currentWalletBalance)
        {
            IsOpen = isOpen;
            CurrentWalletBalance = currentWalletBalance;
        }
    }
}
