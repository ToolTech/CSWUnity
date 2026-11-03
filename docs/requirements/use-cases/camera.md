# Use Cases: Camera and Movement

## Scope

These use cases define platform-independent camera and movement behavior shared
by CSWUnity, CSWUnreal, and other viewer realizations.
They do not prescribe input bindings, application UI, selection behavior, or a
specific viewer camera rig.

The host maps mouse, keyboard, touch, controller, network, replay, or other
input to semantic operations. Core services own the movement, coordinate,
query, constraint, transition, and outcome contracts.

Position terminology follows
[Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md).
The camera's authoritative position is a Global 3D Position in the active Map
Coordinate Context. The coordinate boundary localizes that position into the
active Local 3D Frame, and a viewer adapter maps the Local 3D Position into a
specifically named native camera-position frame.

### Actors

| Actor | Responsibility |
| --- | --- |
| User | Navigates and inspects the synthetic world |
| Host | Supplies input, viewport, configuration, targets, and lifecycle |
| Camera service | Executes semantic camera and movement operations |
| Map service | Supplies extent, coordinate context, lifecycle, and queries |
| Scene service | Consumes the active camera for streaming, culling, and LOD |

## Map lifecycle and framing

### UC-CSWU-101: Establish the initial map view

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MAPCTRL-003, REQ-CSWU-COORD-010
- **Former ID:** CSWU-UC-CAM-001

#### Preconditions

Stable map extent and coordinate metadata are available.

#### Main Flow

1. The map becomes ready.
2. The camera service centers and fits the metadata extent.
3. Local north is placed at the top of the view.
4. The configured initial tilt is applied.

#### Postconditions

The initial view is available immediately and does not wait for all streamed
objects or reframe as content arrives.

### UC-CSWU-102: Fit the active map

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MAPCTRL-003
- **Former ID:** CSWU-UC-CAM-002

#### Main Flow

The host requests Fit Map. The camera frames the stable metadata extent inside
the supplied usable viewport and padding while preserving current heading and
tilt.

### UC-CSWU-103: Return to a host-defined home view

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-003

#### Main Flow

The host requests Home. The camera returns to the configured home pose. Home
and Fit Map remain separate semantic actions.

### UC-CSWU-104: Preserve view across viewport changes

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-004

#### Main Flow

When viewport dimensions or usable bounds change, the camera preserves the
current focus and visible scale as closely as the new aspect ratio permits.

### UC-CSWU-105: Handle map loss or replacement

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MAPCTRL-001, REQ-CSWU-MAPCTRL-002,
  REQ-CSWU-SM-039
- **Former ID:** CSWU-UC-CAM-005

#### Main Flow

When the active map unloads or is replaced, map-dependent actions and tracking
stop. Requests return an unavailable outcome until compatible metadata for a
new map is ready.

## Manual map navigation

### UC-CSWU-110: Pan a projected map

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-010

#### Main Flow

The user pans a flat or projected map. The grabbed map point remains under the
input anchor while heading, tilt, and zoom are preserved. Navigation is not
implicitly clamped to map extent.

### UC-CSWU-111: Pan a spherical map

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-009
- **Former ID:** CSWU-UC-CAM-011

#### Main Flow

The user pans a geodetic or geocentric map as a globe. The local heading,
tilt, and zoom are preserved across poles and coordinate seams without jumps,
unexpected roll, or reversed controls.

### UC-CSWU-112: Zoom around an anchor

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-009
- **Former ID:** CSWU-UC-CAM-012

#### Main Flow

When an input anchor resolves to the map, zoom preserves that point on screen.
Otherwise, the camera uses the current viewport focus or last valid focus.
Zoom rate scales with view distance and respects configured limits.

### UC-CSWU-113: Orbit around a map focus

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-009
- **Former ID:** CSWU-UC-CAM-013

#### Main Flow

The user changes heading and tilt around the current focus. The view remains
upright. If no current intersection exists, the last valid focus is used; if
none exists, the operation reports unavailable.

### UC-CSWU-114: Reset local north

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-COORD-010
- **Former ID:** CSWU-UC-CAM-014

#### Main Flow

The host requests Reset North. The camera changes only heading so that local
geographic north appears at the top of the viewport. Focus, distance, and tilt
remain unchanged, including on spherical maps near poles or seams.

### UC-CSWU-115: Navigate in free flight

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-008
- **Former ID:** CSWU-UC-CAM-015

#### Main Flow

The host selects free flight. The user can translate and rotate in three
dimensions according to the active speed, acceleration, damping, local-up,
and constraint policies.

### UC-CSWU-116: Stop manual movement

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-016

#### Main Flow

When movement input ends, the movement model follows its declared stop policy.
The default direct-navigation profile stops immediately; inertia is provided
only by a model or continued host input that explicitly requests it.

## Focus and semantic movement

### UC-CSWU-120: Focus bounds

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-020

#### Main Flow

The host supplies one or more valid bounds in map coordinates. The camera
centers and frames their combined extent inside the usable viewport while
preserving heading and tilt.

### UC-CSWU-121: Focus a position

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-COORD-003
- **Former ID:** CSWU-UC-CAM-021

#### Main Flow

The host supplies a valid `GeoPosition` or Global 3D Position. After conversion
to the active Map Coordinate Context, it becomes the current focus while
distance is preserved unless the request supplies a target distance.

### UC-CSWU-122: Look, move, or fly to a target

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-COORD-003, REQ-CSWU-MOVE-006
- **Former ID:** CSWU-UC-CAM-022

#### Main Flow

The host requests a semantic look-at, move-to, or fly-to operation using a
`GeoPosition` or Global 3D Position. The operation applies the selected
transition and active constraints without requiring a platform-specific Viewer
Position.

