---
description: >-
  Waystone D-8 (tiered, carried-forward, queued testing; docs/design/D-8-tiered-carried-forward-queued-testing.md, section Per-project onboarding) puts this repo at L1, L2 later. Read cairn's skills/cairn/references/testing.md first (tiers, suites and tests.json, carry-forward, the gate, onboarding levels). Worked example: the two pilots' tests.json, which do not exist yet; their work is Waystone T-184 and is requested as request/waystone/test-tiers in Empyrean and lithobrake. Each will be at tests.json beside its .cairn.yml, and a worked example follows once they land; do not wait for it. Notes for this repo (corrected against its present state, 2026-11-01): now contract 7 (D-8's table said 4-6), verify: scripts/verify.ps1 which builds, runs all tests and the cube scenario (needs a Vulkan device). Small. Set verify-resources: (gpu for the scenario) and delete any hand-lock instructions; a manifest splitting the tests from the cube scenario is the L2 step. Replacement rule: this repo's CLAUDE.md says a change is verified when verify passes and a scenario was run and looked at. Replace it with: verify (or cairn test run) passes, and for anything a player can see or hear look at what the T2 scenario suite produced (cairn test artifacts <suite>) instead of running the scenario again by hand; run one by hand only where no suite covers what changed, and then add a suite. Please answer in the Correspondence section with questions, or file your own task and reply with its id.
---
# REQ-WAYSTONE-pending — L1, L2 later onboarding to D-8 tiered testing

**Type:** feature

**Status:** Submitted

**From:** waystone

**Assessed against:** (fill in the target project's version/commit)

**engine-ref:** — (filled in by the target at triage)

## Ask

Waystone D-8 (tiered, carried-forward, queued testing; docs/design/D-8-tiered-carried-forward-queued-testing.md, section Per-project onboarding) puts this repo at L1, L2 later. Read cairn's skills/cairn/references/testing.md first (tiers, suites and tests.json, carry-forward, the gate, onboarding levels). Worked example: the two pilots' tests.json, which do not exist yet; their work is Waystone T-184 and is requested as request/waystone/test-tiers in Empyrean and lithobrake. Each will be at tests.json beside its .cairn.yml, and a worked example follows once they land; do not wait for it. Notes for this repo (corrected against its present state, 2026-11-01): now contract 7 (D-8's table said 4-6), verify: scripts/verify.ps1 which builds, runs all tests and the cube scenario (needs a Vulkan device). Small. Set verify-resources: (gpu for the scenario) and delete any hand-lock instructions; a manifest splitting the tests from the cube scenario is the L2 step. Replacement rule: this repo's CLAUDE.md says a change is verified when verify passes and a scenario was run and looked at. Replace it with: verify (or cairn test run) passes, and for anything a player can see or hear look at what the T2 scenario suite produced (cairn test artifacts <suite>) instead of running the scenario again by hand; run one by hand only where no suite covers what changed, and then add a suite. Please answer in the Correspondence section with questions, or file your own task and reply with its id.

## Correspondence

- **waystone** (today): opened.
