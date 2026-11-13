# Unreleased

- T-63: A face is an outer loop plus holes: define_mesh's object form, topology, triangulation, check, select, render, export and Carve file version 4
- T-64: delete removes faces and vertices, leaving tombstoned slots so ids are never reused, through get_mesh, the file, render, select, check and export
- T-68: merge_faces dissolves connected faces into one, and bridge joins two open loops with a ring of quads
- T-65: plane_cut splits faces along a plane, keeping both sides or one with the cut filled, on Core's shared face-splitting step that reports each split face's pieces
- T-66: loop_cut adds edge loops across a ring of quads at given fractions
- T-67: knife cuts a path of surface points through faces; a closed path inside a face cuts out an inner face and leaves a hole
- T-69: The gatehouse wall built by cuts alone: cornice, gable, door and a see-through window, shown at each stage and kept as samples/gatehouse.carve
- T-70: Fresh agents cut a wall into a gable with a see-through window from the descriptions alone, recovering from a knife point off the surface
