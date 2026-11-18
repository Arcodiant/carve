# Unreleased

- T-91: A model is a named tree of parts with transforms, envelopes, instances and kept levels, placed in world space for drawing, with the sample rover worked in Core
- T-92: The Carve file holds the part tree as version 7, written only when a model needs it, and save and open carry one part's geometry and levels as a piece
- T-93: define_part, get_parts and keep_level build and read a part tree, the edit tools act on a named part, and check warns when a part leaves its envelope
- T-132: The Carve file holds a part tree beside morph targets as version 9, so save no longer refuses a model with both
