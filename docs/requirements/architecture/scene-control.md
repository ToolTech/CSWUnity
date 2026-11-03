# Requirements: Scene Control Architecture

## Purpose

This document holds the Principle and Mechanism requirements of the
[Scene Control](../functional/scene-control.md) area. The purpose, scope, and
verification of the area are described in that document.

## Scope

The scope of the area is described in
[Scene Control](../functional/scene-control.md#scope). This document contains
requirements of the following groups:

- Control levels
- Coordinate-system control
- Asset and scene-update control

## Control levels

### REQ-CSWU-CTRL-001: Component-level control

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** PlayMode test configuring streaming through components and
  serialized assets only

A host project shall be able to configure common streaming behavior through
Unity components and serialized assets without handling CSW command buffers
directly.

### REQ-CSWU-CTRL-002: Service-level control

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** API review of the typed runtime services against the listed
  areas

A host project shall have typed runtime services for map lifecycle,
coordinates, positions, spatial queries, assets, scene status, and
diagnostics.

### REQ-CSWU-CTRL-003: Extension-level control

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** API review of the extension surface and its threading and
  ownership documentation

Advanced integrations shall have a documented extension API for realization
factories, frame notifications, optional features, and correlated CSW
commands. Low-level buffer access shall be explicit and shall preserve
threading and ownership rules.

## Coordinate-system control

The normative terms and conversion stages are defined in
[Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md).

### REQ-CSWU-COORD-002: Map Coordinate Context

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-002
- **Depends on:** REQ-CSW-COORD-006, REQ-CSW-COORD-007, REQ-CSW-COORD-017
- **Verification:** Coordinate tests plus transport review proving that the Map
  Coordinate Context is established before dependent content and that Origin
  Global 3D Position is named separately from the Map Origin

For a georeferenced map, CSW SceneManager shall provide the CRS descriptor,
required metadata, Global 3D mapping, Map Origin `GeoPosition`, and stable
context identity or revision needed to establish the Map Coordinate Context
before dependent scene content is committed. A non-georeferenced Cartesian map
shall instead declare its explicit 3D frame and lack of geographic anchor.

The Map Origin shall remain a `GeoPosition`. Its mapped XYZ value shall be named
Origin Global 3D Position and shall not be passed or documented as though it
were the same value.

A transport may carry the derived Origin Global 3D Position for efficient scene
setup, but the field shall be named and typed accordingly and the Map Coordinate
Context shall retain the originating Map Origin `GeoPosition`.

### REQ-CSWU-COORD-004: Local 3D localization

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-002
- **Depends on:** REQ-CSW-COORD-002, REQ-CSW-COORD-008
- **Verification:** Coordinate tests of double-precision origin subtraction and
  checked narrowing to Local 3D Position

The shared coordinate boundary shall provide checked, bidirectional
localization between:

- a double-precision Global 3D Position; and
- a single-precision Local 3D Position associated with an explicit Local 3D
  Frame identity and generation.

The Local 3D Frame shall declare its double-precision Local Origin Offset,
basis, and units. Translation-only localization shall compute the Global 3D
offset in double precision before converting the result to `float`.
The subtraction result is a displacement vector and shall be called a Local 3D
Position only after it has been expressed in the declared local frame.

### REQ-CSWU-COORD-005: Precision boundary

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** None
- **Depends on:** REQ-CSW-COORD-008, REQ-CSW-COORD-009
- **Verification:** Precision tests proving that out-of-range and non-finite
  conversions fail explicitly

Numeric `GeoPosition` values, Global 3D Positions, Origin Global 3D Positions,
Local Origin Offsets, and all operands used for origin subtraction shall retain
double precision.

Conversion to Local 3D or viewer-native single precision shall occur only after
origin subtraction and any required double-precision basis or unit mapping.
The conversion shall reject non-finite or out-of-range values rather than
silently overflow.

### REQ-CSWU-COORD-006: Viewer mapping

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** UC-CSWU-002
- **Depends on:** REQ-CSW-COORD-012
- **Verification:** Design review of each viewer mapping plus Unity mapping
  tests

Each viewer adapter shall define a bidirectional mapping between Local 3D
Position and one specifically named native target frame. The mapping shall
state its axis permutation, signs, handedness conversion, unit scale, native
precision, and root, level, actor, or component transform.

Generic names such as *Unity Position* or *Unreal Position* shall not be used
where World, Transform Local, Level, Actor Relative, or Component Relative
semantics differ.

### REQ-CSWU-COORD-007: Semantic transform operations

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** None
- **Depends on:** REQ-CSW-COORD-028, REQ-CSW-COORD-029
- **Verification:** Position, offset, direction, normal, and orientation tests
  proving that translation applies only to positions

Coordinate and viewer adapters shall expose semantically distinct operations
for:

- positions;
- offset and direction vectors;
- normals; and
- orientations or bases.

Only position conversion shall apply the Local Origin Offset. Direction and
offset vectors shall use the applicable linear mapping, normals the normal
mapping, and orientations the basis mapping.

### REQ-CSWU-COORD-008: Local-frame rebasing

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-014, UC-CSWU-161
- **Depends on:** REQ-CSW-COORD-010
- **Verification:** Rebase tests proving unchanged Global 3D Positions, an
  atomic frame-generation change, and no visible jump

Changing a Local Origin Offset shall create a new Local 3D Frame generation.
The frame update and all dependent transforms shall become visible atomically.
Local 3D Positions from different identities or generations shall not be
combined.

Rebasing shall not modify the Map Origin, authoritative `GeoPosition` values,
Global 3D Positions, object identities, or core camera state.

### REQ-CSWU-COORD-011: Mapping API semantics

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** None
- **Verification:** API and test review proving that the listed mapping stages
  remain distinguishable

Generic and templated conversion helpers shall identify their semantic source
and target frames at the API or policy level. Scalar conversion, component
permutation, handedness conversion, unit scaling, origin translation, and
native hierarchy transforms shall remain distinguishable in implementation and
tests.

Matching C++ or C# scalar and vector types shall not be treated as proof that
two values use the same position frame.

## Asset and scene-update control

### REQ-CSWU-ASSET-005: Extensible payloads

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-012
- **Verification:** Test format provider that emits a typed optional extension
  without protocol changes

Scene payloads shall support typed optional extensions for future streamed
formats and feature data without changing the common frame and lifecycle
protocol.

### REQ-CSWU-SCENE-001: Typed buffers

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** API review of the managed buffer types and commands

The managed CSW contract shall expose at least `Generic`, `Error`, `Frame`,
`New`, `Update`, and `Delete` buffers and all commands required to consume
them.

### REQ-CSWU-SCENE-004: Thread-safe transfer

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review plus test proving that callbacks transfer work
  without Unity API calls
- **Superseded by:**
  [REQ-CSWU-SM-009](csw-scenemanager-integration.md#req-cswu-sm-009-edit-access-discipline),
  [REQ-CSWU-SM-010](csw-scenemanager-integration.md#req-cswu-sm-010-scenemanager-threads),
  [REQ-CSWU-SM-013](csw-scenemanager-integration.md#req-cswu-sm-013-receiver-handoff)

SceneManager callbacks shall transfer buffer work without invoking Unity APIs
and without retaining unlocked mutable native references.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Scene Control](../functional/scene-control.md#verification).

## Related Documentation

- [Scene Control requirements](../functional/scene-control.md)
- [CSW SceneManager Integration requirements](csw-scenemanager-integration.md)
- [Scene control and threaded realization](../../design/scene-control-and-threading.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Camera and movement use cases](../use-cases/camera.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
