using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace _3dTesting.MainWindowClasses
{
    /// <summary>
    /// HUD Overlay V2:
    /// - Uses a PNG frame as the base layer.
    /// - Places dynamic values using Canvas slots (pixel-precise).
    /// - Scales uniformly based on a fixed design resolution (2256 width).
    /// - Visible only when ScreenOverlayState.Type == ScreenOverlayType.Game AND ShowOverlay == false.
    /// - Bars only (no labels, no % text) because labels exist in the PNG.
    /// </summary>
    public sealed class HudOverlayHandlerV2
    {
        // ----------------------------
        // DESIGN BASELINE
        // ----------------------------
        private const double DesignWidth = 2256.0;
        private const double DesignHeight = 1504.0;
        private const bool enableLogging = false;

        // You said 0.40 looked correct in-game.
        private const double DesignHudHeightRatio = GameOverlaySetup.HudHeightRatio;

        private static readonly double DesignHudHeight = DesignHeight * DesignHudHeightRatio;

        // Asset pack URI
        public string FrameAssetUri { get; set; } = "GameGraphics\\HudOverlay.png";

        // ----------------------------
        // WPF ROOTS
        // ----------------------------
        private readonly Grid _root;
        private readonly Grid _hudRoot;
        private readonly Image _frameImage;
        private readonly Canvas _canvas;

        // ----------------------------
        // HUD ELEMENTS (SLOTS)
        // ----------------------------
        private readonly Image _minimap;
        private readonly Rectangle _powerBarBackground;
        private readonly Rectangle _powerBarFill;
        private readonly Image _powerupLazerIcon;
        private readonly Image _powerupDecoyIcon;
        private readonly Image _powerupBulletIcon;
        private readonly Image _speedPowerupIcon;
        private readonly BitmapImage? _speedLevel1IconSource;
        private readonly BitmapImage? _speedLevel2IconSource;

        // Enemy remaining: icon + percentage bar (same style as alt/thr/bio)
        private readonly Image _droneIcon;
        private readonly Rectangle _droneBarFill;
        private readonly Image _seederIcon;
        private readonly Rectangle _seederBarFill;

        // Viewport indicator overlaid on the minimap
        private readonly Rectangle _viewportRect;

        // Center FPS line (numbers only; the "FPS | TRI | P" labels are in the PNG)
        private readonly TextBlock _fpsCenter;

        // Bars only (labels exist in PNG)
        private readonly Rectangle _altBarFill;
        private readonly Rectangle _thrBarFill;
        private readonly Rectangle _bioBarFill;

        // Highscore display (between center area and right panel)
        private readonly TextBlock _highscoreLabel;
        private readonly TextBlock _highscoreValue;

        // ----------------------------
        // SLOT LAYOUT (DESIGN COORDS)
        // Tune these numbers to match your PNG precisely.
        // ----------------------------

        // Minimap window inside the frame
        // NOTE: 72x72 is what you want long-term, but your current PNG window is larger.
        // You can keep this large window for now, and later switch to 72x72 when the PNG window is updated.
        //
        // If you want it sharper WITHOUT changing GameHelpers right now:
        // - make the minimap VIEW a bit smaller than the frame-window (less stretch)
        // - and let it sit centered inside the window
        //
        // Current values match what you showed (map fits). We keep it stable.
        private const double MiniMapX = 160;
        private const double MiniMapY = 100;
        private const double MiniMapW = 500;
        private const double MiniMapH = 210;

        // FPS line — adjusted from your earlier values to sit inside the top middle label slot
        private const double FpsX = 870;
        private const double FpsY = 28;
        // Ship power (health) vertical bar — replaces the old active-powerup icon slot
        private const double PowerBarX = 1314;
        private const double PowerBarY = 22;
        private const double PowerBarW = 18;
        private const double PowerBarH = 35;

        private const double PowerupBulletX = 845;
        private const double PowerupDecoyX = 945;
        private const double PowerupLazerX = 1045;
        private const double SpeedPowerupX = 1145;
        private const double PowerupRowY = 85;
        private const double PowerupIconSize = 48;

        // Enemy bars (icon on left, bar on right — below the powerup row)
        // Icon size and bar height match the existing HUD elements (48px icons, 16px bars).
        // Bar width fills from after the icon to the right edge of the center indentation.
        private const double EnemyIconSize = 75;
        private const double EnemyBarOffsetX = EnemyIconSize + 8; // bar starts just right of the icon
        private const double EnemyBarW = (RightPanelX - DroneRowX - EnemyBarOffsetX) * 0.60;
        private const double EnemyBarH = BarH;
        private const double DroneRowX = 825;
        private const double DroneRowY = 145;
        private const double SeederRowX = 825;
        private const double SeederRowY = DroneRowY + EnemyIconSize + 8;

        // Right panel bar fills (positions adjusted down to align with "track" lines in PNG)
        private const double RightPanelX = 1665;

        private const double RightAltY = 127;
        private const double RightThrY = 208;
        private const double RightBioY = 287;

        private const double RightBarX = RightPanelX + 40;

        // Bar dimensions (must match the track width inside PNG)
        private const double BarW = 400;   // slightly wider than before; adjust if your PNG track is shorter/longer
        private const double BarH = 16;

        // Highscore text (centered between center area and right panel)
        private const double HighscoreX = 1440;
        private const double HighscoreY = 130;

        // Visual style for bar fill
        private static readonly Brush BarFillBrush = new SolidColorBrush(Color.FromArgb(220, 0, 255, 255));

        public HudOverlayHandlerV2(Grid root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            _hudRoot = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_hudRoot, int.MaxValue - 250);

            _frameImage = new Image
            {
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 1.0
            };

            _canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };

            // ----- Minimap -----
            _minimap = new Image
            {
                Width = MiniMapW,
                Height = MiniMapH,
                Opacity = 0.90,
                Stretch = Stretch.UniformToFill // keeps it visually tighter / less “smeared”
            };
            Canvas.SetLeft(_minimap, MiniMapX);
            Canvas.SetTop(_minimap, MiniMapY);

            // Viewport indicator — semi-transparent cyan outline on top of the minimap
            _viewportRect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 255, 255)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_viewportRect, 1000);

            // ----- Center FPS / TRI / P -----
            _fpsCenter = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 22,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(_fpsCenter, FpsX);
            Canvas.SetTop(_fpsCenter, FpsY);

            // ----- Ship power (health) vertical bar -----
            _powerBarBackground = new Rectangle
            {
                Width = PowerBarW,
                Height = PowerBarH,
                Fill = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Opacity = 0.5
            };
            Canvas.SetLeft(_powerBarBackground, PowerBarX);
            Canvas.SetTop(_powerBarBackground, PowerBarY);

            _powerBarFill = new Rectangle
            {
                Width = PowerBarW,
                Height = PowerBarH,
                Fill = Brushes.LimeGreen,
                Opacity = 0.9
            };
            Canvas.SetLeft(_powerBarFill, PowerBarX);
            Canvas.SetTop(_powerBarFill, PowerBarY);

            _powerupLazerIcon = CreatePowerupIcon(PowerupLazerX, PowerupRowY, PowerupIconSize);
            _powerupDecoyIcon = CreatePowerupIcon(PowerupDecoyX, PowerupRowY, PowerupIconSize);
            _powerupBulletIcon = CreatePowerupIcon(PowerupBulletX, PowerupRowY, PowerupIconSize);

            _powerupLazerIcon.Source = TryLoadBitmapImage("GameGraphics\\laser_icon_48.png");
            _powerupDecoyIcon.Source = TryLoadBitmapImage("GameGraphics\\decoy_icon_48.png");
            _powerupBulletIcon.Source = TryLoadBitmapImage("GameGraphics\\bullet_icon_48.png");
            _speedLevel1IconSource = TryLoadBitmapImage("GameGraphics\\speed_2_icon_48.png");
            _speedLevel2IconSource = TryLoadBitmapImage("GameGraphics\\speed_3_icon_48.png");
            _speedPowerupIcon = CreatePowerupIcon(SpeedPowerupX, PowerupRowY, PowerupIconSize);
            _speedPowerupIcon.Visibility = Visibility.Collapsed;

            // ----- Enemy icon + bar -----
            _droneIcon = CreatePowerupIcon(DroneRowX, DroneRowY, EnemyIconSize);
            _droneIcon.Source = TryLoadBitmapImage("GameGraphics\\drone_icon_48.png");
            _droneBarFill = CreateBarFill(DroneRowX + EnemyBarOffsetX, DroneRowY + (EnemyIconSize - EnemyBarH) / 2, EnemyBarW, EnemyBarH);

            _seederIcon = CreatePowerupIcon(SeederRowX, SeederRowY, EnemyIconSize);
            _seederIcon.Source = TryLoadBitmapImage("GameGraphics\\seeder_icon_48.png");
            _seederBarFill = CreateBarFill(SeederRowX + EnemyBarOffsetX, SeederRowY + (EnemyIconSize - EnemyBarH) / 2, EnemyBarW, EnemyBarH);

            // ----- Bars only -----
            _altBarFill = CreateBarFill(RightBarX, RightAltY);
            _thrBarFill = CreateBarFill(RightBarX, RightThrY);
            _bioBarFill = CreateBarFill(RightBarX, RightBioY);

            // ----- Highscore -----
            _highscoreLabel = new TextBlock
            {
                Text = "HIGHSCORE",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 22,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(_highscoreLabel, HighscoreX);
            Canvas.SetTop(_highscoreLabel, HighscoreY);

            _highscoreValue = new TextBlock
            {
                Text = "0",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 28,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 0, 255, 255)),
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(_highscoreValue, HighscoreX);
            Canvas.SetTop(_highscoreValue, HighscoreY + 30);

            // Compose
            _hudRoot.Children.Add(_frameImage);
            _hudRoot.Children.Add(_canvas);

            _canvas.Children.Add(_minimap);
            _canvas.Children.Add(_viewportRect);
            _canvas.Children.Add(_fpsCenter);
            _canvas.Children.Add(_powerBarBackground);
            _canvas.Children.Add(_powerBarFill);
            _canvas.Children.Add(_powerupLazerIcon);
            _canvas.Children.Add(_powerupDecoyIcon);
            _canvas.Children.Add(_powerupBulletIcon);
            _canvas.Children.Add(_speedPowerupIcon);

            _canvas.Children.Add(_altBarFill);
            _canvas.Children.Add(_thrBarFill);
            _canvas.Children.Add(_bioBarFill);

            _canvas.Children.Add(_highscoreLabel);
            _canvas.Children.Add(_highscoreValue);

            _canvas.Children.Add(_droneIcon);
            _canvas.Children.Add(_droneBarFill);
            _canvas.Children.Add(_seederIcon);
            _canvas.Children.Add(_seederBarFill);

            _root.Children.Add(_hudRoot);

            TryLoadFrameImage();
        }

        /// <summary>
        /// For existing helpers: lets MainWindow keep using GameHelpers.UpdateMapOverlay(...)
        /// </summary>
        public Image GetMinimapImage() => _minimap;

        /// <summary>
        /// Update HUD values & scaling.
        /// - overlayState determines visibility (Game only, and hidden while other overlay is visible).
        /// - gameplay provides infection level.
        /// - altitude/thrust are passed in since they may live on Ship/Controls.
        /// </summary>
        public void Update(
            ScreenOverlayState overlayState,
            GamePlayState gameplay,
            double screenWidth,
            double screenHeight,
            int fps,
            int triangles)
        {
            if (overlayState == null || gameplay == null)
            {
                _hudRoot.Visibility = Visibility.Collapsed;
                return;
            }

            // Hide HUD when cinematic/modal overlay is visible
            if (GameState.ScreenOverlayState.Type != ScreenOverlayType.Game)
            {
                _hudRoot.Visibility = Visibility.Collapsed;
                return;
            }

            if (screenWidth <= 0) screenWidth = DesignWidth;
            if (screenHeight <= 0) screenHeight = DesignHeight;

            _hudRoot.Visibility = Visibility.Visible;

            // Uniform scale constrained by both width and desired HUD height
            double targetHudHeight = screenHeight * DesignHudHeightRatio;

            double scaleX = screenWidth / DesignWidth;
            double scaleY = targetHudHeight / DesignHudHeight;

            double scale = Math.Min(scaleX, scaleY);

            _hudRoot.LayoutTransform = new ScaleTransform(scale, scale);

            // Logical size (design-space)
            _hudRoot.Width = DesignWidth;
            _hudRoot.Height = DesignHudHeight;

            _frameImage.Width = DesignWidth;
            _frameImage.Height = DesignHudHeight;

            _canvas.Width = DesignWidth;
            _canvas.Height = DesignHudHeight;

            // Center line – numbers only (labels are in PNG)
            var activePowerup = string.IsNullOrWhiteSpace(gameplay.ActivePowerup) ? "LAZER" : gameplay.ActivePowerup.ToUpperInvariant();

            _fpsCenter.Text = $"{fps}                       {triangles}";

            _powerupBulletIcon.Opacity = activePowerup == "BULLET" ? 1.0 : 0.45;
            _powerupDecoyIcon.Opacity = !gameplay.IsDecoyUnlocked ? 0.15
                : activePowerup == "DECOY" ? 1.0 : 0.45;
            _powerupLazerIcon.Opacity = !gameplay.IsLazerUnlocked ? 0.15
                : activePowerup == "LAZER" ? 1.0 : 0.45;
            UpdateSpeedPowerupIcon(gameplay.SpeedPowerUpLevel);

            // Ship power (health) vertical bar: fills bottom-to-top, green→red
            UpdatePowerBar(gameplay);

            SetBarFill(_altBarFill, gameplay.Alt);

            SetBarFill(_thrBarFill, gameplay.Thrust/10);

            SetBarFill(_bioBarFill, CalculateInfectionMeterFill(gameplay));

            // Enemy remaining bars (percentage of initial count)
            float dronePct = gameplay.InitialDrones > 0
                ? (float)gameplay.DronesRemaining / gameplay.InitialDrones
                : 0f;
            float seederPct = gameplay.InitialSeeders > 0
                ? (float)gameplay.SeedersRemaining / gameplay.InitialSeeders
                : 0f;
            SetBarFill(_droneBarFill, dronePct, EnemyBarW);
            SetBarFill(_seederBarFill, seederPct, EnemyBarW);

            _highscoreValue.Text = gameplay.Score.ToString("N0");

            UpdateViewportRect();
        }

        public void ReloadFrame() => TryLoadFrameImage();

        public static float CalculateInfectionMeterFill(GamePlayState gameplay)
        {
            return gameplay == null ? 0f : Clamp01(gameplay.InfectionCriticalProgress);
        }

        /// <summary>
        /// Positions and sizes the viewport rectangle overlay on the minimap.
        /// The minimap shows a cropped portion of the full bitmap.
        /// The viewport (18×18 tiles) is centered on the ship (center of the crop).
        /// </summary>
        private void UpdateViewportRect()
        {
            int cropW = CommonUtilities.CommonSetup.MapSetup.bitmapSize * 2; // 144
            int cropH = CommonUtilities.CommonSetup.MapSetup.bitmapSize;     // 72
            int vpTiles = CommonUtilities.CommonSetup.SurfaceSetup.viewPortSize; // 18

            // The viewport is centered in the crop (ship is at crop center)
            double vpBitmapX = (cropW - vpTiles) / 2.0;
            double vpBitmapZ = (cropH - vpTiles) / 2.0;

            double scaleX = MiniMapW / cropW;
            double scaleZ = MiniMapH / cropH;

            _viewportRect.Width = vpTiles * scaleX;
            _viewportRect.Height = vpTiles * scaleZ;

            Canvas.SetLeft(_viewportRect, MiniMapX + vpBitmapX * scaleX);
            Canvas.SetTop(_viewportRect, MiniMapY + vpBitmapZ * scaleZ);
        }

        /// <summary>
        /// Updates the vertical ship power bar.
        /// Height scales with health percentage (bottom-to-top fill).
        /// Color interpolates: green (100%) → yellow (50%) → red (0%).
        /// </summary>
        private void UpdatePowerBar(GamePlayState gameplay)
        {
            float pct = gameplay.MaxHealth > 0f
                ? Clamp01(gameplay.Health / gameplay.MaxHealth)
                : 0f;

            double fillH = PowerBarH * pct;

            _powerBarFill.Height = fillH;
            // Anchor to bottom: push the top down so the bar grows upward
            Canvas.SetTop(_powerBarFill, PowerBarY + (PowerBarH - fillH));

            // Color: green at 100%, blends to red by 0%, transition centered around 50%
            byte r, g;
            if (pct > 0.5f)
            {
                // 100%→50%: green to yellow (red ramps up 0→255)
                float t = (1f - pct) * 2f; // 0 at 100%, 1 at 50%
                r = (byte)(255 * t);
                g = 255;
            }
            else
            {
                // 50%→0%: yellow to red (green ramps down 255→0)
                float t = pct * 2f; // 1 at 50%, 0 at 0%
                r = 255;
                g = (byte)(255 * t);
            }

            _powerBarFill.Fill = new SolidColorBrush(Color.FromArgb(220, r, g, 0));
        }

        private void UpdateSpeedPowerupIcon(int speedPowerUpLevel)
        {
            _speedPowerupIcon.Visibility = speedPowerUpLevel > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            _speedPowerupIcon.Source = speedPowerUpLevel >= 2
                ? _speedLevel2IconSource
                : _speedLevel1IconSource;
            _speedPowerupIcon.Opacity = 1.0;
        }

        private static Image CreatePowerupIcon(double x, double y, double size)
        {
            var image = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Opacity = 1.0
            };

            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, y);

            return image;
        }

        private static BitmapImage? TryLoadBitmapImage(string assetPath)
        {
            try
            {
                var uri = new Uri(assetPath, UriKind.RelativeOrAbsolute);
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = uri;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void TryLoadFrameImage()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FrameAssetUri))
                {
                    _frameImage.Source = null;
                    return;
                }

                var uri = new Uri(FrameAssetUri, UriKind.RelativeOrAbsolute);

                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = uri;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();

                _frameImage.Source = img;
            }
            catch(Exception ex)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Exception loading map. {ex.Message} ");
                _frameImage.Source = null;
            }
        }

        private static Rectangle CreateBarFill(double x, double y, double maxWidth = BarW, double height = BarH)
        {
            var fill = new Rectangle
            {
                Width = 0,
                Height = height,
                Fill = BarFillBrush,
                Opacity = 0.85
            };

            Canvas.SetLeft(fill, x);
            Canvas.SetTop(fill, y);

            return fill;
        }

        private static void SetBarFill(Rectangle rect, float value01)
        {
            rect.Width = BarW * Clamp01(value01);
        }

        private static void SetBarFill(Rectangle rect, float value01, double maxWidth)
        {
            rect.Width = maxWidth * Clamp01(value01);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
