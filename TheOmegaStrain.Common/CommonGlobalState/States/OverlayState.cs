using RetroMesh.Engine;
using System;
using TheOmegaStrain.Common.Persistence;
using System.Collections.Generic;

namespace TheOmegaStrain.Common.CommonGlobalState.States
{
    public enum ScreenOverlayType
    {
        None = 0,
        Intro = 1,
        Outro = 2,
        Game = 3,
        NameEntry = 4,
        Tutorial = 5,
        Settings = 6
    }

    public enum ScreenOverlaySettingsPanel
    {
        None = 0,
        Audio = 1,
        Graphics = 2,
        Controls = 3
    }

    public enum ScreenOverlayAnchor
    {
        Center = 0,
        Top = 1,
        Bottom = 2
    }

    public enum ScreenOverlayChoiceAction
    {
        None = 0,
        PlanetLostRecovery = 1
    }

        /// <summary>
        /// ScreenOverlayState is a lightweight, global UI state for modal/cinematic overlays.
    /// - Objects are allowed to toggle ShowOverlay and set the text fields.
    /// - Rendering is handled elsewhere (renderer reads this state only).
    /// - Update() provides deterministic fade-in/out based on ShowOverlay.
    /// </summary>
        public sealed class ScreenOverlayState
        {
            public const float DefaultAutoPageSeconds = 20f;

        // -----------------------------
        // Identity / usage
        // -----------------------------
        public ScreenOverlayType Type { get; set; } = ScreenOverlayType.None;

        // -----------------------------
        // Hide or show Debug overlays
        // -----------------------------
        public bool ShowDebugOverlay { get; set; } = false;

        /// <summary>
        /// Main on/off switch. When false, overlay fades out to Opacity=0.
        /// When true, overlay fades in to Opacity=1.
        /// </summary>
        public bool ShowOverlay { get; set; } = false;

        public bool ShowVideoOverlay { get; set; } = false;
        public string VideoClipPath { get; set; } = "";

        /// <summary>
        /// Optional: blocks gameplay input (scene/game can use this if desired).
        /// Renderer does not care.
        /// </summary>
        public bool IsModal { get; set; } = false;

        public bool BlocksGameplayInput => ShowOverlay && IsModal;

        /// <summary>
        /// Some overlays are timeline-driven and must stay visible until their
        /// owner hides them. Mouse/controller overlay activation should not
        /// dismiss those panels.
        /// </summary>
        public bool CanDismissWithInput { get; set; } = true;

        // -----------------------------
        // Content
        // -----------------------------
        public string Header { get; set; } = "";   // ingress/label (small)
        public string Title { get; set; } = "";    // main heading (big)
        public string Body { get; set; } = "";     // multi-line
        public string Footer { get; set; } = "";   // CTA (press any key)

        // -----------------------------
        // Settings overlays
        // -----------------------------
        public ScreenOverlaySettingsPanel SettingsPanel { get; set; } = ScreenOverlaySettingsPanel.None;
        public int SelectedSettingsIndex { get; private set; } = 0;

        // -----------------------------
        // Choice overlays
        // -----------------------------
        public ScreenOverlayChoiceAction ChoiceAction { get; set; } = ScreenOverlayChoiceAction.None;
        public List<string> ChoiceOptions { get; } = new();
        public int SelectedChoiceIndex { get; private set; } = 0;
        public string ChoiceBodyPrefix { get; private set; } = "";
        public bool HasChoiceOptions => ChoiceOptions.Count > 0;
        public string SelectedChoice =>
            HasChoiceOptions && SelectedChoiceIndex >= 0 && SelectedChoiceIndex < ChoiceOptions.Count
                ? ChoiceOptions[SelectedChoiceIndex]
                : "";

        // -----------------------------
        // Paging
        // -----------------------------
        /// <summary>
        /// Per-page content. When populated, Header/Title/Body/Footer are
        /// driven by the current page index. Each inner list has exactly
        /// 4 elements: [Header, Title, Body, Footer].
        /// </summary>
        public List<string[]> Pages { get; set; } = new();

        /// <summary>Zero-based page index shown to the user.</summary>
        public int CurrentPage { get; set; } = 0;

        /// <summary>Total number of pages (derived from Pages.Count).</summary>
        public int TotalPages => Pages.Count > 0 ? Pages.Count : 1;

        /// <summary>True when there are multiple pages to navigate.</summary>
        public bool HasMultiplePages => Pages.Count > 1;

