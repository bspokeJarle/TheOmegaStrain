namespace CommonUtilities.CommonGlobalState.States
{
    public enum WorldFadePhase
    {
        Idle,
        FadeOutRequested,
        FadingOut,
        Black,
        FadeInRequested,
        FadingIn
    }

    public sealed class WorldFadeState
    {
        public const string ShipDestroyedReason = "ShipDestroyed";
        public const string InfectionCriticalReason = "InfectionCritical";
        public const string InfectionCriticalContinueReason = "InfectionCriticalContinue";
        public const string InfectionCriticalPlanetResetReason = "InfectionCriticalPlanetReset";
        public const string VictoryCompleteReason = "VictoryComplete";

        public WorldFadePhase Phase { get; private set; } = WorldFadePhase.Idle;
        public float DurationSeconds { get; private set; } = 1f;
        public string Reason { get; private set; } = string.Empty;

        public bool IsBlack => Phase == WorldFadePhase.Black;

        public bool IsFadeOutPendingOrActive =>
            Phase == WorldFadePhase.FadeOutRequested ||
            Phase == WorldFadePhase.FadingOut ||
            Phase == WorldFadePhase.Black;

        public bool IsFadeInPendingOrActive =>
            Phase == WorldFadePhase.FadeInRequested ||
            Phase == WorldFadePhase.FadingIn;

        public void RequestFadeOut(float durationSeconds = 1f, string reason = "")
        {
            if (Phase == WorldFadePhase.FadeOutRequested ||
                Phase == WorldFadePhase.FadingOut ||
                Phase == WorldFadePhase.Black)
            {
                return;
            }

            DurationSeconds = ClampDuration(durationSeconds);
            Reason = reason ?? string.Empty;
            Phase = WorldFadePhase.FadeOutRequested;
        }

        public void RequestFadeIn(float durationSeconds = 1.5f, string reason = "")
        {
            if (Phase == WorldFadePhase.FadeInRequested ||
                Phase == WorldFadePhase.FadingIn)
            {
                return;
            }

            DurationSeconds = ClampDuration(durationSeconds);
            Reason = reason ?? string.Empty;
            Phase = WorldFadePhase.FadeInRequested;
        }

        public bool TryBeginFadeOut(out float durationSeconds)
        {
            durationSeconds = DurationSeconds;
            if (Phase != WorldFadePhase.FadeOutRequested)
                return false;

            Phase = WorldFadePhase.FadingOut;
            return true;
        }

        public bool TryBeginFadeIn(out float durationSeconds)
        {
            durationSeconds = DurationSeconds;
            if (Phase != WorldFadePhase.FadeInRequested)
                return false;

            Phase = WorldFadePhase.FadingIn;
            return true;
        }

        public void MarkFadeOutComplete()
        {
            if (Phase == WorldFadePhase.FadingOut ||
                Phase == WorldFadePhase.FadeOutRequested)
            {
                Phase = WorldFadePhase.Black;
            }
        }

        public void MarkFadeInComplete()
        {
            if (Phase == WorldFadePhase.FadingIn ||
                Phase == WorldFadePhase.FadeInRequested)
            {
                Phase = WorldFadePhase.Idle;
                Reason = string.Empty;
            }
        }

        public void Reset()
        {
            Phase = WorldFadePhase.Idle;
            DurationSeconds = 1f;
            Reason = string.Empty;
        }

        private static float ClampDuration(float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return 0.01f;

            return durationSeconds;
        }
    }
}
