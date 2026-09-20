---
name: retromesh-create-object
description: Create or refine code-defined 3D objects for any game using RetroMesh, including vehicles, buildings, spacecraft, creatures and props with a requested triangle budget. Use for requests such as creating a truck with about 1000 faces. Covers mesh factories, parts, winding, scale and geometry validation, not game AI or scene behavior unless explicitly requested.
---

# Create a RetroMesh Object

Turn the user's description into a recognizable, editable C# mesh for the target
RetroMesh game. Produce geometry, not an image of geometry. The Omega Strain
inspired the construction patterns; it is not a dependency or a required game style.

## Establish the target contract

- Inspect the target project's instructions, engine references and one comparable
  object factory. Identify its concrete object, part, colored triangle and vector
  types. Verify APIs against the referenced engine version, not a different checkout.
- Reuse the game's wrappers when present. For a standalone model, use
  `Engine3dObject` / `IRenderable3dObject` and engine helpers. Do not create a new
  game, change engine references or import Omega namespaces just to build a mesh.
- If engine source is available, use [the API reference](references/retromesh-api.md)
  to locate the relevant implementations. Without source, use package documentation
  or inspect public types. Do not invent primitive builders or missing properties.
- Separate **model-local axes**, **game-world axes** and **view transforms**.
  Follow the host's forward/up directions, units, rotation order and pivot convention.
  Do not impose Omega's Y-down movement or Z-up model authoring on another game.
- If the host is unspecified, deliver a standalone factory with explicitly documented
  local axes and dimensions. A model-local +X forward, +Z up convention is an example,
  not an engine requirement. Keep placement and camera transforms outside the mesh.

## Interpret the model and budget

Make reasonable design choices from the brief. Ask only about choices that materially
change the result, such as a truck versus a tractor-trailer when the context cannot
resolve it. State assumptions briefly; do not require a long questionnaire.

- Interpret an unspecified face budget as **authored body triangles before culling**.
  A quad built from two triangles counts as two, not one. Respect an explicitly
  requested polygon definition, exact count or hard maximum instead.
- For "about 1000 faces", aim for 900-1100 body triangles unless the user gives
  another tolerance. State this assumption. Count actual geometry after construction.
- Report body triangles, hidden guides, shadow triangles and the total separately.
  Do not count collision-box corners as body faces, hide detail to meet the budget,
  or present a camera-dependent rendered-triangle count as the model's complexity.
- Allocate detail to silhouette first, then characteristic features and surface detail.
  Do not subdivide featureless panels, duplicate faces or add invisible geometry merely
  to reach a count. For an exact target, design useful topology to fit it.

For example, a three-axle truck around 1000 triangles might allocate roughly 180
to the cab, 80 to the chassis, 220 to the cargo body, 384 to six wheels, and 116 to
mirrors, lights, grille and steps: 980 in the **plan**, not a measured result.
Adapt this breakdown to the requested vehicle and measure the finished mesh.

## Build readable, reusable geometry

Use named parts and intent-revealing builders such as `BuildCab`, `BuildCargoBody`
and `BuildWheel`, or equivalent names for the requested subject. Keep dimensions,
palette and meaningful detail settings easy to find. Avoid thousands of unrelated
vertex literals when cross-sections, rings or small loops express the shape better.

Read [the API reference](references/retromesh-api.md) before writing the factory.
Use [MeshFactoryExample.cs](assets/MeshFactoryExample.cs) only when a standalone
engine-only example is useful; its 12-triangle block demonstrates API usage, not a
finished model or a 1000-triangle truck. Adapt its types to the host instead of
copying another set of adapters into a game that already has them.

- Reuse `MeshGeometryOperations.CreateTriangleOutward` and `AddQuadOutward` for
  outward winding. Use a centre inside the relevant convex component, not the
  whole truck for every wheel or mirror. Handle concave shapes as suitable components
  or explicitly authored surfaces; the centre rule is not a general concave mesher.
- Reuse vector, rotation, cloning, scaling and collision-box helpers. Small local
  box/ring builders are appropriate if the target has no equivalent; do not duplicate
  the underlying vector or winding mathematics.
- Keep ordinary closed surfaces one-sided. Fix winding instead of setting
  `noHidden = true` everywhere. Use double-sided faces only for deliberately thin
  surfaces that must be visible from both sides.
- Avoid coplanar overlapping detail and z-fighting. Prefer inset topology or a small,
  scale-appropriate separation. Keep a consistent palette; add textures/UVs only
  when requested or established by the target's asset pipeline.
- Return fresh mutable geometry from each factory call. Inspect cloning and transform
  behavior before sharing vertices or templates. Engine transforms may mutate vertices.
- Apply uniform scale once to the whole object, including existing guides and crash
  boxes. Keep positive finite scale/dimensions explicit. Do not use scale to fix a
  misplaced pivot or screen position.
- Use the host's normal/lighting update before projection. The projector does not
  necessarily rotate an object or calculate missing normals for you.

## Keep integration optional

A request for a model does not authorize enemies, AI, weapon systems, physics,
collision damage, scene spawning or global-state changes. Keep such behavior in
the target game's existing systems, not in generic RetroMesh geometry code.

When collision is requested or required by the host, use a small set of helper-built
boxes appropriate to the silhouette. When shadows or attachment guides are needed,
use the host pipeline and keep guides hidden by default. Verify the up-axis expected
by footprint/shadow helpers before using them. Do not assume ground, gravity, shadows
or even a terrain system exist in every game.

Keep concrete models in game/content code or the user's requested asset location.
Do not add game-specific trucks, enemies or rules to `RetroMesh.Engine` merely
because they render through it. For animations, follow the target's pivot and
per-frame copy/update pattern; a named part alone is not a working animation rig.

## Verify and deliver

Use the target's test framework or a small isolated harness. Verify observable geometry:

- Actual triangle count and per-part counts satisfy the agreed budget; required parts
  are visible and nonempty. Hidden guides and shadows have separate counts.
- Vertices are finite; body triangles have nonzero, scale-appropriate area; colors
  are valid; expected dimensions and pivot/ground-contact points are correct.
- Winding is outward for closed convex components. Recheck reflected/mirrored parts;
  reflection reverses winding. Do not apply one-centre tests to concave assemblies.
- Two factory calls do not share mutable vertices; scaling/rotating one leaves the
  other unchanged. Scaling preserves proportions and aligns any guides/crash boxes.
- Normals and culling work after the host's transform stage. If a preview is available,
  inspect front, side, rear and an oblique view, including the underside where relevant.
  Use the existing renderer; do not build a new viewer unless requested.

Build the relevant project. Distinguish mathematical checks from visual inspection;
say explicitly if no render preview was possible. A passing count test does not
prove that the result looks like the requested subject.

Preserve repository encoding and line endings. On a Windows/.NET target that specifies
CRLF, restore it after edits and check touched files with `git ls-files --eol` and
`git diff --check`; do not normalize unrelated files.

Return the factory location/signature, actual counts, chosen local axes/dimensions,
test results, and the minimal placement example if useful. Do not claim an object
is integrated into gameplay when only its geometry was requested.

## Example requests

- "Use $retromesh-create-object to create a truck with about 1000 faces for this game."
- "Create a 300-triangle cottage using the existing RetroMesh object wrappers."
- "Build a spacecraft with at most 600 triangles, +Y forward and +Z up. Geometry only."
- "Refine this creature's silhouette without exceeding its current triangle budget."

The folder is portable: share `SKILL.md` together with its `references` and `assets`.
An AI without skill discovery can read `SKILL.md` directly and follow its relative links.
