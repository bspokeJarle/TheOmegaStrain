using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Single-engine exhaust adapter. Weapons owns a fired rocket's position,
    /// orientation and fuel; UpdateWeaponParticles consumes its already-rotated guides.
    /// </summary>
    public sealed class RocketControls : IObjectMovement
    {
        private const string StartGuidePartName = "RocketParticlesStartGuide";
        private const string DirectionGuidePartName = "RocketParticlesDirectionGuide";

        // A steady stream, emitted every other frame to keep the particle count sane.
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 3;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; } = null!;
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private int _framesSinceRelease;
        private float _weaponEmissionSeconds;
        private readonly OmegaMeshRotation _rotate = new();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            // Standalone scene objects use this path. Fired weapons are advanced by Weapons.
            ReleaseParticles(theObject);
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            return theObject;
        }

        public void Dispose() { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (++_framesSinceRelease < FramesBetweenReleases)
                return;

            // Rotate the guides for THIS frame. LiveGameLoop only binds guide parts after
            // MoveObject has already run, so relying on the bound coordinates would always
            // emit from the previous frame's orientation.
            var start = GetCurrentFrameRotatedGuide(theObject, StartGuidePartName);
            var guide = GetCurrentFrameRotatedGuide(theObject, DirectionGuidePartName);
            if (start == null || guide == null)
                return;

            _framesSinceRelease = 0;

            EmitExhaust(theObject, start, guide);
        }

        /// <summary>Weapons supplies already-rotated geometry; do not rotate these guides twice.</summary>
        public void UpdateWeaponParticles(I3dObject theObject, float deltaSeconds, bool hasFuel)
        {
            ParentObject = theObject;
            if (hasFuel)
            {
                _weaponEmissionSeconds += deltaSeconds;
                const float interval = FramesBetweenReleases / 90f;
                if (_weaponEmissionSeconds >= interval)
                {
                    _weaponEmissionSeconds %= interval;
                    var startPart = theObject.ObjectParts.Find(p => p.PartName == StartGuidePartName);
                    var guidePart = theObject.ObjectParts.Find(p => p.PartName == DirectionGuidePartName);
                    if (startPart?.Triangles.Count > 0 && guidePart?.Triangles.Count > 0)
                        EmitExhaust(theObject, startPart.Triangles[0], guidePart.Triangles[0]);
                }
            }
            else
            {
                _weaponEmissionSeconds = 0f;
            }

            // Existing particles finish naturally; only NEW engine emissions stop with fuel.
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();
        }

        private void EmitExhaust(I3dObject theObject, ITriangleMeshWithColorAndTexture start,
            ITriangleMeshWithColorAndTexture guide)
        {
            var worldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };

            theObject.Particles?.ReleaseParticles(
                guide, start, worldPosition, this, ParticleThrust, null);
        }

        private ITriangleMeshWithColorAndTexture? GetCurrentFrameRotatedGuide(I3dObject theObject, string partName)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part?.Triangles == null || part.Triangles.Count == 0)
                return null;

            var rotation = theObject.Rotation ?? new Vector3();
            var mesh = new List<ITriangleMeshWithColorAndTexture>
            {
                OmegaObjectHelpers.CopyTriangle(part.Triangles[0])
            };

            mesh = _rotate.RotateZMesh(mesh, rotation.z);
            mesh = _rotate.RotateYMesh(mesh, rotation.y);
            mesh = _rotate.RotateXMesh(mesh, rotation.x);

            return mesh[0];
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
    }
}
