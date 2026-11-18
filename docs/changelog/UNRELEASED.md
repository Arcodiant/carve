# Unreleased

- T-99: A mesh holds named targets as sparse position deltas, defined from a full or sparse position set or a mesh of the same topology, evaluated at any weights and kept exactly through subdivide
- T-102: define_target gives a mesh a named target from positions or a second file, naming the first topology difference; delete_target removes one; get_mesh reports them
- T-100: move, extrude, delete, merge_faces, bridge, plane_cut, loop_cut and knife keep a mesh's targets, new vertices taking their deltas from the vertices they come from
- T-101: The Carve file stores each mesh's targets as sparse deltas in a new version, read back bit-exact, and open refuses to replace a mesh's targets unless asked
- T-104: export writes each target's position and normal deltas and its name as glTF morph targets, read back through the engine's loader
- T-103: render and check take target weights, applied before skinning, and check reports a target that flips, collapses or cracks faces at weight 1
- T-108: render_glb draws a .glb at chosen target weights through the engine's MorphWeights, and the model viewer takes starting weights and, in its window, a live weight slider
