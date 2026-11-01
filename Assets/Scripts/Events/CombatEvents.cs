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
    /// Dispatched when all 3 reels in the cylinder are re-spun (e.g. via Rewarded Ad in Group A or C).
    /// </summary>
    public struct SlotRespinTriggeredEvent
    {
        public int TurnNumber;
        public SlotSymbol[] OldSymbols;
        public SlotSymbol[] NewSymbols;
        public bool ViaRewardedAd;

        public SlotRespinTriggeredEvent(int turnNumber, SlotSymbol[] oldSymbols, SlotSymbol[] newSymbols, bool viaRewardedAd)
        {
            TurnNumber = turnNumber;
            OldSymbols = oldSymbols;
            NewSymbols = newSymbols;
            ViaRewardedAd = viaRewardedAd;
        }
    }

    /// <summary>
    /// Dispatched when player commits their turn (locking in reels for combat actions).
    /// </summary>
    public struct TurnCommittedEvent
    {
        public int TurnNumber;
        public List<SlotSymbol> ExecutedSymbols;
        public SlotSymbol?[] PreservedHoldSlots;

        public TurnCommittedEvent(int turnNumber, List<SlotSymbol> executedSymbols, SlotSymbol?[] preservedHoldSlots = null)
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

    #region Defense QTE & Dodge/Parry Events (Chat 4)

    /// <summary>
    /// Type of enemy gunshot attack.
    /// Red = Unparryable (Player MUST Dodge [Q]).
    /// Yellow = Parryable (Player CAN Parry [E] with precision, or Dodge [Q] safely).
    /// </summary>
    public enum EnemyAttackType
    {
        UNPARRYABLE_RED = 0,
        PARRYABLE_YELLOW = 1
    }

    /// <summary>
    /// Lateral camera sway direction for [Q] Dodge.
    /// </summary>
    public enum DodgeDirection
    {
        Left,
        Right
    }

    /// <summary>
    /// Resolution result for an incoming enemy bullet during Defense QTE.
    /// </summary>
    public enum DefenseResult
    {
        None,
        DodgeSuccess,       // Player dodged [Q] in time (0 damage)
        ParrySuccess,       // Player parried [E] yellow bullet in precision window (Critical Deflection Slash!)
        ParryFailedRed,     // Player tried to parry [E] a red bullet (fails! Full 25 damage)
        ParryFailedTiming,  // Player pressed [E] too early or too late
        HitTaken            // Player took hit (failed to dodge/parry, 25 damage)
    }

    /// <summary>
    /// Dispatched when the enemy bot begins aiming and telegraphing a shot with muzzle glow.
    /// </summary>
    public struct EnemyTelegraphStartedEvent
    {
        public EnemyAttackType AttackType;
        public float TelegraphDuration;
        public bool HasCounterBuff;
        public int BurstIndex;
        public int TotalBursts;
        public int StageNumber;

        public EnemyTelegraphStartedEvent(EnemyAttackType attackType, float telegraphDuration, bool hasCounterBuff, int burstIndex, int totalBursts, int stageNumber)
        {
            AttackType = attackType;
            TelegraphDuration = telegraphDuration;
            HasCounterBuff = hasCounterBuff;
            BurstIndex = burstIndex;
            TotalBursts = totalBursts;
            StageNumber = stageNumber;
        }
    }

    /// <summary>
    /// Dispatched when the enemy pulls the trigger and bullet launches towards player camera.
    /// </summary>
    public struct EnemyBulletFiredEvent
    {
        public EnemyAttackType AttackType;
        public float BulletTravelTime;
        public int BurstIndex;
        public int TotalBursts;

        public EnemyBulletFiredEvent(EnemyAttackType attackType, float bulletTravelTime, int burstIndex, int totalBursts)
        {
            AttackType = attackType;
            BulletTravelTime = bulletTravelTime;
            BurstIndex = burstIndex;
            TotalBursts = totalBursts;
        }
    }

    /// <summary>
    /// Dispatched when player executes a camera dodge sway [Q].
    /// </summary>
    public struct PlayerDodgeTriggeredEvent
    {
        public DodgeDirection Direction;
        public float Duration;

        public PlayerDodgeTriggeredEvent(DodgeDirection direction, float duration)
        {
            Direction = direction;
            Duration = duration;
        }
    }

    /// <summary>
    /// Dispatched when player swings the left-hand parry knife to screen center [E].
    /// </summary>
    public struct PlayerParryTriggeredEvent
    {
        public float ActiveWindowDuration;

        public PlayerParryTriggeredEvent(float activeWindowDuration)
        {
            ActiveWindowDuration = activeWindowDuration;
        }
    }

    /// <summary>
    /// Dispatched when an enemy bullet attack is resolved (dodge, parry, or hit).
    /// </summary>
    public struct DefenseResolvedEvent
    {
        public DefenseResult Result;
        public EnemyAttackType AttackType;
        public int DamageToPlayer;
        public int DamageToEnemy; // Deflected damage
        public bool IsCriticalParry;
        public int BurstIndex;
        public int TotalBursts;

        public DefenseResolvedEvent(DefenseResult result, EnemyAttackType attackType, int damageToPlayer, int damageToEnemy, bool isCriticalParry, int burstIndex, int totalBursts)
        {
            Result = result;
            AttackType = attackType;
            DamageToPlayer = damageToPlayer;
            DamageToEnemy = damageToEnemy;
            IsCriticalParry = isCriticalParry;
            BurstIndex = burstIndex;
            TotalBursts = totalBursts;
        }
    }

    /// <summary>
    /// Dispatched when the COUNTER / SHARPEN buff state changes.
    /// </summary>
    public struct CounterBuffStatusChangedEvent
    {
        public bool IsActive;
        public int BonusDamage;
        public string Reason;

        public CounterBuffStatusChangedEvent(bool isActive, int bonusDamage, string reason)
        {
            IsActive = isActive;
            BonusDamage = bonusDamage;
            Reason = reason;
        }
    }

    /// <summary>
    /// Dispatched when an enemy's turn attack sequence begins.
    /// </summary>
    public struct EnemyTurnStartedEvent
    {
        public int StageNumber;
        public int TotalShots;

        public EnemyTurnStartedEvent(int stageNumber, int totalShots)
        {
            StageNumber = stageNumber;
            TotalShots = totalShots;
        }
    }

    /// <summary>
    /// Dispatched when an enemy's turn attack sequence concludes.
    /// </summary>
    public struct EnemyTurnCompletedEvent
    {
        public int StageNumber;
        public bool PlayerSurviving;

        public EnemyTurnCompletedEvent(int stageNumber, bool playerSurviving)
        {
            StageNumber = stageNumber;
            PlayerSurviving = playerSurviving;
        }
    }

    #endregion
}
