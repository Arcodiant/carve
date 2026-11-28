# Unreleased

- T-230: Carve.Core reads and writes scripts: one tool call a statement, named arguments with JSON values, bit-exact numbers, errors that name the line
- T-231: carve keeps every call that changed the model, with its arguments, beside the undo history; undo drops it, open and the new clear tool start it again
- T-232: save_script writes the session's journal as a .carvescript, paths relative to the script, and the gatehouse scenario's session is committed as samples/gatehouse.carvescript
