using CommonUtilities.CommonSetup;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public sealed class DesertRockControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private const float Yrotation = 0f;
        private const float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            var rotation = theObject.Rotation as Vector3 ?? new Vector3();
            float existingZ = rotation.z;
            rotation.y = Yrotation;
            rotation.x = Xrotation;
            rotation.z = existingZ;
            theObject.Rotation = rotation;

            return theObject;
        }

        public void Dispose() { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
    }
}
