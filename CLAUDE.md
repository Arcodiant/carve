<!-- BEGIN cairn -->
This is a cairn workspace container — an orphan `workspace` branch of this
project's own repo, holding the Claude settings and `workspace.yml` and
nothing else. The code and the work-item tracker both live in `branches/`,
one worktree per branch. See the cairn skill's references/workspace-model.md
for what belongs where and how `cairn workspace bootstrap` reconstructs
`branches/` on a new machine.

Builds, tests and verification run inside the member's own
`branches/task/T-<id>` worktree — never from the container root, which
holds no code. A subagent must never `git reset --hard` or `git checkout
-B` against an `origin/` ref in its worktree: `finish` rebases onto the
target itself, and a reset there has silently dropped a local-only commit.
<!-- END cairn -->
