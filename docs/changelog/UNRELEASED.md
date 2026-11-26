# Unreleased

- T-197: Every mesh report groups a warning kind past 3 into one line (count, worst, 3 samples, the select query), on the fps-arms cage and tool sources now in samples/
- T-198: A smooth-shaded mesh no longer warns of not-planar faces: check notes their count and worst case, and a subdivided mesh can read ok
- T-200: define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
- T-199: help takes a topic beside tool; the warnings topic explains every warning kind, its grouping, its fix and the verbosity levels, and check's catalogue of warnings moves into it
- T-201: The parts, style, UV, skeleton, weight, clip, target, save and bake tools take verbosity, silent answering only ok
- T-202: unwrap returns the island count, worst stretch, flipped total and five largest islands, and every island only at verbosity full
- T-206: The fps-arms cage opened, subdivided twice, checked, unwrapped and exported through the real carve with every result under 2,000 characters, the sizes tabled
