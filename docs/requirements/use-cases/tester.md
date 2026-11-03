# Use Cases: Tester

## Scope

This document contains the tester use cases of the CSWUnity use-case catalog.
Use cases are non-normative and contain no requirements of their own. The
purpose, inclusion rules, and actors of the catalog are described in
[the use-case catalog](../README.md#use-cases).

## Use Cases

### UC-CSWU-013: Replay a deterministic scenario

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Tester
- **Related requirements:** REQ-CSWU-MOVE-003, REQ-CSWU-MOVE-013,
  REQ-CSWU-TIME-003, REQ-CSWU-OBJ-010
- **Former ID:** CSWU-UC-TEST-001

#### Main Flow

The tester replays recorded map, object, environment, time, and input updates
and verifies the same logical scene state, object transforms, environment
snapshots, and query results.

### UC-CSWU-014: Verify coordinate systems

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Tester
- **Related requirements:** REQ-CSWU-COORD-001, REQ-CSWU-COORD-008,
  REQ-CSWU-COORD-009, REQ-CSWU-MOVE-017, REQ-CSWU-QUERY-001, REQ-CSWU-QUERY-002
- **Former ID:** CSWU-UC-TEST-002

#### Main Flow

The tester runs representative UTM, geodetic, and geocentric maps through
known control points, camera paths, intersections, ground clamps, Local Origin
Offset rebases, and round-trip conversions.

### UC-CSWU-015: Verify streaming and rendering performance

- **Status:** Pending
- **Milestone:** M2
- **Primary actor:** Tester
- **Related requirements:** REQ-CSWU-PERF-001, REQ-CSWU-PERF-002,
  REQ-CSWU-PERF-004, REQ-CSWU-PERF-008, REQ-CSWU-PERF-009, REQ-CSWU-RENDER-019,
  REQ-CSWU-RENDER-020
- **Former ID:** CSWU-UC-TEST-003

#### Main Flow

The tester runs a published camera path and external-object load against a
versioned map and environment scenario. Automated gates compare frame time,
main-thread streaming time, latency, queue depth, memory, allocation, visible
content, and resource reuse with the release profile.

### UC-CSWU-016: Verify overload and failure handling

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Tester
- **Related requirements:** REQ-CSWU-QUAL-001, REQ-CSWU-PERF-002,
  REQ-CSWU-OBJ-011, REQ-CSWU-OBJ-012, REQ-CSWU-RENDER-017, REQ-CSWU-RENDER-022,
  REQ-CSWU-QUERY-003
- **Former ID:** CSWU-UC-TEST-004

#### Main Flow

The tester injects unavailable maps, malformed updates, connection loss,
out-of-order messages, queue overload, missing shaders, unsupported
capabilities, cancelled queries, and shutdown during active loading.

### UC-CSWU-017: Verify visual realization

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Tester
- **Related requirements:** REQ-CSWU-RENDER-021
- **Former ID:** CSWU-UC-TEST-005

#### Main Flow

The tester captures approved viewpoints and environment states for supported
render pipelines and compares geometry, material, lighting, weather,
visibility, object placement, and LOD behavior against tolerances or approved
references.

## Related Documentation

- [Next-Generation Architecture requirements](../architecture/next-generation-architecture.md)
- [Next-Generation Architecture Behavior requirements](../functional/next-generation-architecture.md)
- [Scene Control requirements](../functional/scene-control.md)
- [Scene Control Architecture requirements](../architecture/scene-control.md)
- [Movement Models requirements](../architecture/movement-models.md)
- [External Object Integration requirements](../architecture/object-integration.md)
- [External Object Integration Behavior requirements](../functional/object-integration.md)
- [Rendering requirements](../architecture/rendering.md)
- [Rendering Behavior requirements](../functional/rendering.md)
- [Synthetic World Behavior requirements](../functional/synthetic-world.md)
- [Use-case catalog](../README.md#use-cases)
- [Milestones](../milestones.md)
