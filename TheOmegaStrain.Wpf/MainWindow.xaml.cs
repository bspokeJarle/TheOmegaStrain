using TheOmegaStrain.Wpf.Helpers;
using TheOmegaStrain.Wpf.Input;
using TheOmegaStrain.Wpf.MainWindowClasses;
using TheOmegaStrain.Wpf.MainWindowClasses.Overlays;
using TheOmegaStrain.Wpf.Rendering;
using RetroMesh.Rendering.Direct3D11;
using TheOmegaStrain.Runtime.Loops;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
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
using Forms = System.Windows.Forms;

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
        private const bool enableFileLogging = true;
        private const bool EnableSteamDiagnostics = true;
        private readonly DrawingVisualHost visualHost = new();
        private bool _useDirect3D11;
        private Direct3D11ProjectedTriangleRenderer? _direct3DRenderer;
        private Forms.Panel? _direct3DPanel;
        private OverlayHostWindow? _overlayHost;
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
        private bool _isShuttingDown;
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
        private GameInputKey _lastXboxMenuInputKey = GameInputKey.None;
        private DateTime _lastXboxMenuInputAtUtc = DateTime.MinValue;
        private bool _xboxMenuRepeatArmed = false;
        private bool _xboxPauseButtonWasDown = false;
        private bool _xboxExitButtonWasDown = false;
        private readonly XboxQuitHoldTracker _xboxQuitHoldTracker = new();
        private const double XboxMenuInitialRepeatSeconds = 0.32;
        private const double XboxMenuRepeatSeconds = 0.12;
        private HwndSource? _rawMouseSource;
        private SteamManager? _steamManager;
        private SteamGameplaySync? _steamGameplaySync;
        private const int SteamInputDetectionIntervalFrames = 60;
        private int _steamInputDetectionCountdown;

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
            Closed += MainWindow_Closed;
            Loaded += Window_Loaded;

            mainGrid = MainGrid;

            if (!RendererBackendSelection.UseDirect3D11() || !TryInitializeDirect3D11Renderer())
                InitializeWpfRenderer();

            Title = _useDirect3D11
                ? "The Omega Strain — Direct3D 11 — Surface 36 tiles"
                : "The Omega Strain — WPF fallback — Surface 36 tiles";

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
            OverlayRoot.Children.Add(_videoOverlay);

            FadeOverlay = new System.Windows.Shapes.Rectangle
            {
                Fill = Brushes.Black,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Panel.SetZIndex(FadeOverlay, int.MaxValue);
            OverlayRoot.Children.Add(FadeOverlay);

            FpsText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 20,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            OverlayRoot.Children.Add(FpsText);


            // Overlay handlers must be put in the grid
            _overlayManager = new OverlayManager(OverlayRoot);

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
            OverlayRoot.Children.Add(_motherShipHealthBarCanvas);

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
            OverlayRoot.Children.Add(_ramWarningCanvas);

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
            OverlayRoot.Children.Add(_aimAssistCanvas);

            timer.Interval = TimeSpan.FromMilliseconds(8);
            CompositionTarget.Rendering += Handle3dWorldRendering;
            LocationChanged += OnWindowBoundsChanged;
            SizeChanged += OnWindowBoundsChanged;
            stopwatch.Start();
        }

        private Grid OverlayRoot => _overlayHost?.OverlayRoot ?? mainGrid;

        /// <summary>
        /// Forms.Cursor.Hide/Show are reference counted per message queue rather
        /// than scoped to a control, so they must be balanced exactly once. This
        /// flag guarantees that regardless of how initialization or shutdown is
        /// reached.
        /// </summary>
        private bool _winFormsCursorHidden;

        private void HideWinFormsCursor()
        {
            if (_winFormsCursorHidden)
                return;

            Forms.Cursor.Hide();
            _winFormsCursorHidden = true;
        }

        private void ShowWinFormsCursor()
        {
            if (!_winFormsCursorHidden)
                return;

            Forms.Cursor.Show();
            _winFormsCursorHidden = false;
        }

        private bool TryInitializeDirect3D11Renderer()
        {
            try
            {
                _direct3DPanel = new Forms.Panel
                {
                    BackColor = System.Drawing.Color.Black,
                    Dock = Forms.DockStyle.Fill
                };
                Direct3DHost.Child = _direct3DPanel;
                Direct3DHost.Visibility = Visibility.Visible;
                _direct3DPanel.CreateControl();

                // The hosted WinForms panel owns its own HWND and therefore does
                // not inherit the WPF window's Cursor="None". Without this the
                // arrow stays visible over the whole Direct3D render surface.
                _direct3DPanel.Cursor = Forms.Cursors.Default;
                HideWinFormsCursor();
                _direct3DRenderer = new Direct3D11ProjectedTriangleRenderer(
                    _direct3DPanel.Handle,
                    Math.Max(1, _direct3DPanel.ClientSize.Width),
                    Math.Max(1, _direct3DPanel.ClientSize.Height))
                {
                    ShadeNearZ = ScreenSetup.RenderNearZ,
                    ShadeFarZ = ScreenSetup.RenderFarZ,
                    MinimumShade = ScreenSetup.MinimumRenderShade
                };
                _direct3DRenderer.SetProjectionSize(
                    Math.Max(1, ScreenSetup.screenSizeX),
                    Math.Max(1, ScreenSetup.screenSizeY));
                _direct3DPanel.Resize += OnDirect3DPanelResize;
                worldRenderer = _direct3DRenderer;

                _overlayHost = new OverlayHostWindow(this);
                mainGrid.Children.Remove(RenderImage);
                _useDirect3D11 = true;
                return true;
            }
            catch (Exception ex)
            {
                if (Logger.EnableFileLogging)
                    Logger.Log($"[Renderer] Direct3D11 unavailable, falling back to WPF. {ex.GetType().Name}: {ex.Message}");
                SteamDiagnostics.Write($"[Renderer] Direct3D11 unavailable, falling back to WPF. {ex.GetType().Name}: {ex.Message}");

                if (_direct3DPanel != null)
                    _direct3DPanel.Resize -= OnDirect3DPanelResize;
                ShowWinFormsCursor();
                _direct3DRenderer?.Dispose();
                Direct3DHost.Child = null;
                Direct3DHost.Visibility = Visibility.Collapsed;
                _direct3DPanel?.Dispose();
                _direct3DRenderer = null;
                _direct3DPanel = null;
                _overlayHost = null;
                _useDirect3D11 = false;
                return false;
            }
        }

        private void InitializeWpfRenderer()
        {
            Direct3DHost.Child = null;
            Direct3DHost.Visibility = Visibility.Collapsed;
            mainGrid.Children.Add(visualHost);
            worldRenderer = new WorldRenderer(visualHost);
            _useDirect3D11 = false;
        }

        private void UpdateDirect3DBackgroundFlash()
        {
            if (_direct3DRenderer == null)
                return;

            // The WPF backend paints the flash as a background rectangle. Direct3D clears the
            // render target instead, so the same colour must be pushed into ClearColor or the
            // lightning flash is invisible on this backend.
            var weather = GameState.WeatherVisualState;
            var (red, green, blue) = WeatherFlashColorHelpers.GetBackgroundColor(
                weather?.LightningFlashIntensity ?? 0f,
                weather?.ImpactFlashIntensity ?? 0f);

            _direct3DRenderer.ClearColor = new Vortice.Mathematics.Color4(red / 255f, green / 255f, blue / 255f, 1f);
        }

        private void OnDirect3DPanelResize(object? sender, EventArgs e)
        {
            if (_isShuttingDown || _direct3DRenderer == null || _direct3DPanel == null)
                return;

            _direct3DRenderer.Resize(
                Math.Max(1, _direct3DPanel.ClientSize.Width),
                Math.Max(1, _direct3DPanel.ClientSize.Height));
            _direct3DRenderer.SetProjectionSize(
                Math.Max(1, ScreenSetup.screenSizeX),
                Math.Max(1, ScreenSetup.screenSizeY));
        }

        private void OnWindowBoundsChanged(object? sender, EventArgs e)
        {
            _overlayHost?.SynchronizeBounds();
            if (_direct3DRenderer != null && _direct3DPanel != null)
            {
                int width = Math.Max(1, _direct3DPanel.ClientSize.Width);
                int height = Math.Max(1, _direct3DPanel.ClientSize.Height);
                ScreenSetup.Initialize(width, height);
                _direct3DRenderer.SetProjectionSize(width, height);
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;
            Closed -= MainWindow_Closed;
            CompositionTarget.Rendering -= Handle3dWorldRendering;
            LocationChanged -= OnWindowBoundsChanged;
            SizeChanged -= OnWindowBoundsChanged;
            if (_direct3DPanel != null)
                _direct3DPanel.Resize -= OnDirect3DPanelResize;
            ShowWinFormsCursor();
            _direct3DRenderer?.Dispose();
            Direct3DHost.Child = null;
            _direct3DPanel?.Dispose();
            _overlayHost?.Close();
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
            GameState.InputDeviceState.SetSteamStatus(false, 0);
        }

        // Detection only: Steam Input controllers are polled at a low rate so the
        // menu can show controller text, while the game still runs fine without Steam.
        private void UpdateSteamInputDetection()
        {
            if (_steamManager is not { IsAvailable: true })
            {
                GameState.InputDeviceState.SetSteamStatus(false, 0);
                return;
            }

            if (_steamInputDetectionCountdown-- > 0)
                return;

            _steamInputDetectionCountdown = SteamInputDetectionIntervalFrames;
            GameState.InputDeviceState.SetSteamStatus(true, _steamManager.GetConnectedControllerCount());
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
            {
                ScreenSetup.Initialize(w, h);
                _direct3DRenderer?.SetProjectionSize(w, h);
                if (_direct3DPanel != null)
                {
                    _direct3DRenderer?.Resize(
                        Math.Max(1, _direct3DPanel.ClientSize.Width),
                        Math.Max(1, _direct3DPanel.ClientSize.Height));
                }
            }

            _overlayHost?.ShowOverlay();
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
            bool isSettingsPageKey = GameState.ScreenOverlayState is
                { ShowOverlay: true, Type: ScreenOverlayType.Settings } &&
                (e.Key == Key.PageUp || e.Key == Key.PageDown);
            if (isSettingsPageKey && e.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            var gameInputKey = GameState.ScreenOverlayState is { ShowOverlay: true, Type: ScreenOverlayType.Settings }
                ? WpfGameInputKeyMapper.ToSettingsGameInputKey(e.Key)
                : WpfGameInputKeyMapper.ToGameInputKey(e.Key);

            if (IsSteamOverlayShortcut(e))
            {
                LogSteamDiagnostics("ShiftTabReachedGame");
                return;
            }

            if (IsMenuExitKey(gameInputKey))
            {
                DispatchSceneInputKey(gameInputKey);
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
            DispatchSceneInputKey(gameInputKey);

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

        private void UpdateXboxMenuInput()
        {
            if (!XboxControllerInput.TryGetState(controllerIndex: 0, out var controllerState))
            {
                GameState.InputDeviceState.SetXboxControllerConnected(false);
                ResetXboxMenuInputState();
                _xboxPauseButtonWasDown = false;
                _xboxExitButtonWasDown = false;
                _xboxQuitHoldTracker.Reset();
                return;
            }

            GameState.InputDeviceState.SetXboxControllerConnected(true);

            if (TryHandleXboxQuitHold(controllerState))
                return;

            if (TryHandleXboxGameplayExit(controllerState))
                return;

            if (TryHandleXboxPauseToggle(controllerState))
                return;

            if (TryHandleXboxIntroControlsShortcut(controllerState))
                return;

            if (!CanXboxMenuInputControlCurrentState())
            {
                ResetXboxMenuInputState();
                return;
            }

            var key = IsNameEntryOverlayActive()
                ? XboxMenuInputMapper.ToNameEntryGameInputKey(controllerState)
                : IsPlainIntroOverlayActive()
                    ? XboxMenuInputMapper.ToIntroGameInputKey(controllerState)
                    : XboxMenuInputMapper.ToGameInputKey(controllerState);

            if (key == GameInputKey.None && CanXboxShortcutInputControlCurrentState())
                key = XboxMenuInputMapper.ToShortcutGameInputKey(controllerState);

            if (key == GameInputKey.None)
            {
                ResetXboxMenuInputState();
                return;
            }

            if (ShouldDispatchXboxMenuInput(key))
                DispatchSceneInputKey(key);
        }

        private static bool IsNameEntryOverlayActive()
        {
            var overlay = GameState.ScreenOverlayState;
            return overlay is { ShowOverlay: true, Type: ScreenOverlayType.NameEntry };
        }

        private static bool IsPlainIntroOverlayActive()
        {
            var overlay = GameState.ScreenOverlayState;
            return overlay is
            {
                ShowOverlay: true,
                Type: ScreenOverlayType.Intro,
                ChoiceAction: ScreenOverlayChoiceAction.None
            };
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

        private bool DispatchSceneInputKey(GameInputKey key)
        {
            if (key == GameInputKey.None)
                return false;

            var sceneTypeBeforeMenuExit = world?.SceneHandler?.GetActiveScene().SceneType;
            world.SceneHandler.HandleKeyPress(key, world);

            if (GameState.ScreenOverlayState.QuitApplicationRequested)
            {
                Application.Current.Shutdown();
                return true;
            }

            if (IsMenuExitKey(key))
                StopNonMusicAudioIfReturnedToIntro(sceneTypeBeforeMenuExit);

            SyncLocalPauseFromWorld();
            return true;
        }

        private void StopNonMusicAudioIfReturnedToIntro(SceneTypes? previousSceneType)
        {
            if (previousSceneType == null || previousSceneType == SceneTypes.Intro)
                return;

            if (world?.SceneHandler?.GetActiveScene().SceneType == SceneTypes.Intro)
                gameWorldManager.StopNonMusicAudio();
        }

        private static bool IsMenuExitKey(GameInputKey key) => key == GameInputKey.Escape || key == GameInputKey.X;

        private bool TryHandleXboxPauseToggle(XboxControllerSnapshot controllerState)
        {
            bool pausePressed = XboxMenuInputMapper.IsPauseTogglePressed(controllerState);
            if (!pausePressed)
            {
                _xboxPauseButtonWasDown = false;
                return false;
            }

            if (!CanXboxPauseCurrentState())
                return false;

            if (!_xboxPauseButtonWasDown)
                ToggleGameplayPause();

            _xboxPauseButtonWasDown = true;
            return true;
        }

        private bool TryHandleXboxGameplayExit(XboxControllerSnapshot controllerState)
        {
            bool exitPressed = XboxMenuInputMapper.IsExitToMenuPressed(controllerState);
            if (!exitPressed)
            {
                _xboxExitButtonWasDown = false;
                return false;
            }

            if (_xboxExitButtonWasDown)
                return true;

            if (!CanXboxExitCurrentGameplayState())
                return false;

            DispatchSceneInputKey(GameInputKey.Escape);
            _xboxExitButtonWasDown = true;
            return true;
        }

        private bool TryHandleXboxQuitHold(XboxControllerSnapshot controllerState)
        {
            bool viewPressed = XboxMenuInputMapper.IsQuitHoldPressed(controllerState);
            if (!viewPressed)
            {
                _xboxQuitHoldTracker.Reset();
                return false;
            }

            // Once tracking starts, consume View until it is released. Otherwise
            // the same held press would immediately cancel the confirmation dialog.
            if (!_xboxQuitHoldTracker.IsTracking && !CanXboxQuitHoldCurrentState())
                return false;

            if (_xboxQuitHoldTracker.Update(isPressed: true, DateTime.UtcNow))
                DispatchSceneInputKey(GameInputKey.Escape);
            return true;
        }

        private bool TryHandleXboxIntroControlsShortcut(XboxControllerSnapshot controllerState)
        {
            if (!CanXboxIntroControlsShortcutCurrentState())
                return false;

            if (!XboxControllerInput.IsControlPressed(controllerState, XboxControlButton.X))
                return false;

            if (ShouldDispatchXboxMenuInput(GameInputKey.C))
                DispatchSceneInputKey(GameInputKey.X);

            return true;
        }

        private bool CanXboxIntroControlsShortcutCurrentState()
        {
            var overlay = GameState.ScreenOverlayState;
            if (overlay == null ||
                !overlay.ShowOverlay ||
                overlay.Type != ScreenOverlayType.Intro ||
                overlay.CurrentPage != 0)
                return false;

            var sceneType = world?.SceneHandler?.GetActiveScene().SceneType ?? GameState.GamePlayState.CurrentSceneType;
            return sceneType == SceneTypes.Intro;
        }

        private bool CanXboxQuitHoldCurrentState()
        {
            var overlay = GameState.ScreenOverlayState;
            var sceneType = world?.SceneHandler?.GetActiveScene().SceneType ?? GameState.GamePlayState.CurrentSceneType;
            return XboxQuitHoldAvailability.CanStart(
                overlay,
                sceneType,
                GameState.SettingsState.EffectiveControlScheme);
        }

        private bool CanXboxPauseCurrentState()
        {
            if (!IsGameplaySceneForPause())
                return false;

            if (GameState.GamePlayState.IsVictoryRewardPauseActive)
                return false;

            return !GameState.ScreenOverlayState.ShowOverlay;
        }

        private bool CanXboxExitCurrentGameplayState()
        {
            if (GameState.ScreenOverlayState.ShowOverlay)
                return false;

            var sceneType = world?.SceneHandler?.GetActiveScene().SceneType ?? GameState.GamePlayState.CurrentSceneType;
            return sceneType == SceneTypes.Game ||
                   sceneType == SceneTypes.Simulation ||
                   sceneType == SceneTypes.Tutorial;
        }

        private bool CanXboxMenuInputControlCurrentState()
        {
            var overlay = GameState.ScreenOverlayState;
            var sceneType = world?.SceneHandler?.GetActiveScene().SceneType ?? GameState.GamePlayState.CurrentSceneType;

            if (overlay.ShowOverlay)
                return overlay.CanDismissWithInput ||
                       overlay.Type == ScreenOverlayType.NameEntry ||
                       overlay.Type == ScreenOverlayType.Settings ||
                       overlay.ChoiceAction != ScreenOverlayChoiceAction.None;

            if (sceneType == SceneTypes.Intro || sceneType == SceneTypes.Outro)
                return true;

            return world?.IsPaused == true &&
                   (sceneType == SceneTypes.Game ||
                    sceneType == SceneTypes.Simulation ||
                    sceneType == SceneTypes.Tutorial);
        }

        private bool CanXboxShortcutInputControlCurrentState()
        {
            var overlay = GameState.ScreenOverlayState;
            var sceneType = world?.SceneHandler?.GetActiveScene().SceneType ?? GameState.GamePlayState.CurrentSceneType;

            if (overlay.ShowOverlay)
                return overlay.Type == ScreenOverlayType.Intro ||
                       overlay.Type == ScreenOverlayType.Settings;

            if (sceneType == SceneTypes.Intro)
                return true;

            return world?.IsPaused == true &&
                   (sceneType == SceneTypes.Game ||
                    sceneType == SceneTypes.Simulation ||
                    sceneType == SceneTypes.Tutorial);
        }

        private bool ShouldDispatchXboxMenuInput(GameInputKey key)
        {
            var now = DateTime.UtcNow;
            bool isRepeatable = IsRepeatableXboxMenuInput(key);

            if (key != _lastXboxMenuInputKey)
            {
                _lastXboxMenuInputKey = key;
                _lastXboxMenuInputAtUtc = now;
                _xboxMenuRepeatArmed = false;
                return true;
            }

            if (!isRepeatable)
                return false;

            double repeatSeconds = _xboxMenuRepeatArmed
                ? XboxMenuRepeatSeconds
                : XboxMenuInitialRepeatSeconds;

            if ((now - _lastXboxMenuInputAtUtc).TotalSeconds < repeatSeconds)
                return false;

            _lastXboxMenuInputAtUtc = now;
            _xboxMenuRepeatArmed = true;
            return true;
        }

        private static bool IsRepeatableXboxMenuInput(GameInputKey key) =>
            key == GameInputKey.Up ||
            key == GameInputKey.Down ||
            key == GameInputKey.Left ||
            key == GameInputKey.Right;

        private void ResetXboxMenuInputState()
        {
            _lastXboxMenuInputKey = GameInputKey.None;
            _lastXboxMenuInputAtUtc = DateTime.MinValue;
            _xboxMenuRepeatArmed = false;
        }

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
            UpdateSteamInputDetection();
            SynchronizeTutorialOverlayPause();
            UpdateXboxMenuInput();

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
                string rendererBackend = _useDirect3D11 ? "D3D11" : "WPF";
                FpsText.Text = $"[{rendererBackend}] FPS: {frameCount} Triangles:{worldRenderer.GetRenderingTriangleCount()} {gameWorldManager.DebugMessage}";
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
                                UpdateDirect3DBackgroundFlash();
                                if (_useDirect3D11)
                                    Direct3DGraphicsSettings.Apply(screenCoordinates, GameState.SettingsState);
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
