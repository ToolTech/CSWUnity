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
- **Related requirements:** REQ-CSW-ARCH-001, REQ-CSW-ARCH-002, REQ-CSW-ARCH-003,
  REQ-CSW-API-001, REQ-CSW-API-003, REQ-CSW-API-006, REQ-CSW-API-007
- **Former ID:** CSW-UC-001

#### Main Flow

A host application uses the generic CSW API to control 3D functionality. The
same concepts are recognizable whether CSWUnity or CSWUnreal is selected.

### UC-CSW-002: Implement functionality in an engine

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Engine developer
- **Related requirements:** REQ-CSW-ARCH-001, REQ-CSW-ARCH-003, REQ-CSW-SM-005,
  REQ-CSW-API-001, REQ-CSW-API-003, REQ-CSW-API-005, REQ-CSW-API-006,
  REQ-CSW-API-007
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
  REQ-CSW-SM-004, REQ-CSW-SM-005, REQ-CSW-SM-006, REQ-CSW-SM-008,
  REQ-CSW-TERM-001
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
- **Related requirements:** REQ-CSW-MM-001, REQ-CSW-MM-002, REQ-CSW-TERM-001,
  REQ-CSW-API-016
- **Former ID:** CSW-UC-005

#### Main Flow

A host requests a map, adds or removes URL-based map parts, and works primarily
with the map as a whole. It can inspect projection, aggregate map information,
layers, drawing mode, and individual part metadata when needed.

### UC-CSW-006: Navigate a map

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Observer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-014
- **Former ID:** CSW-UC-006

#### Main Flow

An Observer receives mouse or system input and uses a MotionModel such as map
orbit to move its Camera around a selected map. Its View shows what the Camera
sees. The orbit can look straight down on the map at most, at 90°, and never
over that limit. It can be kept above the ground and out of other geometry,
depending on which geometry types have collision switched on.

### UC-CSW-007: Use another Camera for a View

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Engine View
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-002, REQ-CSW-API-009
- **Former ID:** CSW-UC-007

#### Main Flow

SceneManager has a Main Camera describing the primary area of interest. A View
inside Unity or Unreal can use another Camera without moving the Main Camera.

### UC-CSW-008: Position a Camera across coordinate layers

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host
- **Related requirements:** REQ-CSW-CV-002, REQ-CSW-API-004
- **Former ID:** CSW-UC-008

#### Main Flow

A host positions and aims a Camera using generic geographic or global 3D
coordinates. The selected realization maps the same Camera to its current
engine coordinate system.

### UC-CSW-009: Work with world objects and events

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Host
- **Related requirements:** REQ-CSW-OE-001, REQ-CSW-API-013
- **Former ID:** CSW-UC-009

#### Main Flow

A host accesses map objects, including entities and static objects, through an
Object API and reacts to updates and occurrences through an Event API.

### UC-CSW-010: Place an Observer on the ground

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-003, REQ-CSW-CV-005,
  REQ-CSW-CV-006, REQ-CSW-API-004, REQ-CSW-API-008

#### Goal

The programmer places an Observer at a position in the synthetic world, for
example standing on the ground at a given location.

#### Preconditions

- A map is open and its Map Coordinate Context is known.
- An Observer exists with a View, a Camera, and a MotionModel.

#### Trigger

The application selects a start position or moves the Observer to a new place.

#### Main Flow

1. The programmer gives a horizontal GeoPosition or Global 3D Position and a
   height above the ground.
2. The Observer is placed in the world Reference Frame of the map.
3. CSW resolves the ground height from the streamed map.
4. The Observer stands at the given height above the ground, and its View
   shows what its Camera sees.

#### Alternative and Error Flows

- 3a. The terrain detail has not loaded yet. The placement is provisional and
  is corrected when the detail arrives.
- 3b. The position is outside the map. The placement is reported as
  unavailable, and the Observer keeps its previous pose.

#### Postconditions

The Observer is in the world Reference Frame, and its global pose can be read
as a GeoPosition and a Global 3D Position.

#### Acceptance Scenario

On a streamed georeferenced map, the Observer is placed 1.8 m above the ground
at a given latitude and longitude. After the terrain has loaded, the eye height
above the terrain is 1.8 m within the map tolerance, and the reported
GeoPosition matches the requested position.

