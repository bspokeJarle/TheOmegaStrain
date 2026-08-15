using Domain;

namespace GameAiAndControls.Controls
{
    public class LargePalmControls : PalmControlsBase
    {
        protected override float PhaseOffsetRadians => 0.35f;
        protected override float WindRadiansPerSecond => 1.75f;
        protected override float LeafSwayAmplitude => 4.8f;
        protected override float LeafFlutterAmplitude => 1.7f;
    }
}
