using UnityEngine;
using LuckyDuelist.Core;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Tracks and manages Player Health (100 / 100 max HP).
    /// Integrates with HEAL symbols (+20 HP), damage taken, revive via Rewarded Ads,
    /// and dispatches PlayerHealthChangedEvent.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("Health Configuration")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth = 100;
        [SerializeField] private int healSymbolAmount = 20;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        public bool IsAlive => currentHealth > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            currentHealth = maxHealth;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Subscribe<StageStartedEvent>(OnStageStarted);
            EventBus.Subscribe<TurnCommittedEvent>(OnTurnCommitted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
            EventBus.Unsubscribe<TurnCommittedEvent>(OnTurnCommitted);
        }

        private void Start()
        {
            // Initial broadcast
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, 0, "Initial"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Resets player health to maximum (e.g. at stage start).
        /// </summary>
        public void ResetHealth()
        {
            int previous = currentHealth;
            currentHealth = maxHealth;
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, currentHealth - previous, "Reset"));
        }

        /// <summary>
        /// Applies damage to player. Triggers defeat if HP drops to 0.
        /// </summary>
        public void TakeDamage(int amount, string reason = "CombatDamage")
        {
            if (amount <= 0 || currentHealth <= 0) return;

            int prev = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            int delta = currentHealth - prev;

            Debug.Log($"[PlayerHealth] Player took {amount} damage from '{reason}'. HP: {currentHealth}/{maxHealth}");
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, delta, reason));

            if (currentHealth <= 0)
            {
                Debug.Log("[PlayerHealth] Player HP reached 0. Triggering defeat.");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.OnPlayerDefeated();
                }
            }
        }

        /// <summary>
        /// Restores player health up to max HP.
        /// </summary>
        public void Heal(int amount, string reason = "NanoMedkit")
        {
            if (amount <= 0 || currentHealth <= 0) return;

            int prev = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            int delta = currentHealth - prev;

            Debug.Log($"[PlayerHealth] Player healed +{amount} ({reason}). HP: {currentHealth}/{maxHealth}");
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, delta, reason));
        }

        /// <summary>
        /// Directly sets player health for testing or custom restores.
        /// </summary>
        public void SetHealth(int amount)
        {
            int prev = currentHealth;
            currentHealth = Mathf.Clamp(amount, 0, maxHealth);
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, currentHealth - prev, "ManualSet"));
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            int targetHp = Mathf.RoundToInt(maxHealth * evt.RestoredHpPercent);
            currentHealth = Mathf.Max(1, targetHp);
            Debug.Log($"[PlayerHealth] Revive restored player HP to {currentHealth}/{maxHealth} ({evt.RestoredHpPercent * 100}%).");
            EventBus.Publish(new PlayerHealthChangedEvent(currentHealth, maxHealth, currentHealth, "ReviveAd"));
        }

        private void OnStageStarted(StageStartedEvent evt)
        {
            ResetHealth();
        }

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            if (evt.ExecutedSymbols == null) return;

            // Automatically apply +20 HP per HEAL symbol committed
            int healCount = 0;
            for (int i = 0; i < evt.ExecutedSymbols.Count; i++)
            {
                if (evt.ExecutedSymbols[i] == SlotSymbol.HEAL)
                {
                    healCount++;
                }
            }

            if (healCount > 0)
            {
                int totalHeal = healCount * healSymbolAmount;
                Heal(totalHeal, $"HEAL Symbol x{healCount}");
            }
        }
    }
}