### UC-CSW-011: Follow a moving object

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-002, REQ-CSW-CV-003,
  REQ-CSW-CV-004, REQ-CSW-CV-005, REQ-CSW-OE-001, REQ-CSW-API-008,
  REQ-CSW-API-013

#### Goal

The Observer travels with a moving world object, such as an aircraft, a
helicopter, a ship, or a car, as if it were on board.

#### Preconditions

- A map is open.
- The moving object is available through the Object API, and its pose is
  updated by the application, a simulation, or an external system.

#### Trigger

The programmer attaches the Observer to the object or to a part of it.

#### Main Flow

1. The programmer selects the object, or a part of it such as a cockpit or a
   deck, as the Reference Frame of the Observer.
2. The Observer keeps its global pose at the moment of attachment, or takes a
   pose given in the coordinates of the object.
3. While the object moves, turns, and rolls, the Observer moves with it.
4. The Observer can still move within the object according to the movement
   rules of the Reference Frame.
5. The programmer detaches the Observer, which returns to the world Reference
   Frame at its current global pose.

#### Alternative and Error Flows

- 3a. The object is removed. The Observer returns to the world Reference Frame
  at its last valid global pose, and the reason is reported.

#### Postconditions

The global pose of the Observer is the composition of the object pose and the
Observer pose in the object.

#### Acceptance Scenario

The Observer is attached to a ship that moves at 10 m/s and rolls ±10°. With no
Observer input, the Observer pose in the ship stays constant, and its reported
global pose follows the ship in the same refresh.

UC-CSWU-152 describes the camera-only variant of this use case in CSWUnity.

### UC-CSW-012: Move within a Reference Frame

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-002, REQ-CSW-CV-003,
  REQ-CSW-CV-004, REQ-CSW-CV-005, REQ-CSW-API-008

#### Goal

The Observer moves inside its Reference Frame in the way that the frame allows,
for example walking on the deck of a rolling ship, moving inside an aircraft,
changing floors or rooms in a building, or climbing a ladder.

#### Preconditions

- The Observer is in a Reference Frame that supplies movement rules.
- Reference Frames can be nested, such as a room on a floor of a building or a
  ladder on a ship.

#### Trigger

The Observer receives movement input from a user, a script, or a simulation.

#### Main Flow

1. The MotionModel of the Reference Frame interprets the input in the
   coordinates of the frame.
2. The constraints of the frame limit the movement. On a deck the Observer
   walks on the deck surface, in a building it stays on its floor, and on a
   ladder it moves only up and down.
3. The motion of the frame itself, such as the roll of a ship, moves the
   Observer without being treated as input.
4. When the Observer reaches a connection, such as the foot of a ladder, a
   staircase, or a door, it changes to the connected Reference Frame while its
   global pose is preserved.

#### Alternative and Error Flows

- 2a. The movement is blocked by a constraint. The Observer stops at the
  boundary, and the active constraint is reported.

#### Postconditions

The Observer is in a valid pose in its current Reference Frame, and its global
pose can be read as a GeoPosition and a Global 3D Position.

#### Acceptance Scenario

On a ship deck rolling ±10°, the Observer walks 20 m forward. It stays on the
deck surface, and its travelled distance in the deck frame is 20 m within the
tolerance. On a ladder, sideways input does not move the Observer, and upward
input moves it only along the ladder axis.

### UC-CSW-013: Show Views from Cameras on an Observer

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-002, REQ-CSW-CV-005,
  REQ-CSW-CV-007, REQ-CSW-CV-008, REQ-CSW-API-002, REQ-CSW-API-008,
  REQ-CSW-API-009, REQ-CSW-API-010

#### Goal

The programmer shows what a camera at the Observer would see, for example a
View straight ahead and another View straight down. Each View renders the 3D
graphics as a virtual camera.

#### Preconditions

- An Observer exists in a Reference Frame.

#### Trigger

The application adds a View, or changes the orientation of a mounted Camera.

#### Main Flow

1. The programmer mounts a Camera on the Observer with an offset and an
   orientation, such as forward or straight down.
2. The programmer connects a View to that Camera and selects how it is
   rendered: the render target, such as a window, a part of the screen, or an
   offscreen image, its resolution, and the projection with field of view and
   near and far distances.
3. When the Observer moves, turns, or is carried by its Reference Frame, the
   Camera moves with it, and the View renders what the Camera sees with its
   projection.
4. The programmer can mount more Cameras on the same Observer and show them in
   several Views at the same time.

#### Alternative and Error Flows

