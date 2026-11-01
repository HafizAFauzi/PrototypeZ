using System;
using System.Collections;
using UnityEngine;
using LuckyDuelist.Events;

namespace LuckyDuelist.Combat
{
    /// <summary>
    /// Left-Hand Combat Knife Controller for First-Person Bullet Parrying.
    /// Implements GDD Section 1.1, 2.5 and TSD Section 1, 2.4.
    /// Features:
    /// 1. First-person left-hand knife positioning (idle stance on bottom-left).
    /// 2. [E] Tangkis: Swings knife to screen center with a sharp tactical deflecting slash.
    /// 3. Precision Parry Window (~0.2s - 0.3s active duration).
    /// 4. Sfx & VFX feedback: 'CLANG!' + neon sparks on success, staggering recoil on failure.
    /// 5. Knife cosmetic skin support (Default, Cyber Tanto, Viper Dagger).
    /// </summary>
    public class KnifeParryController : MonoBehaviour
    {
        public static KnifeParryController Instance { get; private set; }

        [Header("Mode Configuration")]
        [Tooltip("When true, knife uses 2D Orthographic coordinates matching RightHand_Weapon")]
        [SerializeField] private bool is2DMode = true;
        [SerializeField] private SpriteRenderer knifeSpriteRenderer;

        [Header("Transform & Visual Rig")]
        [Tooltip("Root transform of the knife in the player's left hand.")]
        [SerializeField] private Transform knifeTransform;
        [SerializeField] private MeshRenderer knifeRenderer;
        [SerializeField] private TrailRenderer slashTrailRenderer;

        [Header("Pose Coordinates (Local to Camera Rig)")]
        [SerializeField] private Vector3 idleLocalPosition = new Vector3(-5.2f, -2.8f, 10f);
        [SerializeField] private Vector3 idleLocalEuler = new Vector3(0f, 0f, 25f);
        [SerializeField] private Vector3 parryCenterLocalPosition = new Vector3(-0.6f, -0.4f, 10f);
        [SerializeField] private Vector3 parryCenterLocalEuler = new Vector3(0f, 0f, -45f);

        [Header("Parry Timing (TSD Section 2.4)")]
        [Tooltip("Total duration of the swing & recovery in seconds (default: 0.40s)")]
        [SerializeField] private float parrySwingDuration = 0.40f;
        [Tooltip("Delay from swing start until active parry window opens (default: 0.06s)")]
        [SerializeField] private float parryWindowStart = 0.06f;
        [Tooltip("Time from swing start when active parry window closes (default: 0.28s, window ~0.22s)")]
        [SerializeField] private float parryWindowEnd = 0.28f;
        [Tooltip("Cooldown pause after a swing before next parry can be started")]
        [SerializeField] private float parryCooldown = 0.15f;

        [Header("Audio & Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip parryWhooshSfx;
        [SerializeField] private AudioClip parryClangSfx;
        [SerializeField] private AudioClip parryFailSfx;

        // Cosmetic Skin Data
        public enum KnifeSkin
        {
            DefaultCombatKnife,
            CyberTanto,
            ViperDagger
        }

        [Header("Cosmetic Skin")]
        [SerializeField] private KnifeSkin currentSkin = KnifeSkin.DefaultCombatKnife;

        // Runtime State
        private bool _isParrying = false;
        private float _parryTimer = 0f;
        private float _cooldownTimer = 0f;
        private bool _hasDeflectedInCurrentSwing = false;

        // Public Properties
        public bool IsParrying => _isParrying;
        public float ParryProgress => _isParrying && parrySwingDuration > 0f ? Mathf.Clamp01(_parryTimer / parrySwingDuration) : 0f;
        public bool IsInParryWindow => _isParrying && IsInActiveParryWindow(_parryTimer, parryWindowStart, parryWindowEnd);
        public float ParryWindowDuration => Mathf.Max(0f, parryWindowEnd - parryWindowStart);
        public KnifeSkin CurrentSkin => currentSkin;
        public Transform KnifeRoot => knifeTransform;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this);
                else DestroyImmediate(this);
                return;
            }
            Instance = this;

            EnsureKnifeRigConstructed();
        }

