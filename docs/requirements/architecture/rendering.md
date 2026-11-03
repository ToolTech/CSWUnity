# Requirements: Rendering

## Purpose

CSWUnity shall render large streamed maps, external world objects, and optional
synthetic-environment effects efficiently and coherently in Unity.

## Scope

This document covers the rendering model, culling, LOD, and visibility,
resources and threading, map and synthetic-world features, and rendering
validation. This document holds the Principle and Mechanism requirements of the
area. Its Behavior requirements are in
[Rendering Behavior](../functional/rendering.md).

## Rendering model

### REQ-CSWU-RENDER-001: Coherent coordinate context

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Integration test of spatial and temporal coherence across
  map, object, environment, and camera state

Map geometry, external objects, environment effects, spatial queries, and
camera state shall use the same committed world time, Map Coordinate Context,
Local 3D Frame identity and generation, and Viewer Transform.

### REQ-CSWU-RENDER-002: Representation strategies

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test per representation strategy

The realization registry shall support multiple Unity representation
strategies, including:

- GameObjects/components for interactive semantic objects;
- shared meshes and materials for reusable assets;
- GPU instancing or indirect rendering for dense repeated content;
- batched or data-oriented representations for high-volume content; and
- render-pipeline-specific passes for optional effects.

### REQ-CSWU-RENDER-003: Strategy selection

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review plus capability-based strategy selection test

Representation selection shall be based on declared payload and rendering
capabilities, not hard-coded native node type checks in a monolithic manager.

### REQ-CSWU-RENDER-004: Frame coherence

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** EditMode test proving that rendering observes only committed
  frames (intended fixture: `SceneCommandTests`)

Rendering modules shall observe only committed map, object, environment, and
Local 3D Frame state. Partial scene-buffer application shall not be visible to
dependent modules.

## Culling, LOD, and visibility

### REQ-CSWU-RENDER-005: Source LOD

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** Design review of LOD ownership

CSW SceneManager and GizmoSDK shall retain responsibility for streamed-map
traversal and source LOD.

### REQ-CSWU-RENDER-007: Dense feature culling

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Dense-feature profiling scenario on the release performance
  profile

Dense features such as foliage shall support GPU or otherwise scalable
placement, culling, and drawing where required by the release profile.

## Resources and threading

### REQ-CSWU-RENDER-009: Shared resources

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test of resource leases and
  sharing (intended fixture: `SceneResourceTests`)

Meshes, textures, materials, shaders, and referenced assets shall use explicit
sharing, leases, pooling, caching, budgets, and eviction.

### REQ-CSWU-RENDER-010: Threaded preparation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Design review of the worker-eligible preparation stages

Pure geometry, image, hierarchy, and batching preparation shall be eligible for
workers, Unity Jobs, or Burst. Final Unity publication shall follow supported
thread-affinity rules.

### REQ-CSWU-RENDER-011: Bounded publication

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** None
- **Verification:** EditMode test of publication budgets and stale-work
  cancellation (intended fixture: `SceneCommandTests`)

Main-thread resource and hierarchy publication shall use configurable time and
item budgets with stale-work cancellation.

### REQ-CSWU-RENDER-012: Allocation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Allocation profiling scenario after warm-up

Steady-state streaming within pre-sized capacities shall produce no ordinary
managed garbage allocation on the Unity main thread after warm-up.

### REQ-CSWU-RENDER-013: Upload control

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Upload-policy tests plus profiling scenario (intended
  fixture: `TextureMipUploadPolicyTests`)

Geometry and texture paths shall minimize redundant native/managed/GPU copies
and expose upload volume and latency.

## Map and synthetic-world features

### REQ-CSWU-RENDER-015: Optional environment modules

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** UC-CSWU-004, UC-CSWU-010
- **Verification:** Package review plus test removing each optional environment
  module

Weather, clouds, fog, precipitation, water, vegetation, lighting, and sensor
effects shall be optional modules consuming common world/environment state.

### REQ-CSWU-RENDER-018: Large-world precision

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Rebase test of camera-relative shader data

Rendering shall support Local Origin Offset rebasing and camera-relative shader
data while preserving double-precision authoritative positions.

## Validation

### REQ-CSWU-RENDER-019: Performance profile

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Verification:** Release review of each published rendering configuration

Every validated rendering configuration shall define map, object count,
environment state, camera path, hardware, Unity/render-pipeline version,
quality settings, frame target, CPU/GPU budgets, latency, memory, and allocation
limits.

### REQ-CSWU-RENDER-021: Visual tests

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSWU-017
- **Verification:** Visual comparison against approved reference images

Supported profiles shall include controlled visual tests for geometry,
materials, LOD, shadows, Local Origin Offset changes, weather, lighting,
external objects, and optional sensor modes.

### REQ-CSWU-RENDER-022: Stress tests

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** UC-CSWU-016
- **Verification:** Stress scenario for each listed condition

Stress tests shall cover rapid camera movement, map replacement, dense external
updates, environment transitions, queue overload, asset churn, Local 3D Frame
rebasing, and shutdown during active work.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in [Rendering Behavior](../functional/rendering.md),
is verified when the following acceptance criteria hold:

- Representative maps and synthetic-world scenarios meet their published CPU,
  GPU, memory, allocation, and latency profiles.
- Map, object, environment, and camera state remain spatially and temporally
  coherent.
- Optional rendering modules can be removed without changing core map or
  external-object contracts.
- Stale map or object work cannot become visible after delete, replacement, or
  Local 3D Frame generation change.

## Related Documentation

- [Rendering Behavior requirements](../functional/rendering.md)
- [Rendering architecture](../../design/rendering.md)
- [Scene control and threaded realization](../../design/scene-control-and-threading.md)
- [Synthetic world architecture](../../design/synthetic-world.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
