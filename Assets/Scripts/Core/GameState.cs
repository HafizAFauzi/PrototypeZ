namespace LuckyDuelist.Core
{
    /// <summary>
    /// Global states for Lucky Duelist: Cyber Roulette state machine.
    /// Flow: Splash -> Lobby / Store -> CombatStage1 -> CombatStage2 -> CombatStage3 -> Settlement -> Survey.
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Initial startup screen, logo presentation, initialization of subsystems.
        /// </summary>
        Splash,

        /// <summary>
        /// Main Menu / Lobby: Stage overview, respondent ID display, Play button, and Store access.
        /// </summary>
        Lobby,

        /// <summary>
        /// Virtual In-App Purchase Store for Premium Cosmetics (Enabled for Group B & C, Hidden for Group A).
        /// </summary>
        Store,

        /// <summary>
        /// Stage 1 Combat: Neon Rookie (HP: 70, slow yellow parryable bullets).
        /// </summary>
        CombatStage1,

        /// <summary>
        /// Stage 2 Combat: Chrome Gambler (HP: 90, mixed red unparryable & yellow parryable bullets).
        /// </summary>
        CombatStage2,

        /// <summary>
        /// Stage 3 Combat: The Cyber Baron - Boss (HP: 120, rapid alternating bursts).
        /// </summary>
        CombatStage3,

        /// <summary>
        /// Per-stage settlement screen (displays stage outcome, gold earned, and 2x gold ad offer for Group A/C).
        /// </summary>
        MatchSettlement,

        /// <summary>
        /// Final session settlement screen (displayed after Stage 3 clear or final defeat).
        /// Presents total run metrics, economic summary, and button to proceed to questionnaire.
        /// </summary>
        FinalSettlement,

        /// <summary>
        /// Post-session evaluation survey state (triggers browser link to Google Form with user ID).
        /// </summary>
        Survey
    }
}