        /// <summary>
        /// Advances to the next page if available. Returns true if the page changed.
        /// </summary>
        public bool NextPage()
        {
            if (!HasMultiplePages || CurrentPage >= Pages.Count - 1) return false;
            CurrentPage++;
            ApplyPageContent();
            return true;
        }

        /// <summary>
        /// Goes back to the previous page if available. Returns true if the page changed.
        /// </summary>
        public bool PreviousPage()
        {
            if (!HasMultiplePages || CurrentPage <= 0) return false;
            CurrentPage--;
            ApplyPageContent();
            return true;
        }

        /// <summary>
        /// Copies Header/Title/Body/Footer from the current page entry.
        /// </summary>
        public void ApplyPageContent()
        {
            if (Pages.Count == 0 || CurrentPage < 0 || CurrentPage >= Pages.Count) return;
            var page = Pages[CurrentPage];
            if (page.Length < 4) return;
            Header = page[0];
            Title = page[1];
            Body = page[2];
            Footer = page[3];
        }

        /// <summary>
        /// Adds a page and returns the index. Each page is [Header, Title, Body, Footer].
        /// </summary>
        public int AddPage(string header, string title, string body, string footer)
        {
            Pages.Add(new[] { header, title, body, footer });
            if (Pages.Count > 1 && AutoPageSeconds <= 0f)
                AutoPageSeconds = DefaultAutoPageSeconds;
            return Pages.Count - 1;
        }

        // -----------------------------
        // Name entry state
        // -----------------------------
        public const int MaxCallsignLength = 16;
        public string NameEntryBuffer { get; set; } = "";
        public string NameEntryValidationMessage { get; set; } = "";
        public bool IsNameConfirmed { get; set; } = false;
        private float _cursorBlinkTimer = 0f;

        // -----------------------------
        // Presentation knobs (kept simple)
        // -----------------------------
        public ScreenOverlayAnchor Anchor { get; set; } = ScreenOverlayAnchor.Top;

        /// <summary>
        /// 0..1. How much to dim the whole screen behind the panel.
        /// Renderer can interpret as (alpha = DimStrength * Opacity).
        /// </summary>
        public float DimStrength { get; set; } = 0.55f;

        /// <summary>
        /// 0..1. Background strength for the panel itself (glass fill).
        /// Renderer can interpret as (alpha = PanelFillStrength * Opacity).
        /// </summary>
        public float PanelFillStrength { get; set; } = 0.70f;

        /// <summary>
        /// 0..1. Border strength for the panel.
        /// </summary>
        public float BorderStrength { get; set; } = 0.85f;

        /// <summary>
        /// Panel width relative to screen width (0..1).
        /// </summary>
        public float PanelWidthRatio { get; set; } = 0.70f;

        /// <summary>
        /// Panel height relative to screen height (0..1).
        /// </summary>
        public float PanelHeightRatio { get; set; } = 0.28f;

        /// <summary>
        /// Panel vertical offset as ratio of screen height (0..1).
        /// For Anchor.Top, this is the "top margin"; for Center/Bottom it's used as additional offset.
        /// </summary>
        public float PanelYOffsetRatio { get; set; } = 0.18f;

        /// <summary>
        /// Panel corner radius in DIP. Renderer can use this directly.
        /// </summary>
        public float CornerRadius { get; set; } = 12f;

        /// <summary>
        /// When true, text blocks are center-aligned within the panel.
        /// </summary>
        public bool CenterText { get; set; } = false;

        // -----------------------------
        // Fade (deterministic)
        // -----------------------------
        /// <summary>
        /// Current opacity 0..1 (owned by this state).
        /// </summary>
        public float Opacity { get; private set; } = 0f;

        /// <summary>
        /// Fade speed in opacity units per second.
        /// Example: 2.5 means ~0.4s to fade from 0->1.
        /// </summary>
        public float FadeInSpeed { get; set; } = 2.5f;

        /// <summary>
        /// Fade out speed in opacity units per second.
        /// </summary>
        public float FadeOutSpeed { get; set; } = 3.0f;

        /// <summary>
        /// Optional: if true, ShowOverlay will be turned off after AutoHideSeconds.
        /// Useful for short "mission start" cards.
        /// </summary>
        public bool AutoHide { get; set; } = false;

        /// <summary>
        /// If AutoHide is enabled, overlay will start fading out after this duration (seconds)
        /// counted from the moment ShowOverlay first became true.
        /// </summary>
        public float AutoHideSeconds { get; set; } = 0f;

