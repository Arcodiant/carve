# Unreleased

- T-239: displace and move report moved as {count, first} at summary, the first 10 ids, and the whole list only at full
- T-237: A new edit that cuts selected faces into triangles on their own plane no longer than max_edge, keeping holes and moving no existing vertex, with the border points added to the neighbours' loops so the mesh stays closed
- T-238: displace takes pin_border: a vertex also on a face outside the selected faces or group does not move, so the faces around a worn patch stay flat and unmoved