On a spherical map, long-distance movement follows a continuous valid path and
does not pass through the reference ellipsoid.

### UC-CSWU-123: Resolve invalid targets

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-COORD-009
- **Former ID:** CSWU-UC-CAM-023

#### Main Flow

If bounds, position, focus, or path cannot be resolved, the camera remains at
its current valid pose and reports unavailable rather than applying a partial
movement.

## Constraints and ground following

### UC-CSWU-130: Apply a movement constraint profile

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-006
- **Former ID:** CSWU-UC-CAM-030

#### Main Flow

The host selects a constraint profile. Its zoom, clearance, collision, slope,
pitch, speed, acceleration, and bounds rules apply consistently to manual and
programmatic movement.

### UC-CSWU-131: Move along a constraint boundary

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-006
- **Former ID:** CSWU-UC-CAM-031

#### Main Flow

When requested motion crosses a boundary, the camera stops at the boundary
without jumping and can continue along it when the remaining movement permits.

### UC-CSWU-132: Recover from an invalid pose

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-006
- **Former ID:** CSWU-UC-CAM-032

#### Main Flow

When a stricter constraint profile is enabled while the current pose is
invalid, the camera resolves the nearest valid pose according to the profile
and reports the correction.

### UC-CSWU-133: Follow terrain as a flying carpet

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-007, REQ-CSWU-MOVE-010,
  REQ-CSWU-MOVE-011
- **Former ID:** CSWU-UC-CAM-033

#### Main Flow

The user moves over the map while the model maintains configured clearance
through asynchronous surface queries. Height and optional orientation changes
are smoothed.

#### Alternative and Error Flows

If surface data is unavailable, the configured hold, limited-inertial, safe
climb, or stop policy is applied without blocking the viewer update loop.

## Transitions and outcomes

### UC-CSWU-140: Select immediate or animated movement

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-040

#### Main Flow

The host chooses whether a discrete action is immediate or animated. Initial
map framing is immediate unless a future profile explicitly defines otherwise.

### UC-CSWU-141: Interrupt an active transition

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-041

#### Main Flow

Manual movement, an explicit cancellation, a newer request, map removal, or
target invalidation cancels the active transition. The current valid pose is
preserved.

### UC-CSWU-142: Observe action outcome

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** None
- **Former ID:** CSWU-UC-CAM-042

#### Main Flow

Every discrete or animated request reports a correlation identity and one of
completed, cancelled, unavailable, or failed.

## Target tracking

### UC-CSWU-150: Track a moving target

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-015
- **Former ID:** CSWU-UC-CAM-050

#### Main Flow

The host starts tracking a target resolved to a Global 3D Position. The camera
preserves configured target framing, distance, heading, and tilt using
immediate or smoothed tracking.

### UC-CSWU-151: Stop tracking

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-015
- **Former ID:** CSWU-UC-CAM-051

#### Main Flow

Manual navigation, another discrete action, target removal, or an unreachable
constraint state stops tracking at the last valid pose and reports the reason.
A host profile can request a subsequent Home action.

### UC-CSWU-152: Follow in a target-relative frame

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-015
- **Former ID:** CSWU-UC-CAM-052

#### Main Flow

The host selects attached tracking. The camera preserves a configurable
target-relative pose and optional interactive adjustment while the target
moves and rotates.

## Streaming integration and observation

### UC-CSWU-160: Navigate during streaming

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-MOVE-007
- **Former ID:** CSWU-UC-CAM-060

#### Main Flow

Navigation remains responsive after stable map metadata is ready while detail
continues to stream. New and removed content does not implicitly move or
reframe the camera.

### UC-CSWU-161: Preserve pose during origin rebasing

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** User
- **Related requirements:** REQ-CSWU-COORD-008, REQ-CSWU-MOVE-004
- **Former ID:** CSWU-UC-CAM-061

#### Main Flow

When the active Local Origin Offset changes and a new Local 3D Frame generation
is published, the authoritative Global 3D Camera Pose remains unchanged and the
user observes no jump.

### UC-CSWU-162: Drive scene streaming

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Scene service
- **Related requirements:** REQ-CSWU-POS-001, REQ-CSWU-POS-003,
  REQ-CSWU-SM-020, REQ-CSWU-SM-021, REQ-CSWU-SM-022, REQ-CSWU-SM-041,
  REQ-CSWU-SM-044, REQ-CSWU-SM-049, REQ-CSWU-SM-050
- **Former ID:** CSWU-UC-CAM-062

#### Main Flow

The camera service publishes a coherent position, orientation, projection,
viewport, LOD, and render-time snapshot for CSW SceneManager traversal.

### UC-CSWU-163: Observe camera state

- **Status:** Pending
- **Milestone:** M3
- **Primary actor:** Host
- **Related requirements:** REQ-CSWU-MOVE-016
- **Former ID:** CSWU-UC-CAM-063

#### Main Flow

The host can observe authoritative pose, focus, heading, tilt, distance,
movement model, transition state, and active constraints without taking
ownership of camera state.

## Deferred Candidates

This list is non-normative. Its entries are not use cases or requirements and
have no status or milestone.

The following remain candidates rather than initial requirements:

- multiple simultaneous cameras or viewports;
- runtime perspective/orthographic switching;
- saved views and bookmarks;
- scripted paths, tours, and recordings;
- cross-process camera synchronization;
- fixed-angle heading-step actions; and
- dedicated top-down navigation.

## Related Documentation

- [Movement model requirements](../architecture/movement-models.md)
- [Scene control requirements](../functional/scene-control.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Movement architecture](../../design/movement-models.md)
- [Scene Control Architecture requirements](../architecture/scene-control.md)
- [Movement Models Behavior requirements](../functional/movement-models.md)
- [Use-case catalog](../README.md#use-cases)
- [Milestones](../milestones.md)
