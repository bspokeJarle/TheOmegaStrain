using System;

namespace GameAiAndControls.Controls
{
    public readonly struct ShipLoopStatus
    {
        public ShipLoopStatus(
            bool completed,
            bool hadCollision,
            int cleanLoopCount,
            int collisionLoopCount,
            float progressDegrees)
        {
            Completed = completed;
            HadCollision = hadCollision;
            CleanLoopCount = cleanLoopCount;
            CollisionLoopCount = collisionLoopCount;
            ProgressDegrees = progressDegrees;
        }

        public bool Completed { get; }
        public bool HadCollision { get; }
        public int CleanLoopCount { get; }
        public int CollisionLoopCount { get; }
        public float ProgressDegrees { get; }
    }

    public sealed class ShipLoopTracker
    {
        private const float LoopDegrees = 360f;
        private const float MinTrackingDeltaDegrees = 0.1f;
        private const float CompletionCooldownSeconds = 0.5f;

        private bool _hasLastTilt;
        private float _lastTiltDegrees;
        private bool _isTracking;
        private float _signedProgressDegrees;
        private bool _hadCollision;
        private float _cooldownLeft;

        public ShipLoopStatus LastStatus { get; private set; }
        public bool IsTracking => _isTracking;
        public int CleanLoopCount { get; private set; }
        public int CollisionLoopCount { get; private set; }
        public float ProgressDegrees => MathF.Min(MathF.Abs(_signedProgressDegrees), LoopDegrees);

        public ShipLoopStatus Update(float tiltDegrees, bool isAirborne, bool thrustOn, float deltaTime)
        {
            float safeDeltaTime = MathF.Max(0f, deltaTime);
            if (_cooldownLeft > 0f)
                _cooldownLeft = MathF.Max(0f, _cooldownLeft - safeDeltaTime);

            if (!_hasLastTilt)
            {
                _hasLastTilt = true;
                _lastTiltDegrees = tiltDegrees;
                return SetLastStatus(completed: false, hadCollision: false);
            }

            float tiltDelta = NormalizeAngleDelta(tiltDegrees - _lastTiltDegrees);
            _lastTiltDegrees = tiltDegrees;

            if (!isAirborne)
            {
                CancelActiveLoop();
                return SetLastStatus(completed: false, hadCollision: false);
            }

            if (_cooldownLeft > 0f || MathF.Abs(tiltDelta) < MinTrackingDeltaDegrees)
                return SetLastStatus(completed: false, hadCollision: false);

            if (!_isTracking)
            {
                if (!thrustOn)
                    return SetLastStatus(completed: false, hadCollision: false);

                _isTracking = true;
                _signedProgressDegrees = 0f;
                _hadCollision = false;
            }

            _signedProgressDegrees += tiltDelta;

            if (MathF.Abs(_signedProgressDegrees) < MinTrackingDeltaDegrees)
            {
                CancelActiveLoop();
                return SetLastStatus(completed: false, hadCollision: false);
            }

            if (MathF.Abs(_signedProgressDegrees) < LoopDegrees)
                return SetLastStatus(completed: false, hadCollision: false);

            bool completedWithCollision = _hadCollision;
            if (completedWithCollision)
                CollisionLoopCount++;
            else
                CleanLoopCount++;

            CancelActiveLoop();
            _cooldownLeft = CompletionCooldownSeconds;
            return SetLastStatus(completed: true, hadCollision: completedWithCollision, progressDegrees: LoopDegrees);
        }

        public void MarkCollision()
        {
            if (_isTracking)
                _hadCollision = true;
        }

        public void CancelActiveLoop()
        {
            _isTracking = false;
            _signedProgressDegrees = 0f;
            _hadCollision = false;
        }

        private ShipLoopStatus SetLastStatus(bool completed, bool hadCollision, float? progressDegrees = null)
        {
            LastStatus = new ShipLoopStatus(
                completed,
                hadCollision,
                CleanLoopCount,
                CollisionLoopCount,
                progressDegrees ?? ProgressDegrees);

            return LastStatus;
        }

        private static float NormalizeAngleDelta(float delta)
        {
            if (MathF.Abs(delta) <= 180f)
                return delta;

            float normalized = ((delta + 180f) % 360f + 360f) % 360f - 180f;
            return MathF.Abs(normalized) < MathF.Abs(delta)
                ? normalized
                : delta;
        }
    }
}
