# Requirements: Rendering Behavior

## Purpose

This document holds the Behavior requirements of the
[Rendering](../architecture/rendering.md) area. The purpose, scope, and
verification of the area are described in that document.

## Scope

The scope of the area is described in
[Rendering](../architecture/rendering.md#scope). This document contains
requirements of the following groups:

- Culling, LOD, and visibility
- Map and synthetic-world features
- Validation

## Culling, LOD, and visibility

### REQ-CSWU-RENDER-006: Unity culling

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode culling test with preserved
  activation state

CSWUnity shall support Unity frustum, layer, shadow, occlusion, and
renderer-specific culling without breaking CSW activation semantics.

### REQ-CSWU-RENDER-008: Activation without rebuild

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test of activation changes without rebuilding resources

LOD or streamed visibility changes shall not rebuild immutable geometry or
resources when activation alone is sufficient.

## Map and synthetic-world features

### REQ-CSWU-RENDER-014: Terrain and asset baseline

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** Graphics-enabled PlayMode resource test plus PlayMode test
  with a published GZD map (intended fixtures: `SceneResourceTests`,
  `NativeMapTests`)

The baseline realization shall support the terrain, static geometry, material,
texture, transform, hierarchy, reference-instance, update, activation, and
delete payloads required by the selected GZD compatibility profile.

### REQ-CSWU-RENDER-016: Quality profiles

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Test selecting each quality profile

The host shall be able to select a quality profile controlling loaders, LOD,
shadows, texture and mesh budgets, dense-feature density, environment effects,
sensor effects, and main-thread/GPU budgets.

### REQ-CSWU-RENDER-017: Graceful degradation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M5
- **Use cases:** UC-CSWU-010, UC-CSWU-016
- **Verification:** Test of fallback behavior for each missing capability

When hardware, render-pipeline, shader, compute, or budget capabilities are
missing, optional modules shall disable or select a documented fallback without
invalidating core map and object streaming.

## Validation

### REQ-CSWU-RENDER-020: Rendering metrics

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Verification:** Test reading each listed rendering metric

Metrics shall include source and Unity culling counts, visible instances,
prepared/published/stale work, draw calls, batches, triangles, texture and mesh
memory, cache behavior, upload volume, main-thread time, worker time, GPU time,
and end-to-end visibility latency.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Rendering](../architecture/rendering.md#verification).

## Related Documentation

- [Rendering requirements](../architecture/rendering.md)
- [Rendering architecture](../../design/rendering.md)
- [Scene control and threaded realization](../../design/scene-control-and-threading.md)
- [Synthetic world architecture](../../design/synthetic-world.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
