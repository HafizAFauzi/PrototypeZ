using System.Collections.Generic;
using UnityEngine;
using LuckyDuelist.Combat;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Dispatched when the 3-reel slot cylinder begins spinning.
    /// </summary>
    public struct SlotSpinStartedEvent
    {
        public int TurnNumber;

        public SlotSpinStartedEvent(int turnNumber)
        {
            TurnNumber = turnNumber;
        }
    }

    /// <summary>
    /// Dispatched when an individual reel in the cylinder stops and locks in its symbol.
    /// </summary>
    public struct SlotReelStoppedEvent
    {
        public int ReelIndex;
        public SlotSymbol Symbol;

        public SlotReelStoppedEvent(int reelIndex, SlotSymbol symbol)
        {
            ReelIndex = reelIndex;
            Symbol = symbol;
        }
    }

    /// <summary>
    /// Dispatched when all 3 reels in the cylinder have finished spinning.
    /// </summary>
    public struct SlotSpinCompletedEvent
    {
        public int TurnNumber;
        public SlotSymbol[] Results;

        public SlotSpinCompletedEvent(int turnNumber, SlotSymbol[] results)
        {
            TurnNumber = turnNumber;
            Results = results;
        }
    }

    /// <summary>
    /// Dispatched when a single reel is re-spun (e.g. via Rewarded Ad in Group A or C).
    /// </summary>
    public struct SlotRespinTriggeredEvent
    {
        public int ReelIndex;
        public SlotSymbol OldSymbol;
        public SlotSymbol NewSymbol;
        public bool ViaRewardedAd;

        public SlotRespinTriggeredEvent(int reelIndex, SlotSymbol oldSymbol, SlotSymbol newSymbol, bool viaRewardedAd)
        {
            ReelIndex = reelIndex;
            OldSymbol = oldSymbol;
            NewSymbol = newSymbol;
            ViaRewardedAd = viaRewardedAd;
        }
    }

    /// <summary>
    /// Dispatched whenever hold slots change (a symbol was held, unheld, or cleared).
    /// </summary>
    public struct HoldSlotsChangedEvent
    {
        public SlotSymbol?[] HoldSlots;
        public int HeldCount;

        public HoldSlotsChangedEvent(SlotSymbol?[] holdSlots, int heldCount)
        {
            HoldSlots = holdSlots;
            HeldCount = heldCount;
        }
    }

    /// <summary>
    /// Dispatched when player commits their turn (locking in unheld symbols for combat actions).
    /// </summary>
    public struct TurnCommittedEvent
    {
        public int TurnNumber;
        public List<SlotSymbol> ExecutedSymbols;
        public SlotSymbol?[] PreservedHoldSlots;

        public TurnCommittedEvent(int turnNumber, List<SlotSymbol> executedSymbols, SlotSymbol?[] preservedHoldSlots)
        {
            TurnNumber = turnNumber;
            ExecutedSymbols = executedSymbols;
            PreservedHoldSlots = preservedHoldSlots;
        }
    }

    /// <summary>
    /// Dispatched whenever player health changes (damage taken, healed, or revived).
    /// </summary>
    public struct PlayerHealthChangedEvent
    {
        public int CurrentHp;
        public int MaxHp;
        public int Delta;
        public string Reason;

        public PlayerHealthChangedEvent(int currentHp, int maxHp, int delta, string reason)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Delta = delta;
            Reason = reason;
        }
    }

    /// <summary>
    /// Dispatched when a Wild Joker symbol is resolved/replaced by the player or system.
    /// </summary>
    public struct WildJokerResolvedEvent
    {
        public int ReelIndex;
        public SlotSymbol ChosenSymbol;

        public WildJokerResolvedEvent(int reelIndex, SlotSymbol chosenSymbol)
        {
            ReelIndex = reelIndex;
            ChosenSymbol = chosenSymbol;
        }
    }

    /// <summary>
    /// Dispatched when the FPS Aim Stabilizer session starts upon activating ATTACK symbol(s).
    /// </summary>
    public struct AimStabilizerStartedEvent
    {
        public int TotalBursts;
        public int CurrentBurstIndex;
        public int EnemyHp;
        public bool IsEnemyMarked;

        public AimStabilizerStartedEvent(int totalBursts, int currentBurstIndex, int enemyHp, bool isEnemyMarked)
        {
            TotalBursts = totalBursts;
            CurrentBurstIndex = currentBurstIndex;
            EnemyHp = enemyHp;
            IsEnemyMarked = isEnemyMarked;
        }
    }

    /// <summary>
    /// Dispatched during reticle movement / drift / stabilization update loop.
    /// </summary>
    public struct AimReticleMovedEvent
    {
        public Vector2 ReticlePosition;
        public Vector2 VitalCenter;
        public float VitalRadius;
        public bool IsInsideVital;
        public float LockOnProgress; // 0.0 to 1.0

        public AimReticleMovedEvent(Vector2 reticlePosition, Vector2 vitalCenter, float vitalRadius, bool isInsideVital, float lockOnProgress)
        {
            ReticlePosition = reticlePosition;
            VitalCenter = vitalCenter;
            VitalRadius = vitalRadius;
            IsInsideVital = isInsideVital;
            LockOnProgress = lockOnProgress;
        }
    }

    /// <summary>
    /// Dispatched when a lock-on shot auto-fires at 100% (1.0s) accumulation.
    /// </summary>
    public struct AimShotFiredEvent
    {
        public int DamageDealt;
        public bool IsCritical;
        public int BurstIndex;
        public int TotalBursts;
        public int RemainingEnemyHp;

        public AimShotFiredEvent(int damageDealt, bool isCritical, int burstIndex, int totalBursts, int remainingEnemyHp)
        {
            DamageDealt = damageDealt;
            IsCritical = isCritical;
            BurstIndex = burstIndex;
            TotalBursts = totalBursts;
            RemainingEnemyHp = remainingEnemyHp;
        }
    }

    /// <summary>
    /// Dispatched when all burst shots in an aim session have concluded or target is eliminated.
    /// </summary>
    public struct AimSequenceCompletedEvent
    {
        public int TotalShotsFired;
        public int TotalDamageDealt;
        public bool EnemyDefeated;

        public AimSequenceCompletedEvent(int totalShotsFired, int totalDamageDealt, bool enemyDefeated)
        {
            TotalShotsFired = totalShotsFired;
            TotalDamageDealt = totalDamageDealt;
            EnemyDefeated = enemyDefeated;
        }
    }

    /// <summary>
    /// Dispatched when the enemy's MARK debuff status changes.
    /// </summary>
    public struct EnemyMarkStatusChangedEvent
    {
        public bool IsMarked;
        public string Reason;

        public EnemyMarkStatusChangedEvent(bool isMarked, string reason)
        {
            IsMarked = isMarked;
            Reason = reason;
        }
    }

    /// <summary>
    /// Dispatched whenever the enemy target's health changes.
    /// </summary>
    public struct EnemyHealthChangedEvent
    {
        public int CurrentHp;
        public int MaxHp;
        public int Delta;
        public bool IsCritical;
        public string Reason;

        public EnemyHealthChangedEvent(int currentHp, int maxHp, int delta, bool isCritical, string reason)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Delta = delta;
            IsCritical = isCritical;
            Reason = reason;
        }
    }
}
