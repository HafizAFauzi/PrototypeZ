using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// FPS Aim Stabilizer Controller as specified in GDD Section 2.4 and TSD Section 2.3.
    /// Handles:
    /// 1. Floating Target Box over enemy chassis with Vital Circle (head/chest sweet spot).
    /// 2. Perlin Noise Drift algorithm producing organic erratic reticle movement.
    /// 3. Counter-Drag input vector allowing player to stabilize reticle against drift.
    /// 4. 1.0s Lock-On meter accumulation inside vital circle triggering AUTO-FIRE.
    /// 5. Burst chaining for 2x/3x Attack combos.
    /// 6. Sinergy with [MARK] debuff: enlarged vital circle (1.5x) and 2x Critical Damage (30 DMG).
    /// 7. Procedural cyberpunk HUD overlay, floating combat text, and synthesized audio.
    /// </summary>
    public class AimStabilizerController : MonoBehaviour
    {
        public static AimStabilizerController Instance { get; private set; }

        public enum AimState
        {
            Inactive,
            Stabilizing,
            RecoilCooldown,
            Completed
        }

        [Header("Target Box & Vital Sweet Spot")]
        [SerializeField] private Vector2 targetBoxSize = new Vector2(360f, 360f);
        [SerializeField] private Vector2 vitalCircleOffset = new Vector2(0f, 40f);
        [SerializeField] private float baseVitalRadius = 50f;
        [SerializeField] private float markVitalRadiusMultiplier = 1.5f;

        [Header("Drift & Stabilization Dynamics")]
        [Tooltip("Frequency of the Perlin Noise drift pattern")]
        [SerializeField] private float driftFrequency = 2.0f;
        [Tooltip("Movement speed of the drift in pixels/sec")]
        [SerializeField] private float driftSpeed = 340f;
        [Tooltip("Player mouse/touch counter-drag sensitivity")]
        [SerializeField] private float dragSensitivity = 1.25f;

        [Header("Lock-On & Auto-Fire Mechanics")]
        [Tooltip("Duration required inside vital circle before auto-fire triggers")]
        [SerializeField] private float requiredLockOnDuration = 1.0f;
        [Tooltip("Decay rate of lock-on meter per second when reticle leaves vital circle")]
        [SerializeField] private float lockOnDecayRate = 0.5f;
        [Tooltip("Base shot damage (normal = 15)")]
        [SerializeField] private int baseDamage = 15;
        [Tooltip("Critical damage multiplier when enemy has [MARK] status (2.0x = 30)")]
        [SerializeField] private float criticalMultiplier = 2.0f;

        [Header("Burst Chaining Settings")]
        [Tooltip("Cooldown pause between burst shots")]
        [SerializeField] private float burstIntervalSeconds = 0.6f;
        [Tooltip("Reticle displacement kick magnitude upon firing")]
        [SerializeField] private float recoilKickMagnitude = 55f;

        [Header("Audio Clues (Optional Inspector references)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip gunshotSfx;
        [SerializeField] private AudioClip critGunshotSfx;
        [SerializeField] private AudioClip lockOnToneSfx;

        [Header("UI Overlays (Optional Inspector references)")]
        [SerializeField] private Canvas aimCanvas;
        [SerializeField] private RectTransform targetBoxRoot;
        [SerializeField] private RectTransform vitalCircleRect;
        [SerializeField] private Image vitalCircleImage;
        [SerializeField] private RectTransform reticleRect;
        [SerializeField] private Image reticleImage;
        [SerializeField] private Slider lockOnSlider;
        [SerializeField] private Image lockOnFillImage;
        [SerializeField] private Text lockOnText;
        [SerializeField] private Text burstStatusText;
        [SerializeField] private GameObject markStatusBadge;
        [SerializeField] private Text markStatusText;
        [SerializeField] private Text enemyHpText;
        [SerializeField] private Slider enemyHpSlider;
        [SerializeField] private Image screenFlashImage;
        [SerializeField] private RectTransform floatingTextContainer;

        // Runtime State
        private AimState _currentState = AimState.Inactive;
        private int _totalBursts = 1;
        private int _currentBurstIndex = 0;
        private float _lockOnTimer = 0f;
        private Vector2 _reticlePosition = Vector2.zero;
        private Vector2 _playerDragVelocity = Vector2.zero;
        private float _aimSessionTime = 0f;
                private float _perlinSeedX = 0f;
        private float _perlinSeedY = 0f;
        private int _totalDamageDealtInSession = 0;
        private int _shotsFiredInSession = 0;
        private bool _isDragging = false;
        private Vector2 _lastPointerPosition;

        // Public Properties
        public AimState CurrentState => _currentState;
        public bool IsAiming => _currentState != AimState.Inactive && _currentState != AimState.Completed;
        public int TotalBursts => _totalBursts;
        public int CurrentBurstIndex => _currentBurstIndex;
        public float LockOnTimer => _lockOnTimer;
        public float LockOnProgress => Mathf.Clamp01(_lockOnTimer / Mathf.Max(0.001f, requiredLockOnDuration));
        public Vector2 ReticlePosition => _reticlePosition;
        public Vector2 VitalPosition => vitalCircleOffset;
        public float CurrentVitalRadius => CalculateVitalRadius(baseVitalRadius, IsTargetMarked(), markVitalRadiusMultiplier);
        public Vector2 TargetBoxSize => targetBoxSize;
        public float BaseVitalRadius => baseVitalRadius;
        public int BaseDamage => baseDamage;

        private bool _isSubscribed = false;

        public void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;
            EventBus.Subscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Subscribe<EnemyMarkStatusChangedEvent>(OnEnemyMarkStatusChanged);
            EventBus.Subscribe<EnemyHealthChangedEvent>(OnEnemyHealthChanged);
        }

        public void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;
            EventBus.Unsubscribe<TurnCommittedEvent>(OnTurnCommitted);
            EventBus.Unsubscribe<EnemyMarkStatusChangedEvent>(OnEnemyMarkStatusChanged);
            EventBus.Unsubscribe<EnemyHealthChangedEvent>(OnEnemyHealthChanged);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;

            SubscribeEvents();

            if (Application.isPlaying)
            {
                EnsureAudioSource();
                EnsureUIConstructed();
                SetUIActive(false);
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Shortcut pengetesan langsung di Editor
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("<color=yellow>[AimStabilizer]</color> <b>Debug F5 ditekan:</b> Memicu sesi Aim Stabilizer (1x Attack Normal, 15 DMG)!");
                BeginAimSession(1, debugForceMark: false);
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("<color=yellow>[AimStabilizer]</color> <b>Debug F6 ditekan:</b> Memicu sesi Aim Stabilizer (2x Attack + MARK, Lingkaran Vital Membesar & 30 Crit DMG)!");
                BeginAimSession(2, debugForceMark: true);
            }
#endif

            if (!IsAiming) return;

            // Handle transition interval between slot attacks (handled by coroutine)
            if (_currentState == AimState.RecoilCooldown)
            {
                return;
            }

            if (_currentState != AimState.Stabilizing) return;

            _aimSessionTime += Time.deltaTime;

            // 1. Gather player input
            UpdatePlayerInput();

            // 2. Calculate Drift Velocity (Perlin Noise formula)
            Vector2 driftVel = CalculateDriftVelocity(_aimSessionTime, driftFrequency, driftSpeed, _perlinSeedX, _perlinSeedY);

            // 3. Integrate position & clamp to Target Box bounds
            _reticlePosition = CalculateReticlePosition(_reticlePosition, driftVel, _playerDragVelocity, Time.deltaTime, targetBoxSize, 20f);

            // 4. Check if Reticle is within Vital Circle
            float currentRadius = CurrentVitalRadius;
            bool isInside = IsInsideVitalCircle(_reticlePosition, vitalCircleOffset, currentRadius);

            // 5. Accumulate or Decay Lock-On Meter
            _lockOnTimer = AccumulateLockOn(_lockOnTimer, isInside, Time.deltaTime, requiredLockOnDuration, lockOnDecayRate);

            // 6. Visual & Audio UI Feedback
            UpdateAimVisuals(isInside, currentRadius);

            // 7. Dispatch Reticle Movement Event
            EventBus.Publish(new AimReticleMovedEvent(_reticlePosition, vitalCircleOffset, currentRadius, isInside, LockOnProgress));

            // 8. 1.0s Lock-On Reached -> AUTO-FIRE!
            if (_lockOnTimer >= requiredLockOnDuration)
            {
                AutoFireShot();
            }
        }

        #region Mathematical & Testable Pure Functions

        /// <summary>
        /// Exact Perlin Noise drift formula from TSD Section 2.3.1:
        /// v_drift = Vector2(Perlin(t * f, 0) - 0.5, Perlin(0, t * f) - 0.5) * Speed
        /// </summary>
        public static Vector2 CalculateDriftVelocity(float time, float frequency, float speed, float seedX = 0f, float seedY = 0f)
        {
            float nx = Mathf.PerlinNoise(time * frequency + seedX, 0f) - 0.5f;
            float ny = Mathf.PerlinNoise(0f, time * frequency + seedY) - 0.5f;
            return new Vector2(nx, ny) * speed;
        }

        /// <summary>
        /// Counter-drag input vector from TSD Section 2.3.2.
        /// </summary>
        public static Vector2 CalculatePlayerVelocity(Vector2 dragDelta, float sensitivity, bool invert = false)
        {
            return (invert ? -dragDelta : dragDelta) * sensitivity;
        }

        /// <summary>
        /// Integrates reticle position and clamps within the target box boundary.
        /// </summary>
        public static Vector2 CalculateReticlePosition(Vector2 currentPos, Vector2 driftVel, Vector2 playerVel, float deltaTime, Vector2 boxBounds, float reticleRadius = 0f)
        {
            Vector2 newPos = currentPos + (driftVel + playerVel) * deltaTime;

            float halfW = Mathf.Max(0f, (boxBounds.x * 0.5f) - reticleRadius);
            float halfH = Mathf.Max(0f, (boxBounds.y * 0.5f) - reticleRadius);

            newPos.x = Mathf.Clamp(newPos.x, -halfW, halfW);
            newPos.y = Mathf.Clamp(newPos.y, -halfH, halfH);

            return newPos;
        }

        /// <summary>
        /// Distance check between reticle and vital circle sweet spot.
        /// </summary>
        public static bool IsInsideVitalCircle(Vector2 reticlePos, Vector2 vitalPos, float vitalRadius)
        {
            return Vector2.Distance(reticlePos, vitalPos) <= vitalRadius;
        }

        /// <summary>
        /// Vital circle radius with [MARK] synergy multiplier (1.5x enlargement).
        /// </summary>
        public static float CalculateVitalRadius(float baseRadius, bool isMarked, float markMultiplier = 1.5f)
        {
            return isMarked ? baseRadius * markMultiplier : baseRadius;
        }

        /// <summary>
        /// Damage calculation with [MARK] status (2x Critical Damage).
        /// </summary>
        public static int CalculateDamage(int baseDmg, bool isMarked, float critMultiplier = 2.0f)
        {
            return isMarked ? Mathf.RoundToInt(baseDmg * critMultiplier) : baseDmg;
        }

        /// <summary>
        /// Accumulates lock-on timer when inside vital circle, or decays smoothly when outside.
        /// </summary>
        public static float AccumulateLockOn(float currentTimer, bool isInsideVital, float deltaTime, float maxDuration = 1.0f, float decayRate = 0.6f)
        {
            if (isInsideVital)
            {
                return Mathf.Min(maxDuration, currentTimer + deltaTime);
            }
            else
            {
                return Mathf.Max(0f, currentTimer - (decayRate * deltaTime));
            }
        }

        #endregion

        #region Session & Burst Lifecycle

        /// <summary>
        /// Begins the FPS Aim session with a specified number of attack burst shots.
        /// </summary>
        public void BeginAimSession(int burstCount, bool debugForceMark = false)
        {
            EnsureUIConstructed();

            if (debugForceMark && EnemyHealth.Instance != null)
            {
                EnemyHealth.Instance.ApplyMark("DebugAimSession");
            }

            _totalBursts = Mathf.Max(1, burstCount);
            _currentBurstIndex = 0;
            _totalDamageDealtInSession = 0;
            _shotsFiredInSession = 0;
            _aimSessionTime = 0f;

            Debug.Log($"<color=yellow>[AimStabilizer]</color> <b>Sesi Aim Dimulai!</b> Total Bursts: {_totalBursts}, EnemyMarked: {IsTargetMarked()}");

            SetUIActive(true);
            StartNextBurstShot();

            int enemyHp = EnemyHealth.Instance != null ? EnemyHealth.Instance.CurrentHealth : 70;
            EventBus.Publish(new AimStabilizerStartedEvent(_totalBursts, 1, enemyHp, IsTargetMarked()));
        }

        private void StartNextBurstShot()
        {
            _currentState = AimState.Stabilizing;
            _lockOnTimer = 0f;

            // Generate unpredictable Perlin seeds for each burst
            _perlinSeedX = UnityEngine.Random.Range(20f, 9999f);
            _perlinSeedY = UnityEngine.Random.Range(20f, 9999f);

            // Offset reticle away from sweet spot at start of shot
            float startAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float startDist = UnityEngine.Random.Range(80f, 130f);
            _reticlePosition = vitalCircleOffset + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * startDist;

            // Clamp initial position
            _reticlePosition = CalculateReticlePosition(_reticlePosition, Vector2.zero, Vector2.zero, 0f, targetBoxSize, 20f);

            UpdateBurstStatusUI();
            RefreshMarkBadge();
        }

        /// <summary>
        /// Automatically fires the revolver when Lock-On timer reaches 100% (1.0s).
        /// </summary>
        private void AutoFireShot()
        {
            _shotsFiredInSession++;
            int burstNumber = _currentBurstIndex + 1;

            bool isMarked = IsTargetMarked();
            int damage = CalculateDamage(baseDamage, isMarked, criticalMultiplier);
            _totalDamageDealtInSession += damage;

            Debug.Log($"<color=cyan>[AimStabilizer]</color> <b>AUTO-FIRE MELETUS!</b> Burst {burstNumber}/{_totalBursts} -> Damage: {damage} (Critical: {isMarked})");

            // Deal damage to Enemy
            if (EnemyHealth.Instance != null)
            {
                EnemyHealth.Instance.TakeDamage(damage, isMarked, $"AimAutoFire_Burst{burstNumber}");
            }

            // Audio & Screen Flash
            PlayGunshotAudio(isMarked);
            TriggerMuzzleFlash(isMarked);
            ShowFloatingDamage(damage, isMarked);

            // Recoil kick displacement on reticle
            Vector2 recoilKick = UnityEngine.Random.insideUnitCircle.normalized * recoilKickMagnitude;
            _reticlePosition = CalculateReticlePosition(_reticlePosition, recoilKick, Vector2.zero, 1f, targetBoxSize, 20f);

            // Publish shot fired event
            int enemyHp = EnemyHealth.Instance != null ? EnemyHealth.Instance.CurrentHealth : 0;
            int maxHp = EnemyHealth.Instance != null ? EnemyHealth.Instance.MaxHealth : 70;
            string bar = EnemyHealth.GetHpBarString(enemyHp, maxHp, 10);
            Debug.Log($"<color=cyan><b>[AimStabilizer]</b></color> <b>Tembakan Slot {burstNumber}/{_totalBursts} Sukses!</b> Damage: <b>{damage}</b> {(isMarked ? "<color=#FF0055>(CRIT 2X)</color>" : "")} | Sisa HP Musuh: <color=#00FFE0><b>{enemyHp}/{maxHp} HP</b></color> {bar}");
            EventBus.Publish(new AimShotFiredEvent(damage, isMarked, burstNumber, _totalBursts, enemyHp));

            // Check if enemy defeated or burst chain continues
            bool enemyDefeated = EnemyHealth.Instance != null && !EnemyHealth.Instance.IsAlive;

            _currentBurstIndex++;

            if (_currentBurstIndex < _totalBursts && !enemyDefeated)
            {
                // Multi-Attack Slot: Transition sequentially to the next attack slot
                _currentState = AimState.RecoilCooldown;
                _lockOnTimer = 0f;
                StartCoroutine(NextSlotTransitionRoutine());
            }
            else
            {
                // All bursts complete or enemy defeated!
                CompleteAimSession(enemyDefeated);
            }
        }

        private IEnumerator NextSlotTransitionRoutine()
        {
            // 1. Brief pause for recoil kick & muzzle flash
            yield return new WaitForSeconds(0.25f);

            // 2. Close aim overlay for the transition
            SetUIActive(false);

            // 3. 0.6 second transition delay with active reel highlight in HUD
            yield return new WaitForSeconds(burstIntervalSeconds);

            // Check if enemy defeated during pause
            if (EnemyHealth.Instance != null && !EnemyHealth.Instance.IsAlive)
            {
                CompleteAimSession(enemyDefeated: true);
                yield break;
            }

            // 4. Reopen aim overlay for the next slot
            SetUIActive(true);
            StartNextBurstShot();

            int enemyHp = EnemyHealth.Instance != null ? EnemyHealth.Instance.CurrentHealth : 0;
            EventBus.Publish(new AimStabilizerStartedEvent(_totalBursts, _currentBurstIndex + 1, enemyHp, IsTargetMarked()));
        }

        /// <summary>
        /// Completes the entire aim session.
        /// Clears consumed mark debuff, closes overlay, and publishes completion event.
        /// </summary>
        public void CompleteAimSession(bool enemyDefeated)
        {
            _currentState = AimState.Completed;

            // If enemy was marked, consume debuff at the conclusion of attack sequence
            if (EnemyHealth.Instance != null && EnemyHealth.Instance.IsMarked)
            {
                EnemyHealth.Instance.ClearMark("AttackSequenceCompleted");
            }

            Debug.Log($"<color=green>[AimStabilizer]</color> <b>Sesi Aim Selesai!</b> Total Shots: {_shotsFiredInSession}, Total Damage: {_totalDamageDealtInSession}, Defeated: {enemyDefeated}");
            EventBus.Publish(new AimSequenceCompletedEvent(_shotsFiredInSession, _totalDamageDealtInSession, enemyDefeated));

            StartCoroutine(CloseSessionRoutine());
        }

        private IEnumerator CloseSessionRoutine()
        {
            yield return new WaitForSeconds(0.45f);
            SetUIActive(false);
            _currentState = AimState.Inactive;
        }

        private bool IsTargetMarked()
        {
            return EnemyHealth.Instance != null && EnemyHealth.Instance.IsMarked;
        }

        #endregion

        #region Input Processing

        private void UpdatePlayerInput()
        {
            Vector2 framePixelDelta = Vector2.zero;
            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            // 1. Mouse Drag / Touch Drag (klik dan geser mouse)
            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _lastPointerPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector2 currentPos = Input.mousePosition;
                framePixelDelta += (currentPos - _lastPointerPosition);
                _lastPointerPosition = currentPos;
            }

            // 2. Free Mouse Movement (menggerakkan mouse secara langsung tanpa perlu klik terus-menerus)
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
            {
                framePixelDelta += new Vector2(mouseX, mouseY) * 14f;
            }

            // 3. Keyboard Steering (WASD / Tombol Panah untuk kontrol tambahan yang nyaman)
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                framePixelDelta += new Vector2(h, v) * (280f * dt);
            }

            // Konversi frame delta ke kecepatan (px/sec) agar saat diintegrasikan dengan deltaTime
            // menghasilkan pergerakan 1:1 yang presisi dan responsif
            // Konversi frame delta ke kecepatan (px/sec), dibagi 2 (0.5x) sesuai instruksi agar kontrol lebih berbobot & menantang
            Vector2 inputVelocity = (framePixelDelta / dt) * 0.5f;

            _playerDragVelocity = CalculatePlayerVelocity(inputVelocity, dragSensitivity, invert: false);
        }

        #endregion

        #region UI & Presentation Updates

        private void UpdateAimVisuals(bool isInsideVital, float currentRadius)
        {
            // Update Reticle UI position
            if (reticleRect != null)
            {
                reticleRect.anchoredPosition = _reticlePosition;
            }

            // Update Vital Circle UI size and position
            if (vitalCircleRect != null)
            {
                vitalCircleRect.anchoredPosition = vitalCircleOffset;
                vitalCircleRect.sizeDelta = new Vector2(currentRadius * 2f, currentRadius * 2f);
            }

            // Pulsing scale on vital circle
            if (vitalCircleImage != null)
            {
                float pulse = 1f + (Mathf.Sin(Time.time * 6f) * 0.05f);
                vitalCircleRect.localScale = new Vector3(pulse, pulse, 1f);

                if (IsTargetMarked())
                {
                    vitalCircleImage.color = new Color(1f, 0.2f, 0.7f, 0.85f); // Neon Magenta when MARKED
                }
                else if (isInsideVital)
                {
                    vitalCircleImage.color = new Color(0.2f, 1f, 0.6f, 0.85f); // Neon Green when inside
                }
                else
                {
                    vitalCircleImage.color = new Color(0f, 0.85f, 1f, 0.75f); // Neon Cyan default
                }
            }

            // Reticle color feedback
            if (reticleImage != null)
            {
                if (isInsideVital)
                {
                    reticleImage.color = Color.Lerp(new Color(1f, 0.9f, 0.1f, 1f), new Color(1f, 0.15f, 0.35f, 1f), LockOnProgress);
                }
                else
                {
                    reticleImage.color = new Color(0f, 1f, 0.9f, 0.95f);
                }
            }

            // Lock-On Progress Bar
            float progress = LockOnProgress;
            if (lockOnSlider != null)
            {
                lockOnSlider.value = progress;
            }

            if (lockOnFillImage != null)
            {
                lockOnFillImage.color = Color.Lerp(new Color(0f, 0.8f, 1f), new Color(1f, 0.2f, 0.3f), progress);
            }

            if (lockOnText != null)
            {
                if (progress >= 0.999f)
                {
                    lockOnText.text = "<b><color=#FF0055>100% AUTO-FIRE! TEMBAK!</color></b>";
                }
                else if (isInsideVital)
                {
                    lockOnText.text = $"MENGUNCI SASARAN: {Mathf.RoundToInt(progress * 100f)}% (TAHAN!)";
                }
                else
                {
                    if (_totalBursts > 1)
                    {
                        lockOnText.text = $"SLOT {_currentBurstIndex + 1}: ARAHKAN KE LINGKARAN VITAL // AUTO-FIRE 100%";
                    }
                    else
                    {
                        lockOnText.text = "ARAHKAN KE LINGKARAN VITAL // AUTO-FIRE 100%";
                    }
                }
            }
        }

        private void UpdateBurstStatusUI()
        {
            if (burstStatusText != null)
            {
                if (_totalBursts > 1)
                {
                    burstStatusText.text = $"<color=#FFE600>[ATTACK SLOT {_currentBurstIndex + 1}/{_totalBursts}]</color>";
                }
                else
                {
                    burstStatusText.text = "<color=#00FFE0>[SINGLE SHOT]</color>";
                }
            }
        }

        private void RefreshMarkBadge()
        {
            bool marked = IsTargetMarked();
            if (markStatusBadge != null)
            {
                markStatusBadge.SetActive(marked);
            }

            if (markStatusText != null)
            {
                markStatusText.text = marked ? "TARGET MARKED // 2X CRIT" : "";
            }
        }

        private void TriggerMuzzleFlash(bool isCritical)
        {
            if (screenFlashImage == null) return;

            StopCoroutine(nameof(MuzzleFlashRoutine));
            StartCoroutine(MuzzleFlashRoutine(isCritical));
        }

        private IEnumerator MuzzleFlashRoutine(bool isCritical)
        {
            screenFlashImage.gameObject.SetActive(true);
            screenFlashImage.color = isCritical ? new Color(1f, 0.2f, 0.8f, 0.55f) : new Color(1f, 1f, 1f, 0.45f);

            float elapsed = 0f;
            float duration = 0.12f;
            Color startColor = screenFlashImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                screenFlashImage.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), t);
                yield return null;
            }

            screenFlashImage.gameObject.SetActive(false);
        }

        private void ShowFloatingDamage(int damage, bool isCritical)
        {
            if (floatingTextContainer == null) return;

            GameObject textGo = new GameObject("FloatingDamageText");
            textGo.layer = 5;
            var rect = textGo.AddComponent<RectTransform>();
            rect.SetParent(floatingTextContainer, false);
            rect.anchoredPosition = _reticlePosition + new Vector2(UnityEngine.Random.Range(-20f, 20f), 20f);
            rect.localScale = Vector3.one;

            var text = textGo.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = isCritical ? 34 : 26;
            text.fontStyle = FontStyle.Bold;

            if (isCritical)
            {
                text.text = $"CRIT! -{damage}";
                text.color = new Color(1f, 0.15f, 0.7f, 1f);
            }
            else
            {
                text.text = $"-{damage}";
                text.color = new Color(0f, 1f, 1f, 1f);
            }

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            StartCoroutine(FloatingTextRoutine(rect, text));
        }

        private IEnumerator FloatingTextRoutine(RectTransform rect, Text text)
        {
            float duration = 0.85f;
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, 65f);
            Color startColor = text.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                text.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), t);
                yield return null;
            }

            Destroy(rect.gameObject);
        }

        private void SetUIActive(bool active)
        {
            if (targetBoxRoot != null)
            {
                targetBoxRoot.gameObject.SetActive(active);
                if (active)
                {
                    targetBoxRoot.SetAsLastSibling();
                }
            }
        }

        #endregion

        #region Audio Feedback

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
            }
        }

        private void PlayGunshotAudio(bool isCritical)
        {
            if (audioSource == null) return;

            AudioClip clip = isCritical ? critGunshotSfx : gunshotSfx;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                // Play runtime generated procedural tone if no audio clip assigned
                PlayProceduralGunshotSound(isCritical);
            }
        }

        private void PlayProceduralGunshotSound(bool isCritical)
        {
            // Creates a brief snappy blast audio clip
            int sampleRate = 44100;
            float duration = 0.18f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 28f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * decay;
                float lowTone = Mathf.Sin(2f * Mathf.PI * (isCritical ? 90f : 140f) * t) * decay;
                samples[i] = Mathf.Clamp(noise * 0.7f + lowTone * 0.5f, -1f, 1f);
            }

            AudioClip synthClip = AudioClip.Create("SynthGunshot", totalSamples, 1, sampleRate, false);
            synthClip.SetData(samples, 0);
            audioSource.PlayOneShot(synthClip);
        }

        #endregion

        #region Event Listeners

        private void OnTurnCommitted(TurnCommittedEvent evt)
        {
            if (evt.ExecutedSymbols == null) return;

            // 1. Process MARK symbol
            bool hasMark = evt.ExecutedSymbols.Contains(SlotSymbol.MARK);
            if (hasMark && EnemyHealth.Instance != null)
            {
                EnemyHealth.Instance.ApplyMark("CommittedSlotSymbol");
            }

            // 2. Count ATTACK symbols
            int attackCount = 0;
            for (int i = 0; i < evt.ExecutedSymbols.Count; i++)
            {
                if (evt.ExecutedSymbols[i] == SlotSymbol.ATTACK)
                {
                    attackCount++;
                }
            }

            Debug.Log($"<color=orange>[AimStabilizer]</color> <b>OnTurnCommitted Diterima!</b> Simbol: [{string.Join(", ", evt.ExecutedSymbols)}], Jumlah ATTACK: {attackCount}");

            // 3. Trigger Aim Stabilizer if player committed 1x, 2x, or 3x Attack!
            if (attackCount > 0)
            {
                Debug.Log($"<color=lime>[AimStabilizer]</color> <b>MEMULAI SESI AIM STABILIZER!</b> ({attackCount}x BURST SHOTS)");
                BeginAimSession(attackCount);
            }
            else
            {
                Debug.Log($"<color=cyan>[AimStabilizer]</color> Giliran ini tidak memiliki simbol [ATTACK]. Aim Stabilizer standby.");
            }
        }

        private void OnEnemyMarkStatusChanged(EnemyMarkStatusChangedEvent evt)
        {
            RefreshMarkBadge();
        }

        private void OnEnemyHealthChanged(EnemyHealthChangedEvent evt)
        {
            if (enemyHpText != null)
            {
                enemyHpText.text = $"ENEMY HP: {evt.CurrentHp}/{evt.MaxHp}";
            }
            if (enemyHpSlider != null)
            {
                enemyHpSlider.value = evt.MaxHp > 0 ? (float)evt.CurrentHp / evt.MaxHp : 0f;
            }
        }

        #endregion

        #region Procedural Cyberpunk UI Construction

        private static Font GetDefaultFont()
        {
            try
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f != null) return f;
            }
            catch {}

            try
            {
                Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
                if (allFonts != null && allFonts.Length > 0)
                {
                    for (int i = 0; i < allFonts.Length; i++)
                    {
                        if (allFonts[i] != null) return allFonts[i];
                    }
                }
            }
            catch {}

            return null;
        }

        public void EnsureUIConstructed()
        {
            try
            {
                BuildProceduralUI();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AimStabilizerController] Procedural UI fallback notice: {ex.Message}");
            }
        }

        private void BuildProceduralUI()
        {
            // Find or create Canvas if missing
            if (aimCanvas == null)
            {
                aimCanvas = FindObjectOfType<Canvas>();
                if (aimCanvas == null)
                {
                    GameObject canvasGo = new GameObject("[AimCanvas]");
                    aimCanvas = canvasGo.AddComponent<Canvas>();
                    aimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGo.AddComponent<CanvasScaler>();
                    canvasGo.AddComponent<GraphicRaycaster>();
                }
            }

            if (targetBoxRoot != null)
            {
                // Ensure target box is parented to current canvas
                if (targetBoxRoot.parent != aimCanvas.transform)
                {
                    targetBoxRoot.SetParent(aimCanvas.transform, false);
                }
                targetBoxRoot.localScale = Vector3.one;
                return;
            }

            // Target Box Root (Floating Frame over Enemy)
            GameObject boxGo = new GameObject("TargetBox_Root");
            boxGo.layer = 5; // Layer "UI"
            targetBoxRoot = boxGo.AddComponent<RectTransform>();
            targetBoxRoot.SetParent(aimCanvas.transform, false);
            targetBoxRoot.anchorMin = new Vector2(0.5f, 0.5f);
            targetBoxRoot.anchorMax = new Vector2(0.5f, 0.5f);
            targetBoxRoot.pivot = new Vector2(0.5f, 0.5f);
            targetBoxRoot.anchoredPosition = new Vector2(0f, 60f); // Centered and slightly elevated
            targetBoxRoot.sizeDelta = targetBoxSize;
            targetBoxRoot.localScale = Vector3.one;
            targetBoxRoot.SetAsLastSibling();

            // Semi-transparent Cyberpunk Background
            var bgImage = boxGo.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.08f, 0.14f, 0.45f);

            var outline = boxGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.9f, 1f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            Font font = GetDefaultFont();

            // Header: Target Info
            GameObject headerGo = new GameObject("Header_TargetInfo");
            headerGo.layer = 5;
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.SetParent(targetBoxRoot, false);
            headerRect.anchorMin = new Vector2(0.05f, 0.91f);
            headerRect.anchorMax = new Vector2(0.95f, 0.99f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            headerRect.localScale = Vector3.one;

            enemyHpText = headerGo.AddComponent<Text>();
            if (font != null) enemyHpText.font = font;
            enemyHpText.text = "ENEMY HP: 70/70";
            enemyHpText.alignment = TextAnchor.MiddleLeft;
            enemyHpText.fontSize = 13;
            enemyHpText.fontStyle = FontStyle.Bold;
            enemyHpText.color = new Color(0.2f, 1f, 0.7f, 1f);

            // Burst Counter Text (Top Right)
            GameObject burstGo = new GameObject("BurstStatus_Text");
            burstGo.layer = 5;
            var burstRect = burstGo.AddComponent<RectTransform>();
            burstRect.SetParent(targetBoxRoot, false);
            burstRect.anchorMin = new Vector2(0.5f, 0.91f);
            burstRect.anchorMax = new Vector2(0.95f, 0.99f);
            burstRect.offsetMin = Vector2.zero;
            burstRect.offsetMax = Vector2.zero;
            burstRect.localScale = Vector3.one;

            burstStatusText = burstGo.AddComponent<Text>();
            if (font != null) burstStatusText.font = font;
            burstStatusText.text = "[SINGLE SHOT]";
            burstStatusText.alignment = TextAnchor.MiddleRight;
            burstStatusText.fontSize = 13;
            burstStatusText.fontStyle = FontStyle.Bold;
            burstStatusText.color = new Color(1f, 0.9f, 0.2f, 1f);

            // MARK Status Badge (Under Header)
            markStatusBadge = new GameObject("Mark_Badge");
            markStatusBadge.layer = 5;
            var markRect = markStatusBadge.AddComponent<RectTransform>();
            markRect.SetParent(targetBoxRoot, false);
            markRect.anchorMin = new Vector2(0.1f, 0.82f);
            markRect.anchorMax = new Vector2(0.9f, 0.89f);
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;
            markRect.localScale = Vector3.one;

            var markBg = markStatusBadge.AddComponent<Image>();
            markBg.color = new Color(0.8f, 0.1f, 0.5f, 0.75f);

            var markTextGo = new GameObject("Mark_Text");
            markTextGo.layer = 5;
            markStatusText = markTextGo.AddComponent<Text>();
            markStatusText.transform.SetParent(markRect, false);
            markStatusText.rectTransform.anchorMin = Vector2.zero;
            markStatusText.rectTransform.anchorMax = Vector2.one;
            markStatusText.rectTransform.localScale = Vector3.one;
            if (font != null) markStatusText.font = font;
            markStatusText.text = "TARGET MARKED // 2X CRIT";
            markStatusText.alignment = TextAnchor.MiddleCenter;
            markStatusText.fontSize = 12;
            markStatusText.fontStyle = FontStyle.Bold;
            markStatusText.color = Color.white;
            markStatusBadge.SetActive(false);

            // Vital Circle (Sweet Spot)
            GameObject vitalGo = new GameObject("VitalCircle");
            vitalGo.layer = 5;
            vitalCircleRect = vitalGo.AddComponent<RectTransform>();
            vitalCircleRect.SetParent(targetBoxRoot, false);
            vitalCircleRect.anchorMin = new Vector2(0.5f, 0.5f);
            vitalCircleRect.anchorMax = new Vector2(0.5f, 0.5f);
            vitalCircleRect.pivot = new Vector2(0.5f, 0.5f);
            vitalCircleRect.anchoredPosition = vitalCircleOffset;
            vitalCircleRect.sizeDelta = new Vector2(baseVitalRadius * 2f, baseVitalRadius * 2f);
            vitalCircleRect.localScale = Vector3.one;

            vitalCircleImage = vitalGo.AddComponent<Image>();
            vitalCircleImage.color = new Color(0f, 0.85f, 1f, 0.5f);

            var vitalOutline = vitalGo.AddComponent<Outline>();
            vitalOutline.effectColor = new Color(0.2f, 1f, 0.8f, 0.9f);
            vitalOutline.effectDistance = new Vector2(3f, -3f);

            // Vital Sweet Spot Center Icon
            GameObject sweetSpotGo = new GameObject("SweetSpot_Icon");
            sweetSpotGo.layer = 5;
            var sweetRect = sweetSpotGo.AddComponent<RectTransform>();
            sweetRect.SetParent(vitalCircleRect, false);
            sweetRect.anchorMin = new Vector2(0.35f, 0.35f);
            sweetRect.anchorMax = new Vector2(0.65f, 0.65f);
            sweetRect.offsetMin = Vector2.zero;
            sweetRect.offsetMax = Vector2.zero;
            sweetRect.localScale = Vector3.one;
            var sweetImage = sweetSpotGo.AddComponent<Image>();
            sweetImage.color = new Color(1f, 1f, 1f, 0.85f);

            // Reticle (Crosshair)
            GameObject reticleGo = new GameObject("Reticle");
            reticleGo.layer = 5;
            reticleRect = reticleGo.AddComponent<RectTransform>();
            reticleRect.SetParent(targetBoxRoot, false);
            reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRect.pivot = new Vector2(0.5f, 0.5f);
            reticleRect.anchoredPosition = Vector2.zero;
            reticleRect.sizeDelta = new Vector2(36f, 36f);
            reticleRect.localScale = Vector3.one;

            reticleImage = reticleGo.AddComponent<Image>();
            reticleImage.color = new Color(0f, 1f, 0.9f, 0.95f);

            var reticleOutline = reticleGo.AddComponent<Outline>();
            reticleOutline.effectColor = Color.black;
            reticleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Lock-On Slider Meter (Bottom of Target Box)
            GameObject sliderGo = new GameObject("LockOn_Slider");
            sliderGo.layer = 5;
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            sliderRect.SetParent(targetBoxRoot, false);
            sliderRect.anchorMin = new Vector2(0.08f, 0.05f);
            sliderRect.anchorMax = new Vector2(0.92f, 0.12f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            sliderRect.localScale = Vector3.one;

            lockOnSlider = sliderGo.AddComponent<Slider>();
            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = new Color(0.12f, 0.15f, 0.22f, 0.9f);

            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.layer = 5;
            var fillArea = fillAreaGo.AddComponent<RectTransform>();
            fillArea.SetParent(sliderRect, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.sizeDelta = Vector2.zero;
            fillArea.localScale = Vector3.one;

            var fillGo = new GameObject("Fill");
            fillGo.layer = 5;
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.SetParent(fillArea, false);
            fillRect.sizeDelta = Vector2.zero;
            fillRect.localScale = Vector3.one;
            lockOnFillImage = fillGo.AddComponent<Image>();
            lockOnFillImage.color = new Color(0f, 0.9f, 1f, 1f);

            lockOnSlider.fillRect = fillRect;
            lockOnSlider.minValue = 0f;
            lockOnSlider.maxValue = 1f;
            lockOnSlider.value = 0f;

            // Lock-On Status Text
            GameObject lockTextGo = new GameObject("LockOn_Text");
            lockTextGo.layer = 5;
            var lockTextRect = lockTextGo.AddComponent<RectTransform>();
            lockTextRect.SetParent(targetBoxRoot, false);
            lockTextRect.anchorMin = new Vector2(0.08f, 0.13f);
            lockTextRect.anchorMax = new Vector2(0.92f, 0.19f);
            lockTextRect.offsetMin = Vector2.zero;
            lockTextRect.offsetMax = Vector2.zero;
            lockTextRect.localScale = Vector3.one;

            lockOnText = lockTextGo.AddComponent<Text>();
            if (font != null) lockOnText.font = font;
            if (_totalBursts > 1)
                    {
                        lockOnText.text = $"SLOT {_currentBurstIndex + 1}: ARAHKAN KE LINGKARAN VITAL // AUTO-FIRE 100%";
                    }
                    else
                    {
                        lockOnText.text = "ARAHKAN KE LINGKARAN VITAL // AUTO-FIRE 100%";
                    }
            lockOnText.alignment = TextAnchor.MiddleCenter;
            lockOnText.fontSize = 12;
            lockOnText.fontStyle = FontStyle.Bold;
            lockOnText.color = Color.white;

            // Floating Numbers Container
            GameObject floatGo = new GameObject("FloatingTextContainer");
            floatGo.layer = 5;
            floatingTextContainer = floatGo.AddComponent<RectTransform>();
            floatingTextContainer.SetParent(targetBoxRoot, false);
            floatingTextContainer.anchorMin = Vector2.zero;
            floatingTextContainer.anchorMax = Vector2.one;
            floatingTextContainer.offsetMin = Vector2.zero;
            floatingTextContainer.offsetMax = Vector2.zero;
            floatingTextContainer.localScale = Vector3.one;

            // Screen Muzzle Flash Overlay (Full Canvas)
            GameObject flashGo = new GameObject("ScreenMuzzleFlash");
            flashGo.layer = 5;
            var flashRect = flashGo.AddComponent<RectTransform>();
            flashRect.SetParent(aimCanvas.transform, false);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            flashRect.localScale = Vector3.one;

            screenFlashImage = flashGo.AddComponent<Image>();
            screenFlashImage.color = new Color(1f, 1f, 1f, 0f);
            screenFlashImage.raycastTarget = false;
            flashGo.SetActive(false);
        }

        #endregion
    }
}
