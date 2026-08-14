using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public sealed class ObjectFrameTransformer
    {
        private readonly MeshRotation meshRotation;

        public ObjectFrameTransformer()
            : this(new MeshRotation())
        {
        }

        public ObjectFrameTransformer(MeshRotation meshRotation)
        {
            this.meshRotation = meshRotation ?? throw new ArgumentNullException(nameof(meshRotation));
        }

        public void RotateObjectGeometry(IRenderable3dObject renderableObject)
        {
            if (renderableObject.Rotation == null)
                return;

            if (renderableObject.CrashBoxesFollowRotation)
                renderableObject.CrashBoxes = RotateCrashBoxes(renderableObject.CrashBoxes, renderableObject.Rotation);

            foreach (var part in renderableObject.ObjectParts)
            {
                part.Triangles = RotateMesh(part.Triangles, renderableObject.Rotation);
            }
        }

        public List<List<IVector3>> RotateCrashBoxes(
            IReadOnlyList<List<IVector3>>? crashBoxes,
            IVector3 rotation)
        {
            if (crashBoxes == null || crashBoxes.Count == 0)
                return new List<List<IVector3>>();

            var rotatedCrashboxes = new List<List<IVector3>>(crashBoxes.Count);

            for (int boxIndex = 0; boxIndex < crashBoxes.Count; boxIndex++)
            {
                var crashBox = crashBoxes[boxIndex];
                var rotated = new List<IVector3>(crashBox.Count);

                for (int pointIndex = 0; pointIndex < crashBox.Count; pointIndex++)
                {
                    rotated.Add(RotatePoint(crashBox[pointIndex], rotation));
                }

                rotatedCrashboxes.Add(rotated);
            }

            return rotatedCrashboxes;
        }

        public IVector3 RotatePoint(IVector3 point, IVector3 rotation)
        {
            var rotatedPoint = meshRotation.RotatePoint(rotation.z, point, 'Z');
            rotatedPoint = meshRotation.RotatePoint(rotation.y, rotatedPoint, 'Y');
            return meshRotation.RotatePoint(rotation.x, rotatedPoint, 'X');
        }

        public List<ITriangleMeshWithColor> RotateMesh(
            List<ITriangleMeshWithColor> mesh,
            IVector3 rotation)
        {
            var rotatedMesh = meshRotation.RotateMesh(mesh, rotation.z, 'Z');
            rotatedMesh = meshRotation.RotateMesh(rotatedMesh, rotation.y, 'Y');
            return meshRotation.RotateMesh(rotatedMesh, rotation.x, 'X');
        }
    }
}
