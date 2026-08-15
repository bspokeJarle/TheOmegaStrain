using Domain;
using Gma.System.MouseKeyHook;
using CommonUtilities.CommonSetup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameAiAndControls.Controls
{
    public class HouseControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private float Yrotation = 0;
        private float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float Zrotation = 35;
        
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            //TODO: In time I want the trees to have animations on the branches, now nothing
            //Set parent object
            ParentObject = theObject;
            if (theObject.Rotation!=null) theObject.Rotation.y=Yrotation;
            if (theObject.Rotation!=null) theObject.Rotation.x=Xrotation;
            if (theObject.Rotation!=null) theObject.Rotation.z=Zrotation;

            //For now, just rotate the object at a fixed speed
            //Zrotation += 2;
            //Xrotation += 2;
            return theObject;
        }

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            throw new NotImplementedException();
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }
    }
}
