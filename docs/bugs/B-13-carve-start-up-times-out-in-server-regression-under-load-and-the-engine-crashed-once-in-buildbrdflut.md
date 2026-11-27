---
description: >-
  carve start-up times out in server-regression under load, and the engine crashed once in BuildBrdfLut
---
# B-13 — carve start-up times out in server-regression under load, and the engine crashed once in BuildBrdfLut

**Status:** Open

**Severity:** Major

**Milestone:** — (off-milestone)

## Symptom

Seen during M-23 (2026-11-26), each time on a loaded runner, each passing on the next run. None of the failing tests touches what M-23 changed.

- `server-regression`, M-23's verify after the rebase onto `main`: `GraphMaterialRenderTests.Weathered_graphs_on_the_raised_hoop_barrel_draw_as_their_bakes_named_as_pngs` and `A_fine_graph_texture_is_drawn_as_its_bake_named_as_a_png_is`, `TimeoutException: No response to 1 after 00:00:30`. That is call 1, so the wait is in `carve`'s start-up, not in a bake (B-11 widened the bake calls only). Job J-20261126-140618-8fbc7b.
- `server-regression`, T-202's first `finish`: four tests timed out the same way, two stdio tests, one `GraphImageToolTests` and one `GraphMaterialRenderTests`.
- `scenario-patina`, the same verify: the engine crashed at start-up with an access violation (0xC0000005) in `Vulkan.Device.CreateGraphicsPipeline` under `EnvironmentBaker.BuildBrdfLut`. Job J-20261126-140555-c01f5e.

`cairn test report --since 2d` puts `server-regression` at 2 flaky and 5 failed runs in the window, and `scenario-patina` at 2 flaky.

## Cause

Not found. Read the jobs' logs first: whether call 1 waits on the engine thread's device creation while other GPU jobs run, and whether the access violation is the driver's under concurrent device creation (an engine need if so).

## Fix

## Verified by
