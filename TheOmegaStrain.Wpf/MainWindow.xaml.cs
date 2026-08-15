using TheOmegaStrain.Wpf.Helpers;
using TheOmegaStrain.Wpf.Input;
using TheOmegaStrain.Wpf.MainWindowClasses;
using TheOmegaStrain.Wpf.MainWindowClasses.Overlays;
using TheOmegaStrain.Wpf.Rendering;
using TheOmegaStrain.Runtime.Loops;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Steam;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TheOmegaStrain.Wpf
{
    public class DrawingVisualHost : FrameworkElement
    {
        private VisualCollection visuals;

        public DrawingVisualHost()
        {
            visuals = new VisualCollection(this);
        }

        public void AddVisual(DrawingVisual visual)
        {
            visuals.Clear();
            visuals.Add(visual);
        }

        protected override int VisualChildrenCount => visuals.Count;
        protected override Visual GetVisualChild(int index) => visuals[index];
    }

    public partial class MainWindow : Window
    {
        private const bool enableLogging = false;
        private const bool enableFileLogging = LiveGameLoop.EnableCpuHeadroomLogging;
        private const bool EnableSteamDiagnostics = true;
        private DrawingVisualHost visualHost = new DrawingVisualHost();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly Stopwatch stopwatch = new Stopwatch();
        private int frameCount = 0;

        private Grid mainGrid;
        private GameWorldManager gameWorldManager = new GameWorldManager();
        private IProjectedTriangleRenderer<ProjectedTriangleMesh> worldRenderer;

        public GameWorld world = new();
        public MediaPlayer player = new MediaPlayer();

        private bool isPaused = false;
        private int pauseFrameCount = 0;
        private const int limitFrameCount = 10;
        private const int MaxPooledTriangleLists = 4;
        private DateTime fadeOutTrigged = DateTime.MinValue;
        private int _updateInProgress = 0;
        private long _lastTickTimestamp = 0;
        private long _lastWorldUpdateTimestamp = 0;
        private int _minimapFrameSkip = 0;
        private readonly object _triangleListPoolLock = new();
        private readonly Stack<List<ProjectedTriangleMesh>> _triangleListPool = new();

        // Overlay handlers
        private OverlayManager _overlayManager;
        private MediaElement _videoOverlay;
        private string? _currentVideoClipPath;
        private bool _videoOverlayIsPlaying;
        private bool _videoEntrancePlayed;
        private bool _xboxOverlayButtonWasDown = false;
        private HwndSource? _rawMouseSource;
        private SteamManager? _steamManager;
        private SteamGameplaySync? _steamGameplaySync;

        // MotherShip health bar (in-world overlay)
        private readonly Canvas _motherShipHealthBarCanvas;
        private readonly System.Windows.Shapes.Rectangle _motherShipHealthBarBg;
        private readonly System.Windows.Shapes.Rectangle _motherShipHealthBarFill;
        private const double MotherShipBarWidth = 16;
        private const double MotherShipBarHeight = 50;
        private const double MotherShipBarOffsetX = 140;

        // MotherShip ram warning (flashing reticle)
        private readonly Canvas _ramWarningCanvas;
        private readonly System.Windows.Shapes.Ellipse _ramWarningOuter;
        private readonly System.Windows.Shapes.Ellipse _ramWarningInner;
        private const double RamWarningSize = 100;

        // Aim assist target indicator (white reticle)
        private readonly Canvas _aimAssistCanvas;
        private readonly System.Windows.Shapes.Ellipse _aimAssistOuter;
        private readonly System.Windows.Shapes.Ellipse _aimAssistInner;
        private const double AimAssistIndicatorSize = 90;

        private bool isFading = false;
        private bool _isFadingIn = false;
        private int Fps = 0;

        private static int TargetFps => ScreenSetup.RuntimeTargetFps;
        private static double TargetFrameIntervalMs => ScreenSetup.TargetFrameIntervalMs;
        private long _lastFrameTick = 0;
        private double _tickAccumulatorMs = 0;
        private int _currentDisplayRefreshHz = ScreenSetup.targetFps;

        public MainWindow()
        {
            ScreenSetup.ConfigureRuntimeTargetFps(_currentDisplayRefreshHz);

            Logger.EnableFileLogging = enableFileLogging;
            if (Logger.EnableFileLogging)
            {
                Logger.ClearLog();
                if (enableFileLogging)
                    Logger.Log($"[PerfLogging] enabled targetFps={TargetFps} targetFrameMs={TargetFrameIntervalMs:0.###} displayRefreshHz={_currentDisplayRefreshHz} source=StartupFallback");
            }
            if (EnableSteamDiagnostics)
            {
                SteamDiagnostics.Enabled = true;
                SteamDiagnostics.Clear();
                SteamDiagnostics.Write("[SteamDiag] file logging enabled.");
            }

            GameState.SurfaceState.RecordingFps = ScreenSetup.RuntimeTargetFps;

            PersistenceSetup.Initialize();
            GameState.GamePlayState.PlayerName = PersistenceSetup.LoadLastPlayerName();
            GameState.EventBus = world.EventBus;
            InitializeSteam();

            InitializeComponent();
            this.PreviewKeyDown += new KeyEventHandler(HandleKeys);
            this.PreviewMouseDown += HandleMouseInputForOverlay;
            SourceInitialized += (_, _) =>
            {
                ConfigureRuntimeFpsForCurrentMonitor("SourceInitialized");
                InitializeRawMouseInput();
            };
            LocationChanged += (_, _) => ConfigureRuntimeFpsForCurrentMonitor("LocationChanged");
            Closing += MainWindow_Closing;
            Loaded += Window_Loaded;

            mainGrid = new Grid();
            Content = mainGrid;

            mainGrid.Children.Add(visualHost);
            worldRenderer = new WorldRenderer(visualHost);

            _videoOverlay = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                IsMuted = true,
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _videoOverlay.MediaEnded += VideoOverlayOnMediaEnded;
            _videoOverlay.RenderTransform = new TranslateTransform(0, 0);
            _videoOverlay.RenderTransformOrigin = new Point(0.5, 0.5);
            Panel.SetZIndex(_videoOverlay, 1);
            mainGrid.Children.Add(_videoOverlay);

            FadeOverlay = new System.Windows.Shapes.Rectangle
            {
                Fill = Brushes.Black,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Panel.SetZIndex(FadeOverlay, int.MaxValue);
            mainGrid.Children.Add(FadeOverlay);

            FpsText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 20,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            mainGrid.Children.Add(FpsText);


            // Overlay handlers must be put in the grid
            _overlayManager = new OverlayManager(mainGrid);

            // MotherShip in-world health bar
            _motherShipHealthBarCanvas = new Canvas
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(_motherShipHealthBarCanvas, 10);

            _motherShipHealthBarBg = new System.Windows.Shapes.Rectangle
            {
                Width = MotherShipBarWidth,
                Height = MotherShipBarHeight,
                Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 0, 255, 255)),
                StrokeThickness = 2
            };

            _motherShipHealthBarFill = new System.Windows.Shapes.Rectangle
            {
                Width = MotherShipBarWidth - 2,
                Height = MotherShipBarHeight - 2,
                Fill = Brushes.LimeGreen,
                Opacity = 0.9
            };

            _motherShipHealthBarCanvas.Children.Add(_motherShipHealthBarBg);
            _motherShipHealthBarCanvas.Children.Add(_motherShipHealthBarFill);
            mainGrid.Children.Add(_motherShipHealthBarCanvas);

            // MotherShip ram warning reticle
            _ramWarningCanvas = new Canvas
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(_ramWarningCanvas, 10);

            _ramWarningOuter = new System.Windows.Shapes.Ellipse
            {
                Width = RamWarningSize,
                Height = RamWarningSize,
                Fill = System.Windows.Media.Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 60, 30)),
                StrokeThickness = 3
            };

            _ramWarningInner = new System.Windows.Shapes.Ellipse
            {
                Width = RamWarningSize * 0.5,
                Height = RamWarningSize * 0.5,
                Fill = new SolidColorBrush(Color.FromArgb(80, 255, 40, 20)),
                Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 80, 40)),
                StrokeThickness = 2
            };

            _ramWarningCanvas.Children.Add(_ramWarningOuter);
            _ramWarningCanvas.Children.Add(_ramWarningInner);
            mainGrid.Children.Add(_ramWarningCanvas);

            // Aim assist target indicator (white concentric circles)
            _aimAssistCanvas = new Canvas
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(_aimAssistCanvas, 10);

            _aimAssistOuter = new System.Windows.Shapes.Ellipse
            {
                Width = AimAssistIndicatorSize,
                Height = AimAssistIndicatorSize,
                Fill = System.Windows.Media.Brushes.Transparent,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                StrokeThickness = 3
            };

            _aimAssistInner = new System.Windows.Shapes.Ellipse
            {
                Width = AimAssistIndicatorSize * 0.5,
                Height = AimAssistIndicatorSize * 0.5,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                StrokeThickness = 2
            };

            _aimAssistCanvas.Children.Add(_aimAssistOuter);
            _aimAssistCanvas.Children.Add(_aimAssistInner);
            mainGrid.Children.Add(_aimAssistCanvas);

            timer.Interval = TimeSpan.FromMilliseconds(8);
            CompositionTarget.Rendering += Handle3dWorldRendering;
            stopwatch.Start();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            ShutdownRawMouseInput();
            ShutdownSteam();
            // Closing the window is not a checkpoint. Progress and highscores
            // are persisted by checkpoint flows: powerups and motherships.
        }

        private void InitializeSteam()
        {
            _steamManager = new SteamManager();

            LogSteamDiagnostics("BeforeInitialize");

            if (!_steamManager.Initialize(SteamGameConfig.RuntimeAppId))
            {
                if (Logger.EnableFileLogging)
                    Logger.Log($"[Steam] unavailable: {_steamManager.LastError ?? "SteamAPI.Init returned false"}");
                SteamDiagnostics.Write($"[Steam] unavailable: {_steamManager.LastError ?? "SteamAPI.Init returned false"}");
                LogSteamDiagnostics("InitializeFailed");
                return;
            }

            _steamGameplaySync = new SteamGameplaySync(
                _steamManager,
                world.EventBus,
                CreateSteamGameplaySnapshot);

            if (Logger.EnableFileLogging)
                Logger.Log($"[Steam] initialized appId={_steamManager.AppId} steamId={_steamManager.SteamId} loggedOn={_steamManager.IsLoggedOn}");
            LogSteamDiagnostics("Initialized");
        }

        private void ShutdownSteam()
        {
            _steamGameplaySync?.Dispose();
            _steamGameplaySync = null;
            _steamManager?.Dispose();
            _steamManager = null;
        }

        private static SteamGameplaySnapshot CreateSteamGameplaySnapshot()
        {
            var gameplay = GameState.GamePlayState;
            return new SteamGameplaySnapshot(
                gameplay.CurrentSceneType,
                gameplay.SceneIndex,
                gameplay.Score,
                gameplay.TotalKills,
                gameplay.TotalShotsFired,
                gameplay.TotalDeaths,
                gameplay.Accuracy,
                gameplay.PowerUpsCollected,
                gameplay.SpeedPowerUpLevel,
                TutorialProgressService.HasCompletedTutorial(gameplay.PlayerName));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureRuntimeFpsForCurrentMonitor("Loaded");

            int w = (int)ActualWidth;
            int h = (int)ActualHeight;
            if (w > 0 && h > 0)
                ScreenSetup.Initialize(w, h);
        }

        private void InitializeRawMouseInput()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            _rawMouseSource = HwndSource.FromHwnd(hwnd);
            if (_rawMouseSource == null)
                return;

            _rawMouseSource.AddHook(RawMouseWndProc);
            if (!RawMouseInput.RegisterMouse(hwnd))
            {
                _rawMouseSource.RemoveHook(RawMouseWndProc);
                _rawMouseSource = null;
            }
        }

        private void ShutdownRawMouseInput()
        {
            _rawMouseSource?.RemoveHook(RawMouseWndProc);
            _rawMouseSource = null;
            RawMouseInput.ClearRegistration();
        }

        private IntPtr RawMouseWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == RawMouseInput.WmInput &&
                RawMouseInput.TryReadMouseDelta(lParam, out int deltaX, out int deltaY))
            {
                DispatchRawMouseDelta(deltaX, deltaY);
            }

            return IntPtr.Zero;
        }

        private void DispatchRawMouseDelta(int deltaX, int deltaY)
        {
            var settings = GameState.SettingsState;
            settings.Normalize();

            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
                return;

            if (TryGetActiveShipControls(out var shipControls, out _))
                shipControls.HandleRawMouseDelta(deltaX, deltaY);
        }

        private void HandleKeys(object sender, KeyEventArgs e)
        {
            bool overlayWasShowing = GameState.ScreenOverlayState.ShowOverlay;

            if (IsSteamOverlayShortcut(e))
            {
                LogSteamDiagnostics("ShiftTabReachedGame");
                return;
            }

            if (IsMenuExitKey(e.Key))
            {
                var sceneTypeBeforeMenuExit = world?.SceneHandler?.GetActiveScene().SceneType;
                if (ShouldShutdownFromMenuExitKey(e.Key))
                {
                    Application.Current.Shutdown();
                    e.Handled = true;
                    return;
                }

                world.SceneHandler.HandleKeyPress(WpfGameInputKeyMapper.ToGameInputKey(e.Key), world);
                StopNonMusicAudioIfReturnedToIntro(sceneTypeBeforeMenuExit);
                SyncLocalPauseFromWorld();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (IsGameplaySceneForPause() &&
                    !GameState.GamePlayState.IsVictoryRewardPauseActive &&
                    !GameState.ScreenOverlayState.BlocksGameplayInput)
                    ToggleGameplayPause();
                e.Handled = true;
                return;
            }
            //Send keys to Scenehandler to handle scene switches and overlay switches
            world.SceneHandler.HandleKeyPress(WpfGameInputKeyMapper.ToGameInputKey(e.Key), world);
            SyncLocalPauseFromWorld();

            if (overlayWasShowing || GameState.ScreenOverlayState.BlocksGameplayInput)
                e.Handled = true;
        }

        private static bool IsSteamOverlayShortcut(KeyEventArgs e)
        {
            return e.Key == Key.Tab &&
                   (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        }

        private void LogSteamDiagnostics(string source)
        {
            if (!EnableSteamDiagnostics)
                return;

            string baseAppIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");
            string cwdAppIdPath = Path.Combine(Environment.CurrentDirectory, "steam_appid.txt");
            string appIdText = ReadAppIdText(baseAppIdPath, cwdAppIdPath);

            SteamDiagnostics.Write(
                $"[SteamDiag] source={source} " +
                $"runtimeAppId={SteamGameConfig.RuntimeAppId} " +
                $"baseDir='{AppContext.BaseDirectory}' " +
                $"cwd='{Environment.CurrentDirectory}' " +
                $"baseAppIdExists={File.Exists(baseAppIdPath)} " +
                $"cwdAppIdExists={File.Exists(cwdAppIdPath)} " +
                $"appIdText='{appIdText}' " +
                $"steamManagerCreated={_steamManager != null} " +
                $"steamRunning={_steamManager?.IsSteamRunning.ToString() ?? "n/a"} " +
                $"initialized={_steamManager?.IsInitialized.ToString() ?? "n/a"} " +
                $"appId={_steamManager?.AppId.ToString() ?? "n/a"} " +
                $"steamId={_steamManager?.SteamId.ToString() ?? "n/a"} " +
                $"loggedOn={_steamManager?.IsLoggedOn.ToString() ?? "n/a"} " +
                $"overlayEnabled={_steamManager?.IsOverlayEnabled.ToString() ?? "n/a"} " +
                $"lastError='{_steamManager?.LastError ?? ""}'");
        }

        private static string ReadAppIdText(params string[] paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path))
                        return File.ReadAllText(path).Trim();
                }
                catch
                {
                    return "<read failed>";
                }
            }

            return "";
        }

        private void HandleMouseInputForOverlay(object sender, MouseButtonEventArgs e)
        {
            var settings = GameState.SettingsState;
            settings.Normalize();

            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
                return;

            if (!CanNonKeyboardInputActivateOverlay())
                return;

            HandleNonKeyboardOverlayActivation();
            e.Handled = true;
        }

        private void UpdateXboxOverlayActivation()
        {
            var settings = GameState.SettingsState;
            settings.Normalize();

            if (settings.ActiveControlScheme != ControlInputMode.XboxController ||
                !CanNonKeyboardInputActivateOverlay())
            {
                _xboxOverlayButtonWasDown = false;
                return;
            }

            bool buttonIsDown =
                XboxControllerInput.TryGetState(controllerIndex: 0, out var controllerState) &&
                XboxControllerInput.HasButtonInput(controllerState);

            if (buttonIsDown && !_xboxOverlayButtonWasDown)
                HandleNonKeyboardOverlayActivation();

            _xboxOverlayButtonWasDown = buttonIsDown;
        }

        private bool CanNonKeyboardInputActivateOverlay()
        {
            var overlay = GameState.ScreenOverlayState;
            if (overlay == null || !overlay.ShowOverlay)
                return false;

            if (overlay.Type == ScreenOverlayType.NameEntry)
                return false;

            if (overlay.ChoiceAction != ScreenOverlayChoiceAction.None)
                return false;

            if (!overlay.CanDismissWithInput)
                return false;

            return true;
        }

        private void HandleNonKeyboardOverlayActivation()
        {
            var sceneTypeBeforeMenuExit = world?.SceneHandler?.GetActiveScene().SceneType;
            world.SceneHandler.HandleOverlayActivation(world);
            StopNonMusicAudioIfReturnedToIntro(sceneTypeBeforeMenuExit);
            SyncLocalPauseFromWorld();
        }

        private void StopNonMusicAudioIfReturnedToIntro(SceneTypes? previousSceneType)
        {
            if (previousSceneType == null || previousSceneType == SceneTypes.Intro)
                return;

            if (world?.SceneHandler?.GetActiveScene().SceneType == SceneTypes.Intro)
                gameWorldManager.StopNonMusicAudio();
        }

        private bool ShouldShutdownFromMenuExitKey(Key key)
        {
            var overlay = GameState.ScreenOverlayState;
            if (overlay.Type == ScreenOverlayType.NameEntry && overlay.ShowOverlay)
                return false;

            if (overlay.Type == ScreenOverlayType.Settings && overlay.ShowOverlay)
                return false;

            if (world?.SceneHandler?.GetActiveScene().SceneType != SceneTypes.Intro)
                return false;

            return IsMenuExitKey(key);
        }

        private static bool IsMenuExitKey(Key key) => key == Key.Escape || key == Key.X;

        private void SyncLocalPauseFromWorld()
        {
            isPaused = world.IsPaused;
            if (!isPaused)
                pauseFrameCount = 0;
        }

        private void ToggleGameplayPause()
        {
            bool shouldPause = !world.IsPaused;
            world.IsPaused = shouldPause;
            isPaused = shouldPause;
            pauseFrameCount = shouldPause ? limitFrameCount : 0;

            if (IsGameplaySceneForPause())
                GameState.GamePlayState.Phase = shouldPause ? GamePhase.Paused : GamePhase.Playing;

            if (shouldPause)
                ClearShipGameplayInputForPause();
            else
                ResumeShipAfterGameplayPause();
        }

        private static bool IsGameplaySceneForPause()
        {
            var sceneType = GameState.GamePlayState.CurrentSceneType;
            return sceneType == SceneTypes.Game ||
                   sceneType == SceneTypes.Simulation ||
                   sceneType == SceneTypes.Tutorial;
        }

        private void ClearShipGameplayInputForPause()
        {
            if (world?.WorldInhabitants == null)
                return;

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                var obj = world.WorldInhabitants[i];
                if (obj.ObjectName == "Ship" && obj.Movement is ShipControls shipControls)
                {
                    shipControls.ClearGameplayInputForPause();
                    return;
                }
            }
        }

        private void ResumeShipAfterGameplayPause()
        {
            if (TryGetActiveShipControls(out var shipControls, out var ship))
                shipControls.ResumeFromGameplayPause(ship);
        }

        private bool TryGetActiveShipControls(out ShipControls shipControls, out I3dObject ship)
        {
            shipControls = null!;
            ship = null!;

            if (world?.WorldInhabitants == null)
                return false;

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                var obj = world.WorldInhabitants[i];
                if (obj.ObjectName == "Ship" && obj.Movement is ShipControls controls)
                {
                    shipControls = controls;
                    ship = obj;
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveSurfaceMapForMinimap()
        {
            var surfaceState = GameState.SurfaceState;
            return GameState.ScreenOverlayState.Type == ScreenOverlayType.Game &&
                   surfaceState?.GlobalMapPixels != null &&
                   surfaceState.GlobalMapPosition != null &&
                   surfaceState.SurfaceViewportObject != null;
        }

        public async Task FadeOutAsync(float durationSeconds = 1.0f)
        {
            FadeOverlay.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FillBehavior = FillBehavior.HoldEnd
            };

            var tcs = new TaskCompletionSource<bool>();
            animation.Completed += (s, e) => tcs.SetResult(true);
            FadeOverlay.BeginAnimation(UIElement.OpacityProperty, animation);

            await tcs.Task;
        }

        public async Task FadeInAsync(float durationSeconds = 1.0f)
        {
            FadeOverlay.Visibility = Visibility.Visible;
            FadeOverlay.Opacity = 1;

            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                FillBehavior = FillBehavior.Stop
            };

            var tcs = new TaskCompletionSource<bool>();
            animation.Completed += (s, e) =>
            {
                FadeOverlay.Visibility = Visibility.Collapsed;
                FadeOverlay.Opacity = 0;
                tcs.SetResult(true);
            };

            FadeOverlay.BeginAnimation(UIElement.OpacityProperty, animation);

            await tcs.Task;
        }

        private void Handle3dWorldRendering(object? sender, EventArgs e)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            if (_lastFrameTick == 0)
            {
                _lastFrameTick = nowTicks;
                return;
            }

            var elapsedMs = (nowTicks - _lastFrameTick) * 1000.0 / Stopwatch.Frequency;
            _lastFrameTick = nowTicks;
            _tickAccumulatorMs += elapsedMs;

            if (_tickAccumulatorMs < TargetFrameIntervalMs)
                return;

            int steps = (int)Math.Floor(_tickAccumulatorMs / TargetFrameIntervalMs);
            if (steps > 3) steps = 3;

            _tickAccumulatorMs -= steps * TargetFrameIntervalMs;

            for (int i = 0; i < steps; i++)
            {
                Handle3dWorld(TargetFrameIntervalMs / 1000.0);
            }
        }

        private void ConfigureRuntimeFpsForCurrentMonitor(string source)
        {
            int displayRefreshHz = GetDisplayRefreshHzForWindow();
            int previousTargetFps = ScreenSetup.RuntimeTargetFps;
            int previousRefreshHz = _currentDisplayRefreshHz;

            ScreenSetup.ConfigureRuntimeTargetFps(displayRefreshHz);
            GameState.SurfaceState.RecordingFps = ScreenSetup.RuntimeTargetFps;
            _currentDisplayRefreshHz = displayRefreshHz;

            if (enableFileLogging &&
                (previousTargetFps != ScreenSetup.RuntimeTargetFps || previousRefreshHz != displayRefreshHz))
            {
                Logger.Log($"[PerfLogging] enabled targetFps={TargetFps} targetFrameMs={TargetFrameIntervalMs:0.###} displayRefreshHz={displayRefreshHz} source={source}");
            }
        }

        private int GetDisplayRefreshHzForWindow()
        {
            var handle = new WindowInteropHelper(this).Handle;
            string? deviceName = null;

            if (handle != IntPtr.Zero)
            {
                var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MonitorInfoEx();
                    monitorInfo.cbSize = Marshal.SizeOf<MonitorInfoEx>();
                    if (GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        deviceName = monitorInfo.szDevice;
                    }
                }
            }

            var mode = new DevMode();
            mode.dmSize = (short)Marshal.SizeOf<DevMode>();

            return EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode)
                ? mode.dmDisplayFrequency
                : ScreenSetup.targetFps;
        }

        private const int EnumCurrentSettings = -1;
        private const uint MonitorDefaultToNearest = 2;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        private async void Handle3dWorld(double dtSeconds)
        {
            if (Logger.ShouldLog(enableLogging))
            {
                var nowTicks = Stopwatch.GetTimestamp();
                if (_lastTickTimestamp != 0)
                {
                    var tickMs = (nowTicks - _lastTickTimestamp) * 1000.0 / Stopwatch.Frequency;
                    Logger.Log($"[Tick] dtMs={tickMs:0.###}");
                }
                _lastTickTimestamp = nowTicks;
            }

            float dt = (float)dtSeconds;
            _steamGameplaySync?.Update();
            SynchronizeTutorialOverlayPause();
            UpdateXboxOverlayActivation();

            if (GameState.ScreenOverlayState.ShowDebugOverlay == false)
                FpsText.Visibility = Visibility.Collapsed;
            else
                FpsText.Visibility = Visibility.Visible;

            if (world.IsPaused)
                pauseFrameCount++;

            if (GameState.GamePlayState.IsVictoryRewardPauseActive && world.IsPaused)
            {
                isPaused = true;
                pauseFrameCount = limitFrameCount;
            }

            if (pauseFrameCount < limitFrameCount && GameState.WorldFade.TryBeginFadeIn(out var fadeInDurationSeconds))
            {
                if (!isFading)
                {
                    // FadeOut was skipped (e.g., ship exploded before the fadeout delay elapsed)
                    // Snap overlay to opaque before fading in
                    FadeOverlay.Visibility = Visibility.Visible;
                    FadeOverlay.Opacity = 1;
                    isFading = true;
                }
                _isFadingIn = true;
                await FadeInAsync(fadeInDurationSeconds);
                _isFadingIn = false;
                isFading = false;
                GameState.WorldFade.MarkFadeInComplete();
                fadeOutTrigged = DateTime.MinValue;
            }

            if (pauseFrameCount <= limitFrameCount && !isFading && GameState.WorldFade.TryBeginFadeOut(out var fadeOutDurationSeconds))
            {
                isFading = true;
                await FadeOutAsync(fadeOutDurationSeconds);
                GameState.WorldFade.MarkFadeOutComplete();
                fadeOutTrigged = DateTime.MinValue;
            }

            if (GameState.GamePlayState.IsVictoryRewardPauseActive)
            {
                gameWorldManager.UpdatePausedVictoryReward(world);
                SyncLocalPauseFromWorld();
            }

            // Overlay update + render (UI layer)
            // FadeOverlay (Z=MaxValue) covers everything during fade,
            // so overlay state progresses naturally — no need to suppress ShowOverlay
            if (GameState.ScreenOverlayState != null)
            {
                GameState.ScreenOverlayState.Update(dt);

                UpdateVideoOverlay();

                double w = ActualWidth > 0 ? ActualWidth : Width;
                double h = ActualHeight > 0 ? ActualHeight : Height;
                if (w <= 0) w = 1920;
                if (h <= 0) h = 1080;

                var gameplay = GameState.GamePlayState;
                int triangles = worldRenderer.GetRenderingTriangleCount();

                _overlayManager.Update(GameState.ScreenOverlayState, gameplay, w, h, Fps, triangles);

                // MotherShip in-world health bar
                UpdateMotherShipHealthBar(gameplay);

                // MotherShip ram warning reticle
                UpdateMotherShipRamWarning(gameplay);

                // Aim assist target indicator
                UpdateAimAssistIndicator(gameplay);

            }

            world.SceneHandler.UpdateFrame(world);

            frameCount++;
            if (stopwatch.ElapsedMilliseconds >= 1000)
            {
                Fps = frameCount;
                FpsText.Text = $"FPS: {frameCount} Triangles:{worldRenderer.GetRenderingTriangleCount()} {gameWorldManager.DebugMessage}";
                frameCount = 0;
                stopwatch.Restart();
            }

            // Minimap -> HUD slot only
            if (!isFading && HasActiveSurfaceMapForMinimap() && ++_minimapFrameSkip >= 3)
            {
                _minimapFrameSkip = 0;
                if (GameState.SurfaceState.GlobalMapPosition != null)
                {
                    GameHelpers.UpdateDirtyTilesInMap(GameState.SurfaceState.GlobalMapPixels);

                    // Crop the source bitmap, draw markers on the copy, display it.
                    // Markers never touch the source bitmap — no save/restore needed.
                    GameHelpers.UpdateMapOverlayWithMarkers(
                        _overlayManager.GetMinimapImage(),
                        GameState.SurfaceState.GlobalMapPixels,
                        Convert.ToInt32(GameState.SurfaceState.GlobalMapPosition.x),
                        Convert.ToInt32(GameState.SurfaceState.GlobalMapPosition.z)
                    );
                }
            }

            if (pauseFrameCount >= limitFrameCount)
            {
                _lastWorldUpdateTimestamp = 0;
                return;
            }

            if (System.Threading.Interlocked.Exchange(ref _updateInProgress, 1) == 1)
                return;

            GameState.DeltaTime = CaptureWorldUpdateDeltaTime(dt);

            _ = Task.Run(() =>
            {
                bool shouldLog = Logger.ShouldLog(enableLogging);
                long startTicks = shouldLog ? Stopwatch.GetTimestamp() : 0;
                var screenCoordinates = RentTriangleList();
                var crashBoxCoordinates = RentTriangleList();
                bool handedToDispatcher = false;

                void ReturnLists()
                {
                    ReturnTriangleList(screenCoordinates);
                    ReturnTriangleList(crashBoxCoordinates);
                }

                try
                {
                    gameWorldManager.UpdateWorld(world, ref screenCoordinates, ref crashBoxCoordinates);
                    bool freezeFrameActivated =
                        GameState.GamePlayState.IsVictoryRewardPauseActive &&
                        world.IsPaused;

                    if (shouldLog)
                    {
                        var elapsedMs = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
                        Logger.Log($"[UpdateWorld] ms={elapsedMs:0.###}");
                    }

                    if ((!isFading || _isFadingIn) && !freezeFrameActivated)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                worldRenderer.RenderTriangles(screenCoordinates);
                            }
                            finally
                            {
                                ReturnLists();
                            }
                        });
                        handedToDispatcher = true;
                    }
                }
                finally
                {
                    if (!handedToDispatcher)
                    {
                        ReturnLists();
                    }

                    System.Threading.Interlocked.Exchange(ref _updateInProgress, 0);
                }
            });
        }

        private float CaptureWorldUpdateDeltaTime(float fallbackDeltaTime)
        {
            long now = Stopwatch.GetTimestamp();
            float deltaTime = _lastWorldUpdateTimestamp == 0
                ? fallbackDeltaTime
                : (now - _lastWorldUpdateTimestamp) / (float)Stopwatch.Frequency;

            _lastWorldUpdateTimestamp = now;
            return deltaTime > 0f
                ? Math.Clamp(deltaTime, 0f, 0.1f)
                : GameState.GameplayBaselineDeltaTime;
        }

        private void SynchronizeTutorialOverlayPause()
        {
            if (GameState.TutorialState?.InstructionOverlayPauseActive != true)
                return;

            if (world?.SceneHandler?.GetActiveScene().SceneType != SceneTypes.Tutorial)
                return;

            var now = DateTime.UtcNow;
            var overlay = GameState.ScreenOverlayState;
            if (GameState.TutorialState.ShouldAutoCloseInstructionOverlay(now))
            {
                RestoreTutorialShipAfterOverlayPause();
                world.SceneHandler.GetActiveScene().SetupGameOverlay();
                world.IsPaused = false;
                isPaused = false;
                pauseFrameCount = 0;
                return;
            }

            if (overlay.ShowOverlay && overlay.Type == ScreenOverlayType.Tutorial)
            {
                overlay.Footer = GameState.TutorialState.CanCloseInstructionOverlay(now)
                    ? "PRESS ANY KEY OR ESC TO CONTINUE"
                    : "HAL-E SPEAKING - ESC TO SKIP";
            }

            if (!world.IsPaused)
            {
                CaptureTutorialShipPauseSnapshot();
                pauseFrameCount = 0;
            }

            world.IsPaused = true;
            isPaused = true;
        }

        private void CaptureTutorialShipPauseSnapshot()
        {
            if (world?.WorldInhabitants == null)
                return;

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                var obj = world.WorldInhabitants[i];
                if (obj.ObjectName == "Ship" && obj.Movement is ShipControls shipControls)
                {
                    shipControls.CaptureOverlayPauseTransform(obj);
                    return;
                }
            }
        }

        private void RestoreTutorialShipAfterOverlayPause()
        {
            if (world?.WorldInhabitants == null)
                return;

            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                var obj = world.WorldInhabitants[i];
                if (obj.ObjectName == "Ship" && obj.Movement is ShipControls shipControls)
                {
                    shipControls.RestoreOverlayPauseTransformAndSuppressCrashDetection(obj);
                    return;
                }
            }
        }

        private List<ProjectedTriangleMesh> RentTriangleList()
        {
            lock (_triangleListPoolLock)
            {
                if (_triangleListPool.Count > 0)
                    return _triangleListPool.Pop();
            }

            return new List<ProjectedTriangleMesh>();
        }

        private void ReturnTriangleList(List<ProjectedTriangleMesh> list)
        {
            list.Clear();

            lock (_triangleListPoolLock)
            {
                if (_triangleListPool.Count < MaxPooledTriangleLists)
                    _triangleListPool.Push(list);
            }
        }

        private void UpdateVideoOverlay()
        {
            var state = GameState.ScreenOverlayState;
            if (state == null)
                return;

            if (state.ShowVideoOverlay && !string.IsNullOrWhiteSpace(state.VideoClipPath))
            {
                var resolvedPath = ResolveVideoPath(state.VideoClipPath);
                if (!string.Equals(_currentVideoClipPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
                {
                    _videoOverlay.Stop();
                    _videoOverlay.Source = new Uri(resolvedPath, UriKind.Absolute);
                    _currentVideoClipPath = resolvedPath;
                    _videoOverlayIsPlaying = false;
                    _videoEntrancePlayed = false;
                }

                double w = ActualWidth > 0 ? ActualWidth : Width;
                double h = ActualHeight > 0 ? ActualHeight : Height;
                if (w <= 0) w = 1920;
                if (h <= 0) h = 1080;

                _videoOverlay.Width = w;
                _videoOverlay.Height = h;
                _videoOverlay.Visibility = Visibility.Visible;

                if (!_videoEntrancePlayed)
                {
                    StartVideoEntranceAnimation(h);
                    _videoEntrancePlayed = true;
                }

                if (!_videoOverlayIsPlaying && _videoOverlay.Source != null)
                {
                    _videoOverlay.Play();
                    _videoOverlayIsPlaying = true;
                }
            }
            else
            {
                if (_videoOverlay.Visibility != Visibility.Collapsed)
                {
                    _videoOverlay.Stop();
                    _videoOverlay.Visibility = Visibility.Collapsed;
                }

                _currentVideoClipPath = null;
                _videoOverlayIsPlaying = false;
                _videoEntrancePlayed = false;
            }
        }

    private void VideoOverlayOnMediaEnded(object? sender, RoutedEventArgs e)
    {
        if (_videoOverlay.Visibility != Visibility.Visible)
            return;

        _videoOverlay.Position = TimeSpan.Zero;
        _videoOverlay.Play();
    }

        private void UpdateMotherShipHealthBar(GamePlayState gameplay)
        {
            if (gameplay == null || !gameplay.ShowMotherShipHealthBar || !gameplay.MotherShipIsOnScreen)
            {
                _motherShipHealthBarCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            if (GameState.ScreenOverlayState.Type != ScreenOverlayType.Game || isFading)
            {
                _motherShipHealthBarCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            _motherShipHealthBarCanvas.Visibility = Visibility.Visible;

            float pct = Math.Clamp(gameplay.MotherShipHealthPercent, 0f, 1f);

            // Position the bar to the right of the mothership's screen center
            double barX = gameplay.MotherShipScreenX + MotherShipBarOffsetX;
            double maxBarY = ScreenSetup.screenSizeY * 0.83 - MotherShipBarHeight / 2;
            double barY = Math.Min(gameplay.MotherShipScreenY - MotherShipBarHeight / 2, maxBarY);

            Canvas.SetLeft(_motherShipHealthBarBg, barX);
            Canvas.SetTop(_motherShipHealthBarBg, barY);

            // Fill grows from bottom to top
            double fillH = (MotherShipBarHeight - 2) * pct;
            _motherShipHealthBarFill.Height = fillH;
            Canvas.SetLeft(_motherShipHealthBarFill, barX + 1);
            Canvas.SetTop(_motherShipHealthBarFill, barY + 1 + (MotherShipBarHeight - 2 - fillH));

            // Color: green at 100%, yellow at 50%, red at 0%
            byte r, g;
            if (pct > 0.5f)
            {
                float t = (1f - pct) * 2f;
                r = (byte)(255 * t);
                g = 255;
            }
            else
            {
                float t = pct * 2f;
                r = 255;
                g = (byte)(255 * t);
            }

            _motherShipHealthBarFill.Fill = new SolidColorBrush(Color.FromArgb(220, r, g, 0));
        }

        private void UpdateMotherShipRamWarning(GamePlayState gameplay)
        {
            if (gameplay == null || !gameplay.MotherShipRamWarningActive || isFading)
            {
                _ramWarningCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            if (GameState.ScreenOverlayState.Type != ScreenOverlayType.Game)
            {
                _ramWarningCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            _ramWarningCanvas.Visibility = Visibility.Visible;

            double cx = gameplay.MotherShipRamWarningScreenX;
            double cy = gameplay.MotherShipRamWarningScreenY;

            Canvas.SetLeft(_ramWarningOuter, cx - RamWarningSize / 2);
            Canvas.SetTop(_ramWarningOuter, cy - RamWarningSize / 2);

            double innerSize = RamWarningSize * 0.5;
            Canvas.SetLeft(_ramWarningInner, cx - innerSize / 2);
            Canvas.SetTop(_ramWarningInner, cy - innerSize / 2);
        }

        private void UpdateAimAssistIndicator(GamePlayState gameplay)
        {
            if (gameplay == null || !gameplay.AimAssistTargetActive || isFading)
            {
                _aimAssistCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            if (GameState.ScreenOverlayState.Type != ScreenOverlayType.Game)
            {
                _aimAssistCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            // Flash the indicator using a sine wave (toggles opacity)
            double phase = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) % 600.0 / 600.0;
            double alpha = 0.5 + 0.5 * Math.Sin(phase * Math.PI * 2);
            byte outerAlpha = (byte)(200 * alpha);
            byte innerAlpha = (byte)(60 * alpha);
            byte innerStrokeAlpha = (byte)(160 * alpha);

            _aimAssistOuter.Stroke = new SolidColorBrush(Color.FromArgb(outerAlpha, 255, 255, 255));
            _aimAssistInner.Fill = new SolidColorBrush(Color.FromArgb(innerAlpha, 255, 255, 255));
            _aimAssistInner.Stroke = new SolidColorBrush(Color.FromArgb(innerStrokeAlpha, 255, 255, 255));

            _aimAssistCanvas.Visibility = Visibility.Visible;

            double cx = gameplay.AimAssistTargetScreenX;
            double cy = gameplay.AimAssistTargetScreenY;

            Canvas.SetLeft(_aimAssistOuter, cx - AimAssistIndicatorSize / 2);
            Canvas.SetTop(_aimAssistOuter, cy - AimAssistIndicatorSize / 2);

            double innerSize = AimAssistIndicatorSize * 0.5;
            Canvas.SetLeft(_aimAssistInner, cx - innerSize / 2);
            Canvas.SetTop(_aimAssistInner, cy - innerSize / 2);
        }

        private static string ResolveVideoPath(string clipPath)
        {
            if (Path.IsPathRooted(clipPath))
                return clipPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, clipPath);
        }

        private void StartVideoEntranceAnimation(double screenHeight)
        {
            if (_videoOverlay.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform(0, 0);
                _videoOverlay.RenderTransform = translate;
            }

            var startOffset = screenHeight * 1.25;
            translate.Y = startOffset;

            var animation = new DoubleAnimation
            {
                From = startOffset,
                To = 0,
                Duration = TimeSpan.FromSeconds(3.0),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            translate.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
