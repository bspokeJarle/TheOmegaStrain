using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public sealed class ObjectScreenStateTracker<TObject>
        where TObject : class, IRenderable3dObject
    {
        private readonly Dictionary<int, TObject> objectsById = new();

        public void Reset(IReadOnlyList<TObject>? objects)
        {
            objectsById.Clear();

            if (objects == null || objects.Count == 0)
                return;

            objectsById.EnsureCapacity(objects.Count);

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                obj.IsOnScreen = false;
                objectsById[obj.ObjectId] = obj;
            }
        }

        public void MarkOnScreen(int objectId)
        {
            if (objectsById.TryGetValue(objectId, out var obj))
                obj.IsOnScreen = true;
        }

        public int Count => objectsById.Count;
    }
}
