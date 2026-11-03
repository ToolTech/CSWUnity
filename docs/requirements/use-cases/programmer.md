# Use Cases: Programmer

## Scope

This document contains the programmer use cases of the CSWUnity use-case
catalog. Use cases are non-normative and contain no requirements of their own.
The purpose, inclusion rules, and actors of the catalog are described in
[the use-case catalog](../README.md#use-cases).

## Use Cases

### UC-CSWU-007: Integrate CSWUnity into a host project

- **Status:** Pending
- **Milestone:** M1
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSWU-PKG-001, REQ-CSWU-PKG-002,
  REQ-CSWU-PKG-004, REQ-CSWU-INIT-001, REQ-CSWU-INIT-008, REQ-CSWU-QUAL-004,
  REQ-CSWU-SM-011, REQ-CSWU-SM-034, REQ-CSWU-SM-035, REQ-CSWU-SM-047
- **Former ID:** CSWU-UC-PKG-001

#### Main Flow

1. The programmer installs supported Unity packages.
2. The programmer creates runtime, realization, resource, movement, and
   optional feature profiles.
3. The programmer adds a runtime host and supplies explicit camera and
   lifecycle references.
4. The project starts without depending on the CSWUnity sample scene.

### UC-CSWU-008: Connect an external object system

- **Status:** Pending
- **Milestone:** M4
- **Primary actor:** System integrator
- **Related requirements:** REQ-CSWU-OBJ-001, REQ-CSWU-OBJ-002,
  REQ-CSWU-OBJ-005, REQ-CSWU-OBJ-006, REQ-CSWU-OBJ-007, REQ-CSWU-OBJ-008,
  REQ-CSWU-OBJ-009, REQ-CSWU-OBJ-010, REQ-CSWU-OBJ-019
- **Former ID:** CSWU-UC-OBJ-002

#### Main Flow

1. The programmer implements or configures a transport adapter.
2. The adapter maps external identity, type, time, authority, coordinates,
   attributes, and events into the CSW object contract.
3. The object store validates and orders updates.
4. Existing Unity presenters consume the normalized object state.

#### Postconditions

Transport code does not depend on Unity objects, and presenters do not depend
on HLA, MCP, Distribution, DIS, or another transport.

### UC-CSWU-009: Add a world-object presenter

- **Status:** Pending
- **Milestone:** M4
- **Primary actor:** Feature programmer
- **Related requirements:** REQ-CSWU-OBJ-017, REQ-CSWU-OBJ-018, REQ-CSWU-OBJ-021
- **Former ID:** CSWU-UC-OBJ-003

#### Main Flow

1. The programmer declares the object schema and presentation capabilities.
2. The programmer implements create, update, visibility, and release behavior.
3. The presenter is registered in a realization profile.
4. Validation detects missing dependencies or ambiguous selectors.

### UC-CSWU-010: Add an environment realization

- **Status:** Pending
- **Milestone:** M5
- **Primary actor:** Feature programmer
- **Related requirements:** REQ-CSWU-ENV-001, REQ-CSWU-ENV-012,
  REQ-CSWU-WORLD-004, REQ-CSWU-WORLD-005, REQ-CSWU-RENDER-015,
  REQ-CSWU-RENDER-017
- **Former ID:** CSWU-UC-ENV-002

#### Main Flow

1. The programmer selects an environment state field such as wind, fog,
   clouds, precipitation, water, lighting, or vegetation state.
2. The programmer implements a render-pipeline-specific Unity module.
3. The module declares required semantic inputs and hardware capabilities.
4. The module subscribes to environment snapshots and Local 3D Frame updates.

### UC-CSWU-011: Add a movement model

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Feature programmer
- **Related requirements:** REQ-CSWU-MOVE-002, REQ-CSWU-MOVE-003,
  REQ-CSWU-MOVE-005, REQ-CSWU-MOVE-012, REQ-CSWU-MOVE-013
- **Former ID:** CSWU-UC-MOVE-002

#### Main Flow

1. The programmer implements the movement-model contract without reading Unity
   input directly.
2. The model consumes immutable input, coordinates, time, and optional surface
   samples.
3. A profile exposes model parameters.
4. The same model runs with live input, recorded input, and automated tests.

### UC-CSWU-012: Add a future streamed format

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Feature programmer
- **Related requirements:** REQ-CSWU-MAP-002, REQ-CSWU-MAP-003,
  REQ-CSWU-ASSET-005
- **Former ID:** CSWU-UC-FORMAT-001

#### Main Flow

1. GizmoSDK or CSW SceneManager gains support for the source format.
2. The format produces the common scene and resource payloads.
3. Existing compatible CSWUnity realizers continue to work.
4. Format-specific metadata is handled by optional typed extensions.

## Related Documentation

- [Next-Generation Architecture requirements](../architecture/next-generation-architecture.md)
- [Scene Control Architecture requirements](../architecture/scene-control.md)
- [Streaming Map Input Architecture requirements](../architecture/streaming-map-input.md)
- [Runtime Initialization requirements](../architecture/runtime-initialization.md)
- [Movement Models requirements](../architecture/movement-models.md)
- [Movement Models Behavior requirements](../functional/movement-models.md)
- [External Object Integration requirements](../architecture/object-integration.md)
- [Rendering requirements](../architecture/rendering.md)
- [Rendering Behavior requirements](../functional/rendering.md)
- [Synthetic World requirements](../architecture/synthetic-world.md)
- [Use-case catalog](../README.md#use-cases)
- [Milestones](../milestones.md)
