using Domain;
using static Domain._3dSpecificsImplementations;

namespace CommonUtilities.OmegaEngineAdapters
{
    public class OmegaMeshRotation : MeshRotation
    {
        public new IVector3 RotatePoint(double angleInDegrees, IVector3 coord, char axis)
        {
            var rotated = base.RotatePoint(angleInDegrees, coord, axis);
            return new Vector3(rotated.x, rotated.y, rotated.z);
        }
    }
}
