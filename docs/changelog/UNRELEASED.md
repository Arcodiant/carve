# Unreleased

- T-53: A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
- T-54: An agent sets UVs by coordinates with set_uvs and marks seams with mark_edges, and get_mesh reads both back
- T-60: check warns of unmapped, flipped, zero-area and overlapping UV faces
- T-55: project_uvs lays chosen faces' UVs by planar, box or cylindrical projection into a rectangle
- T-56: unwrap cuts at seams and open edges, flattens each island by LSCM and packs them, shown on the barrel sample
- T-57: render mode checker draws the model lit with a generated checker texture, and unmapped faces magenta
- T-59: export writes each corner's UV as TEXCOORD_0, splitting vertices where UVs differ, and warns of unmapped faces
- T-58: render mode uvs draws a mesh's UV layout flat over the dimmed checker, with seams, labels and highlight
- T-61: The barrel is seamed, unwrapped and seen in both UV looks, and the crate box-projected, saved, reopened and exported, in suite scenario-uvs
- T-62: Fresh agents unwrap a model from the tool descriptions alone and find and fix a planted unmapped extrusion
