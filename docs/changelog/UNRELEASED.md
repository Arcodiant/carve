# Unreleased

- T-77: Committed code generates the textured barrel's eight 256 x 256 PNG textures, and a test keeps the committed files equal to it pixel for pixel
- T-74: A model holds named PBR materials, faces are assigned one, and edits, undo and the Carve file carry them with texture paths relative to the file
- T-75: An agent defines named materials from PNG textures and PBR values, assigns them to faces or groups, and reads them back through get_mesh and check
- T-76: export writes each material as a glTF metallic-roughness material with its textures embedded, static and skinned, and the engine's loader reads every field back
