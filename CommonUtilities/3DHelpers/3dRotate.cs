using Domain;
using static Domain._3dSpecificsImplementations;

namespace CommonUtilities._3DHelpers
{
    public class _3dRotationCommon : MeshRotation
    {
        public new IVector3 RotatePoint(double angleInDegrees, IVector3 coord, char axis)
        {
            var rotated = base.RotatePoint(angleInDegrees, coord, axis);
            return new Vector3(rotated.x, rotated.y, rotated.z);
        }
    }
}
