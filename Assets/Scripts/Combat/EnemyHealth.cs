using UnityEngine;
using LuckyDuelist.Core;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Tracks and manages Enemy Bot Health & Debuff Status in 1v1 Combat:
    /// - Stage 1: Neon Rookie (70 HP)
    /// - Stage 2: Chrome Gambler (90 HP)
    /// - Stage 3: The Cyber Baron (120 HP)
    /// Handles damage reception, MARK debuff status (2x Critical multiplier synergy),
    /// and dispatches EnemyHealthChangedEvent / EnemyMarkStatusChangedEvent.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        public static EnemyHealth Instance { get; private set; }

        [Header("Enemy State")]
        [SerializeField] private int maxHealth = 70;
        [SerializeField] private int currentHealth = 70;
        [SerializeField] private string enemyName = "Neon Rookie";
        [SerializeField] private int stageNumber = 1;
        [SerializeField] private bool isMarked = false;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public float HealthPercent => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        public bool IsAlive => currentHealth > 0;
        public bool IsMarked => isMarked;
        public string EnemyName => enemyName;
        public int StageNumber => stageNumber;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            currentHealth = maxHealth;
        }

        private void Start()
        {
            EventBus.Subscribe<StageStartedEvent>(OnStageStarted);

            // Initial broadcast
            EventBus.Publish(new EnemyHealthChangedEvent(currentHealth, maxHealth, 0, false, "Initial"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
        }

        /// <summary>
        /// Applies damage to the enemy. Triggers stage victory if HP drops to 0.
        /// </summary>
        public void TakeDamage(int amount, bool isCritical = false, string reason = "PlayerAttack")
        {
            if (amount <= 0 || currentHealth <= 0) return;

            int prev = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            int delta = currentHealth - prev;

            Debug.Log($"<color=red>[EnemyHealth]</color> {enemyName} took {amount} {(isCritical ? "CRITICAL " : "")}damage ({reason}). HP: {currentHealth}/{maxHealth}");
            EventBus.Publish(new EnemyHealthChangedEvent(currentHealth, maxHealth, delta, isCritical, reason));

            if (currentHealth <= 0)
            {
                Debug.Log($"<color=green>[EnemyHealth]</color> {enemyName} has been defeated!");
                if (GameManager.Instance != null)
                {
                    int turns = SlotRevolverController.Instance != null ? SlotRevolverController.Instance.TurnNumber : 1;
                    int playerHp = PlayerHealth.Instance != null ? PlayerHealth.Instance.CurrentHealth : 100;
                    GameManager.Instance.OnStageCompleted(isVictory: true, turnsTaken: turns, playerRemainingHp: playerHp, botRemainingHp: 0);
                }
            }
        }

        /// <summary>
        /// Applies the [MARK] status debuff to the enemy.
        /// Sinergy: Expands vital circle and doubles attack damage to 2x (30 Crit Damage).
        /// </summary>
        public void ApplyMark(string reason = "SlotSymbol.MARK")
        {
            if (isMarked) return;

            isMarked = true;
            Debug.Log($"<color=magenta>[EnemyHealth]</color> {enemyName} has been [MARKED] ({reason})! Next attacks deal 2x CRITICAL DAMAGE!");
            EventBus.Publish(new EnemyMarkStatusChangedEvent(true, reason));
        }

        /// <summary>
        /// Clears the [MARK] status debuff (e.g. after attack burst sequence concludes).
        /// </summary>
        public void ClearMark(string reason = "BurstCompleted")
        {
            if (!isMarked) return;

            isMarked = false;
            Debug.Log($"<color=yellow>[EnemyHealth]</color> [MARK] debuff expired on {enemyName} ({reason}).");
            EventBus.Publish(new EnemyMarkStatusChangedEvent(false, reason));
        }

        /// <summary>
        /// Resets the enemy health and mark status for a specific stage.
        /// </summary>
        public void ResetForStage(int stageNum, string name, int maxHp)
        {
            stageNumber = stageNum;
            enemyName = name;
            maxHealth = maxHp;
            currentHealth = maxHp;
            isMarked = false;

            EventBus.Publish(new EnemyHealthChangedEvent(currentHealth, maxHealth, 0, false, "StageReset"));
            EventBus.Publish(new EnemyMarkStatusChangedEvent(false, "StageReset"));
        }

        /// <summary>
        /// Sets current health directly (useful for tests or debug).
        /// </summary>
        public void SetHealth(int amount)
        {
            int prev = currentHealth;
            currentHealth = Mathf.Clamp(amount, 0, maxHealth);
            EventBus.Publish(new EnemyHealthChangedEvent(currentHealth, maxHealth, currentHealth - prev, false, "ManualSet"));
        }

        private void OnStageStarted(StageStartedEvent evt)
        {
            ResetForStage(evt.StageNumber, evt.StageName, evt.BossMaxHp);
        }
    }
}
