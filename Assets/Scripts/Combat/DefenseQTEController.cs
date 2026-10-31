using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LuckyDuelist.Events;
using LuckyDuelist.Core;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Master Defense QTE Controller as specified in GDD Section 2.2, 2.5 and TSD Section 2.4.
    /// Orchestrates:
    /// 1. Enemy pistol telegraph (Red Unparryable vs Yellow Parryable).
    /// 2. Sinergi [COUNTER / SHARPEN]: Audio cue warning ("Ching-Ching / Kokangan Senjata") & extra True Damage.
    /// 3. In-flight bullet tracking & precision window detection.
    /// 4. Player defense resolution:
    ///    - [Q] Hindar: Camera sway, 0 damage.
    ///    - [E] Tangkis: Knife swing. Yellow -> 'CLANG!' + neon sparks + Critical Deflection Slash (35-45 DMG); Red -> Fail! Full damage.
    /// 5. Multi-burst sequencing from BotGunnerAI across 3 stages.
    /// 6. Telemetry aggregation (dodge_red_count, parry_yellow_count, unparryable_fail_count, damage_deflected).
    /// </summary>
    public class DefenseQTEController : MonoBehaviour
    {
        public static DefenseQTEController Instance { get; private set; }

        public const int BASE_ENEMY_BULLET_DAMAGE = 25;
        public const int MIN_DEFLECTION_DAMAGE = 35;
        public const int MAX_DEFLECTION_DAMAGE = 45;
        public const int COUNTER_SHARPEN_TRUE_DAMAGE_BONUS = 15;

        [Header("Audio Clues & Sfx")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cockingWarningCueSfx;
        [SerializeField] private AudioClip enemyGunshotSfx;
        [SerializeField] private AudioClip dodgeWhooshSfx;
        [SerializeField] private AudioClip playerHurtSfx;

        [Header("Visual Elements (Optional Inspector references)")]
        [Tooltip("Transform of enemy gun muzzle in 3D world space")]
        [SerializeField] private Transform enemyMuzzleTransform;
        [SerializeField] private Light muzzleTelegraphLight;
        [SerializeField] private Canvas defenseCanvas;
        [SerializeField] private Text defenseAlertText;
        [SerializeField] private Image screenVignetteImage;

        // Telemetry & Statistics
        private int _dodgeRedCount = 0;
        private int _parryYellowCount = 0;
        private int _unparryableFailCount = 0;
        private int _damageDeflected = 0;

        // State Tracking
        private bool _isCounterBuffActive = false;
        private bool _isDefensePhaseActive = false;
        private Coroutine _currentSequenceCoroutine;

        // Public Properties
        public bool IsDefensePhaseActive => _isDefensePhaseActive;
        public bool IsCounterBuffActive => _isCounterBuffActive;
        public int DodgeRedCount => _dodgeRedCount;
        public int ParryYellowCount => _parryYellowCount;
        public int UnparryableFailCount => _unparryableFailCount;
        public int DamageDeflected => _damageDeflected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSource();
            EnsureProceduralMuzzle();
        }

        private void Start()
        {
            EventBus.Subscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Subscribe<AimSequenceCompletedEvent>(OnAimSequenceCompleted);
            EventBus.Subscribe<StageStartedEvent>(OnStageStarted);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            EventBus.Unsubscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Unsubscribe<AimSequenceCompletedEvent>(OnAimSequenceCompleted);
            EventBus.Unsubscribe<StageStartedEvent>(OnStageStarted);
        }

        #region Counter / Sharpen Buff Management

        /// <summary>
        /// Activates the COUNTER / SHARPEN blade buff.
        /// When active, plays audio cue warning during telegraph and grants +15 True Damage on parry.
        /// </summary>
        public void ActivateCounterBuff(string reason = "SlotSymbol.COUNTER_SHARPEN")
        {
            _isCounterBuffActive = true;
            Debug.Log($"<color=cyan>[DefenseQTE]</color> COUNTER / SHARPEN buff activated ({reason})! Audio cue warning primed & +{COUNTER_SHARPEN_TRUE_DAMAGE_BONUS} True Damage.");
            EventBus.Publish(new CounterBuffStatusChangedEvent(true, COUNTER_SHARPEN_TRUE_DAMAGE_BONUS, reason));
        }

        /// <summary>
        /// Consumes or clears the COUNTER / SHARPEN blade buff.
        /// </summary>
        public void ClearCounterBuff(string reason = "Consumed")
        {
            if (!_isCounterBuffActive) return;
            _isCounterBuffActive = false;
            EventBus.Publish(new CounterBuffStatusChangedEvent(false, 0, reason));
        }

        #endregion

        #region Enemy Attack Sequence Flow

        /// <summary>
        /// Starts the full enemy turn attack sequence based on current stage and turn.
        /// </summary>
        public void StartEnemyAttackSequence()
        {
            if (_isDefensePhaseActive) return;

            int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStageIndex : 1;
            int turn = SlotRevolverController.Instance != null ? SlotRevolverController.Instance.TurnNumber : 1;

            List<EnemyShotData> shots;
            if (BotGunnerAI.Instance != null)
            {
                shots = BotGunnerAI.Instance.PlanAttackPattern(stage, turn);
            }
            else
            {
                shots = BotGunnerAI.GenerateAttackPattern(stage, turn);
            }

            if (_currentSequenceCoroutine != null)
            {
                StopCoroutine(_currentSequenceCoroutine);
            }
            _currentSequenceCoroutine = StartCoroutine(EnemyAttackSequenceRoutine(stage, shots));
        }

        private IEnumerator EnemyAttackSequenceRoutine(int stage, List<EnemyShotData> shots)
        {
            _isDefensePhaseActive = true;
            Debug.Log($"<color=red>[DefenseQTE]</color> Enemy Turn Started! Stage {stage}, Shots: {shots.Count}");
            EventBus.Publish(new EnemyTurnStartedEvent(stage, shots.Count));

            // Initial brief delay before enemy draws gun
            yield return new WaitForSeconds(0.4f);

            for (int i = 0; i < shots.Count; i++)
            {
                // Check if combat already ended (e.g. player died or bot died from earlier parry)
                if (IsCombatFinished()) break;

                EnemyShotData shot = shots[i];
                yield return StartCoroutine(ExecuteSingleShotRoutine(shot, i, shots.Count, stage));

                if (IsCombatFinished()) break;

                if (i < shots.Count - 1 && shot.PauseAfterShot > 0f)
                {
                    yield return new WaitForSeconds(shot.PauseAfterShot);
                }
            }

            _isDefensePhaseActive = false;
            bool playerAlive = PlayerHealth.Instance == null || PlayerHealth.Instance.IsAlive;

            Debug.Log($"<color=green>[DefenseQTE]</color> Enemy Turn Concluded! Player Surviving: {playerAlive}");
            EventBus.Publish(new EnemyTurnCompletedEvent(stage, playerAlive));

            HideAlertUI();
        }

        private IEnumerator ExecuteSingleShotRoutine(EnemyShotData shot, int burstIndex, int totalBursts, int stage)
        {
            // -------------------------------------------------------------
            // PHASE 1: TELEGRAPH (Moncong Senjata Memancarkan Cahaya)
            // -------------------------------------------------------------
            bool isRed = shot.AttackType == EnemyAttackType.UNPARRYABLE_RED;
            Color telegraphColor = isRed ? new Color(1f, 0.15f, 0.2f) : new Color(1f, 0.9f, 0.15f);

            ShowMuzzleTelegraph(telegraphColor);
            ShowAlertUI(isRed, burstIndex, totalBursts);

            // Sinergi [COUNTER / SHARPEN]: Play Audio Cue Warning ("Ching-Ching / Kokangan Senjata")
            if (_isCounterBuffActive)
            {
                PlayCockingWarningSfx();
                Debug.Log("<color=cyan>[DefenseQTE]</color> ⚡ WARNING CUE: Kokangan Senjata heard! Musuh akan menembak!");
            }

            EventBus.Publish(new EnemyTelegraphStartedEvent(shot.AttackType, shot.TelegraphDuration, _isCounterBuffActive, burstIndex, totalBursts, stage));

            // Wait telegraph duration
            yield return new WaitForSeconds(shot.TelegraphDuration);

            // -------------------------------------------------------------
            // PHASE 2: TRIGGER PULL & BULLET FLIGHT (Peluru Meluncur)
            // -------------------------------------------------------------
            HideMuzzleTelegraph();
            PlayGunshotSfx();

            EventBus.Publish(new EnemyBulletFiredEvent(shot.AttackType, shot.BulletTravelTime, burstIndex, totalBursts));

            // Spawn procedural projectile visual travelling to camera
            StartCoroutine(SpawnBulletProjectileRoutine(shot.AttackType, shot.BulletTravelTime));

            // Bullet travel time loop: player can press Q or E
            float flightElapsed = 0f;
            while (flightElapsed < shot.BulletTravelTime)
            {
                flightElapsed += Time.deltaTime;
                yield return null;
            }

            // -------------------------------------------------------------
            // PHASE 3: IMPACT RESOLUTION
            // -------------------------------------------------------------
            bool isDodging = FPSCameraController.Instance != null && FPSCameraController.Instance.IsDodging;
            bool isParryingInWindow = KnifeParryController.Instance != null && KnifeParryController.Instance.IsInParryWindow;
            bool isParrying = KnifeParryController.Instance != null && KnifeParryController.Instance.IsParrying;

            int baseDeflection = UnityEngine.Random.Range(MIN_DEFLECTION_DAMAGE, MAX_DEFLECTION_DAMAGE + 1);
            int counterBonus = _isCounterBuffActive ? COUNTER_SHARPEN_TRUE_DAMAGE_BONUS : 0;

            DefenseResolution resolution = ResolveDefenseOutcome(
                shot.AttackType,
                isDodging,
                isParryingInWindow,
                isParrying,
                _isCounterBuffActive,
                baseDeflection,
                counterBonus,
                BASE_ENEMY_BULLET_DAMAGE
            );

            // Apply Consequences
            ApplyDefenseResult(resolution, burstIndex, totalBursts);
        }

        #endregion

        #region Defense Resolution Logic

        public struct DefenseResolution
        {
            public DefenseResult Result;
            public EnemyAttackType AttackType;
            public int DamageToPlayer;
            public int DamageToEnemy;
            public bool IsCriticalParry;
            public string Description;
        }

        /// <summary>
        /// Pure function calculating defense outcome.
        /// Fully deterministic and unit testable.
        /// </summary>
        public static DefenseResolution ResolveDefenseOutcome(
            EnemyAttackType attackType,
            bool isDodging,
            bool isParryingInWindow,
            bool isParryingAttempted,
            bool isCounterActive,
            int baseDeflectionDamage,
            int counterBonusDamage,
            int enemyBulletDamage = BASE_ENEMY_BULLET_DAMAGE)
        {
            DefenseResolution res = new DefenseResolution
            {
                AttackType = attackType,
                DamageToPlayer = 0,
                DamageToEnemy = 0,
                IsCriticalParry = false
            };

            if (attackType == EnemyAttackType.UNPARRYABLE_RED)
            {
                // RED BULLET (UNPARRYABLE):
                // 1. If player tried to parry [E] -> ParryFailedRed (Full Damage!)
                // 2. If player dodged [Q] -> DodgeSuccess (0 Damage)
                // 3. Otherwise -> HitTaken (Full Damage)
                if (isParryingAttempted)
                {
                    res.Result = DefenseResult.ParryFailedRed;
                    res.DamageToPlayer = enemyBulletDamage;
                    res.Description = "Pisau gagal menahan peluru merah! Kena damage penuh.";
                }
                else if (isDodging)
                {
                    res.Result = DefenseResult.DodgeSuccess;
                    res.DamageToPlayer = 0;
                    res.Description = "Kamera tersentak miring ke samping! Peluru lewat (0 Damage).";
                }
                else
                {
                    res.Result = DefenseResult.HitTaken;
                    res.DamageToPlayer = enemyBulletDamage;
                    res.Description = "Kena tembakan peluru merah!";
                }
            }
            else // PARRYABLE_YELLOW
            {
                // YELLOW BULLET (PARRYABLE):
                // 1. If player parried in active precision window [E] -> ParrySuccess! (CLANG + Critical Deflection Slash)
                // 2. If player dodged [Q] -> DodgeSuccess (Safe option, 0 Damage)
                // 3. If player parried outside window -> ParryFailedTiming
                // 4. Otherwise -> HitTaken
                if (isParryingInWindow)
                {
                    res.Result = DefenseResult.ParrySuccess;
                    res.DamageToPlayer = 0;
                    res.DamageToEnemy = baseDeflectionDamage + counterBonusDamage;
                    res.IsCriticalParry = true;
                    res.Description = $"CLANG! Peluru kuning berbalik menghantam musuh dengan Critical Deflection Slash ({res.DamageToEnemy} DMG)!";
                }
                else if (isDodging)
                {
                    res.Result = DefenseResult.DodgeSuccess;
                    res.DamageToPlayer = 0;
                    res.Description = "Menghindar peluru kuning dengan aman (0 Damage).";
                }
                else if (isParryingAttempted)
                {
                    res.Result = DefenseResult.ParryFailedTiming;
                    res.DamageToPlayer = enemyBulletDamage;
                    res.Description = "Timing parry pisau meleset! Kena tembakan.";
                }
                else
                {
                    res.Result = DefenseResult.HitTaken;
                    res.DamageToPlayer = enemyBulletDamage;
                    res.Description = "Kena tembakan peluru kuning!";
                }
            }

            return res;
        }

        private void ApplyDefenseResult(DefenseResolution res, int burstIndex, int totalBursts)
        {
            Debug.Log($"<color=yellow>[DefenseQTE] Result: {res.Result}</color> - {res.Description}");

            switch (res.Result)
            {
                case DefenseResult.DodgeSuccess:
                    PlayWhooshSfx();
                    ShowFloatingDefenseText("DODGE! 0 DMG", new Color(0.2f, 1f, 0.4f));
                    if (res.AttackType == EnemyAttackType.UNPARRYABLE_RED)
                    {
                        _dodgeRedCount++;
                    }
                    break;

                case DefenseResult.ParrySuccess:
                    if (KnifeParryController.Instance != null)
                    {
                        KnifeParryController.Instance.OnParrySuccess(_isCounterBuffActive);
                    }
                    ShowFloatingDefenseText($"PARRY DEFLECT! CRIT -{res.DamageToEnemy}", new Color(1f, 0.95f, 0.2f));

                    _parryYellowCount++;
                    _damageDeflected += res.DamageToEnemy;

                    // Apply reflected damage to enemy!
                    if (EnemyHealth.Instance != null)
                    {
                        EnemyHealth.Instance.TakeDamage(res.DamageToEnemy, isCritical: true, reason: "CriticalDeflectionSlash");
                    }

                    // Consume counter buff if it was active
                    if (_isCounterBuffActive)
                    {
                        ClearCounterBuff("ConsumedByParry");
                    }
                    break;

                case DefenseResult.ParryFailedRed:
                    if (KnifeParryController.Instance != null)
                    {
                        KnifeParryController.Instance.OnParryFailedRed();
                    }
                    PlayHurtSfx();
                    ShowFloatingDefenseText("CANNOT PARRY RED! -25 HP", new Color(1f, 0.15f, 0.25f));
                    _unparryableFailCount++;

                    if (PlayerHealth.Instance != null)
                    {
                        PlayerHealth.Instance.TakeDamage(res.DamageToPlayer, "UnparryableRedGunshot");
                    }
                    break;

                case DefenseResult.ParryFailedTiming:
                    PlayHurtSfx();
                    ShowFloatingDefenseText("PARRY MISSTIMED! -25 HP", new Color(1f, 0.4f, 0.2f));

                    if (PlayerHealth.Instance != null)
                    {
                        PlayerHealth.Instance.TakeDamage(res.DamageToPlayer, "MissedTimingGunshot");
                    }
                    break;

                case DefenseResult.HitTaken:
                default:
                    PlayHurtSfx();
                    ShowFloatingDefenseText("HIT! -25 HP", new Color(1f, 0.2f, 0.2f));

                    if (PlayerHealth.Instance != null)
                    {
                        PlayerHealth.Instance.TakeDamage(res.DamageToPlayer, "EnemyGunshot");
                    }
                    break;
            }

            EventBus.Publish(new DefenseResolvedEvent(
                res.Result,
                res.AttackType,
                res.DamageToPlayer,
                res.DamageToEnemy,
                res.IsCriticalParry,
                burstIndex,
                totalBursts
            ));
        }

        private bool IsCombatFinished()
        {
            bool playerDead = PlayerHealth.Instance != null && !PlayerHealth.Instance.IsAlive;
            bool enemyDead = EnemyHealth.Instance != null && !EnemyHealth.Instance.IsAlive;
            return playerDead || enemyDead;
        }

        #endregion

        #region Event Callbacks

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            if (evt.ExecutedSymbols == null) return;

            // 1. Check COUNTER / SHARPEN symbol
            if (evt.ExecutedSymbols.Contains(SlotSymbol.COUNTER_SHARPEN))
            {
                ActivateCounterBuff();
            }

            // 2. Check if player committed ANY attack symbol
            bool hasAttack = evt.ExecutedSymbols.Contains(SlotSymbol.ATTACK);
            if (!hasAttack)
            {
                // Player only healed, sharpened, or buffed -> directly transition to enemy's turn!
                StartCoroutine(TriggerEnemyTurnDelayed(0.8f));
            }
        }

        private void OnAimSequenceCompleted(AimSequenceCompletedEvent evt)
        {
            // If the enemy wasn't eliminated during player's aim phase, enemy now retaliates!
            if (!evt.EnemyDefeated)
            {
                StartCoroutine(TriggerEnemyTurnDelayed(0.85f));
            }
        }

        private void OnStageStarted(StageStartedEvent evt)
        {
            // Reset per-stage statistics
            _dodgeRedCount = 0;
            _parryYellowCount = 0;
            _unparryableFailCount = 0;
            _damageDeflected = 0;
            _isCounterBuffActive = false;
            _isDefensePhaseActive = false;

            if (_currentSequenceCoroutine != null)
            {
                StopCoroutine(_currentSequenceCoroutine);
                _currentSequenceCoroutine = null;
            }

            HideAlertUI();
            HideMuzzleTelegraph();
        }

        private IEnumerator TriggerEnemyTurnDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsCombatFinished())
            {
                StartEnemyAttackSequence();
            }
        }

        #endregion

        #region Procedural Visuals & Projectile

        private void EnsureProceduralMuzzle()
        {
            if (enemyMuzzleTransform != null) return;

            // Default muzzle position in front of player
            GameObject muzzleGo = new GameObject("[EnemyPistolMuzzle]");
            muzzleGo.transform.position = new Vector3(0f, 0f, 4.2f);
            enemyMuzzleTransform = muzzleGo.transform;

            // Muzzle Telegraph Light
            muzzleTelegraphLight = muzzleGo.AddComponent<Light>();
            muzzleTelegraphLight.type = LightType.Point;
            muzzleTelegraphLight.range = 5.0f;
            muzzleTelegraphLight.intensity = 0f;
            muzzleTelegraphLight.enabled = false;
        }

        private void ShowMuzzleTelegraph(Color color)
        {
            if (muzzleTelegraphLight != null)
            {
                muzzleTelegraphLight.color = color;
                muzzleTelegraphLight.intensity = 3.5f;
                muzzleTelegraphLight.enabled = true;
            }
        }

        private void HideMuzzleTelegraph()
        {
            if (muzzleTelegraphLight != null)
            {
                muzzleTelegraphLight.enabled = false;
            }
        }

        private IEnumerator SpawnBulletProjectileRoutine(EnemyAttackType attackType, float travelTime)
        {
            bool isRed = attackType == EnemyAttackType.UNPARRYABLE_RED;
            Color bulletColor = isRed ? new Color(1f, 0.15f, 0.2f) : new Color(1f, 0.95f, 0.2f);

            GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.name = "EnemyBulletTracer";
            proj.transform.localScale = new Vector3(0.12f, 0.12f, 0.45f);

            var col = proj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = proj.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.material.color = bulletColor;
            }

            Vector3 startPos = enemyMuzzleTransform != null ? enemyMuzzleTransform.position : new Vector3(0f, 0f, 4.2f);
            Vector3 endPos = Vector3.forward * 0.4f;
            if (Camera.main != null)
            {
                endPos = Camera.main.transform.position + Camera.main.transform.forward * 0.35f;
            }

            float elapsed = 0f;
            while (elapsed < travelTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelTime);
                proj.transform.position = Vector3.Lerp(startPos, endPos, t);
                proj.transform.LookAt(endPos);
                yield return null;
            }

            Destroy(proj);
        }

        private void ShowAlertUI(bool isRed, int burstIndex, int totalBursts)
        {
            EnsureDefenseUI();
            if (defenseAlertText != null)
            {
                defenseAlertText.gameObject.SetActive(true);
                string burstInfo = totalBursts > 1 ? $" [BURST {burstIndex + 1}/{totalBursts}]" : "";

                if (isRed)
                {
                    defenseAlertText.text = $"<b><color=#FF0055>⚠ PELURU MERAH (UNPARRYABLE)!{burstInfo}\nWAJIB TEKAN [Q] HINDAR!</color></b>";
                }
                else
                {
                    defenseAlertText.text = $"<b><color=#FFE600>⚡ PELURU KUNING (PARRYABLE)!{burstInfo}\nTEKAN [E] PARRY PRESISI (ATAU [Q] HINDAR)</color></b>";
                }
            }
        }

        private void HideAlertUI()
        {
            if (defenseAlertText != null)
            {
                defenseAlertText.gameObject.SetActive(false);
            }
        }

        private void EnsureDefenseUI()
        {
            if (defenseCanvas != null) return;

            GameObject canvasGo = new GameObject("[DefenseQTEDisplay]");
            defenseCanvas = canvasGo.AddComponent<Canvas>();
            defenseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject textGo = new GameObject("AlertText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.sizeDelta = new Vector2(650f, 90f);

            defenseAlertText = textGo.AddComponent<Text>();
            defenseAlertText.font = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            defenseAlertText.fontSize = 26;
            defenseAlertText.alignment = TextAnchor.MiddleCenter;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            textGo.SetActive(false);
        }

        private void ShowFloatingDefenseText(string message, Color color)
        {
            EnsureDefenseUI();
            if (defenseCanvas == null) return;

            GameObject textGo = new GameObject("FloatingDefenseFeedback");
            var rect = textGo.AddComponent<RectTransform>();
            rect.SetParent(defenseCanvas.transform, false);
            rect.anchoredPosition = new Vector2(0f, 20f);

            var txt = textGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 32;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = message;
            txt.color = color;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            StartCoroutine(FloatingTextFadeRoutine(rect, txt));
        }

        private IEnumerator FloatingTextFadeRoutine(RectTransform rect, Text txt)
        {
            float duration = 0.85f;
            float elapsed = 0f;
            Vector2 start = rect.anchoredPosition;
            Vector2 end = start + new Vector2(0f, 60f);
            Color startCol = txt.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                txt.color = Color.Lerp(startCol, new Color(startCol.r, startCol.g, startCol.b, 0f), t);
                yield return null;
            }

            Destroy(rect.gameObject);
        }

        #endregion

        #region Procedural Audio Synthesis

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.playOnAwake = false;
        }

        private void PlayCockingWarningSfx()
        {
            if (audioSource == null) return;
            if (cockingWarningCueSfx != null)
            {
                audioSource.PlayOneShot(cockingWarningCueSfx, 1.0f);
            }
            else
            {
                audioSource.PlayOneShot(CreateProceduralCockingWarningClip(), 1.0f);
            }
        }

        private void PlayGunshotSfx()
        {
            if (audioSource == null) return;
            if (enemyGunshotSfx != null)
            {
                audioSource.PlayOneShot(enemyGunshotSfx, 0.9f);
            }
            else
            {
                audioSource.PlayOneShot(CreateProceduralGunshotClip(), 0.9f);
            }
        }

        private void PlayWhooshSfx()
        {
            if (audioSource == null) return;
            if (dodgeWhooshSfx != null)
            {
                audioSource.PlayOneShot(dodgeWhooshSfx, 0.7f);
            }
            else
            {
                audioSource.PlayOneShot(KnifeParryController.CreateProceduralWhooshClip(), 0.7f);
            }
        }

        private void PlayHurtSfx()
        {
            if (audioSource == null) return;
            if (playerHurtSfx != null)
            {
                audioSource.PlayOneShot(playerHurtSfx, 0.9f);
            }
            else
            {
                audioSource.PlayOneShot(KnifeParryController.CreateProceduralFailClip(), 0.85f);
            }
        }

        /// <summary>
        /// Synthesizes the characteristic "Ching-Ching / Kokangan Senjata" double-click warning cue clip.
        /// </summary>
        public static AudioClip CreateProceduralCockingWarningClip()
        {
            int sampleRate = 22050;
            float lengthSec = 0.28f;
            int totalSamples = (int)(sampleRate * lengthSec);
            float[] samples = new float[totalSamples];

            // Double sharp high-pitch metallic click (at t=0.02s and t=0.14s)
            float click1Time = 0.02f;
            float click2Time = 0.14f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float v = 0f;

                // First click ("Ching")
                if (t >= click1Time && t < click1Time + 0.08f)
                {
                    float dt = t - click1Time;
                    float decay = Mathf.Exp(-dt * 45f);
                    v += (Mathf.Sin(2f * Mathf.PI * 2400f * dt) + Mathf.Sin(2f * Mathf.PI * 3600f * dt) * 0.6f) * decay;
                }

                // Second click ("Ching")
                if (t >= click2Time && t < click2Time + 0.08f)
                {
                    float dt = t - click2Time;
                    float decay = Mathf.Exp(-dt * 45f);
                    v += (Mathf.Sin(2f * Mathf.PI * 2800f * dt) + Mathf.Sin(2f * Mathf.PI * 4200f * dt) * 0.6f) * decay;
                }

                samples[i] = v * 0.8f;
            }

            AudioClip clip = AudioClip.Create("CockingWarning_Procedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateProceduralGunshotClip()
        {
            int sampleRate = 22050;
            float lengthSec = 0.35f;
            int totalSamples = (int)(sampleRate * lengthSec);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 20f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.8f;
                float pop = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.5f;
                samples[i] = (noise + pop) * decay * 0.9f;
            }

            AudioClip clip = AudioClip.Create("EnemyGunshot_Procedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
