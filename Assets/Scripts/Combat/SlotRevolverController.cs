using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LuckyDuelist.Events;
using LuckyDuelist.Experiment;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Core Controller for the 3-Reel Tactical Revolver Cylinder Slot Machine.
    /// Handles:
    /// 1. Probabilistic rolling of 5 symbols (ATTACK 35%, MARK 20%, HEAL 20%, COUNTER 15%, WILD 10%).
    /// 2. 3-Reel diegetic cylinder spinning with staggered stopping & visual/audio hooks.
    /// 3. 2 Hold Slots inventory system (preserve up to 2 symbols across turns, min 1 symbol executed per turn).
    /// 4. Single-reel re-spin via Rewarded Ads (max 1x per turn, gated by ExperimentManager).
    /// 5. Wild Joker resolution & turn commitment.
    /// </summary>
    public class SlotRevolverController : MonoBehaviour
    {
        public static SlotRevolverController Instance { get; private set; }

        public const int REEL_COUNT = 3;
        public const int MAX_HOLD_SLOTS = 2;

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
        [SerializeField] private AudioClip holdToggleSfx;

        // Internal State
        private readonly SlotSymbol[] _currentReelSymbols = new SlotSymbol[REEL_COUNT];
        private readonly bool[] _isReelHeld = new bool[REEL_COUNT];
        private readonly SlotSymbol?[] _holdSlots = new SlotSymbol?[MAX_HOLD_SLOTS];
        private readonly int?[] _holdSourceReel = new int?[MAX_HOLD_SLOTS]; // tracks which reel index was put into hold slot

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

        public int HeldCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MAX_HOLD_SLOTS; i++)
                {
                    if (_holdSlots[i].HasValue) count++;
                }
                return count;
            }
        }

        public bool CanHoldMore => HeldCount < MAX_HOLD_SLOTS;

        public SlotSymbol[] CurrentReelSymbols => (SlotSymbol[])_currentReelSymbols.Clone();
        public SlotSymbol?[] HoldSlots => (SlotSymbol?[])_holdSlots.Clone();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Default initial roll (before first player spin)
            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = SlotSymbol.ATTACK;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            return !_isSpinning && !_hasSpunThisTurn;
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
                // Non-held reels are freshly randomized
                _currentReelSymbols[i] = RollSymbol();
                _isReelHeld[i] = false;
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
                _isReelHeld[i] = false;

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

        #region Re-Spin Single Reel via Rewarded Ads

        /// <summary>
        /// Checks whether a specific reel can be re-spun via Rewarded Ad.
        /// Rules:
        /// - Reel index must be 0, 1, or 2.
        /// - Cylinder must have spun and finished spinning.
        /// - Turn must not be committed.
        /// - Max 1 respin per turn.
        /// - Reel cannot be held in a hold slot.
        /// - Experiment Group must have IsRespinAdEnabled == true (Group A & C: true, Group B: false).
        /// </summary>
        public bool CanRespinReel(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT) return false;
            if (!_hasSpunThisTurn || _isSpinning || _turnCommitted) return false;
            if (_hasUsedRespinThisTurn) return false;
            if (_isReelHeld[reelIndex]) return false;

            if (ExperimentManager.Instance != null && !ExperimentManager.Instance.IsRespinAdEnabled)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Re-spins a single reel. Re-rolls symbol and sets HasUsedRespinThisTurn flag.
        /// </summary>
        public bool RespinReel(int reelIndex, bool isAdWatched = false)
        {
            if (!CanRespinReel(reelIndex))
            {
                Debug.LogWarning($"[SlotRevolverController] Cannot respin reel {reelIndex}. Validation failed.");
                return false;
            }

            SlotSymbol oldSymbol = _currentReelSymbols[reelIndex];
            SlotSymbol newSymbol = RollSymbol();
            _currentReelSymbols[reelIndex] = newSymbol;
            _hasUsedRespinThisTurn = true;

            if (audioSource != null && reelStopSfx != null)
            {
                audioSource.PlayOneShot(reelStopSfx);
            }

            Debug.Log($"[SlotRevolverController] Reel {reelIndex} re-spun from {oldSymbol} to {newSymbol} (AdWatched: {isAdWatched}).");
            EventBus.Publish(new SlotRespinTriggeredEvent(reelIndex, oldSymbol, newSymbol, isAdWatched));
            EventBus.Publish(new SlotReelStoppedEvent(reelIndex, newSymbol));

            return true;
        }

        #endregion

        #region 2 Hold Slots Inventory System

        /// <summary>
        /// Checks if a specific reel is currently held.
        /// </summary>
        public bool IsReelHeld(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT) return false;
            return _isReelHeld[reelIndex];
        }

        /// <summary>
        /// Retrieves the symbol at a hold slot index (0 or 1). Returns null if empty.
        /// </summary>
        public SlotSymbol? GetHoldSlot(int holdSlotIndex)
        {
            if (holdSlotIndex < 0 || holdSlotIndex >= MAX_HOLD_SLOTS) return null;
            return _holdSlots[holdSlotIndex];
        }

        /// <summary>
        /// Attempts to place a reel's symbol into an available Hold Slot.
        /// Validates:
        /// 1. Reel is valid and not already held.
        /// 2. Hold inventory has an available slot (max 2).
        /// 3. Rule: Minimal 1 symbol must be executed on active turn!
        ///    If holding this reel would leave 0 symbols for execution, it is rejected.
        /// </summary>
        public bool TryHoldReel(int reelIndex, int targetSlot = -1)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT) return false;
            if (_isReelHeld[reelIndex] || !_hasSpunThisTurn || _isSpinning || _turnCommitted) return false;

            // Check rule: Minimal 1 symbol must be executed
            int currentExecutableReels = 0;
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (!_isReelHeld[i]) currentExecutableReels++;
            }

            // If only 1 reel is currently executable, holding it would leave 0 executable symbols
            if (currentExecutableReels <= 1)
            {
                Debug.LogWarning("[SlotRevolverController] Cannot hold: Minimal 1 symbol must be executed on active turn.");
                return false;
            }

            // Find target slot
            int slotIndex = targetSlot;
            if (slotIndex < 0 || slotIndex >= MAX_HOLD_SLOTS || _holdSlots[slotIndex].HasValue)
            {
                slotIndex = -1;
                for (int i = 0; i < MAX_HOLD_SLOTS; i++)
                {
                    if (!_holdSlots[i].HasValue)
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }

            if (slotIndex == -1)
            {
                Debug.LogWarning("[SlotRevolverController] Hold Slots are full (Max 2).");
                return false;
            }

            _holdSlots[slotIndex] = _currentReelSymbols[reelIndex];
            _holdSourceReel[slotIndex] = reelIndex;
            _isReelHeld[reelIndex] = true;

            if (audioSource != null && holdToggleSfx != null)
            {
                audioSource.PlayOneShot(holdToggleSfx);
            }

            Debug.Log($"[SlotRevolverController] Held Reel {reelIndex} ({_currentReelSymbols[reelIndex]}) in Hold Slot {slotIndex}.");
            EventBus.Publish(new HoldSlotsChangedEvent(HoldSlots, HeldCount));
            return true;
        }

        /// <summary>
        /// Releases a symbol from a Hold Slot.
        /// If it was held from a reel during this turn, that reel is unmarked as held.
        /// </summary>
        public bool TryReleaseHold(int holdSlotIndex)
        {
            if (holdSlotIndex < 0 || holdSlotIndex >= MAX_HOLD_SLOTS) return false;
            if (!_holdSlots[holdSlotIndex].HasValue) return false;

            SlotSymbol releasedSymbol = _holdSlots[holdSlotIndex].Value;
            int? sourceReel = _holdSourceReel[holdSlotIndex];

            _holdSlots[holdSlotIndex] = null;
            _holdSourceReel[holdSlotIndex] = null;

            if (sourceReel.HasValue && sourceReel.Value >= 0 && sourceReel.Value < REEL_COUNT)
            {
                _isReelHeld[sourceReel.Value] = false;
            }

            if (audioSource != null && holdToggleSfx != null)
            {
                audioSource.PlayOneShot(holdToggleSfx);
            }

            Debug.Log($"[SlotRevolverController] Released Hold Slot {holdSlotIndex} ({releasedSymbol}).");
            EventBus.Publish(new HoldSlotsChangedEvent(HoldSlots, HeldCount));
            return true;
        }

        /// <summary>
        /// Toggles the hold state of a reel (holds if unheld, releases if already held).
        /// </summary>
        public bool ToggleHoldReel(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT) return false;

            if (_isReelHeld[reelIndex])
            {
                // Find which hold slot contains this reel
                for (int i = 0; i < MAX_HOLD_SLOTS; i++)
                {
                    if (_holdSourceReel[i] == reelIndex)
                    {
                        return TryReleaseHold(i);
                    }
                }
                _isReelHeld[reelIndex] = false;
                return true;
            }
            else
            {
                return TryHoldReel(reelIndex);
            }
        }

        /// <summary>
        /// Clears all hold slots (e.g. at match reset).
        /// </summary>
        public void ClearHoldSlots()
        {
            for (int i = 0; i < MAX_HOLD_SLOTS; i++)
            {
                _holdSlots[i] = null;
                _holdSourceReel[i] = null;
            }
            for (int i = 0; i < REEL_COUNT; i++)
            {
                _isReelHeld[i] = false;
            }
            EventBus.Publish(new HoldSlotsChangedEvent(HoldSlots, HeldCount));
        }

        #endregion

        #region Wild Joker Resolution

        /// <summary>
        /// Checks if any non-held reel currently holds a WILD_JOKER.
        /// </summary>
        public bool HasPendingWildJoker(out int reelIndex)
        {
            reelIndex = -1;
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (!_isReelHeld[i] && _currentReelSymbols[i] == SlotSymbol.WILD_JOKER)
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

            // Must have at least 1 symbol ready to execute
            int executableCount = 0;
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (!_isReelHeld[i]) executableCount++;
            }

            return executableCount >= 1;
        }

        /// <summary>
        /// Commits the active turn.
        /// Collects all non-held symbols into executed list, preserves held symbols in HoldSlots for next turn,
        /// increments turn count, and publishes TurnCommittedEvent.
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
                if (!_isReelHeld[i] && _currentReelSymbols[i] == SlotSymbol.WILD_JOKER)
                {
                    ResolveWildJoker(i, SlotSymbol.ATTACK);
                }
            }

            // Gather all unheld symbols to execute
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (!_isReelHeld[i])
                {
                    executedSymbols.Add(_currentReelSymbols[i]);
                }
            }

            _turnCommitted = true;
            _hasUsedRespinThisTurn = false;
            _hasSpunThisTurn = false;

            // Clear reel held flags for the upcoming turn (symbols stay in _holdSlots!)
            for (int i = 0; i < REEL_COUNT; i++)
            {
                _isReelHeld[i] = false;
            }
            for (int i = 0; i < MAX_HOLD_SLOTS; i++)
            {
                // Uncouple source reel reference so hold slots carry over cleanly to next turn
                _holdSourceReel[i] = null;
            }

            _turnNumber++;

            Debug.Log($"[SlotRevolverController] Turn {_turnNumber - 1} committed with {executedSymbols.Count} actions. Held items preserved: {HeldCount}.");
            EventBus.Publish(new TurnCommittedEvent(_turnNumber - 1, executedSymbols, HoldSlots));

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
            ClearHoldSlots();

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _currentReelSymbols[i] = SlotSymbol.ATTACK;
                _isReelHeld[i] = false;
            }
        }

        #endregion
    }
}
