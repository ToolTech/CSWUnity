# Use Cases: Operator

## Scope

This document contains the operator use cases of the CSWUnity use-case catalog.
Use cases are non-normative and contain no requirements of their own. The
purpose, inclusion rules, and actors of the catalog are described in
[the use-case catalog](../README.md#use-cases).

## Use Cases

### UC-CSWU-001: Explore a streamed map

- **Status:** Pending
- **Milestone:** M1
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-MAP-001, REQ-CSWU-MAP-006,
  REQ-CSWU-MAP-008, REQ-CSWU-MAPCTRL-001, REQ-CSWU-POS-003,
  REQ-CSWU-RENDER-004, REQ-CSWU-RENDER-005, REQ-CSWU-RENDER-014,
  REQ-CSWU-PERF-001, REQ-CSWU-PERF-009, REQ-CSWU-SM-007, REQ-CSWU-SM-018,
  REQ-CSWU-SM-020, REQ-CSWU-SM-027, REQ-CSWU-SM-031, REQ-CSWU-SM-039,
  REQ-CSWU-SM-041, REQ-CSWU-SM-042, REQ-CSWU-SM-043, REQ-CSWU-SM-044,
  REQ-CSWU-SM-049
- **Former ID:** CSWU-UC-MAP-001

#### Preconditions

- CSWUnity is running in a host Unity project.
- A compatible GZD map is available.
- A camera and movement profile are selected.

#### Main Flow

1. The operator opens the map.
2. CSW SceneManager interprets and streams the map.
3. CSWUnity realizes visible terrain, geometry, materials, and assets.
4. The operator moves the camera.
5. LOD and dynamic content change without loading the complete map at once.

#### Postconditions

The map remains visually coherent and responsive within the active performance
profile.

### UC-CSWU-002: Work in different map coordinate systems

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-COORD-001, REQ-CSWU-COORD-002,
  REQ-CSWU-COORD-003, REQ-CSWU-COORD-004, REQ-CSWU-COORD-006,
  REQ-CSWU-COORD-009, REQ-CSWU-MAP-007, REQ-CSWU-POS-002
- **Former ID:** CSWU-UC-COORD-001

#### Main Flow

1. The operator or application opens a UTM, geodetic, or geocentric map.
2. The map establishes its Map Coordinate Context.
3. Camera, scene objects, and spatial queries use Global 3D Positions in that
   context.
4. The coordinate boundary derives Local 3D Positions for the active Local 3D
   Frame without changing the authoritative Global 3D Positions.
5. The viewer adapter maps those values to explicitly named native viewer
   positions.
6. The operator views each `GeoPosition` in the representation required by the
   host application.

#### Postconditions

Map content and scene objects align, and round-trip conversion between
`GeoPosition`, Global 3D Position, Local 3D Position, and supported native
viewer positions remains within the configured tolerance.

### UC-CSWU-003: Navigate with a flying-carpet model

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-MOVE-007, REQ-CSWU-MOVE-010,
  REQ-CSWU-MOVE-011, REQ-CSWU-QUERY-002
- **Former ID:** CSWU-UC-MOVE-001

#### Main Flow

1. The operator selects the ground-following movement profile.
2. The operator supplies forward, lateral, vertical-clearance, and look input.
3. The movement model requests surface information asynchronously.
4. The camera follows terrain at the configured clearance and smoothing.
5. If surface data is unavailable, the configured fallback policy is applied.

#### Postconditions

The camera moves smoothly without blocking the Unity main thread or crossing
the configured minimum clearance.

### UC-CSWU-004: Observe changing weather and wind

- **Status:** Pending
- **Milestone:** M5
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-TIME-002, REQ-CSWU-ENV-001,
  REQ-CSWU-ENV-004, REQ-CSWU-ENV-006, REQ-CSWU-ENV-007, REQ-CSWU-ENV-008,
  REQ-CSWU-ENV-012, REQ-CSWU-WORLD-002, REQ-CSWU-WORLD-007, REQ-CSWU-RENDER-015
- **Former ID:** CSWU-UC-ENV-001

#### Main Flow

1. An authoritative environment source updates time, wind, visibility, cloud,
   precipitation, or other available environment state.
2. Physical, object-motion, sensor, and presentation consumers receive
   timestamped state from the same environment context.
3. Installed Unity feature modules update their visual realization.

#### Postconditions

The synthetic world changes coherently. Unsupported visual features report a
capability gap rather than changing simulation semantics.

### UC-CSWU-005: Observe externally controlled objects

- **Status:** Pending
- **Milestone:** M4
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-OBJ-001, REQ-CSWU-OBJ-003,
  REQ-CSWU-OBJ-013, REQ-CSWU-OBJ-014, REQ-CSWU-OBJ-015, REQ-CSWU-OBJ-016,
  REQ-CSWU-OBJ-017, REQ-CSWU-OBJ-018
- **Former ID:** CSWU-UC-OBJ-001

#### Main Flow

1. An external system creates one or more typed world objects.
2. It publishes timestamped state updates and events.
3. CSWUnity maps each object to a registered Unity presenter.
4. Motion updates are interpolated or extrapolated according to policy.
5. Removal releases the Unity representation and shared resources.

#### Postconditions

Objects move smoothly, preserve identity, and remain consistent across delayed,
duplicate, or partial updates.

### UC-CSWU-006: Query the synthetic world

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Operator
- **Related requirements:** REQ-CSWU-QUERY-001, REQ-CSWU-QUERY-002,
  REQ-CSWU-QUERY-003, REQ-CSWU-QUERY-004, REQ-CSWU-SM-033
- **Former ID:** CSWU-UC-QUERY-001

#### Main Flow

1. The operator or application submits a ray intersection or ground-clamp
   request.
2. CSW SceneManager evaluates the streamed scene and dynamic data.
3. CSWUnity returns a correlated result containing position, normal, up,
   distance or altitude, and stable object identity where applicable.

#### Postconditions

The request is asynchronous, cancellable, and does not stall rendering.

## Related Documentation

- [Next-Generation Architecture requirements](../architecture/next-generation-architecture.md)
- [Scene Control requirements](../functional/scene-control.md)
- [Scene Control Architecture requirements](../architecture/scene-control.md)
- [Streaming Map Input requirements](../functional/streaming-map-input.md)
- [Movement Models requirements](../architecture/movement-models.md)
- [Movement Models Behavior requirements](../functional/movement-models.md)
- [External Object Integration requirements](../architecture/object-integration.md)
- [External Object Integration Behavior requirements](../functional/object-integration.md)
- [Rendering requirements](../architecture/rendering.md)
- [Rendering Behavior requirements](../functional/rendering.md)
- [Synthetic World requirements](../architecture/synthetic-world.md)
- [Synthetic World Behavior requirements](../functional/synthetic-world.md)
- [Use-case catalog](../README.md#use-cases)
- [Milestones](../milestones.md)
