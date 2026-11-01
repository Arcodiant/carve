---
description: >-
  render takes any direction, a zoom and a target, so an agent can look at a mesh from any side and close up
---
# T-9 — Render from any direction

**Status:** Open

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

An agent can look at the mesh from any side and close up on a detail, and the existing `front` and `back` frames come out byte for byte the same as before.

## Scope

In `src/Carve.Server`:

- **`render(view?, direction?, zoom = 1, target?)`**:
  - `view` is `front` or `back`, as now. `direction` is `[x, y, z]`, from the point looked at toward the eye. Neither given means `view: front`.
  - **Tool errors**, the mesh untouched: both `view` and `direction` given; a zero or non-finite direction; `zoom` outside 1 to 8; a `target` outside the mesh's bounds (inclusive); a `target` that isn't three finite numbers.
- **The camera** (`ViewFraming`, pure CPU, generalised from the two fixed directions to any unit direction):
  - **Up** is +Y. When the direction is within 1 degree of vertical (`|d.y| > cos 1°`), up is -Z. This is the measured reason: `Matrix4x4.CreateLookAt` returns NaN looking along Y with up +Y.
  - **Distance**: the eye is placed as M-1 frames the whole mesh, along the direction, measured from `target` (default: the centre of the bounds). When `target` is off centre, the distance grows by the target's offset from the centre, so the eye always stays outside the bounding sphere.
  - **Zoom** divides the tangent of half the field of view. It never moves the eye.
  - **The light turns with the camera** for any direction, as `LightOrientation` does for the two views now.
  - All of this is in drawn space (`DrawnSpace`): `target` is mapped through it.
- **The request** carries a camera spec (direction, zoom, target) in place of `RenderView`, through `RenderQueue`, `RenderPump` and `RenderSystem.ApplyView`. A change of camera is a change of view, applied on one `Update` and rendered on a later one, as now.
- **The caption**:
  - `view` with zoom 1 and no target: unchanged, e.g. `512x512, view front, 8 vertices, 6 faces`.
  - Otherwise, in this form: `512x512, direction (2, 1, 1), zoom 3, target (1.02, 0.4, 0.5), 72 vertices, 70 faces`. The direction is echoed as given, and zoom and target appear only when set.
- **The tool description, as a priority.** It says what `direction` means and that up is +Y (-Z when looking straight up or down), with six axis directions as examples. It also says what zoom and target do, that a head-on axis view shows faces pointing one way as one flat shade (so a slight tilt such as `[0.4, 2, 0.8]` reads better from above), and every error above. Keep the M-1 text on views, empty results and winding.

**Content, in `scenarios/Carve.Scenario.Cube`** (this task owns it in this wave):

- Add the cube from `direction [0.4, 2, 0.8]`.
- Add the cube from `[1, 0, 0]`: one face, flat, a positive check that its centre is not background.
- Add the cube from `[0, -1, 0]`, which proves the -Z up.
- Add a `zoom 3` frame with `target` at the cube's corner `(1, 1, 1)`.
- Each new frame gets a **positive pixel check**: the expected number of differing face shades where faces are seen, a sampled pixel that is not background, and corners that are.
- Add one error call each, for `view` together with `direction` and for `zoom: 9`.

**Byte-identical guard**: before changing any code, run `run-scenario.ps1 cube` on `main` and keep its frames. After the change, the eight existing frames must be byte-identical to those. If they are not, **stop and report**; do not adjust the framing to match.

Tests in `tests/Carve.Server.Tests`:

- `ViewFraming` keeps all corners of a cube, a long box and a flat quad inside the margin for `[1,0,0]`, `[0,1,0]`, `[0,-1,0]`, `[1,1,1]` and `[0.4,2,0.8]`.
- Up is -Z only inside the 1 degree cone, and no matrix contains NaN.
- At `front` and `back` the frame equals the M-1 frame exactly.
- Zoom 3 shrinks the projected size of a box three times.
- An off-centre target keeps the eye outside the bounding sphere.
- Through the client: every error case above, and the caption forms.
- **Show the new pixel checks failing** on a broken product (such as up swapped, or zoom ignored) and say so in the closing report.

## Notes

- **Decided by the person:**
  - The camera: `view` or `direction`, auto-framed, light turning with it; up +Y, -Z within 1°.
  - Zoom narrows the view about `target`, which defaults to the centre. Zoom 1 with no target is today's frame, byte for byte. A target outside the bounds and a zoom outside 1 to 8 are errors.
  - No ids drawn on the image.
- **Seen at planning** (approximate previews in `artifacts/m2-probe/view-*.png`, made by rotating the mesh on the M-1 build): a head-on `[0,1,0]` view of the crate is nearly flat, so the person chose `[0.4, 2, 0.8]` for the crate's top view. The product's framing will be a little tighter than those previews.
- **Open rules: stop and ask.** Any change to the field of view, the margin, the light's angles or intensities, the background, or the image size. Any change that alters an existing frame's bytes. Any tolerance in a pixel check that is looser than M-1's 10/255.
- Files owned: `RenderTool`, `RenderQueue`, `RenderPump`, `RenderSystem`, `ViewFraming`, `MeshBridge` (only if `DrawnSpace` needs a helper), their tests, and `scenarios/Carve.Scenario.Cube`. Not `ModelStore`, `Program.cs` or `DefineMeshTool`, which belong to T-11. `RenderPump` only reads `ModelStore.Current` and `Version`, and T-11 keeps those.
- Six new frames at one size: look at them together at the end, not after each change.
- Writes down for T-12: the caption form and the camera spec type's name.
- Moderately design-heavy: `render`'s arguments are the public interface agents learn.
- Leave the worktree before `finish`.