        /// <summary>
        /// When greater than zero, multi-page overlays auto-advance to the next page
        /// every AutoPageSeconds. Wraps around when the last page is reached.
        /// Applies to any overlay with HasMultiplePages == true.
        /// </summary>
        public float AutoPageSeconds { get; set; } = 0f;

        private float _shownTimeSeconds = 0f;
        private bool _wasShowingLastUpdate = false;
        private float _autoPageElapsedSeconds = 0f;
        private int _autoPageLastIndex = -1;

        // -----------------------------
        // Convenience
        // -----------------------------
        public bool ShouldRender => Opacity > 0.001f;

        /// <summary>
        /// Fully hides without fade (hard reset).
        /// </summary>
        public void HardHide()
        {
            ShowOverlay = false;
            ShowVideoOverlay = false;
            VideoClipPath = "";
            CanDismissWithInput = true;
            Opacity = 0f;
            _shownTimeSeconds = 0f;
            _wasShowingLastUpdate = false;
        }

        /// <summary>
        /// Clears content and resets presentation knobs to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            Type = ScreenOverlayType.None;
            ShowOverlay = false;
            IsModal = false;
            CanDismissWithInput = true;

            Header = "";
            Title = "";
            Body = "";
            Footer = "";
            SettingsPanel = ScreenOverlaySettingsPanel.None;
            SelectedSettingsIndex = 0;
            ClearChoiceOptions();

            ShowVideoOverlay = false;
            VideoClipPath = "";

            Anchor = ScreenOverlayAnchor.Top;
            DimStrength = 0.55f;
            PanelFillStrength = 0.70f;
            BorderStrength = 0.85f;

            PanelWidthRatio = 0.70f;
            PanelHeightRatio = 0.28f;
            PanelYOffsetRatio = 0.18f;
            CornerRadius = 12f;
            CenterText = false;

            FadeInSpeed = 2.5f;
            FadeOutSpeed = 3.0f;

            AutoHide = false;
            AutoHideSeconds = 0f;
            AutoPageSeconds = 0f;
            _autoPageElapsedSeconds = 0f;
            _autoPageLastIndex = -1;

            NameEntryBuffer = "";
            NameEntryValidationMessage = "";
            IsNameConfirmed = false;
            _cursorBlinkTimer = 0f;

            Pages.Clear();
            CurrentPage = 0;

            HardHide();
        }

