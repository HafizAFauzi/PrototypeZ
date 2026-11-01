using UnityEngine;
using LuckyDuelist.Combat;
using LuckyDuelist.Experiment;
using LuckyDuelist.UI;
using LuckyDuelist.Monetization;

namespace LuckyDuelist.Core
{
    /// <summary>
    /// Automatic runtime bootstrapper for Lucky Duelist: Cyber Roulette.
    /// Ensures that core singleton managers (GameManager, ExperimentManager)
    /// and researcher tools are always present, even if entering play mode from any scene.
    /// </summary>
    public static class AppBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeCoreSystems()
        {
            EnsureExperimentManager();
            EnsureGameManager();
            EnsureDummyAdPlayer();
            EnsureVirtualIAPStore();
            EnsureCosmeticApplier();
            EnsureResearcherOverlay();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeCombatSystems()
        {
            EnsureSlotRevolver();
            EnsurePlayerHealth();
            EnsureEnemyHealth();
            EnsureAimStabilizer();
            EnsureFPSCameraController();
            EnsureKnifeParryController();
            EnsureBotGunnerAI();
            EnsureDefenseQTEController();
        }

        private static void EnsureExperimentManager()
        {
            if (ExperimentManager.Instance == null)
            {
                GameObject go = new GameObject("[ExperimentManager]");
                go.AddComponent<ExperimentManager>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [ExperimentManager].");
            }
        }

        private static void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                GameObject go = new GameObject("[GameManager]");
                go.AddComponent<GameManager>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [GameManager].");
            }
        }

        private static void EnsureDummyAdPlayer()
        {
            if (DummyAdPlayer.Instance == null)
            {
                GameObject go = new GameObject("[DummyAdPlayer]");
                go.AddComponent<DummyAdPlayer>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [DummyAdPlayer].");
            }
        }

        private static void EnsureVirtualIAPStore()
        {
            if (VirtualIAPStore.Instance == null)
            {
                GameObject go = new GameObject("[VirtualIAPStore]");
                go.AddComponent<VirtualIAPStore>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [VirtualIAPStore].");
            }
        }

        private static void EnsureCosmeticApplier()
        {
            if (CosmeticApplier.Instance == null)
            {
                GameObject go = new GameObject("[CosmeticApplier]");
                go.AddComponent<CosmeticApplier>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [CosmeticApplier].");
            }
        }

        private static void EnsureResearcherOverlay()
        {
            if (Object.FindObjectOfType<SecretOverrideButton>() == null)
            {
                GameObject go = new GameObject("[ResearcherDebugOverlay]");
                go.AddComponent<SecretOverrideButton>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned [ResearcherDebugOverlay] (Press F12 to view).");
            }
        }

        private static void EnsureSlotRevolver()
        {
            if (SlotRevolverController.Instance == null)
            {
                GameObject go = new GameObject("[SlotRevolverController]");
                go.AddComponent<SlotRevolverController>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [SlotRevolverController].");
            }
        }

        private static void EnsurePlayerHealth()
        {
            if (PlayerHealth.Instance == null)
            {
                GameObject go = new GameObject("[PlayerHealth]");
                go.AddComponent<PlayerHealth>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [PlayerHealth].");
            }
        }

        private static void EnsureEnemyHealth()
        {
            if (EnemyHealth.Instance == null)
            {
                GameObject go = new GameObject("[EnemyHealth]");
                go.AddComponent<EnemyHealth>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [EnemyHealth].");
            }
        }

        private static void EnsureAimStabilizer()
        {
            if (AimStabilizerController.Instance == null)
            {
                GameObject go = new GameObject("[AimStabilizerController]");
                go.AddComponent<AimStabilizerController>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[AppBootstrapper] Auto-spawned persistent [AimStabilizerController].");
            }
        }

        private static void EnsureFPSCameraController()
        {
            if (FPSCameraController.Instance == null)
            {
                GameObject go = new GameObject("[FPSCameraController]");
                go.AddComponent<FPSCameraController>();
                Debug.Log("[AppBootstrapper] Auto-spawned [FPSCameraController].");
            }
        }

        private static void EnsureKnifeParryController()
        {
            if (KnifeParryController.Instance == null)
            {
                GameObject go = new GameObject("[KnifeParryController]");
                go.AddComponent<KnifeParryController>();
                Debug.Log("[AppBootstrapper] Auto-spawned [KnifeParryController].");
            }
        }

        private static void EnsureBotGunnerAI()
        {
            if (BotGunnerAI.Instance == null)
            {
                GameObject go = new GameObject("[BotGunnerAI]");
                go.AddComponent<BotGunnerAI>();
                Debug.Log("[AppBootstrapper] Auto-spawned [BotGunnerAI].");
            }
        }

        private static void EnsureDefenseQTEController()
        {
            if (DefenseQTEController.Instance == null)
            {
                GameObject go = new GameObject("[DefenseQTEController]");
                go.AddComponent<DefenseQTEController>();
                Debug.Log("[AppBootstrapper] Auto-spawned [DefenseQTEController].");
            }
        }
    }
}
