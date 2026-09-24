using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.CommonGlobalState;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TheOmegaStrain.Wpf.MainWindowClasses
{
    /// <summary>
    /// Simple WPF overlay renderer for ScreenOverlayState.
    /// - Uses a dim background + framed panel.
    /// - Auto-sizes panel height to content so footer never overflows.
    /// - Keeps implementation close to the original working version.
    /// </summary>
    public sealed class OverlayHandler
    {
        private const double MinimumPanelHeightDip = 160.0;
        private const double PanelEdgeMarginDip = 24.0;
        private const double SoftMaxPanelHeightRatio = 0.90;

        private readonly Grid _root;

        private readonly Grid _overlayRoot;
        private readonly Rectangle _dim;
        private readonly Border _panel;

        private readonly StackPanel _stack;
        private readonly TextBlock _header;
        private readonly TextBlock _title;
        private readonly ScaleTransform _titleScale = new ScaleTransform(1, 1);
        private readonly TextBlock _body;
        private readonly TextBlock _footer;
        private readonly TextBlock _pageIndicator;

        public OverlayHandler(Grid root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            _overlayRoot = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            Panel.SetZIndex(_overlayRoot, int.MaxValue - 50);

            _dim = new Rectangle
            {
                Fill = Brushes.Black,
                Opacity = 0.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _panel = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 255, 120)),
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 140, 0, 0),
                Width = 900,
                Height = double.NaN // auto by default
            };

            _stack = new StackPanel { Orientation = Orientation.Vertical };

            _header = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                Foreground = Brushes.Lime,
                Opacity = 0.9
            };

            _title = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 44,
                Foreground = Brushes.Lime,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            _title.RenderTransformOrigin = new Point(0.5, 0.5);
            _title.RenderTransform = _titleScale;

            _body = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 20,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.95,
                Margin = new Thickness(0, 0, 0, 14)
            };

            _footer = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                Foreground = Brushes.Lime,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.9
            };

            _pageIndicator = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                Foreground = Brushes.Lime,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0)
            };

            _stack.Children.Add(_header);
            _stack.Children.Add(_title);
            _stack.Children.Add(_body);
            _stack.Children.Add(_footer);
            _stack.Children.Add(_pageIndicator);

            _panel.Child = _stack;

            _overlayRoot.Children.Add(_dim);
            _overlayRoot.Children.Add(_panel);

            _root.Children.Add(_overlayRoot);
        }

        public void Update(ScreenOverlayState state, double screenWidth, double screenHeight)
        {
            if (state == null) return;

            // Note: Game-type overlays were previously always collapsed here, which made the
            // victory panel ("PLANET SECURED / ALL THREATS ELIMINATED") invisible even though
            // LiveGameLoop built it. The HUD-only state is naturally hidden by ShouldRender
            // returning false (Opacity fades to 0 when ShowOverlay is false), so we let Game
            // overlays through and rely on the same gate as every other overlay type.
            if (!state.ShouldRender)
            {
                _overlayRoot.Visibility = Visibility.Collapsed;
                _overlayRoot.Opacity = 0;
                return;
            }

            if (screenWidth <= 0) screenWidth = 1920;
            if (screenHeight <= 0) screenHeight = 1080;

            _overlayRoot.Visibility = Visibility.Visible;
            _overlayRoot.Opacity = Clamp01(state.Opacity);

            // Dim behind
            _dim.Opacity = Clamp01(state.DimStrength) * Clamp01(state.Opacity);

            // Content
            _header.Text = state.Header ?? "";
            _title.Text = state.Title ?? "";
            _titleScale.ScaleX = state.TitleScale;
            _titleScale.ScaleY = state.TitleScale;
            _body.Text = state.Body ?? "";
            _footer.Text = state.Footer ?? "";

            // Hide empty blocks -> tighter layout
            _header.Visibility = string.IsNullOrWhiteSpace(_header.Text) ? Visibility.Collapsed : Visibility.Visible;
            _title.Visibility  = string.IsNullOrWhiteSpace(_title.Text)  ? Visibility.Collapsed : Visibility.Visible;
            _body.Visibility   = string.IsNullOrWhiteSpace(_body.Text)   ? Visibility.Collapsed : Visibility.Visible;
            _footer.Visibility = string.IsNullOrWhiteSpace(_footer.Text) ? Visibility.Collapsed : Visibility.Visible;

            // Page indicator
            if (state.Type == ScreenOverlayType.Settings &&
                state.SettingsPanel != ScreenOverlaySettingsPanel.None)
            {
                bool controllerActive = GameState.SettingsState.EffectiveControlScheme == ControlInputMode.XboxController;
                _pageIndicator.Text = BuildSettingsPageIndicatorText(state.SettingsPanel, controllerActive);
                _pageIndicator.Visibility = Visibility.Visible;
            }
            else if (state.HasMultiplePages)
            {
                bool controllerActive = GameState.SettingsState.EffectiveControlScheme == ControlInputMode.XboxController;
                _pageIndicator.Text = BuildPageIndicatorText(state.TotalPages, state.CurrentPage, controllerActive);
                _pageIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                _pageIndicator.Visibility = Visibility.Collapsed;
            }

            // Text alignment
            var align = state.CenterText ? TextAlignment.Center : TextAlignment.Left;
            _header.TextAlignment = align;
            _title.TextAlignment = align;
            _body.TextAlignment = align;
            _footer.TextAlignment = TextAlignment.Center;

            // Panel width from ratio
            double panelW = screenWidth * Clamp01(state.PanelWidthRatio);
            if (panelW < 420) panelW = 420;
            if (panelW > screenWidth - 40) panelW = Math.Max(420, screenWidth - 40);

            _panel.Width = panelW;
            _panel.CornerRadius = new CornerRadius(state.CornerRadius);

            // Keep readable content as one centered composition while preserving
            // left-aligned terminal text inside that composition.
            double panelContentWidth = Math.Max(
                0,
                panelW - _panel.Padding.Left - _panel.Padding.Right -
                _panel.BorderThickness.Left - _panel.BorderThickness.Right);
            double contentColumnWidth = Math.Min(panelContentWidth, 960.0);
            _header.Width = contentColumnWidth;
            _title.Width = contentColumnWidth;
            _body.Width = contentColumnWidth;
            _header.HorizontalAlignment = HorizontalAlignment.Center;
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _body.HorizontalAlignment = HorizontalAlignment.Center;

            // Anchor
            _panel.VerticalAlignment = state.Anchor switch
            {
                ScreenOverlayAnchor.Bottom => VerticalAlignment.Bottom,
                ScreenOverlayAnchor.Center => VerticalAlignment.Center,
                _ => VerticalAlignment.Top
            };

            // Y offset
            double yOffset = screenHeight * Clamp01(state.PanelYOffsetRatio);
            _panel.Margin = state.Anchor switch
            {
                ScreenOverlayAnchor.Bottom => new Thickness(0, 0, 0, yOffset),
                ScreenOverlayAnchor.Center => new Thickness(0, yOffset, 0, 0),
                _ => new Thickness(0, yOffset, 0, 0)
            };

            // Panel fill/border strengths
            _panel.Background = new SolidColorBrush(Color.FromArgb(
                (byte)(Clamp01(state.PanelFillStrength) * 255),
                0, 0, 0));

            _panel.BorderBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(Clamp01(state.BorderStrength) * 255),
                0, 255, 120));

            // ----------------------------
            // Dynamic height to fit content (no cutting)
            // ----------------------------
            // Measure the content directly. Measuring the Border after it previously
            // had an explicit height can preserve that old desired size in WPF and
            // make a short menu look much taller than its content.
            _panel.Height = double.NaN;
            double horizontalChrome = _panel.Padding.Left + _panel.Padding.Right +
                                      _panel.BorderThickness.Left + _panel.BorderThickness.Right;
            double verticalChrome = _panel.Padding.Top + _panel.Padding.Bottom +
                                    _panel.BorderThickness.Top + _panel.BorderThickness.Bottom;
            _stack.Measure(new Size(Math.Max(0, panelW - horizontalChrome), double.PositiveInfinity));
            double desiredH = _stack.DesiredSize.Height + verticalChrome;

            _panel.Height = CalculatePanelHeight(desiredH, screenHeight, yOffset, state.Anchor);
        }

        public static string BuildPageIndicatorText(int totalPages, int currentPage, bool controllerActive = false)
        {
            if (totalPages <= 1)
                return string.Empty;

            var dots = new System.Text.StringBuilder();
            for (int i = 0; i < totalPages; i++)
                dots.Append(i == currentPage ? " [*] " : " [ ] ");

            dots.Append("   ");
            dots.Append(controllerActive ? "D-PAD LEFT/RIGHT TO NAVIGATE" : "LEFT/RIGHT TO NAVIGATE");
            return dots.ToString();
        }

        public static string BuildSettingsPageIndicatorText(
            ScreenOverlaySettingsPanel panel,
            bool controllerActive = false)
        {
            int pageCount = Enum.GetValues<ScreenOverlaySettingsPanel>().Length - 1;
            int currentPage = Math.Clamp((int)panel - 1, 0, pageCount - 1);
            string indicator = BuildPageIndicatorText(pageCount, currentPage, controllerActive);
            string defaultHint = controllerActive
                ? "D-PAD LEFT/RIGHT TO NAVIGATE"
                : "LEFT/RIGHT TO NAVIGATE";
            string settingsHint = controllerActive
                ? "LB/RB TO CHANGE SETTINGS PAGE"
                : "PAGE UP/DOWN TO CHANGE SETTINGS PAGE";
            return indicator.Replace(defaultHint, settingsHint, StringComparison.Ordinal);
        }

        public static double CalculatePanelHeight(
            double desiredHeight,
            double screenHeight,
            double yOffset,
            ScreenOverlayAnchor anchor)
        {
            if (screenHeight <= 0) screenHeight = 1080;
            if (desiredHeight < 0) desiredHeight = 0;

            double minHeight = Math.Max(screenHeight * 0.18, MinimumPanelHeightDip);
            double finalHeight = Math.Max(desiredHeight, minHeight);
            double screenSoftMax = screenHeight * SoftMaxPanelHeightRatio;
            double availableHeight = CalculateAvailablePanelHeight(screenHeight, yOffset, anchor);
            double maxHeight = Math.Max(0, Math.Min(screenSoftMax, availableHeight));

            return maxHeight <= 0
                ? 0
                : Math.Min(finalHeight, maxHeight);
        }

        public static double CalculateAvailablePanelHeight(
            double screenHeight,
            double yOffset,
            ScreenOverlayAnchor anchor)
        {
            if (screenHeight <= 0) screenHeight = 1080;

            yOffset = Math.Max(0, yOffset);
            double verticalMargin = anchor == ScreenOverlayAnchor.Center
                ? (PanelEdgeMarginDip * 2.0) + yOffset
                : PanelEdgeMarginDip + yOffset;

            return Math.Max(0, screenHeight - verticalMargin);
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
