using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Static land-based prop controller for the teddy bear. The surface-anchoring
    /// system positions it from its SurfaceBasedId and ObjectOffsets, so it never
    /// moves; the only job here is to hold a fixed facing.
    /// </summary>
    public class TeddyBearControls : IObjectMovement
    {
        // The mesh is authored facing +X (screen right). Ground props lay flat under the
        // surface-facing pitch on X, after which the horizontal facing (yaw) is the Z
        // rotation: Z = 0 faces +X, Z = 180 faces -X (see PolarBearControls). The two
        // camera-facing directions are therefore Z = 90 and Z = 270; 270 turns the bear to
        // look toward the camera, along -Z. (If it ends up facing away, change this to 90.)
        private const float FaceTowardCameraZDegrees = 90f;
        private static float BaseXRotation => WorldViewSetup.SurfaceFacingObjectPitchDegrees;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            // Re-apply every frame: the render loop hands us per-frame object copies.
            var rotation = theObject.Rotation as Vector3 ?? new Vector3();
            rotation.x = BaseXRotation;
            rotation.y = 0f;
            rotation.z = FaceTowardCameraZDegrees;
            theObject.Rotation = rotation;
            return theObject;
        }

        public void ReleaseParticles()
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
