using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.Diagnostics;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;

namespace TheOmegaStrain.Runtime.Rendering;

/// <summary>Observes live AI and weapons; visibility never gates threat detection.</summary>
public sealed class IncomingThreatWarningManager
{
    private readonly Func<DateTime> _now;
    private readonly Action<string>? _diagnosticSink;
    private DateTime _nextDiagnosticAt;
    private readonly List<OmegaObject3D> _worldCandidates = new();
    private IAudioInstance? _warningInstance;
    private DateTime _nextWarningAt;
    private HashSet<int> _detected = new();
    private HashSet<int> _current = new();
    private readonly HashSet<int> _announced = new();
    private readonly HashSet<int> _pendingEnemies = new();
    private readonly HashSet<int> _pendingDrones = new();
    private GamePlayState? _gameplay;
    private int _sceneIndex = -1;

    public IncomingThreatWarningManager(Func<DateTime>? now = null, Action<string>? diagnosticSink = null)
    {
        _now = now ?? (() => DateTime.UtcNow);
        _diagnosticSink = diagnosticSink;
    }

    public void UpdateFromWorld(OmegaObject3D? shipFrame, IReadOnlyList<OmegaObject3D> aiObjects,
        GamePlayState gameplay, bool gameplayAllowed, IAudioPlayer? audio, ISoundRegistry? sounds)
    {
        _worldCandidates.Clear();
        if (shipFrame != null) _worldCandidates.Add(shipFrame);
        foreach (var ai in aiObjects)
        {
            _worldCandidates.Add(ai);
            // Weapon state belongs to the owner and survives frame deep copies.
            // Include its live rockets even when the owner is offscreen or already dead.
            if (ai.WeaponSystems == null) continue;
            foreach (var weapon in ai.WeaponSystems.ActiveWeapons)
                if (weapon.WeaponObject is OmegaObject3D rocket && rocket.ObjectName == "EnemyRocket")
                    _worldCandidates.Add(rocket);
        }
        if (ShouldLogSample())
        {
            WriteDiagnostic($"SOURCES ai={aiObjects.Count} rockets={_worldCandidates.Count - aiObjects.Count - (shipFrame == null ? 0 : 1)} ship={Describe(shipFrame)}");
            WriteDiagnostic($"AI-NAMES {string.Join(", ", aiObjects.Take(12).Select(ai => $"{ai.ObjectName}#{ai.ObjectId}"))}");
        }
        Update(_worldCandidates, gameplay, gameplayAllowed, audio, sounds, threatGeometryRotated: false);
    }

    public void Update(IReadOnlyList<OmegaObject3D> frameObjects, GamePlayState gameplay,
        bool gameplayAllowed, IAudioPlayer? audio, ISoundRegistry? sounds, bool threatGeometryRotated = true)
    {
        bool log = ShouldLogSample();
        if (log)
        {
            _nextDiagnosticAt = _now().AddSeconds(1);
            WriteDiagnostic($"DETECT allowed={gameplayAllowed} phase={gameplay.Phase} victoryPause={gameplay.IsVictoryRewardPauseActive} candidates={frameObjects.Count} rotated={threatGeometryRotated} audio={audio?.GetType().Name ?? "null"} registry={sounds?.GetType().Name ?? "null"}");
        }
        if (!ReferenceEquals(_gameplay, gameplay) || _sceneIndex != gameplay.SceneIndex)
            Reset(gameplay);

        if (!gameplayAllowed || !gameplay.IsPlaying || gameplay.IsVictoryRewardPauseActive)
        {
            if (log) WriteDiagnostic("BLOCK gameplay gate (see GATES/UI-TICK for individual conditions)");
            Reset(gameplay);
            return;
        }

        OmegaObject3D? ship = null;
        foreach (var obj in frameObjects)
        {
            if (obj.ObjectName == "Ship") { ship = obj; break; }
        }
        if (!IsLiveObject(ship))
        {
            if (log) WriteDiagnostic($"BLOCK ship-not-live {Describe(ship)}");
            Reset(gameplay);
            return;
        }
        if (!OmegaPerspectiveProjectorFactory.TryProjectCrashCenter(ship!, out var shipCenter, out var shipScreen))
        {
            if (log) WriteDiagnostic($"BLOCK ship-projection {Describe(ship)}");
            Reset(gameplay);
            return;
        }
        if (log) WriteDiagnostic($"SHIP {Describe(ship)} center={Position(shipCenter)} screen={Position(shipScreen)}");

        _current.Clear();
        _pendingEnemies.Clear();
        _pendingDrones.Clear();
        bool bestIsRocket = false;
        float bestDistance = float.PositiveInfinity;
        Vector3? bestScreen = null;
        int candidateCount = 0;
        int liveCount = 0;
        int projectionFailures = 0;
        int outsideRange = 0;
        foreach (var obj in frameObjects)
        {
            // Player rockets and friendly decoys are deliberately excluded.
            if (obj.ObjectName is not ("AttackShip" or "KamikazeDrone" or "EnemyRocket"))
                continue;
            candidateCount++;
            bool logObject = log && candidateCount <= 12;
            if (!IsLiveObject(obj))
            {
                if (logObject) WriteDiagnostic($"REJECT not-live {Describe(obj)}");
                continue;
            }
            liveCount++;
            // AI templates need their centre rotated for reading, but live rockets
            // were already transformed by Weapons when launched (and when steering).
            bool isRocket = obj.ObjectName == "EnemyRocket";
            if (!OmegaPerspectiveProjectorFactory.TryProjectCrashCenter(obj, out var center, out var screen,
                    threatGeometryRotated || isRocket))
            {
                projectionFailures++;
                if (logObject) WriteDiagnostic($"REJECT projection {Describe(obj)}");
                continue;
            }

            float distance = VectorMath.Length(VectorMath.Subtract(center, shipCenter));
            // A small release margin prevents repeated warnings at the detection boundary.
            float range = _detected.Contains(obj.ObjectId)
                ? EnemySetup.IncomingThreatWarningReleaseRange : EnemySetup.IncomingThreatWarningRange;
            if (logObject) WriteDiagnostic($"CANDIDATE {Describe(obj)} center={Position(center)} distance={distance:0.##} range={range:0.##} accepted={distance <= range}");
            if (distance > range)
            {
                outsideRange++;
                continue;
            }

            _current.Add(obj.ObjectId);
            if (!_announced.Contains(obj.ObjectId))
                (obj.ObjectName == "KamikazeDrone" ? _pendingDrones : _pendingEnemies).Add(obj.ObjectId);
            if (bestScreen == null || (isRocket && !bestIsRocket) ||
                (isRocket == bestIsRocket && distance < bestDistance))
            {
                bestScreen = screen;
                bestDistance = distance;
                bestIsRocket = isRocket;
            }
        }

        _announced.IntersectWith(_current);
        (_detected, _current) = (_current, _detected);
        if (log) WriteDiagnostic($"RESULT eligible={candidateCount} live={liveCount} projectionFailures={projectionFailures} outsideRange={outsideRange} detected={_detected.Count} pendingEnemy={_pendingEnemies.Count} pendingDrone={_pendingDrones.Count} marker={bestScreen != null} (details limited to first 12)");
        if (bestScreen == null)
        {
            gameplay.IncomingThreatWarningActive = false;
            return;
        }

        // The UI reads the last completed result while the worker scans the next frame.
        // Do not briefly clear the flag during detection: that makes the arrow disappear.
        SetMarker(gameplay, shipScreen, bestScreen);
        // Ordinary one-shot sound, like Ship's effects, not HAL-E speech. Batch only
        // the category played and retry other live threats after this clip finishes.
        var pending = _pendingEnemies.Count > 0 ? _pendingEnemies : _pendingDrones;
        string soundId = _pendingEnemies.Count > 0
            ? "ship_enemy_incoming_warning" : "ship_drone_incoming_warning";
        if (log)
        {
            bool registered = sounds?.TryGet(soundId, out _) == true;
            WriteDiagnostic($"AUDIO id={soundId} pending={pending.Count} registered={registered} cooldownRemaining={Math.Max(0, (_nextWarningAt - _now()).TotalSeconds):0.##} master={GameState.SettingsState.MasterVolumePercent} voice={GameState.SettingsState.VoiceVolumePercent} effects={GameState.SettingsState.EffectsVolumePercent}");
        }
        if (pending.Count > 0 && audio != null && sounds != null && _now() >= _nextWarningAt &&
            sounds.TryGet(soundId, out var definition))
        {
            _warningInstance?.Stop(playEndSegment: false);
            if (DiagnosticsEnabled) WriteDiagnostic($"PLAY id={soundId} path={Path.GetFullPath(Path.Combine(AudioSetup.AudioBasePath, definition.File))} exists={File.Exists(Path.Combine(AudioSetup.AudioBasePath, definition.File))} volume={definition.Settings.Volume} is3D={definition.Settings.Is3D}");
            try
            {
                _warningInstance = audio.Play(definition, AudioPlayMode.OneShot, new AudioPlayOptions());
            }
            catch (Exception ex)
            {
                if (DiagnosticsEnabled) WriteDiagnostic($"PLAY-ERROR {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            if (DiagnosticsEnabled) WriteDiagnostic($"PLAY-RETURN id={soundId} instance={_warningInstance?.Id} playing={_warningInstance?.IsPlaying}");
            // Both supplied clips are under three seconds; leave a short speech gap.
            _nextWarningAt = _now().AddSeconds(3.25);
            _announced.UnionWith(pending);
        }
    }

    public void Reset(GamePlayState gameplay)
    {
        if (_warningInstance != null && DiagnosticsEnabled) WriteDiagnostic($"RESET stops audio id={_warningInstance.SoundId} playing={_warningInstance.IsPlaying} scene={gameplay.SceneIndex} phase={gameplay.Phase}");
        _gameplay = gameplay;
        _sceneIndex = gameplay.SceneIndex;
        _warningInstance?.Stop(playEndSegment: false);
        _warningInstance = null;
        _nextWarningAt = DateTime.MinValue;
        _detected.Clear();
        _current.Clear();
        _announced.Clear();
        _pendingEnemies.Clear();
        _pendingDrones.Clear();
        gameplay.IncomingThreatWarningActive = false;
        gameplay.IncomingThreatWarningScreenX = 0f;
        gameplay.IncomingThreatWarningScreenY = 0f;
        gameplay.IncomingThreatWarningAngle = 0f;
    }

    private bool DiagnosticsEnabled => _diagnosticSink != null || Logger.ShouldLog(ThreatWarningDiagnostics.Enabled);
    private bool ShouldLogSample() => DiagnosticsEnabled && _now() >= _nextDiagnosticAt;
    private void WriteDiagnostic(string message)
    {
        if (_diagnosticSink != null) _diagnosticSink(message);
        else ThreatWarningDiagnostics.Write(message);
    }
    private static string Position(IVector3? p) => p == null ? "null" : FormattableString.Invariant($"({p.x:0.##},{p.y:0.##},{p.z:0.##})");
    private static string Describe(OmegaObject3D? obj) => obj == null ? "null" :
        $"{obj.ObjectName}#{obj.ObjectId} active={obj.IsActive} onscreen={obj.IsOnScreen} health={obj.ImpactStatus?.ObjectHealth} crashed={obj.ImpactStatus?.HasCrashed} exploded={obj.ImpactStatus?.HasExploded} boxes={obj.CrashBoxes?.Count} world={Position(obj.WorldPosition)} offsets={Position(obj.ObjectOffsets)}";

    private static bool IsLiveObject(OmegaObject3D? obj) =>
        obj is { IsActive: true } && obj.CrashBoxes?.Count > 0 &&
        obj.ImpactStatus?.HasCrashed != true && obj.ImpactStatus?.HasExploded != true &&
        obj.ImpactStatus?.ObjectHealth is not <= 0;

    private static void SetMarker(GamePlayState gameplay, Vector3 shipScreen, Vector3 threatScreen)
    {
        var direction = VectorMath.Subtract(threatScreen, shipScreen);
        // Coincident projected centres have no unique bearing; keep a visible upward cue.
        if (VectorMath.Length(direction) <= 0.001f)
            direction = new EngineVector3(0f, -1f, 0f);
        direction = VectorMath.Normalize(direction);
        float radius = 120f * ScreenSetup.ScreenScaleX;
        float margin = 30f * ScreenSetup.ScreenScaleX;
        gameplay.IncomingThreatWarningScreenX = Math.Clamp(shipScreen.x + direction.x * radius,
            margin, MathF.Max(margin, ScreenSetup.screenSizeX - margin));
        gameplay.IncomingThreatWarningScreenY = Math.Clamp(shipScreen.y + direction.y * radius,
            margin, MathF.Max(margin, ScreenSetup.screenSizeY - margin));
        gameplay.IncomingThreatWarningAngle = GeometryMath.GetHeadingFromDirection(direction.x, direction.y, 0f).Z;
        gameplay.IncomingThreatWarningActive = true;
    }
}
