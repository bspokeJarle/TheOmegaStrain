namespace Domain
{
    public interface IImpactStatus : IImpactState
    {
        IParticle? SourceParticle { get; set; }
        int? ObjectHealth { get; set; }
    }
}
