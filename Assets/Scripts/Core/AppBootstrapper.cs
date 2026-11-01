using UnityEngine;
using LuckyDuelist.Combat;
using LuckyDuelist.Experiment;
using LuckyDuelist.UI;

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
            EnsureResearcherOverlay();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeCombatSystems()
        {
            EnsureEnemyHealth();
            EnsureAimStabilizer();
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
    }
}