        private void Start()
        {
            EnsureAudioSource();
            ApplySkin(currentSkin);
            SetKnifePose(idleLocalPosition, Quaternion.Euler(idleLocalEuler));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
            }

            // Keyboard shortcut [E] for Parry
            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerParry();
            }

            if (_isParrying)
            {
                _parryTimer += dt;
                float progress = ParryProgress;

                // Animate knife transform to center and return to idle
                Vector3 currentPos = CalculateKnifePosition(progress, idleLocalPosition, parryCenterLocalPosition);
                Quaternion currentRot = CalculateKnifeRotation(progress, Quaternion.Euler(idleLocalEuler), Quaternion.Euler(parryCenterLocalEuler));
                SetKnifePose(currentPos, currentRot);

                if (_parryTimer >= parrySwingDuration)
                {
                    _isParrying = false;
                    _parryTimer = 0f;
                    _cooldownTimer = parryCooldown;
                    _hasDeflectedInCurrentSwing = false;

                    if (slashTrailRenderer != null)
                    {
                        slashTrailRenderer.emitting = false;
                    }

                    // Reset to clean idle
                    SetKnifePose(idleLocalPosition, Quaternion.Euler(idleLocalEuler));
                }
            }
        }

        #region Public Actions

        /// <summary>
        /// Initiates a knife parry swing into screen center.
        /// Active precision window (~0.2s - 0.3s) during which incoming parryable bullets are deflected.
        /// </summary>
        public bool TriggerParry()
        {
            if (_isParrying || _cooldownTimer > 0f)
            {
                return false;
            }

            _isParrying = true;
            _parryTimer = 0f;
            _hasDeflectedInCurrentSwing = false;

            if (slashTrailRenderer != null)
            {
                slashTrailRenderer.Clear();
                slashTrailRenderer.emitting = true;
            }

            PlayWhooshSfx();
            Debug.Log($"<color=yellow>[KnifeParry]</color> Parry swing triggered [E]! Active window: {parryWindowStart:F2}s - {parryWindowEnd:F2}s");
            EventBus.Publish(new PlayerParryTriggeredEvent(ParryWindowDuration));

            return true;
        }

        /// <summary>
        /// Called by DefenseQTEController when a successful parry occurs against a yellow bullet.
        /// Triggers high-impact metallic CLANG audio, neon sparks, and screen freeze pulse.
        /// </summary>
        public void OnParrySuccess(bool hasCounterBonus)
        {
            _hasDeflectedInCurrentSwing = true;
            PlayClangSfx(hasCounterBonus);

            // Trigger screen spark flash
            TriggerSparkEffect(hasCounterBonus);

            if (FPSCameraController.Instance != null)
            {
                FPSCameraController.Instance.TriggerShake(0.12f, 0.18f);
            }
        }

        /// <summary>
        /// Called when player tried to parry an unparryable red bullet.
        /// Knife staggers back, dull failure sound plays.
        /// </summary>
        public void OnParryFailedRed()
        {
            PlayFailSfx();

            if (FPSCameraController.Instance != null)
            {
                FPSCameraController.Instance.TriggerShake(0.20f, 0.25f);
            }
        }

        /// <summary>
        /// Sets cosmetic skin for the knife.
        /// </summary>
        public void ApplySkin(KnifeSkin skin)
        {
            currentSkin = skin;
            Color bladeColor = Color.white;
            Color emissiveColor = Color.cyan;

            switch (skin)
            {
                case KnifeSkin.DefaultCombatKnife:
                    bladeColor = new Color(0.75f, 0.75f, 0.75f);
                    emissiveColor = new Color(0.1f, 0.8f, 1.0f);
                    break;
                case KnifeSkin.CyberTanto:
                    bladeColor = new Color(0.2f, 0.25f, 0.3f);
                    emissiveColor = new Color(0.0f, 1.0f, 0.9f); // Bright Cyan
                    break;
                case KnifeSkin.ViperDagger:
                    bladeColor = new Color(0.12f, 0.12f, 0.12f);
                    emissiveColor = new Color(0.15f, 1.0f, 0.25f); // Poison Neon Green
                    break;
            }

            if (knifeRenderer != null && knifeRenderer.material != null)
            {
                knifeRenderer.material.color = bladeColor;
                if (knifeRenderer.material.HasProperty("_EmissionColor"))
                {
                    knifeRenderer.material.SetColor("_EmissionColor", emissiveColor);
                }
            }

            if (knifeSpriteRenderer != null)
            {
                knifeSpriteRenderer.color = bladeColor;
            }

            if (slashTrailRenderer != null)
            {
                slashTrailRenderer.startColor = emissiveColor;
                slashTrailRenderer.endColor = new Color(emissiveColor.r, emissiveColor.g, emissiveColor.b, 0f);
            }
        }

        #endregion

        #region Procedural Rig Construction

        private void EnsureKnifeRigConstructed()
        {
            if (knifeTransform != null) return;

            // Find camera to parent knife under First-Person camera
            Transform parentCam = null;
            if (FPSCameraController.Instance != null && FPSCameraController.Instance.TargetCamera != null)
            {
                parentCam = FPSCameraController.Instance.TargetCamera;
            }
            else if (Camera.main != null)
            {
                parentCam = Camera.main.transform;
            }

            GameObject knifeRoot = new GameObject("[ParryKnifeRig]");
            if (parentCam != null)
            {
                knifeRoot.transform.SetParent(parentCam, false);
            }
            knifeTransform = knifeRoot.transform;

            // Procedural Combat Knife Mesh (Handle + Guard + Blade)
            // 1. Blade
            GameObject bladeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeGo.name = "Blade";
            bladeGo.transform.SetParent(knifeTransform, false);
            bladeGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            bladeGo.transform.localScale = new Vector3(0.015f, 0.24f, 0.045f);
            var bladeCol = bladeGo.GetComponent<Collider>();
            if (bladeCol != null) Destroy(bladeCol);

            // 2. Handle
            GameObject handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handleGo.name = "Handle";
            handleGo.transform.SetParent(knifeTransform, false);
            handleGo.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            handleGo.transform.localScale = new Vector3(0.022f, 0.08f, 0.022f);
            var handleCol = handleGo.GetComponent<Collider>();
            if (handleCol != null) Destroy(handleCol);

            // 3. Guard
            GameObject guardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guardGo.name = "Guard";
            guardGo.transform.SetParent(knifeTransform, false);
            guardGo.transform.localPosition = Vector3.zero;
            guardGo.transform.localScale = new Vector3(0.035f, 0.012f, 0.07f);
            var guardCol = guardGo.GetComponent<Collider>();
            if (guardCol != null) Destroy(guardCol);

            knifeRenderer = bladeGo.GetComponent<MeshRenderer>();

            // Setup Trail
            slashTrailRenderer = knifeRoot.AddComponent<TrailRenderer>();
            slashTrailRenderer.time = 0.18f;
            slashTrailRenderer.startWidth = 0.08f;
            slashTrailRenderer.endWidth = 0.005f;
            slashTrailRenderer.emitting = false;
        }

        private void SetKnifePose(Vector3 localPos, Quaternion localRot)
        {
            if (knifeTransform == null) return;
            knifeTransform.localPosition = localPos;
            knifeTransform.localRotation = localRot;
        }

        #endregion

        #region Pure Calculation Helpers (Unit Testable)

        /// <summary>
        /// Checks if a given elapsed parry time falls within the active parry precision window.
        /// </summary>
        public static bool IsInActiveParryWindow(float timeElapsed, float windowStart, float windowEnd)
        {
            return timeElapsed >= windowStart && timeElapsed <= windowEnd;
        }

        /// <summary>
        /// Calculates knife local position along the slash arc (idle -> center -> return).
        /// Uses smooth sinusoidal curve peaking at t=0.45.
        /// </summary>
        public static Vector3 CalculateKnifePosition(float progress, Vector3 idlePos, Vector3 centerPos)
        {
            float t = Mathf.Clamp01(progress);
            // Swing forward curve: quick strike forward (0 -> 0.4) and smooth recovery (0.4 -> 1.0)
            float weight;
            if (t <= 0.4f)
            {
                weight = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            }
            else
            {
                weight = Mathf.SmoothStep(1f, 0f, (t - 0.4f) / 0.6f);
            }
            return Vector3.Lerp(idlePos, centerPos, weight);
        }

        /// <summary>
        /// Calculates knife local rotation along the slash arc.
        /// </summary>
        public static Quaternion CalculateKnifeRotation(float progress, Quaternion idleRot, Quaternion parryRot)
        {
            float t = Mathf.Clamp01(progress);
            float weight;
            if (t <= 0.4f)
            {
                weight = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            }
            else
            {
                weight = Mathf.SmoothStep(1f, 0f, (t - 0.4f) / 0.6f);
            }
            return Quaternion.Slerp(idleRot, parryRot, weight);
        }

        #endregion

        #region Procedural Audio & Spark VFX

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

        private void PlayWhooshSfx()
        {
            if (audioSource == null) return;
            if (parryWhooshSfx != null)
            {
                audioSource.PlayOneShot(parryWhooshSfx, 0.7f);
            }
            else
            {
                // Procedural swoosh clip
                audioSource.PlayOneShot(CreateProceduralWhooshClip(), 0.6f);
            }
        }

        private void PlayClangSfx(bool hasCounterBonus)
        {
            if (audioSource == null) return;
            if (parryClangSfx != null)
            {
                audioSource.PlayOneShot(parryClangSfx, 1.0f);
            }
            else
            {
                // Procedural metallic CLANG clip
                audioSource.PlayOneShot(CreateProceduralClangClip(hasCounterBonus), 1.0f);
            }
        }

        private void PlayFailSfx()
        {
            if (audioSource == null) return;
            if (parryFailSfx != null)
            {
                audioSource.PlayOneShot(parryFailSfx, 0.9f);
            }
            else
            {
                audioSource.PlayOneShot(CreateProceduralFailClip(), 0.8f);
            }
        }

        private void TriggerSparkEffect(bool hasCounterBonus)
        {
            // Spawn quick glowing spark burst in front of camera
            GameObject sparkGo = new GameObject("ParrySparks");
            if (knifeTransform != null)
            {
                sparkGo.transform.position = knifeTransform.position + knifeTransform.forward * 0.1f;
            }

            var light = sparkGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = hasCounterBonus ? new Color(0f, 1f, 0.9f) : new Color(1f, 0.9f, 0.2f);
            light.intensity = 4.5f;
            light.range = 3.5f;

            StartCoroutine(FlashRoutine(sparkGo, light));
        }

        private IEnumerator FlashRoutine(GameObject go, Light light)
        {
            float elapsed = 0f;
            float duration = 0.15f;
            float startInt = light.intensity;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (light != null) light.intensity = Mathf.Lerp(startInt, 0f, t);
                yield return null;
            }

            Destroy(go);
        }

        public static AudioClip CreateProceduralClangClip(bool extraHarmonics)
        {
            int sampleRate = 22050;
            float lengthSec = 0.35f;
            int totalSamples = (int)(sampleRate * lengthSec);
            float[] samples = new float[totalSamples];

            float baseFreq1 = 1250f;
            float baseFreq2 = 1880f;
            float baseFreq3 = 3100f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 16f); // sharp ring decay

                float s1 = Mathf.Sin(2f * Mathf.PI * baseFreq1 * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * baseFreq2 * t) * 0.7f;
                float s3 = Mathf.Sin(2f * Mathf.PI * baseFreq3 * t) * 0.4f;

                float extra = extraHarmonics ? Mathf.Sin(2f * Mathf.PI * 4500f * t) * 0.3f : 0f;

                samples[i] = (s1 + s2 + s3 + extra) * decay * 0.7f;
            }

            AudioClip clip = AudioClip.Create("ParryClang_Procedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateProceduralWhooshClip()
        {
            int sampleRate = 22050;
            float lengthSec = 0.22f;
            int totalSamples = (int)(sampleRate * lengthSec);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / lengthSec;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 650f, progress) * t) * 0.3f;
                samples[i] = (noise + tone) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("KnifeWhoosh_Procedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateProceduralFailClip()
        {
            int sampleRate = 22050;
            float lengthSec = 0.28f;
            int totalSamples = (int)(sampleRate * lengthSec);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 12f);
                float s1 = Mathf.Sin(2f * Mathf.PI * 220f * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.5f;
                samples[i] = (s1 * 0.6f + noise * 0.4f) * decay * 0.6f;
            }

            AudioClip clip = AudioClip.Create("ParryFail_Procedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
