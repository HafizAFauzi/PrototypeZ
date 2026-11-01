using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LuckyDuelist.Core;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Core Controller for the 3-Reel Tactical Revolver Cylinder Slot Machine.
    /// Handles:
    /// 1. Probabilistic rolling of 5 symbols (ATTACK 35%, MARK 20%, HEAL 20%, COUNTER 15%, WILD 10%).
    /// 2. 3-Reel diegetic cylinder spinning with staggered stopping & visual/audio hooks.
    /// 3. Direct 3-reel action execution on turn commit.
    /// 4. Full 3-reel re-spin via Rewarded Ads (max 1x per turn, gated by ExperimentManager: Group A & C enabled, Group B disabled).
    /// 5. Wild Joker resolution & turn commitment.
    /// </summary>
    public class SlotRevolverController : MonoBehaviour
    {
        public static SlotRevolverController Instance { get; private set; }

        public const int REEL_COUNT = 3;

        [Header("Symbol Probability Weights (Sum = 100)")]
        [Range(1, 100)] [SerializeField] private int attackWeight = 35;
        [Range(1, 100)] [SerializeField] private int markWeight = 20;
        [Range(1, 100)] [SerializeField] private int healWeight = 20;
        [Range(1, 100)] [SerializeField] private int counterWeight = 15;
        [Range(1, 100)] [SerializeField] private int wildWeight = 10;

        [Header("Spinning & Timing Settings")]
        [Tooltip("Delay between each reel stopping during spin animation")]
        [SerializeField] private float spinReelInterval = 0.35f;
        [Tooltip("Base duration before the first reel stops")]
        [SerializeField] private float firstReelStopDelay = 0.45f;
        [Tooltip("Rotational speed (deg/sec) of the diegetic 3D cylinder transform during spin")]
        [SerializeField] private float diegeticSpinSpeed = 900f;

        [Header("Diegetic 3D Revolver References (Optional)")]
        [SerializeField] private Transform cylinderTransform;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cylinderSpinSfx;
        [SerializeField] private AudioClip reelStopSfx;

        // Internal State
        private readonly SlotSymbol[] _currentReelSymbols = new SlotSymbol[REEL_COUNT];

        private bool _isSpinning = false;
        private bool _hasSpunThisTurn = false;
        private bool _turnCommitted = false;
        private bool _hasUsedRespinThisTurn = false;
        private int _turnNumber = 1;

        // Custom Random Provider (for Unit Testing / Seeded Tests)
        private System.Random _customRandom;

        // Public Properties
        public bool IsSpinning => _isSpinning;
        public bool HasSpunThisTurn => _hasSpunThisTurn;
        public bool IsTurnCommitted => _turnCommitted;
        public bool HasUsedRespinThisTurn => _hasUsedRespinThisTurn;
        public int TurnNumber => _turnNumber;

        public SlotSymbol[] CurrentReelSymbols => (SlotSymbol[])_currentReelSymbols.Clone();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this);
                else DestroyImmediate(this);
                return;
            }
            Instance = this;

            // Default initial roll (before first player spin)
            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = SlotSymbol.ATTACK;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StageStartedEvent>(OnStageStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
        }

        private void OnStageStarted(StageStartedEvent evt)
        {
            ResetForNewMatch();
            Debug.Log($"[SlotRevolverController] Reset for Stage {evt.StageNumber}: {evt.StageName}. Turn reset to 1.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
        }

        private void Update()
        {
            // Rotate diegetic cylinder transform if spinning
            if (_isSpinning && cylinderTransform != null)
            {
                cylinderTransform.Rotate(Vector3.forward, diegeticSpinSpeed * Time.deltaTime, Space.Self);
            }
        }

        #region Seed & RNG Configuration

        /// <summary>
        /// Injects a custom System.Random for deterministic unit tests.
        /// </summary>
        public void SetRandomProvider(System.Random rng)
        {
            _customRandom = rng;
        }

        /// <summary>
        /// Sets probability weights for the 5 symbols.
        /// </summary>
        public void SetWeights(int attack, int mark, int heal, int counter, int wild)
        {
            attackWeight = Mathf.Max(0, attack);
            markWeight = Mathf.Max(0, mark);
            healWeight = Mathf.Max(0, heal);
            counterWeight = Mathf.Max(0, counter);
            wildWeight = Mathf.Max(0, wild);
        }

        /// <summary>
        /// Calculates the theoretical probability [0.0 - 1.0] for a given symbol.
        /// </summary>
        public float GetSymbolProbability(SlotSymbol symbol)
        {
            int total = attackWeight + markWeight + healWeight + counterWeight + wildWeight;
            if (total <= 0) return 0f;

            int weight;
            switch (symbol)
            {
                case SlotSymbol.ATTACK: weight = attackWeight; break;
                case SlotSymbol.MARK: weight = markWeight; break;
                case SlotSymbol.HEAL: weight = healWeight; break;
                case SlotSymbol.COUNTER_SHARPEN: weight = counterWeight; break;
                case SlotSymbol.WILD_JOKER: weight = wildWeight; break;
                default: weight = 0; break;
            }

            return (float)weight / total;
        }

        /// <summary>
        /// Rolls a random symbol using cumulative probability distribution.
        /// </summary>
        public SlotSymbol RollSymbol()
        {
            int total = attackWeight + markWeight + healWeight + counterWeight + wildWeight;
            if (total <= 0) return SlotSymbol.ATTACK;

            int rollValue;
            if (_customRandom != null)
            {
                rollValue = _customRandom.Next(0, total);
            }
            else
            {
                rollValue = UnityEngine.Random.Range(0, total);
            }

            int cumulative = 0;

            cumulative += attackWeight;
            if (rollValue < cumulative) return SlotSymbol.ATTACK;

            cumulative += markWeight;
            if (rollValue < cumulative) return SlotSymbol.MARK;

            cumulative += healWeight;
            if (rollValue < cumulative) return SlotSymbol.HEAL;

            cumulative += counterWeight;
            if (rollValue < cumulative) return SlotSymbol.COUNTER_SHARPEN;

            return SlotSymbol.WILD_JOKER;
        }

        #endregion

        #region Cylinder Spinning Mechanics

        /// <summary>
        /// Checks if the player is allowed to initiate a spin on the cylinder.
        /// </summary>
        public bool CanSpin()
        {
            if (_isSpinning || _hasSpunThisTurn) return false;
            if (AimStabilizerController.Instance != null && AimStabilizerController.Instance.IsAiming) return false;
            if (DefenseQTEController.Instance != null && DefenseQTEController.Instance.IsDefensePhaseActive) return false;
            if (GameManager.Instance != null && (GameManager.Instance.CurrentState == GameState.MatchSettlement || GameManager.Instance.CurrentState == GameState.FinalSettlement)) return false;
            return true;
        }

        /// <summary>
        /// Triggers the 3-reel cylinder spin sequence with coroutine delays and events.
        /// </summary>
        public bool SpinCylinder()
        {
            if (!CanSpin())
            {
                Debug.LogWarning("[SlotRevolverController] Cannot spin: already spinning or turn already spun.");
                return false;
            }

            StartCoroutine(SpinRoutine());
            return true;
        }

        /// <summary>
        /// Instantly spins the 3 reels (useful for unit tests or instant playback).
        /// </summary>
        public void SpinCylinderInstant()
        {
            if (_isSpinning) return;

            _isSpinning = true;
            _hasSpunThisTurn = true;
            _turnCommitted = false;

            EventBus.Publish(new SlotSpinStartedEvent(_turnNumber));

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = RollSymbol();
                EventBus.Publish(new SlotReelStoppedEvent(i, _currentReelSymbols[i]));
            }

            _isSpinning = false;
            EventBus.Publish(new SlotSpinCompletedEvent(_turnNumber, CurrentReelSymbols));
        }

        private IEnumerator SpinRoutine()
        {
            _isSpinning = true;
            _hasSpunThisTurn = true;
            _turnCommitted = false;

            EventBus.Publish(new SlotSpinStartedEvent(_turnNumber));

            if (audioSource != null && cylinderSpinSfx != null)
            {
                audioSource.PlayOneShot(cylinderSpinSfx);
            }

            // Wait for first reel delay
            yield return new WaitForSeconds(firstReelStopDelay);

            // Staggered reel stops
            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = RollSymbol();

                if (audioSource != null && reelStopSfx != null)
                {
                    audioSource.PlayOneShot(reelStopSfx);
                }

                EventBus.Publish(new SlotReelStoppedEvent(i, _currentReelSymbols[i]));

                if (i < REEL_COUNT - 1)
                {
                    yield return new WaitForSeconds(spinReelInterval);
                }
            }

            _isSpinning = false;
            EventBus.Publish(new SlotSpinCompletedEvent(_turnNumber, CurrentReelSymbols));
        }

        #endregion

        #region Re-Spin All 3 Reels via Rewarded Ads

        /// <summary>
        /// Checks whether the cylinder can be re-spun via Rewarded Ad.
        /// Rules:
        /// - Cylinder must have spun and finished spinning.
        /// - Turn must not be committed.
        /// - Max 1 re-spin per turn.
        /// - Experiment Group must have IsRespinAdEnabled == true (Group A & C: true, Group B: false).
        /// </summary>
        public bool CanRespin()
        {
            if (!_hasSpunThisTurn || _isSpinning || _turnCommitted) return false;
            if (_hasUsedRespinThisTurn) return false;

            if (ExperimentManager.Instance != null && !ExperimentManager.Instance.IsRespinAdEnabled)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Re-spins all 3 reels on the cylinder with full visual animation and audio.
        /// </summary>
        public bool RespinAllReels(bool isAdWatched = false)
        {
            if (!CanRespin())
            {
                Debug.LogWarning("[SlotRevolverController] Cannot respin: validation failed.");
                return false;
            }

            _hasUsedRespinThisTurn = true;
            StartCoroutine(RespinRoutine(isAdWatched));
            return true;
        }

        /// <summary>
        /// Instantly re-spins all 3 reels (useful for unit tests or instant playback).
        /// </summary>
        public void RespinAllReelsInstant(bool isAdWatched = false)
        {
            if (!CanRespin()) return;

            _hasUsedRespinThisTurn = true;
            SlotSymbol[] oldSymbols = (SlotSymbol[])_currentReelSymbols.Clone();

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = RollSymbol();
                EventBus.Publish(new SlotReelStoppedEvent(i, _currentReelSymbols[i]));
            }

            Debug.Log($"[SlotRevolverController] All 3 reels re-spun (AdWatched: {isAdWatched}).");
            EventBus.Publish(new SlotRespinTriggeredEvent(_turnNumber, oldSymbols, CurrentReelSymbols, isAdWatched));
            EventBus.Publish(new SlotSpinCompletedEvent(_turnNumber, CurrentReelSymbols));
        }

        private IEnumerator RespinRoutine(bool isAdWatched)
        {
            _isSpinning = true;
            SlotSymbol[] oldSymbols = (SlotSymbol[])_currentReelSymbols.Clone();

            EventBus.Publish(new SlotSpinStartedEvent(_turnNumber));

            if (audioSource != null && cylinderSpinSfx != null)
            {
                audioSource.PlayOneShot(cylinderSpinSfx);
            }

            yield return new WaitForSeconds(firstReelStopDelay);

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = RollSymbol();

                if (audioSource != null && reelStopSfx != null)
                {
                    audioSource.PlayOneShot(reelStopSfx);
                }

                EventBus.Publish(new SlotReelStoppedEvent(i, _currentReelSymbols[i]));

                if (i < REEL_COUNT - 1)
                {
                    yield return new WaitForSeconds(spinReelInterval);
                }
            }

            _isSpinning = false;
            Debug.Log($"[SlotRevolverController] All 3 reels re-spun (AdWatched: {isAdWatched}).");
            EventBus.Publish(new SlotRespinTriggeredEvent(_turnNumber, oldSymbols, CurrentReelSymbols, isAdWatched));
            EventBus.Publish(new SlotSpinCompletedEvent(_turnNumber, CurrentReelSymbols));
        }

        #endregion

        #region Wild Joker Resolution

        /// <summary>
        /// Checks if any reel currently holds a WILD_JOKER.
        /// </summary>
        public bool HasPendingWildJoker(out int reelIndex)
        {
            reelIndex = -1;
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (_currentReelSymbols[i] == SlotSymbol.WILD_JOKER)
                {
                    reelIndex = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Resolves a WILD_JOKER symbol on a reel into the player's chosen symbol.
        /// </summary>
        public bool ResolveWildJoker(int reelIndex, SlotSymbol chosenSymbol)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT) return false;
            if (_currentReelSymbols[reelIndex] != SlotSymbol.WILD_JOKER) return false;
            if (chosenSymbol == SlotSymbol.WILD_JOKER) return false;

            _currentReelSymbols[reelIndex] = chosenSymbol;
            Debug.Log($"[SlotRevolverController] Resolved WILD_JOKER at Reel {reelIndex} to {chosenSymbol}.");

            EventBus.Publish(new WildJokerResolvedEvent(reelIndex, chosenSymbol));
            EventBus.Publish(new SlotReelStoppedEvent(reelIndex, chosenSymbol));
            return true;
        }

        #endregion

        #region Turn Commitment

        /// <summary>
        /// Determines if the active turn can be committed for combat execution.
        /// </summary>
        public bool CanCommitTurn()
        {
            if (!_hasSpunThisTurn || _isSpinning || _turnCommitted) return false;
            if (AimStabilizerController.Instance != null && AimStabilizerController.Instance.IsAiming) return false;
            return true;
        }

        /// <summary>
        /// Commits the active turn.
        /// Collects all 3 reel symbols into executed list, increments turn count, and publishes TurnCommittedEvent.
        /// </summary>
        public bool CommitTurn(out List<SlotSymbol> executedSymbols)
        {
            executedSymbols = new List<SlotSymbol>();

            if (!CanCommitTurn())
            {
                Debug.LogWarning("[SlotRevolverController] Cannot commit turn: requirements not met.");
                return false;
            }

            // Auto-resolve any remaining Wild Jokers if player didn't manually pick
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (_currentReelSymbols[i] == SlotSymbol.WILD_JOKER)
                {
                    ResolveWildJoker(i, SlotSymbol.ATTACK);
                }
            }

            // All 3 reels are directly executed
            for (int i = 0; i < REEL_COUNT; i++)
            {
                executedSymbols.Add(_currentReelSymbols[i]);
            }

            _turnCommitted = true;
            _hasUsedRespinThisTurn = false;
            _hasSpunThisTurn = false;
            _turnNumber++;

            Debug.Log($"[SlotRevolverController] Turn {_turnNumber - 1} committed with {executedSymbols.Count} actions.");
            EventBus.Publish(new TurnCommittedEvent(_turnNumber - 1, executedSymbols));

            return true;
        }

        /// <summary>
        /// Resets the controller for a new combat match.
        /// </summary>
        public void ResetForNewMatch()
        {
            _turnNumber = 1;
            _isSpinning = false;
            _hasSpunThisTurn = false;
            _turnCommitted = false;
            _hasUsedRespinThisTurn = false;

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = SlotSymbol.ATTACK;
            }
        }

        #endregion
    }
}
