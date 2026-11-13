# Unreleased

- T-53: A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
- T-54: An agent sets UVs by coordinates with set_uvs and marks seams with mark_edges, and get_mesh reads both back
- T-60: check warns of unmapped, flipped, zero-area and overlapping UV faces
- T-55: project_uvs lays chosen faces' UVs by planar, box or cylindrical projection into a rectangle
- T-56: unwrap cuts at seams and open edges, flattens each island by LSCM and packs them, shown on the barrel sample
