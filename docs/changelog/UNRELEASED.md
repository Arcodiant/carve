# Unreleased

- T-45: get_clips reads clips back, and looping clips that do not close or clips that travel are warned about
- T-83: Loop closure compares every axis of a translation
- T-50: export writes chosen meshes to their own .glb with every bone in the same order and no clips, so a costume can be swapped at runtime on the body's palette
- T-47: render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
- T-84: The gardener's shoulder is coloured as its sleeve
- T-82: render draws with the engine's PBR material, as a loaded .glb does