- 1a. The mount is stabilized. A downward Camera keeps looking along the local
  vertical, and a forward Camera keeps a level horizon, even when the Observer
  or its Reference Frame rolls.
- 2a. The View uses a Camera other than the Main Camera. Streaming follows the
  Main Camera, and the other View shows what has been streamed for it.

#### Postconditions

Each View shows its Camera, and the global pose of each mounted Camera can be
read as a GeoPosition and a Global 3D Position.

#### Acceptance Scenario

An Observer on a ship rolling ±10° has a forward View rendered to the main
window with a 60° vertical field of view and a stabilized downward View
rendered to a 512 × 512 offscreen image with a 30° field of view. The forward
View rolls with the ship, and the downward View stays along the local vertical
within the tolerance. Each View reports the projection it renders with, and the
global positions of both Cameras equal the Observer position plus their mount
offsets in the same refresh.

### UC-CSW-014: Control an Observer with input devices

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** User
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-009, REQ-CSW-API-010,
  REQ-CSW-API-014, REQ-CSW-API-015

#### Goal

The user steers an Observer with a mouse, a keyboard, or another position
device.

#### Preconditions

- An Observer exists with a MotionModel.
- The application has connected one or more input sources to the Observer.

#### Trigger

The user moves the mouse, presses keys, or moves the position device.

#### Main Flow

1. The input source turns the device input into semantic input, such as move,
   look, zoom, and speed.
2. The MotionModel of the Observer applies the semantic input.
3. The Observer and its Cameras move, and the Views show the new pose.

#### Alternative and Error Flows

- 1a. The application switches the input source at runtime, for example from
  mouse and keyboard to a joystick or a recording. The Observer keeps its pose.
- 1b. The input source is disconnected. The Observer stops at its current pose.

#### Postconditions

The Observer pose reflects the input, and its global pose can be read.

#### Acceptance Scenario

The same recorded sequence of semantic input moves the Observer to the same
pose whether it was produced by mouse and keyboard or by a joystick.

### UC-CSW-015: Fly as a UFO

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** User
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-009, REQ-CSW-CV-010,
  REQ-CSW-API-015

#### Goal

The user hovers in the air and looks in different directions, as if seated in
a flying UFO.

#### Preconditions

- The Observer uses the UFO MotionModel and has an input source.

#### Main Flow

1. The Observer hovers at its position in the air.
2. The user looks in any direction with the mouse or another device.
3. The user moves forward, backward, sideways, up, and down relative to the
   view with keys or another device.
4. When the user releases the controls, the Observer stays where it is.

#### Postconditions

The Observer hovers at the reached pose.

#### Acceptance Scenario

The user turns the view 360° without moving the position, then flies 100 m
forward. Without input for 10 s afterwards, the position and orientation stay
unchanged within the tolerance.

UC-CSWU-115 describes free flight in CSWUnity.

### UC-CSW-016: Fly on a flying carpet

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** User
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-006, REQ-CSW-CV-009,
  REQ-CSW-CV-011, REQ-CSW-API-004, REQ-CSW-API-008, REQ-CSW-API-011,
  REQ-CSW-API-012, REQ-CSW-API-014, REQ-CSW-API-015

#### Goal

The user flies over the terrain as if seated on a flying carpet, and steers
its flight path.

#### Preconditions

- A map with terrain is open.
- The Observer uses the flying-carpet MotionModel and has an input source.

#### Main Flow

1. The carpet travels along its flight path at a height above the ground.
2. The user steers the direction, the speed, and the height with the mouse or
   the keys.
3. The carpet follows the terrain and keeps its clearance over hills and
   valleys.
4. The user can look around from the carpet without changing its path.

#### Alternative and Error Flows

- 3a. The terrain below has not loaded yet. The carpet keeps a provisional
  height and adjusts it when the terrain arrives.

#### Postconditions

The carpet is on its flight path at its height above the ground.

#### Acceptance Scenario

The carpet flies 2 km at 50 m above varying terrain. Its height above the
ground stays within the tolerance, and it never falls below the minimum
clearance.

UC-CSWU-003 and UC-CSWU-133 describe the flying carpet in CSWUnity.

### UC-CSW-017: Orbit a focus object

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** User
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-009, REQ-CSW-CV-012,
  REQ-CSW-CV-013, REQ-CSW-CV-014, REQ-CSW-OE-001, REQ-CSW-API-015

