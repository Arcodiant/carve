# Unreleased

- T-15: Carve.Core reads and writes the Carve file, JSON geometry that reloads bit-exact with every id kept
- T-16: An agent exports the current mesh with export to a .glb with flat normals and one matte material, which Empyrean's glTF parser reads back exactly
- T-17: An agent saves its mesh to a .carve file and opens it again in a later session, with a worked example from an empty file to a game's content folder
- T-18: A headless viewer spike loads a .glb from a content folder through IModelLoader, as a game does, and captures it from fixed views
