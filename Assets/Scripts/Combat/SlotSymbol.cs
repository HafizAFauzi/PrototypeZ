using System;
using UnityEngine;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// The 5 symbols randomized on the tactical revolver 3-reel slot cylinder.
    /// Probabilities based on GDD Section 2.2:
    /// - ATTACK: 35%
    /// - MARK: 20%
    /// - HEAL: 20%
    /// - COUNTER_SHARPEN: 15%
    /// - WILD_JOKER: 10%
    /// </summary>
    public enum SlotSymbol
    {
        ATTACK = 0,
        MARK = 1,
        HEAL = 2,
        COUNTER_SHARPEN = 3,
        WILD_JOKER = 4
    }

    /// <summary>
    /// Metadata helper for presentation, color-coding, icons, and descriptions of SlotSymbols.
    /// </summary>
    public static class SlotSymbolMetadata
    {
        public static string GetName(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.ATTACK:
                    return "ATTACK";
                case SlotSymbol.MARK:
                    return "MARK";
                case SlotSymbol.HEAL:
                    return "HEAL";
                case SlotSymbol.COUNTER_SHARPEN:
                    return "COUNTER";
                case SlotSymbol.WILD_JOKER:
                    return "WILD";
                default:
                    return symbol.ToString();
            }
        }

        public static string GetIcon(SlotSymbol symbol)
        {
            // Note: variation selector \uFE0F is stripped to prevent TextMeshPro glyph warnings in SDF fonts
            switch (symbol)
            {
                case SlotSymbol.ATTACK:
                    return "\u2694"; // ⚔
                case SlotSymbol.MARK:
                    return "\uD83C\uDFAF"; // 🎯
                case SlotSymbol.HEAL:
                    return "\uD83D\uDC89"; // 💉
                case SlotSymbol.COUNTER_SHARPEN:
                    return "\uD83D\uDDE1"; // 🗡
                case SlotSymbol.WILD_JOKER:
                    return "\u2B50"; // ⭐
                default:
                    return "?";
            }
        }

        public static Color GetColor(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.ATTACK:
                    // Cyber Blaster Red / Orange
                    return new Color(1.0f, 0.35f, 0.35f, 1.0f);
                case SlotSymbol.MARK:
                    // Cyber Target Gold / Amber
                    return new Color(1.0f, 0.85f, 0.2f, 1.0f);
                case SlotSymbol.HEAL:
                    // Nano-Medkit Neon Emerald
                    return new Color(0.2f, 0.95f, 0.45f, 1.0f);
                case SlotSymbol.COUNTER_SHARPEN:
                    // Parry Knife Electric Cyan
                    return new Color(0.25f, 0.85f, 1.0f, 1.0f);
                case SlotSymbol.WILD_JOKER:
                    // Casino Neon Violet / Magenta
                    return new Color(0.95f, 0.35f, 1.0f, 1.0f);
                default:
                    return Color.white;
            }
        }

        public static string GetDescription(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.ATTACK:
                    return "Mengisi peluru aktif; memicu fase bidik FPS (Aim Stabilizer).";
                case SlotSymbol.MARK:
                    return "Menandai tubuh musuh; debuff 2x Critical Damage.";
                case SlotSymbol.HEAL:
                    return "Nano-Medkit: Pemulihan instan +20 HP ke pemain.";
                case SlotSymbol.COUNTER_SHARPEN:
                    return "Asah pisau parry; cue peringatan & ekstra parry damage.";
                case SlotSymbol.WILD_JOKER:
                    return "Kartu langka (Rare); bebas memilih simbol pengganti apa pun.";
                default:
                    return string.Empty;
            }
        }
    }
}