#### Goal

The user puts the focus on something in the map, such as a house, and circles
around it at the same radial distance while always looking in at it.

#### Preconditions

- A map is open and the area around the focus has loaded.
- The Observer uses the orbital MotionModel and has an input source.

#### Trigger

The user clicks a house in the View, or the application names a map feature, a
world object, or a position as the focus.

#### Main Flow

1. CSW picks the house and returns its focus point and extent.
2. The Observer looks at the focus point from a distance that frames the house,
   at a heading and an elevation angle.
3. The user drags the mouse or presses the arrow keys. The Observer circles
   around the house at the same radial distance, and the house stays in the
   center of the View.
4. The user zooms in or out, which changes only the distance to the focus.

#### Alternative and Error Flows

- 1a. The click hits nothing in the loaded map. The focus does not change, and
  the result says that nothing was hit.
- 1b. The focus is a moving object, such as a car. The orbit follows it.
- 1c. The focus object is removed. The Observer stops at its last valid pose,
  and the reason is reported.
- 3a. The user drags upward past straight down. The elevation stops at 90°, and
  the View does not flip over.
- 3b. The user drags downward toward the horizon. The Observer stops at the
  minimum clearance above the ground.
- 3c. A neighbouring house is between the Observer and the focus, and
  collision is on for buildings. The Observer moves in toward the focus to
  stay in front of the building, and returns to the chosen distance when the
  way is clear. With collision off for buildings, the Observer passes through
  the building at the chosen distance.

#### Postconditions

The Observer looks at the focus from the selected distance and angles.

#### Acceptance Scenario

The user clicks a house and drags the mouse sideways for a full turn. The
radial distance to the focus point of the house stays constant within the
tolerance, and the focus point stays in the center of the View for the whole
turn. When the focus is a moving car at 50 m instead, the car also stays in the
center, and the distance stays at 50 m within the tolerance. Dragging past
straight down stops at 90° without flipping the View, and with collision on for
terrain and buildings the Observer never ends up below the ground or inside a
building.

UC-CSW-006 describes orbiting a map, and UC-CSWU-113 describes orbiting in
CSWUnity.

### UC-CSW-018: Set up an Observer

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-007, REQ-CSW-CV-008,
  REQ-CSW-CV-009, REQ-CSW-CV-015, REQ-CSW-API-002, REQ-CSW-API-008,
  REQ-CSW-API-009, REQ-CSW-API-010, REQ-CSW-API-015

#### Goal

The programmer sets up an Observer whose View shows what it sees and plays what
it hears, and which the user can move with input.

#### Preconditions

- A map is open.

#### Trigger

The application needs a point of view in the synthetic world.

#### Main Flow

1. The programmer creates an Observer. It carries a Camera that looks forward
   relative to the Observer, and a View is connected to that Camera.
2. The View shows what the Camera sees and plays what the Observer hears.
3. The programmer selects a MotionModel that describes how the Observer can
   move, such as UFO, flying carpet, or orbit.
4. The programmer connects input, such as a mouse and a keyboard, to the
   MotionModel.
5. The user gives input. The MotionModel moves the Observer, the Camera moves
   with it, and the View shows and plays the result.

#### Alternative and Error Flows

- 1a. The programmer turns the Camera, for example straight down, or adds more
  Cameras and Views to the Observer, as in UC-CSW-013.
- 2a. Several Views are shown. The application selects which View the sound is
  heard from.
- 4a. No input is connected. The Observer stays where it is unless the
  application moves it.

#### Postconditions

The Observer has a View, a Camera, a MotionModel, and input, and the View
follows the Observer.

#### Acceptance Scenario

An Observer created without further setup shows the forward picture, and a
sound source 10 m to its left is heard from the left. After the user turns the
Observer 90° to the left with the mouse, the sound source is heard from the
front and is visible in the center of the View.

### UC-CSW-019: Specialize an Observer in an application layer

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-001, REQ-CSW-CV-009, REQ-CSW-CV-014,
  REQ-CSW-CV-016, REQ-CSW-API-015

#### Goal

The programmer gives an Observer more advanced movement, another input, or
another way to interact with the environment and its geometry, in the
application layer that uses CSWUnity.

#### Preconditions

- An Observer is set up as in UC-CSW-018.
- The built-in MotionModels, input sources, or interactions are not enough
  for the application.

#### Trigger

