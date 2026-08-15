namespace CommonUtilities.CommonGlobalState.States
{
    using System;

    public sealed class TutorialRuntimeState
    {
        public const double InstructionOverlayMinimumSeconds = 5.0;

        public bool DecoySelectCueSpoken { get; set; }
        public bool CompleteCueSpoken { get; set; }
        public bool InstructionOverlayPauseActive { get; private set; }
        public string InstructionOverlayKey { get; private set; } = "";
        public DateTime InstructionOverlayCanCloseAt { get; private set; } = DateTime.MinValue;
        public DateTime InstructionOverlayAutoCloseAt { get; private set; } = DateTime.MinValue;

        public void ShowInstructionOverlay(string key)
        {
            ShowInstructionOverlay(key, DateTime.UtcNow);
        }

        public void ShowInstructionOverlay(string key, DateTime shownAtUtc)
        {
            ShowInstructionOverlay(key, shownAtUtc, InstructionOverlayMinimumSeconds);
        }

        public void ShowInstructionOverlay(string key, DateTime shownAtUtc, double autoCloseSeconds)
        {
            InstructionOverlayPauseActive = true;
            InstructionOverlayKey = key ?? "";
            InstructionOverlayCanCloseAt = shownAtUtc.AddSeconds(InstructionOverlayMinimumSeconds);
            InstructionOverlayAutoCloseAt = shownAtUtc.AddSeconds(Math.Max(InstructionOverlayMinimumSeconds, autoCloseSeconds));
        }

        public bool CanCloseInstructionOverlay(DateTime nowUtc) =>
            InstructionOverlayPauseActive && nowUtc >= InstructionOverlayCanCloseAt;

        public bool ShouldAutoCloseInstructionOverlay(DateTime nowUtc) =>
            InstructionOverlayPauseActive && nowUtc >= InstructionOverlayAutoCloseAt;

        public double GetInstructionOverlayHoldSecondsLeft(DateTime nowUtc)
        {
            if (!InstructionOverlayPauseActive)
                return 0.0;

            return Math.Max(0.0, (InstructionOverlayCanCloseAt - nowUtc).TotalSeconds);
        }

        public void ClearInstructionOverlay()
        {
            InstructionOverlayPauseActive = false;
            InstructionOverlayKey = "";
            InstructionOverlayCanCloseAt = DateTime.MinValue;
            InstructionOverlayAutoCloseAt = DateTime.MinValue;
        }

        public void Reset()
        {
            DecoySelectCueSpoken = false;
            CompleteCueSpoken = false;
            ClearInstructionOverlay();
        }
    }
}
