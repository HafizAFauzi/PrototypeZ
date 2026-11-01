using System;
using System.Collections.Generic;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Configuration data for an individual enemy gunshot.
    /// </summary>
    [Serializable]
    public struct EnemyShotData
    {
        public EnemyAttackType AttackType;
        public float TelegraphDuration;
        public float BulletTravelTime;
        public float PauseAfterShot;

        public EnemyShotData(EnemyAttackType attackType, float telegraphDuration, float bulletTravelTime, float pauseAfterShot = 0.35f)
        {
            AttackType = attackType;
            TelegraphDuration = telegraphDuration;
            BulletTravelTime = bulletTravelTime;
            PauseAfterShot = pauseAfterShot;
        }
    }

    /// <summary>
    /// Bot Gunner AI as specified in GDD Section 3 and TSD Section 2.4.
    /// Governs the 3-stage enemy shooting progression:
    /// - Stage 1: Neon Rookie (HP 70) -> 100% Slow Yellow Parryable bullets (telegraph ~1.25s, flight ~0.90s).
    /// - Stage 2: Chrome Gambler (HP 90) -> Mixed Red Unparryable & Yellow Parryable bullets (telegraph ~0.95s, flight ~0.65s).
    /// - Stage 3: The Cyber Baron (Boss, HP 120) -> Rapid multi-shot alternating bursts (telegraph ~0.65s, flight ~0.45s).
    /// </summary>
    public class BotGunnerAI : MonoBehaviour
    {
        public static BotGunnerAI Instance { get; private set; }

        [Header("Stage 1: Neon Rookie Settings")]
        [SerializeField] private float stage1Telegraph = 1.25f;
        [SerializeField] private float stage1BulletTravel = 0.90f;

        [Header("Stage 2: Chrome Gambler Settings")]
        [SerializeField] private float stage2Telegraph = 0.95f;
        [SerializeField] private float stage2BulletTravel = 0.65f;

        [Header("Stage 3: The Cyber Baron Settings")]
        [SerializeField] private float stage3Telegraph = 0.65f;
        [SerializeField] private float stage3BulletTravel = 0.45f;
        [SerializeField] private float stage3BurstInterval = 0.32f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this);
                else DestroyImmediate(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Generates the planned attack pattern for the bot for the current stage and turn.
        /// </summary>
        public List<EnemyShotData> PlanAttackPattern(int stageNumber, int turnNumber)
        {
            return GenerateAttackPattern(
                stageNumber,
                turnNumber,
                stage1Telegraph, stage1BulletTravel,
                stage2Telegraph, stage2BulletTravel,
                stage3Telegraph, stage3BulletTravel, stage3BurstInterval
            );
        }

        #region Pure Pattern Generation (Unit Testable)

        /// <summary>
        /// Pure deterministic generator for bot attack patterns.
        /// </summary>
        public static List<EnemyShotData> GenerateAttackPattern(
            int stageNumber,
            int turnNumber,
            float s1Telegraph = 1.25f, float s1Travel = 0.90f,
            float s2Telegraph = 0.95f, float s2Travel = 0.65f,
            float s3Telegraph = 0.65f, float s3Travel = 0.45f, float s3BurstInterval = 0.32f)
        {
            var pattern = new List<EnemyShotData>();

            switch (stageNumber)
            {
                case 1:
                    // Stage 1: Neon Rookie - 100% slow yellow parryable bullets.
                    pattern.Add(new EnemyShotData(EnemyAttackType.PARRYABLE_YELLOW, s1Telegraph, s1Travel, 0.4f));
                    break;

                case 2:
                    // Stage 2: Chrome Gambler - Mix of Red Unparryable & Yellow Parryable bullets.
                    // Alternates starting with Yellow on turn 1, Red on turn 2, Yellow on turn 3, etc.
                    EnemyAttackType s2Type = (turnNumber % 2 == 0)
                        ? EnemyAttackType.UNPARRYABLE_RED
                        : EnemyAttackType.PARRYABLE_YELLOW;

                    pattern.Add(new EnemyShotData(s2Type, s2Telegraph, s2Travel, 0.4f));
                    break;

                case 3:
                default:
                    // Stage 3: The Cyber Baron - Rapid alternating bursts!
                    // Turn 1: Red shot followed immediately by fast Yellow shot (tests [Q] then [E]!)
                    // Turn 2: Yellow shot followed by Yellow shot
                    // Turn 3+: Red shot followed by Yellow shot
                    if (turnNumber % 2 == 1)
                    {
                        // Rapid Red -> Yellow burst combo
                        pattern.Add(new EnemyShotData(EnemyAttackType.UNPARRYABLE_RED, s3Telegraph, s3Travel, s3BurstInterval));
                        pattern.Add(new EnemyShotData(EnemyAttackType.PARRYABLE_YELLOW, s3Telegraph * 0.85f, s3Travel * 0.9f, 0.4f));
                    }
                    else
                    {
                        // Rapid Yellow -> Yellow burst combo
                        pattern.Add(new EnemyShotData(EnemyAttackType.PARRYABLE_YELLOW, s3Telegraph, s3Travel, s3BurstInterval));
                        pattern.Add(new EnemyShotData(EnemyAttackType.PARRYABLE_YELLOW, s3Telegraph * 0.85f, s3Travel * 0.9f, 0.4f));
                    }
                    break;
            }

            return pattern;
        }

        /// <summary>
        /// Helper to query whether a given attack type is parryable with [E].
        /// </summary>
        public static bool IsParryable(EnemyAttackType attackType)
        {
            return attackType == EnemyAttackType.PARRYABLE_YELLOW;
        }

        /// <summary>
        /// Helper to query whether a given attack type requires [Q] Dodge.
        /// </summary>
        public static bool IsUnparryable(EnemyAttackType attackType)
        {
            return attackType == EnemyAttackType.UNPARRYABLE_RED;
        }

        #endregion
    }
}