The application needs a behavior that CSW does not provide, such as a vehicle
that drives on roads, a steering wheel as input, or a person who opens doors.

#### Main Flow

1. In the application layer, the programmer writes a MotionModel, an input
   source, or an interaction. The programmer may use engine features, such as
   Unity physics or the Unity Input System.
2. The programmer connects it to the Observer in the same way as a built-in
   one.
3. The Observer moves and interacts by the new behavior.
4. The View, the Camera, the Reference Frame, the global pose, and the
   MotionModel limits work as before.

#### Alternative and Error Flows

- 1a. The programmer extends a built-in part instead of writing a new one, for
  example an orbit that glides smoothly to a new focus.

#### Postconditions

The Observer uses the specialized parts, and neither CSW nor CSWUnity has been
changed.

#### Acceptance Scenario

An Observer flies with the UFO MotionModel. The application replaces it at
runtime with a road-driving MotionModel written in the application layer, and
connects a steering wheel as input. Without changes to CSW or CSWUnity, the
Observer drives along the road, its global pose and its View stay correct, and
it still never goes below the ground.

REQ-CSWU-MOVE-012 and REQ-CSWU-MOVE-013 describe custom movement models and
input adapters in CSWUnity.

### UC-CSW-020: Compose the synthetic world from maps and models

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-MM-001, REQ-CSW-WC-001, REQ-CSW-WC-002,
  REQ-CSW-CV-006, REQ-CSW-CV-013, REQ-CSW-TERM-001, REQ-CSW-API-016

#### Goal

The programmer decides what the synthetic world contains geometrically, such
as several maps and models, and changes it while the application runs.

#### Preconditions

- The engine realization is running.

#### Trigger

The application sets up its world, or the scenario needs other content.

#### Main Flow

1. The programmer adds a coarse country map and a detailed city map.
2. The programmer sets the city map to be shown where the two maps overlap.
3. The programmer adds a model, such as a harbour crane, and places it at a
   GeoPosition with an orientation.
4. Each map and model reports when it is active.
5. Observers see, stand on, and pick the combined world.

#### Alternative and Error Flows

- 1a. A map has a coordinate system that CSW cannot convert. It is rejected
  with the reason, and the other maps stay active.
- 3a. The model is placed in a Reference Frame, such as on a ship deck, and
  moves with it.
- 5a. The programmer removes or hides the city map while the application runs.
  The country map is shown again in that area.

#### Postconditions

The world contains the selected maps and models, and each one reports its
state.

#### Acceptance Scenario

In the area where the maps overlap, an Observer placed on the ground stands on
the surface of the city map, and a click on the crane hits the crane. After
the city map is removed, the same Observer placement uses the surface of the
country map, without restarting the world.

REQ-CSWU-MAPCTRL-004 describes several maps in CSWUnity.

### UC-CSW-021: Populate the world from an application layer

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-WC-001, REQ-CSW-WC-003, REQ-CSW-OE-001,
  REQ-CSW-CV-013, REQ-CSW-API-004, REQ-CSW-API-011, REQ-CSW-API-012,
  REQ-CSW-API-013

#### Goal

The programmer adds more objects, static or moving, from the application layer
that uses CSWUnity into the synthetic world.

#### Preconditions

- A world with at least one map is active, as in UC-CSW-020.

#### Trigger

The scenario needs more content, such as traffic, people, or a forest.

#### Main Flow

1. The programmer adds a batch of static objects, such as 10 000 trees, with
   their models and placements in one call.
2. The programmer adds moving objects, such as cars, from models or from
   engine-native objects, such as Unity GameObjects.
3. The programmer moves each car by updating its pose, by giving it a path, or
   by connecting a MotionModel.
4. The objects are shown in Views, can be picked, and are available through the
   Object API and Event API.

#### Alternative and Error Flows

- 1a. An object source cannot be loaded. That object reports the failure with
  the reason, and the other objects are added.
- 2a. An Observer follows one of the cars, as in UC-CSW-011.
- 3a. The application layer removes a batch of objects in one call when the
  scenario no longer needs them.

#### Postconditions

The objects of the application layer are part of the world in the same way as
built-in content.

#### Acceptance Scenario

The application layer adds 10 000 trees and 100 cars driving on a path in a
few calls, without changes to CSW or CSWUnity. A click on a car hits it, an
Observer can follow it, and the trees are shown where they were placed.

