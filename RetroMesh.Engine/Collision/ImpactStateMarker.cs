using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public delegate IImpactState? ImpactStateProvider<TObject>(TObject obj)
        where TObject : class, IRenderable3dObject;

    public delegate bool ExistingImpactNamePredicate(string impactName);

    public readonly record struct ImpactMarkResult(
        bool HasImpactState,
        bool WasAlreadyCrashed,
        bool Updated,
        string SourceName,
        string? CrashBoxName);

    public static class ImpactStateMarker
    {
        public static ImpactMarkResult MarkImpact(
            IImpactState? impactState,
            string sourceName,
            ImpactDirection? direction,
            string? crashBoxName = null,
            ExistingImpactNamePredicate? preserveExistingImpactName = null)
        {
            if (impactState == null)
            {
                return new ImpactMarkResult(
                    HasImpactState: false,
                    WasAlreadyCrashed: false,
                    Updated: false,
                    SourceName: sourceName,
                    CrashBoxName: crashBoxName);
            }

            bool wasAlreadyCrashed = impactState.HasCrashed;
            bool shouldUpdate =
                !wasAlreadyCrashed ||
                preserveExistingImpactName?.Invoke(impactState.ObjectName) != true;

            impactState.HasCrashed = true;

            if (shouldUpdate)
            {
                impactState.ObjectName = sourceName;
                impactState.ImpactDirection = direction;
                impactState.CrashBoxName = crashBoxName;
            }

            return new ImpactMarkResult(
                HasImpactState: true,
                WasAlreadyCrashed: wasAlreadyCrashed,
                Updated: shouldUpdate,
                SourceName: sourceName,
                CrashBoxName: shouldUpdate ? crashBoxName : impactState.CrashBoxName);
        }

        public static ImpactMarkResult MarkObjectImpact<TObject>(
            TObject target,
            string sourceName,
            ImpactDirection? direction,
            int targetBoxIndex,
            ImpactStateProvider<TObject> getImpactState,
            ExistingImpactNamePredicate? preserveExistingImpactName = null)
            where TObject : class, IRenderable3dObject
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (getImpactState == null)
                throw new ArgumentNullException(nameof(getImpactState));

            return MarkImpact(
                getImpactState(target),
                sourceName,
                direction,
                GetCrashBoxName(target.CrashBoxNames, targetBoxIndex),
                preserveExistingImpactName);
        }

        public static (ImpactMarkResult A, ImpactMarkResult B) MarkCollisionPair<TObject>(
            TObject a,
            TObject b,
            CollisionBoxPairResult collision,
            ImpactStateProvider<TObject> getImpactState,
            ExistingImpactNamePredicate? preserveExistingImpactName = null)
            where TObject : class, IRenderable3dObject
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            if (b == null)
                throw new ArgumentNullException(nameof(b));

            if (getImpactState == null)
                throw new ArgumentNullException(nameof(getImpactState));

            var resultA = MarkObjectImpact(
                a,
                b.ObjectName,
                collision.DirectionA,
                collision.BoxIndexA,
                getImpactState,
                preserveExistingImpactName);

            var resultB = MarkObjectImpact(
                b,
                a.ObjectName,
                collision.DirectionB,
                collision.BoxIndexB,
                getImpactState,
                preserveExistingImpactName);

            return (resultA, resultB);
        }

        public static string? GetCrashBoxName(IReadOnlyList<string?>? crashBoxNames, int boxIndex)
        {
            return crashBoxNames != null && boxIndex >= 0 && boxIndex < crashBoxNames.Count
                ? crashBoxNames[boxIndex]
                : null;
        }
    }
}
