# Use Cases: Generic CSW Baseline

## Scope

These use cases are non-normative stories describing expected use. Normative
statements are kept in [Generic CSW requirements](requirements.md).

This document is hosted content of the CSW owner (owner code `CSW`) until the
CSW repository has its own requirements root. Its use-case IDs keep the owner
code `CSW`, so they do not change when the document moves. The CSW owner has no
milestones yet, so every use case is `Unassigned`.

## Use Cases

### UC-CSW-001: Control a selected 3D engine through CSW

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host application
- **Related requirements:** REQ-CSW-ARCH-001, REQ-CSW-ARCH-002, REQ-CSW-ARCH-003
- **Former ID:** CSW-UC-001

#### Main Flow

A host application uses the generic CSW API to control 3D functionality. The
same concepts are recognizable whether CSWUnity or CSWUnreal is selected.

### UC-CSW-002: Implement functionality in an engine

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Engine developer
- **Related requirements:** REQ-CSW-ARCH-001, REQ-CSW-ARCH-003, REQ-CSW-SM-005
- **Former ID:** CSW-UC-002

#### Main Flow

An engine developer implements a realization or hook inside CSWUnity or
CSWUnreal and registers it with CSW. Engine-native code can also use the
realization directly for an engine-only workflow.

### UC-CSW-003: Stream scene changes into an engine

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Selected engine realization
- **Related requirements:** REQ-CSW-SM-001, REQ-CSW-SM-002, REQ-CSW-SM-003,
  REQ-CSW-SM-004, REQ-CSW-SM-005, REQ-CSW-SM-006, REQ-CSW-SM-008
- **Former ID:** CSW-UC-003

#### Main Flow

SceneManager processes Gizmo3D scene changes asynchronously. The selected
engine receives buffered new, update, delete, frame, status, and response work,
then executes it through registered factories in its own update context.

### UC-CSW-004: Request and poll an asynchronous result

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Caller
- **Related requirements:** REQ-CSW-SM-007
- **Former ID:** CSW-UC-004

#### Main Flow

A caller submits a request with a correlation identity and continues working.
The result later becomes available through a callback, event, or polling API
using the same identity.

### UC-CSW-005: Work with a composed map

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host
- **Related requirements:** REQ-CSW-MM-001, REQ-CSW-MM-002
- **Former ID:** CSW-UC-005

#### Main Flow

A host requests a map, adds or removes URL-based map parts, and works primarily
with the map as a whole. It can inspect projection, aggregate map information,
layers, drawing mode, and individual part metadata when needed.

### UC-CSW-006: Navigate a map

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Observer
- **Related requirements:** REQ-CSW-CV-001
- **Former ID:** CSW-UC-006

#### Main Flow

An Observer receives mouse or system input and uses a MotionModel such as map
orbit to move its Camera around a selected map. Its View shows what the Camera
sees.

### UC-CSW-007: Use another Camera for a View

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Engine View
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-002
- **Former ID:** CSW-UC-007

#### Main Flow

SceneManager has a Main Camera describing the primary area of interest. A View
inside Unity or Unreal can use another Camera without moving the Main Camera.

### UC-CSW-008: Position a Camera across coordinate layers

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host
- **Related requirements:** REQ-CSW-CV-002
- **Former ID:** CSW-UC-008

#### Main Flow

A host positions and aims a Camera using generic geographic or global 3D
coordinates. The selected realization maps the same Camera to its current
engine coordinate system.

### UC-CSW-009: Work with world objects and events

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host
- **Related requirements:** REQ-CSW-OE-001
- **Former ID:** CSW-UC-009

#### Main Flow

A host accesses map objects, including entities and static objects, through an
Object API and reacts to updates and occurrences through an Event API.

## Related Documentation

- [Generic CSW baseline requirements](requirements.md)
- [Coordinate nomenclature and mapping](coordinate-nomenclature.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
