using System;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Monetization
{
    /// <summary>
    /// Data model representing a premium cosmetic item in the Virtual IAP Store catalog.
    /// Derived from GDD Section 4.2 & ERD COSMETIC_ITEMS table.
    /// </summary>
    [Serializable]
    public class CosmeticCatalogItem
    {
        public string ItemId { get; private set; }
        public CosmeticItemType ItemType { get; private set; }
        public string ItemName { get; private set; }
        public float PriceIdr { get; private set; }
        public string AssetPrefabKey { get; private set; }
        public CosmeticRarity Rarity { get; private set; }
        public string Description { get; private set; }
        public Color AccentColor { get; private set; }
        public bool IsDefaultOwned { get; private set; }
        public Sprite IconSprite { get; set; }

        public CosmeticCatalogItem(
            string itemId,
            CosmeticItemType itemType,
            string itemName,
            float priceIdr,
            string assetPrefabKey,
            CosmeticRarity rarity,
            string description,
            Color accentColor,
            bool isDefaultOwned = false)
        {
            ItemId = itemId;
            ItemType = itemType;
            ItemName = itemName;
            PriceIdr = priceIdr;
            AssetPrefabKey = assetPrefabKey;
            Rarity = rarity;
            Description = description;
            AccentColor = accentColor;
            IsDefaultOwned = isDefaultOwned;
        }

        public string GetRarityLabel()
        {
            switch (Rarity)
            {
                case CosmeticRarity.Common: return "COMMON";
                case CosmeticRarity.Rare: return "RARE";
                case CosmeticRarity.Epic: return "EPIC";
                default: return "STANDARD";
            }
        }

        public Color GetRarityColor()
        {
            switch (Rarity)
            {
                case CosmeticRarity.Common: return new Color(0.2f, 0.8f, 1.0f); // Bright Cyan
                case CosmeticRarity.Rare: return new Color(0.8f, 0.3f, 1.0f);   // Neon Purple
                case CosmeticRarity.Epic: return new Color(1.0f, 0.8f, 0.1f);   // Cyber Gold
                default: return Color.gray;
            }
        }

        public string GetCategoryLabel()
        {
            switch (ItemType)
            {
                case CosmeticItemType.WeaponRevolver: return "REVOLVER SKIN";
                case CosmeticItemType.CylinderSpinner: return "SPINNER SKIN";
                case CosmeticItemType.WeaponKnife: return "KNIFE SKIN";
                default: return "COSMETIC";
            }
        }
    }
}
