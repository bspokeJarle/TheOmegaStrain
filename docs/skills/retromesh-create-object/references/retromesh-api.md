# RetroMesh mesh authoring reference

Verified against RetroMesh.Engine source with `VersionPrefix` 0.2.0. The target's
referenced API remains authoritative: older templates may use different triangle
interfaces. No Omega code or local machine path is required to use this reference.

## Types and source locations

Paths below are relative to the **engine repository**, not this skill folder.

| Responsibility | Source |
| --- | --- |
| Renderable contract/default object | `RetroMesh.Engine/Objects/IRenderable3dObject.cs`, `Engine3dObject.cs` |
| Parts | `RetroMesh.Engine/Objects/I3dObjectPart.cs`, `Engine3dObjectPart.cs` |
| Vertices and triangles | `RetroMesh.Engine/Objects/EngineVector3.cs`, `EngineTriangleMesh.cs`, `ITriangleMeshWithColorAndTexture.cs` |
| Winding, scale, boxes, optional shadows | `RetroMesh.Engine/Geometry/MeshGeometryOperations.cs` |
| Vector operations | `RetroMesh.Engine/Geometry/VectorMath.cs`, `GeometryMath.cs` |
| In-place rotation and normals | `RetroMesh.Engine/Geometry/MeshRotation.cs` |
| Mutable geometry copies | `RetroMesh.Engine/Objects/EngineObjectCloner.cs` |
| Projection and culling | `RetroMesh.Engine/Rendering/PerspectiveWorldProjector.cs`, `PerspectiveProjectionPipeline.cs` |

These types are in the `RetroMesh.Engine` namespace. `Engine3dObject.ObjectId` is
required; let the host supply identity. Set part `IsVisible` explicitly, since the
engine part's default is false. Initialize `CrashBoxes` to an empty list even for
an object without collision: the object-wide scale helper iterates that list.

`EngineTriangleMesh` implements the geometry interface, not the colored/textured
interface. Reuse the game's concrete colored triangle class. In a standalone
project, the small implementation in the example asset is sufficient: it adds
`Color`, `TextureId` and three UV properties, not another geometry implementation.

## Useful signatures

Factories let the engine work with the game's concrete types. Verify overloads in
the target before pasting. Here `IVector3` coordinates use lowercase `x`, `y`, `z`.

```csharp
TTriangle MeshGeometryOperations.CreateTriangleOutward<TTriangle>(
    IVector3 v1, IVector3 v2, IVector3 v3, IVector3 center,
    string color, Func<TTriangle> triangleFactory, bool noHidden = false)
    where TTriangle : ITriangleMeshWithColorAndTexture;

void MeshGeometryOperations.AddQuadOutward<TTriangle>(
    IList<ITriangleMeshWithColorAndTexture> triangles,
    IVector3 v1, IVector3 v2, IVector3 v3, IVector3 v4, IVector3 center,
    string color, Func<TTriangle> triangleFactory, bool noHidden = false)
    where TTriangle : ITriangleMeshWithColorAndTexture;

void MeshGeometryOperations.ApplyScaleToObject(
    IRenderable3dObject? obj, float scale,
    Func<IVector3, IVector3> vectorFactory);

List<TVector> MeshGeometryOperations.GenerateCrashBoxCorners<TVector>(
    IVector3 min, IVector3 max, Func<float, float, float, TVector> vectorFactory)
    where TVector : IVector3;

List<ITriangleMeshWithColorAndTexture> EngineObjectCloner.CopyTriangles(
    IReadOnlyList<ITriangleMeshWithColorAndTexture> triangles,
    Func<ITriangleMeshWithColorAndTexture> triangleFactory,
    Func<IVector3, IVector3> vectorFactory);
```

`GenerateCrashBoxCorners<IVector3>` can directly produce the element type needed by
`List<List<IVector3>>`. Do not cast `List<EngineVector3>` to `List<IVector3>`;
generic lists are invariant. Convert elements or specify the generic type.

`VectorMath` supplies `Add`, `Subtract`, `Multiply`, `Dot`, `Length`, `Normalize`
and `RotateAroundAxis`. `MeshGeometryOperations.Cross` supplies the cross product.
Use those for diagnostics as well as construction.

## Geometry ownership and transforms

Outward triangle/quad creation keeps supplied vertex references. Adjacent triangles
can therefore share mutable vertices. `ApplyScaleToObject` avoids rescaling a shared
mesh vertex, but `ApplyScaleToTriangles` and `MeshRotation` do not provide the same
deduplication. A shared vertex can rotate repeatedly within one mesh pass.

Use the host's established copy path, or `EngineObjectCloner.CopyTriangles` to
detach each triangle's vertices before mutable triangle-by-triangle transforms.
Do not repeatedly transform an already transformed template in a frame loop.
`CopyRenderableObject(..., copyCrashboxes: false)` deliberately shares crash boxes;
use the appropriate copy mode if a generated instance will change them.

`MeshRotation.RotateXMesh/RotateYMesh/RotateZMesh` take angles in **degrees**, mutate
vertices, and update `normal1` plus the lighting `angle`. The current projection
pipeline culls by `normal1.z` unless `noHidden` is true. Setting `Rotation` on a
bare `Engine3dObject` does not by itself run a transform stage. If testing a factory
directly, pass fresh geometry through the host's transform path before projection.
The example's zero-degree rotation only initializes normals for its unrotated mesh.

## Budgets and useful topology

Count `part.Triangles.Count` from the authored mesh, grouped by role. Separate body,
guides and shadows explicitly. Do not filter arbitrary unwanted faces away to hit
the budget. Actual rendered count varies with visibility, camera and culling.

Useful construction estimates (confirm against generated triangles):

- Closed box: six quads, twelve triangles.
- Connection between two rings with N corresponding vertices: 2N triangles.
- A centre-fan cap on one N-vertex ring: N triangles.
- Cylinder with one side band and two centre-fan caps: 4N triangles.
- Two wheel rims, bevel rings, hubs and treads add geometry beyond a simple cylinder.

Loops express repeated shape; they are not permission to fill the budget with
unnecessary flat subdivisions. Keep dimensions and chosen segment counts explicit.

## Optional footprint, shadow and game adapters

The inspected `NormalizeSurfaceFootprintPivot` uses the low-Z band as the bottom.
`AddSimplifiedShadowPart` builds an XY footprint. These are particular authoring
conventions, not proof that every RetroMesh game's world is Z-up. Use them only
when the model and host projection agree; otherwise use the host's adapter.

The Omega Strain demonstrates named body/engine/detail builders in
`TheOmegaStrain.Game/World/Objects/AttackShip.cs`. Its
`TheOmegaStrain.Game/Helpers/OmegaObject3DHelpers.cs` delegates winding, scaling and
box generation to the engine APIs above. Those are useful patterns, but do not copy
its `GameState`, `ParentSurface`, health, movement, shadow configuration or camera
pitch into other games. A road vehicle, platform-game prop and space-game object
can share mesh-building techniques without sharing gameplay or world conventions.
