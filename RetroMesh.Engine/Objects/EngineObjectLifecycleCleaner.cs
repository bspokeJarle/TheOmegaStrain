using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public sealed class NotImplementedTypeDisposalGuard<TResource>
        where TResource : class
    {
        private readonly Action<TResource> dispose;
        private readonly HashSet<Type> suppressedTypes = new();
        private readonly object sync = new();

        public NotImplementedTypeDisposalGuard(Action<TResource> dispose)
        {
            this.dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        public bool TryDispose(TResource? resource)
        {
            if (resource == null)
                return false;

            var resourceType = resource.GetType();
            lock (sync)
            {
                if (suppressedTypes.Contains(resourceType))
                    return false;
            }

            try
            {
                dispose(resource);
                return true;
            }
            catch (NotImplementedException)
            {
                lock (sync)
                {
                    suppressedTypes.Add(resourceType);
                }

                return false;
            }
        }
    }

    public delegate void ObjectResourceRelease<in TObject>(TObject obj)
        where TObject : class, IRenderable3dObject;

    public static class EngineObjectLifecycleCleaner
    {
        public static void Cleanup<TObject>(
            IEnumerable<TObject> objects,
            ObjectResourceRelease<TObject>? releaseObjectResources = null)
            where TObject : class, IRenderable3dObject
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                releaseObjectResources?.Invoke(obj);
                ClearRenderableState(obj);
            }
        }

        public static void ClearRenderableState(IRenderable3dObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            obj.CrashBoxes?.Clear();
            obj.ObjectParts?.Clear();
            obj.CalculatedCrashOffset = null;
            obj.WorldPosition = null;
            obj.ObjectOffsets = null;
        }
    }
}