        public void SetChoiceOptions(
            ScreenOverlayChoiceAction action,
            string bodyPrefix,
            params string[] options)
        {
            ChoiceAction = action;
            ChoiceBodyPrefix = bodyPrefix ?? "";
            ChoiceOptions.Clear();
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (!string.IsNullOrWhiteSpace(option))
                        ChoiceOptions.Add(option);
                }
            }

            SelectedChoiceIndex = 0;
            ApplyChoiceBody();
        }

        public bool MoveChoiceSelection(int delta)
        {
            if (!HasChoiceOptions || delta == 0)
                return false;

            int next = SelectedChoiceIndex + delta;
            if (next < 0)
                next = ChoiceOptions.Count - 1;
            else if (next >= ChoiceOptions.Count)
                next = 0;

            if (next == SelectedChoiceIndex)
                return false;

            SelectedChoiceIndex = next;
            ApplyChoiceBody();
            return true;
        }

        public void ClearChoiceOptions()
        {
            ChoiceAction = ScreenOverlayChoiceAction.None;
            ChoiceBodyPrefix = "";
            ChoiceOptions.Clear();
            SelectedChoiceIndex = 0;
        }

        private void ApplyChoiceBody()
        {
            if (!HasChoiceOptions)
            {
                Body = ChoiceBodyPrefix;
                return;
            }

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(ChoiceBodyPrefix))
            {
                lines.Add(ChoiceBodyPrefix);
                lines.Add("");
            }

            for (int i = 0; i < ChoiceOptions.Count; i++)
            {
                lines.Add($"{(i == SelectedChoiceIndex ? ">" : " ")} {ChoiceOptions[i]}");
            }

            Body = string.Join("\n", lines);
        }

        /// <summary>
        /// Preferred update call once per frame (before render pass).
        /// dtSeconds should be stable (e.g., 1/60f).
        /// </summary>
        public void Update(float dtSeconds)
        {
            if (dtSeconds <= 0f) return;

            // Track how long we've been "requested visible"
            if (ShowOverlay)
            {
                if (!_wasShowingLastUpdate)
                {
                    // Rising edge: overlay just turned on
                    _shownTimeSeconds = 0f;
                }
                else
                {
                    _shownTimeSeconds += dtSeconds;
                }
            }

            _wasShowingLastUpdate = ShowOverlay;

            // Auto-hide behavior (optional)
            if (ShowOverlay && AutoHide && AutoHideSeconds > 0f && _shownTimeSeconds >= AutoHideSeconds)
            {
                ShowOverlay = false;
            }

            // Auto-paging behavior (optional): cycle through Pages every AutoPageSeconds.
            if (ShowOverlay && AutoPageSeconds > 0f && HasMultiplePages)
            {
                if (_autoPageLastIndex != CurrentPage)
                {
                    _autoPageLastIndex = CurrentPage;
                    _autoPageElapsedSeconds = 0f;
                }
                else
                {
                    _autoPageElapsedSeconds += dtSeconds;
                    if (_autoPageElapsedSeconds >= AutoPageSeconds)
                    {
                        _autoPageElapsedSeconds = 0f;
                        CurrentPage = (CurrentPage + 1) % TotalPages;
                        ApplyPageContent();
                        _autoPageLastIndex = CurrentPage;
                    }
                }
            }
            else
            {
                _autoPageElapsedSeconds = 0f;
                _autoPageLastIndex = CurrentPage;
            }

            // Fade to target
            float target = ShowOverlay ? 1f : 0f;

            if (Opacity < target)
            {
                Opacity += FadeInSpeed * dtSeconds;
                if (Opacity > 1f) Opacity = 1f;
            }
            else if (Opacity > target)
            {
                Opacity -= FadeOutSpeed * dtSeconds;
                if (Opacity < 0f) Opacity = 0f;
            }

            // If fully invisible, Type can be reset automatically if you want.
            // Keeping it as-is is often useful for debugging/replay.

            // Name entry: update Body with buffer + blinking cursor
            if (Type == ScreenOverlayType.NameEntry && ShowOverlay)
            {
                _cursorBlinkTimer += dtSeconds;
                bool showCursor = ((int)(_cursorBlinkTimer / 0.5f)) % 2 == 0;
                string cursor = showCursor ? "_" : " ";
                string display = NameEntryBuffer + cursor;
                string validation = string.IsNullOrEmpty(NameEntryValidationMessage)
                    ? "" : $"\n{NameEntryValidationMessage}";
                Body = $"CALLSIGN: {display}{validation}";
            }
        }

        // -----------------------------
        // Quick presets (optional, but handy)
        // -----------------------------
        public void SetIntroPreset(string title, string footer = "PRESS ANY KEY")
        {
            Type = ScreenOverlayType.Intro;
            Anchor = ScreenOverlayAnchor.Top;
            IsModal = true;
            CanDismissWithInput = true;

            Header = "RETROMESH BOOT SEQUENCE";
            Title = title;
            Body = "";
            Footer = footer;

            DimStrength = 0.55f;
            PanelWidthRatio = 0.72f;
            PanelHeightRatio = 0.26f;
            PanelYOffsetRatio = 0.16f;

            AutoHide = false;
            AutoHideSeconds = 0f;
        }

        public void SetOutroPreset(string title, string body, string footer = "PRESS ANY KEY")
        {
            Type = ScreenOverlayType.Outro;
            Anchor = ScreenOverlayAnchor.Center;
            IsModal = true;
            CanDismissWithInput = true;

            Header = "TRANSMISSION ENDS";
            Title = title;
            Body = body ?? "";
            Footer = footer;

            DimStrength = 0.65f;
            PanelWidthRatio = 0.75f;
            PanelHeightRatio = 0.32f;
            PanelYOffsetRatio = 0.00f;

            AutoHide = false;
            AutoHideSeconds = 0f;
        }

        public void SetGameOverlayPreset(string header, string title, string body, string footer = "")
        {
            Type = ScreenOverlayType.Game;
            Anchor = ScreenOverlayAnchor.Top;
            IsModal = false;
            CanDismissWithInput = true;

            Header = header ?? "";
            Title = title ?? "";
            Body = body ?? "";
            Footer = footer ?? "";

            DimStrength = 0.25f; // lighter in-game
            PanelWidthRatio = 0.62f;
            PanelHeightRatio = 0.22f;
            PanelYOffsetRatio = 0.06f;

            AutoHide = false;
            AutoHideSeconds = 0f;
        }

        public void SetNameEntryPreset(string defaultName = "")
        {
            Type = ScreenOverlayType.NameEntry;
            Anchor = ScreenOverlayAnchor.Center;
            IsModal = true;
            CanDismissWithInput = true;

            Pages.Clear();
            CurrentPage = 0;

            Header = "RETROMESH // PILOT REGISTRY";
            Title = "IDENTIFY YOURSELF";
            NameEntryBuffer = PlayerNameFormatter.Normalize(defaultName);
            NameEntryValidationMessage = "";
            IsNameConfirmed = false;
            _cursorBlinkTimer = 0f;
            Body = "";
            Footer = "ENTER TO CONFIRM  //  ESC TO GO BACK";

            DimStrength = 0.65f;
            PanelWidthRatio = 0.68f;
            PanelHeightRatio = 0.26f;
            PanelYOffsetRatio = 0.00f;
            CenterText = true;

            AutoHide = false;
            AutoHideSeconds = 0f;
            ShowOverlay = true;
        }

        /// <summary>
        /// Processes a key press during name entry. Returns true if the key was consumed.
        /// </summary>
        public bool ProcessNameEntryKey(GameInputKey key)
        {
            if (Type != ScreenOverlayType.NameEntry || !ShowOverlay) return false;

            // Letters A-Z
            if (key >= GameInputKey.A && key <= GameInputKey.Z)
            {
                if (NameEntryBuffer.Length < MaxCallsignLength)
                    NameEntryBuffer += (char)('A' + (key - GameInputKey.A));
                return true;
            }

            // Digits 0-9 (top row)
            if (key >= GameInputKey.D0 && key <= GameInputKey.D9)
            {
                if (NameEntryBuffer.Length < MaxCallsignLength)
                    NameEntryBuffer += (char)('0' + (key - GameInputKey.D0));
                return true;
            }

            // NumPad digits
            if (key >= GameInputKey.NumPad0 && key <= GameInputKey.NumPad9)
            {
                if (NameEntryBuffer.Length < MaxCallsignLength)
                    NameEntryBuffer += (char)('0' + (key - GameInputKey.NumPad0));
                return true;
            }

            // Backspace
            if (key == GameInputKey.Back && NameEntryBuffer.Length > 0)
            {
                NameEntryBuffer = NameEntryBuffer[..^1];
                NameEntryValidationMessage = "";
                return true;
            }

            // Space
            if (key == GameInputKey.Space && NameEntryBuffer.Length < MaxCallsignLength)
            {
                NameEntryBuffer += ' ';
                return true;
            }

            // Minus / underscore
            if ((key == GameInputKey.OemMinus || key == GameInputKey.Subtract) && NameEntryBuffer.Length < MaxCallsignLength)
            {
                NameEntryBuffer += '-';
                return true;
            }

            return false;
        }

        public void SetSettingsPreset(ScreenOverlaySettingsPanel panel, string title, string body, string footer)
        {
            Type = ScreenOverlayType.Settings;
            SettingsPanel = panel;
            SelectedSettingsIndex = 0;
            Anchor = ScreenOverlayAnchor.Center;
            IsModal = true;
            CanDismissWithInput = true;

            Pages.Clear();
            CurrentPage = 0;
            ClearChoiceOptions();

            Header = panel switch
            {
                ScreenOverlaySettingsPanel.Audio => "RETROMESH // AUDIO CONFIG",
                ScreenOverlaySettingsPanel.Controls => "RETROMESH // CONTROL CONFIG",
                _ => "RETROMESH // GRAPHICS CONFIG"
            };
            Title = title ?? "";
            Body = body ?? "";
            Footer = footer ?? "";

            DimStrength = 0.60f;
            PanelFillStrength = 0.78f;
            BorderStrength = 0.90f;
            PanelWidthRatio = 0.68f;
            PanelHeightRatio = panel == ScreenOverlaySettingsPanel.Controls ? 0.52f : 0.38f;
            PanelYOffsetRatio = 0.00f;
            CenterText = false;

            AutoHide = false;
            AutoHideSeconds = 0f;
            AutoPageSeconds = 0f;
            ShowOverlay = true;
        }

        public bool MoveSettingsSelection(int delta, int optionCount)
        {
            if (optionCount <= 0 || delta == 0)
                return false;

            int next = SelectedSettingsIndex + delta;
            if (next < 0)
                next = optionCount - 1;
            else if (next >= optionCount)
                next = 0;

            if (next == SelectedSettingsIndex)
                return false;

            SelectedSettingsIndex = next;
            return true;
        }
    }
}
