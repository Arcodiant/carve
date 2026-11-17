# Unreleased

- T-77: Committed code generates the textured barrel's eight 256 x 256 PNG textures, and a test keeps the committed files equal to it pixel for pixel
- T-74: A model holds named PBR materials, faces are assigned one, and edits, undo and the Carve file carry them with texture paths relative to the file
- T-75: An agent defines named materials from PNG textures and PBR values, assigns them to faces or groups, and reads them back through get_mesh and check
- T-76: export writes each material as a glTF metallic-roughness material with its textures embedded, static and skinned, and the engine's loader reads every field back
- T-78: render draws a face with a material through Material.Pbr, the same material the engine's loader makes of the export, lit with a fixed sky so metal reads
- T-79: The model viewer takes --sky to light a model with Carve's fixed sky, and view.json reports every PBR field and texture of each material
- T-80: The barrel is given wood, iron, end-grain and rune materials, seen in nine frames, saved, reopened, exported and shown in the viewer, in suite scenario-materials
- T-81: Fresh agents texture a model from the tool descriptions alone and recover from a planted missing texture path