UC-CSWU-008 and UC-CSWU-009 describe external object systems and world-object
presenters in CSWUnity.

### UC-CSW-022: Control rendering from external code

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** External code
- **Related requirements:** REQ-CSW-RC-001, REQ-CSW-MM-002, REQ-CSW-CV-008,
  REQ-CSW-API-009, REQ-CSW-API-010, REQ-CSW-API-016

#### Goal

External code controls how the 3D engine renders the synthetic world, without
access to the internals of the engine.

#### Preconditions

- A world is active and at least one View is rendered.

#### Trigger

The application, an operator tool, or a test needs another rendering, such as
lower detail on weak hardware, wireframe for debugging, or no shadows in an
offscreen View.

#### Main Flow

1. The external code reads the current rendering parameters.
2. It lowers the LOD factor and the view distance for the whole world.
3. It switches off shadows for the offscreen View only.
4. From the next rendered frame, the engine renders with the new values, and
   each change reports the value that the engine applied.

#### Alternative and Error Flows

- 2a. The external code selects a quality profile instead of setting single
  parameters.
- 3a. The external code sets an engine-specific parameter, such as a Unity
  render-pipeline setting, under a name that shows the engine.
- 4a. The engine does not support a parameter. The change reports it as
  unsupported, and the other changes still apply.

#### Postconditions

The engine renders with the applied values, and the external code can read
them back.

#### Acceptance Scenario

While the application runs, external code switches off shadows in the offscreen
View and halves the LOD factor for the world. In the next frame, the offscreen
View has no shadows, the main View still has shadows, and the reported LOD
factor is the halved value. The same commands give the same result in CSWUnity
and CSWUnreal.

REQ-CSWU-RENDER-016 and REQ-CSWU-MAPCTRL-005 describe quality profiles and
streaming controls in CSWUnity.

### UC-CSW-023: Place an object at a Frame Position

- **Status:** Pending
- **Milestone:** Unassigned
- **Primary actor:** Application programmer
- **Related requirements:** REQ-CSW-CV-017, REQ-CSW-CV-003, REQ-CSW-CV-005,
  REQ-CSW-CV-006, REQ-CSW-WC-003, REQ-CSW-API-004

#### Goal

The programmer defines a position in the Reference Frame where the Observer
moves and places an object there. CSWUnity keeps the object at that position
when the map changes.

#### Preconditions

- A map is active and an Observer is placed in a Reference Frame.

#### Trigger

The application needs an object at a fixed place in the world, such as a flag
on the ground near the Observer or a crate on the deck where the Observer
walks.

#### Main Flow

1. The programmer defines a Frame Position in the world frame of the map, 0 m
   above the ground, 50 m in front of the Observer.
2. SceneManager resolves the ground height and reports the Frame Position as
   provisional.
3. The programmer attaches a flag object to the Frame Position.
4. More detailed terrain streams in. SceneManager resolves a new ground height,
   reports the Frame Position as resolved, and the flag follows to the new
   height in the same refresh.
5. The Observer moves far away and the engine origin is rebased. The flag stays
   at the same global position without a visible jump.

#### Alternative and Error Flows

- 1a. The programmer defines the Frame Position in the Reference Frame of a
  ship deck. The crate on it follows the deck when the ship moves and rolls.
- 1b. The programmer gives the Frame Position as a GeoPosition. It keeps that
  global position instead of a height above the ground.
- 4a. The country map is replaced by a city map with higher priority. The Frame
  Position keeps its global position, the flag stands on the ground of the city
  map, and the change of pose is reported.
- 4b. The map is unloaded. The Frame Position is reported as unavailable, and
  the flag keeps its last pose until a map is loaded again.
- 5a. The programmer removes the Frame Position. The flag is detached and stays
  at its current global pose.

#### Postconditions

The object is at the Frame Position, and its global position is known.

#### Acceptance Scenario

A flag is attached to a Frame Position on the ground. When terrain detail
streams in, when the country map is replaced by a city map, and when the origin
is rebased, the flag stands on the visible ground at the same latitude and
longitude, without the application moving it. A crate on a Frame Position on a
rolling deck stays on the deck.

REQ-CSWU-POS-002, UC-CSWU-105, and UC-CSWU-161 describe object positioning,
map replacement, and origin rebasing in CSWUnity.

## Related Documentation

- [Generic CSW baseline requirements](requirements.md)
- [Coordinate nomenclature and mapping](coordinate-nomenclature.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
