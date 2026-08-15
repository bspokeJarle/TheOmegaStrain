using Domain;

namespace GameAiAndControls.Controls
{
    public class SmallPalmControls : PalmControlsBase
    {
        protected override float PhaseOffsetRadians => 1.70f;
        protected override float WindRadiansPerSecond => 2.15f;
        protected override float LeafSwayAmplitude => 3.9f;
        protected override float LeafFlutterAmplitude => 1.4f;
    }
}
