namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class ImpactStatus : EngineImpactState, IImpactStatus
        {
            public IParticle? SourceParticle { get; set; }
            public int? ObjectHealth { get; set; } = 100;
        }
    }
}
