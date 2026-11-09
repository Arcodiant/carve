# Unreleased

- T-29: The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
- T-30: An agent colours faces and named face groups, and sees the colours in render and as glTF materials
- T-31: An agent defines a skeleton of named bones with parents and rest transforms, reads it back, and is refused over 255 bones
- T-32: An agent sets per-vertex weights and renders the model in a given pose, skinned by Carve.Core
- T-33: An agent defines named clips of rotation and translation keys and renders any clip at any time
- T-34: export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
- T-35: The whole model saves to and reloads from the Carve file, and version 1 files still open
- T-46: export in clips mode writes an armature, skin and animations with no mesh, which LoadAnimationSet reads with the same bones and clips
