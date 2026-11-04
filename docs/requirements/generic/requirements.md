# Requirements: Generic CSW Baseline

## Purpose

This baseline contains only requirements explicitly discussed for the common
CSW 3D layer and the SceneManager mechanism reviewed in CSWUnreal. It does not
add unrelated retry, payload, persistence, test, or compatibility requirements.

## Scope

This document is hosted content of the CSW owner (owner code `CSW`) until the
CSW repository has its own requirements root. Its requirement IDs keep the owner
code `CSW`, so they do not change when the document moves. The requirements
apply to CSWUnity, CSWUnreal, and other engine realizations, which reference
these IDs from their own requirements. The `generic/` folder is a topic folder,
so each requirement states its kind. The CSW owner has no milestones yet, so
every requirement is `Unassigned`.

## Documentation

### REQ-CSW-DOC-001: Separate use cases and requirements

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Documentation review

Use cases and normative requirements shall be maintained in separate files.
Use cases shall describe expected use as stories, while requirements shall be
measurable statements.

## Terminology

### REQ-CSW-TERM-001: StreamingMap

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003, UC-CSW-005, UC-CSW-020
- **Verification:** Documentation and API review proving that the technology,
  its engine APIs, and the projects are named as stated, and that the
  namespaces follow REQ-CSW-API-006 and REQ-CSW-API-007

CSW shall distinguish three names:

| Name | Meaning |
| --- | --- |
| **StreamingMap** | The technology of streaming maps, which provides the synthetic world with map data through the SceneManager. It is written as one word. |
| `CSW.<Engine>.StreamingMap` | The API through which an engine realization exposes StreamingMap, for example `CSW.Unity.StreamingMap` and `CSW.Unreal.StreamingMap` |
| `CSW<Engine>` | The project that encapsulates an engine realization: its packages, its StreamingMap API, its other engine classes, its tests, examples, and documentation, for example CSWUnity and CSWUnreal |

Every engine realization presents the same StreamingMap technology. New
documentation and new identifiers shall use the name StreamingMap for the
technology, not "Streaming Map", "Map Streamer", or "MapStreamer". Lowercase
"map streaming" may still describe the activity. Engine-independent StreamingMap
classes, if CSW defines them, shall be declared in `CSW.StreamingMap`, as
required by REQ-CSW-API-006 and REQ-CSW-API-007. No class in the namespace
`CSW` shall be named `StreamingMap`, so that the library namespace stays free.
Existing identifiers that contain `MapStreamer` or `map-streamer` remain valid
until a planned migration renames them.

## API levels

| Level | Responsibility |
| --- | --- |
| Generic CSW API | Engine-neutral concepts and logic used by a host through corresponding C++ and C# APIs |
| CSW realization API | Registered factories, hooks, buffers, and contracts used to realize generic capabilities in a selected engine |
| Engine-native API | Unity- or Unreal-native components and APIs used directly inside that engine |

### REQ-CSW-ARCH-001: Three API levels

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** API review of the three levels proving that the generic API
  exposes no engine types

CSW shall keep the generic CSW API, the CSW realization API, and the
engine-native API as distinguishable levels. Engine types shall not be required
by the generic CSW API.

### REQ-CSW-ARCH-002: Corresponding realizations

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001
- **Verification:** Cross-engine API review of corresponding responsibilities

CSWUnity and CSWUnreal shall provide corresponding responsibilities and API
semantics for the generic capabilities through C# and C++ respectively. An
engine-native extension may add engine-specific behavior but shall not silently
change the meaning of a corresponding generic operation.

### REQ-CSW-ARCH-003: Control from either useful level

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Reference scenario in each engine realization

A host shall be able to control a selected engine through the generic CSW API.
An engine developer shall be able to implement or use an engine-native
realization without changing generic CSW code. Shared logic may reside in CSW
and invoke registered realization hooks, or a selected engine may provide the
complete implementation behind the same generic contract.

## API naming

### REQ-CSW-API-001: CSW namespace

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** API review of the C# and C++ language surfaces

The engine-independent classes of the generic CSW API shall be declared
directly in the namespace `CSW` in each language surface, so that each class
is named `CSW.<ClassName>`, for example `CSW.Camera`. This is consistent with
the existing classes `CSW.SceneManager` and `CSW.Platform`, which the
`CSW.SceneManager` package delivers in the namespace `CSW`.

Classes that control a specific engine shall be declared directly in that
engine's namespace under `CSW`:

- C# classes that control Unity shall be named `CSW.Unity.<ClassName>`;
- C# classes that control Unreal, if such classes are added, shall be named
  `CSW.Unreal.<ClassName>`.

An engine realization shall not declare types in the `CSW` namespace itself.
The engine realization of a generic CSW class shall have the same simple name
in the engine namespace, so that `CSW.Unity.Window` and `CSW.Unreal.Window`
realize `CSW.Window`, and `CSW.Unity.Position` places Unity objects at a
`CSW.Position`. Engine-independent utilities are the exception defined by
REQ-CSW-API-005 and are named `CSW.Utility.<ClassName>`. Libraries are named
as required by REQ-CSW-API-006, and every engine-coupled namespace begins with
the engine namespace of REQ-CSW-API-007.

### REQ-CSW-API-002: CSW.Camera

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-013, UC-CSW-018
- **Verification:** API review proving that the Camera type is `CSW.Camera`
  and exposes no engine types

The Camera of REQ-CSW-CV-001 shall be the class `Camera` in the namespace
`CSW`, with the qualified name `CSW.Camera` in C# and `CSW::Camera` in C++.
`CSW.Camera` shall be engine-neutral. It shall not derive from, wrap in its
public API, or require an engine camera type such as `UnityEngine.Camera`. The
engine realization shall connect a `CSW.Camera` to the engine's native camera
through the View. Code that uses both `CSW.Camera` and an engine camera shall
be able to tell them apart by qualified name or alias.

### REQ-CSW-API-003: CSW.SceneManager package

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Package and assembly review proving that every fully
  generic class is in a `CSW.SceneManager` package or assembly that references
  no engine assembly

Fully generic, engine-independent classes, such as `CSW.SceneManager` and
`CSW.Camera`, shall be delivered in `CSW.SceneManager`:

- a class that the CSW repository provides shall be imported from the
  `CSW.SceneManager` package;
- a class that an engine repository defines locally, before the CSW repository
  provides it, shall be placed in a local `CSW.SceneManager` assembly in that
  repository.

`CSW.SceneManager` names the package or assembly, not a namespace. The classes
keep the namespace `CSW` of REQ-CSW-API-001, so that moving a locally defined
class to the CSW repository changes neither its name nor its users. A local
`CSW.SceneManager` assembly shall reference no engine assembly and no engine
realization assembly, and shall not define a class that the imported
`CSW.SceneManager` package already provides.

### REQ-CSW-API-004: CSW.Position

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-008, UC-CSW-010, UC-CSW-016, UC-CSW-021, UC-CSW-023
- **Verification:** API review against the
  [coordinate nomenclature](coordinate-nomenclature.md), plus tests that set a
  global and a local value, convert each in one Map Coordinate Context, and use
  each placement mode, including a ground-clamped position before and after
  the terrain detail has loaded and a navigated position along a path

The generic position definition of the
[coordinate nomenclature](coordinate-nomenclature.md) shall be realized as the
class `Position` in the namespace `CSW`, with the qualified name
`CSW.Position` in C# and `CSW::Position` in C++. It succeeds the `MapPos` class
of the earlier CSWUnity map API. A caller shall be able to set the value of a
`CSW.Position` in one of two forms:

- **Global:** a `GeoPosition`, in a stated representation such as Geodetic,
  Geocentric, Projected, UTM, or Flat-Earth Position, with its CRS; or a Global
  3D Position in a named Global 3D Frame.
- **Local:** a position in the realized coordinate system of the map, that is,
  in a Local 3D Frame in which the map is realized, such as the map-anchored
  frame or the frame of a realized node, stated with the identity and
  generation of that frame. It is not an engine or viewer position; Unity and
  Unreal coordinates require the Viewer Coordinate Mapping.

A `CSW.Position` shall also have one of three placement modes, which decide how
its value is kept up to date:

- **Floating:** the position keeps the value that it was set to, free of the
  ground, as for a hovering or flying object. This is the default mode.
- **Ground-clamped:** the horizontal part of the value is kept, and the height
  is the height of the ground at that point plus a height above the ground,
  which is zero by default. The ground height shall be resolved from the
  streamed map, from the terrain by default and also from other surfaces, such
  as buildings, when the caller selects them, as for the surface placement of
  REQ-CSW-CV-006. The position shall stay clamped when terrain detail streams in
  or the map changes, and shall report whether it is resolved, provisional, or
  unavailable.
- **Navigated:** the value is updated by a `CSW.Navigator` of REQ-CSW-API-012
  that moves the position along a `CSW.Path` of REQ-CSW-API-011, as a vgPlayer
  of Vega is moved by a vgNavigator along a vgPath, and as a `CSW.Player` of
  REQ-CSW-API-013 is moved. A path point that is
  ground-clamped keeps the navigated position clamped at that point.

The position shall report its form and its placement mode. It shall state its
Map Coordinate Context, hold a global value in double precision, and hold a
local value as a double-precision local working value, as allowed by
REQ-CSW-COORD-011. When its context is available, it shall be convertible to a
`GeoPosition`, a Global 3D Position, and a Local 3D Position through the
coordinate service. A ground-clamped position shall be converted only when it
is resolved, and a local position shall not be converted silently when its
frame generation is no longer current. A `CSW.Position` shall be engine-neutral;
how an engine object is placed at it is defined by each engine realization.

Generic API operations that take or return a position, such as Camera,
Observer, Player, surface placement, path, and Frame Position operations, shall use
`CSW.Position`. Until the CSW repository provides `CSW.Position`, it shall be
defined locally as required by REQ-CSW-API-003.

### REQ-CSW-API-005: CSW.Utility namespace

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-002
- **Verification:** Source review proving that every utility class is in the
  namespace `CSW.Utility` and in a `CSW.Utility` folder, and that no other
  folder or namespace declares utility classes

Utilities, helper code, and small, simple classes or functions that can be
called as utilities shall be declared in the namespace `CSW.Utility`, so that
each is named `CSW.Utility.<ClassName>`, for example `CSW.Utility.GfxCaps`.
Their source files shall be in one folder named `CSW.Utility` in each package
or assembly that contains utilities.

A utility shall be stateless, or limited to the settings it reads, and shall
not own scene, map, or engine object lifetimes. A class that is a step of a
CSW pipeline, a manager, a component, or a bootstrap is not a utility, even if
its name contains *Helper*. `CSW.Utility` is engine-independent. Code that
uses an engine or its API, including utilities and extension methods, shall
instead be declared in that engine's namespace of REQ-CSW-API-001, such as
`CSW.Unity.<ClassName>`, and kept in that engine's folder.

### REQ-CSW-API-006: Library namespaces

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Source and package review proving that every library
  namespace is `CSW.Foundation` or `CSW.<LibName>`, below the engine namespace
  of REQ-CSW-API-007 when it binds to an engine API, and that its folder has
  the same name

A broader library function, or a group of functions that share a namespace,
shall be declared in a library namespace under `CSW`:

- a library that is not specific to an engine shall be declared either in
  `CSW.Foundation`, with classes named `CSW.Foundation.<ClassName>`, or in its
  own namespace `CSW.<LibName>`, with classes named
  `CSW.<LibName>.<ClassName>`. The SceneManager library of the CSW repository
  is the reference example of a `CSW.<LibName>` library, and `CSW.StreamingMap`
  of REQ-CSW-TERM-001 is another;
- a library that binds to the Unity API shall be declared in
  `CSW.Unity.Foundation`, or in `CSW.Unity.<LibName>` for a named library, with
  classes named `CSW.Unity.Foundation.<ClassName>` or
  `CSW.Unity.<LibName>.<ClassName>`, for example `CSW.Unity.StreamingMap`. A
  library that binds to the Unreal API shall correspondingly be declared in
  `CSW.Unreal.Foundation` or `CSW.Unreal.<LibName>`.

A library's source files shall be in a folder with the same name as its
namespace. `<LibName>` shall not be the simple name of an existing class in the
namespace in which the library is declared, such as `CSW` or `CSW.Unity`. The `CSW.SceneManager` package currently delivers the class
`CSW.SceneManager` in the namespace `CSW`, as REQ-CSW-API-003 records, so the
SceneManager library's classes remain in `CSW` until the CSW repository
changes that class. A single class that controls an engine remains in the engine
namespace of REQ-CSW-API-001, and a small engine-independent utility remains in
`CSW.Utility` as required by REQ-CSW-API-005.

### REQ-CSW-API-007: Engine namespace

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Source review proving that every namespace containing
  code with a specific coupling to an engine begins with that engine's
  namespace, and that no other namespace does

Code with a specific coupling to an engine or its API shall be declared in
the engine namespace directly under `CSW`, or in a library namespace below it:

- every C# class coupled to Unity shall be named `CSW.Unity.<ClassName>` or
  `CSW.Unity.<LibName>.<ClassName>`, for example `CSW.Unity.Window` and
  `CSW.Unity.StreamingMap.<ClassName>`;
- every C# class coupled to Unreal, if C# code for Unreal is added, shall be
  named `CSW.Unreal.<ClassName>` or `CSW.Unreal.<LibName>.<ClassName>`, for
  example `CSW.Unreal.Window`.

The engine segment shall come directly after `CSW` and shall appear only
there; a namespace shall not end in an engine suffix such as `.Unity`. A
namespace without an engine coupling shall not contain an engine segment, and
engine-coupled code shall not be declared in a namespace without one. Because
the generic class and its realization share a simple name, code inside an
engine namespace shall refer to the generic class by its qualified name, such
as `CSW.Window`, and to engine types that share a simple name by their
qualified name or an alias, such as `UnityEngine.Camera`. A file in a `CSW`
namespace shall refer to Unity's own `Unity.*` namespaces through `using`
directives at the top of the file or with `global::`, because inside `CSW` the
name `Unity` resolves to `CSW.Unity`.

### REQ-CSW-API-008: CSW.Observer

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-011, UC-CSW-012, UC-CSW-013, UC-CSW-016,
  UC-CSW-018
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization with an Observer that carries
  two Cameras, is moved by a MotionModel and by application code, and is
  attached to a moving world object

The Observer of REQ-CSW-CV-001 shall be realized as the class `Observer` in the
namespace `CSW`, with the qualified name `CSW.Observer` in C# and
`CSW::Observer` in C++. A `CSW.Observer` is a camera carrier, as the
vgObserver of Vega is: it is the moving viewpoint, and it carries one or more
`CSW.Camera` objects of REQ-CSW-API-002, each mounted with an offset and an
orientation relative to the Observer as required by REQ-CSW-CV-007. A
`CSW.Observer` shall not render by itself; a View shows what one of its
Cameras sees.

A `CSW.Observer` shall have a pose, that is, a `CSW.Position` of
REQ-CSW-API-004 and an orientation, in its Reference Frame of
REQ-CSW-CV-003, and shall be able to be moved:

- by a `CSW.MotionModel` of REQ-CSW-API-015 driven by a `CSW.Input` of
  REQ-CSW-API-014, as a vgObserver is moved by a vgMotion model;
- by application code that sets its pose directly;
- by a `CSW.Navigator` of REQ-CSW-API-012 along a `CSW.Path` of
  REQ-CSW-API-011, as a vgPlayer is moved by a vgNavigator; and
- by attaching it to a `CSW.Player` of REQ-CSW-API-013, or to another
  Reference Frame carried by a world object, so that it follows the object, as
  a vgObserver is tethered to a vgPlayer.

When an Observer moves, its mounted Cameras shall follow it in the same
refresh. A `CSW.Observer` shall be engine-neutral and shall expose no engine
type. An engine realization shall connect each carried `CSW.Camera` to its
native camera, as required by REQ-CSW-API-002. Until the CSW repository
provides `CSW.Observer`, it shall be defined locally as required by
REQ-CSW-API-003.

### REQ-CSW-API-009: CSW.View

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-007, UC-CSW-013, UC-CSW-018, UC-CSW-022
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization with two Views of one
  Observer, one in a region of a window and one in an offscreen image, in
  which one View switches to another Camera without moving the Main Camera

The View of REQ-CSW-CV-008 shall be realized as the class `View` in the
namespace `CSW`, with the qualified name `CSW.View` in C# and `CSW::View` in
C++. A `CSW.View` takes its input from one `CSW.Camera` of REQ-CSW-API-002 and
draws what that Camera sees into a viewport region of one physical
`CSW.Window` of REQ-CSW-API-010:

- **Input:** the Camera gives the View its pose. The View references the
  Camera and does not own or move it. The application can switch the View to
  another Camera at runtime, from the next refresh, without moving the Main
  Camera of REQ-CSW-CV-002.
- **Output:** the View owns the viewport region and the projection, but no
  pixels of its own. It draws only into the Window that it is placed in, and
  a View that is not placed in a Window is not rendered.

A Camera can be the input of several Views, and a Window can hold several
Views. It corresponds to the
vgChannel of Vega, the pfChannel of Performer, and the camera and viewport
settings of a Gizmo3D `gzRenderBase`. The name `View` is used instead of
`Channel` because Unreal and current rendering APIs use the view term.

A `CSW.View` shall be engine-neutral and shall expose no engine type. Each
engine realization shall map it to its native rendering objects, for example:

| CSW | Unity | Unreal | Gizmo3D |
| --- | --- | --- | --- |
| `CSW.Camera`, the pose | `Camera` transform | `UCameraComponent` or `ACameraActor`, giving `FMinimalViewInfo` | `gzCamera` |
| `CSW.View` | `Camera` with `Camera.rect` | Viewport region of `UGameViewportClient`, rendered as an `FSceneView` | `gzRenderBase` camera, `setViewportOffset`, and `setViewportScale` |
| Projection | `fieldOfView`, `nearClipPlane`, `farClipPlane`, `projectionMatrix` | `FieldOfView`, near clip plane, `FSceneView` projection matrix | `gzCamera` projection |

In Unity one `Camera` component both holds the pose and renders, so the Unity
realization may realize a `CSW.Camera` and its `CSW.View` with one `Camera`.
Unreal separates them: the camera supplies the view information, and the
viewport or scene capture renders it. Until the CSW repository provides
`CSW.View`, it shall be defined locally as required by REQ-CSW-API-003.

### REQ-CSW-API-010: CSW.Window

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-013, UC-CSW-014, UC-CSW-018, UC-CSW-022
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization that opens one on-screen
  Window with two Views and one offscreen Window, refreshes both from
  application code, and reads input from the on-screen Window

The render target that an application controls shall be realized as the class
`Window` in the namespace `CSW`, with the qualified name `CSW.Window` in C# and
`CSW::Window` in C++, as the `gzWindow` of Gizmo3D is. A `CSW.Window` is either
an on-screen window or display, or an offscreen image or texture. It shall hold
one or more `CSW.View` objects of REQ-CSW-API-009, each in its own viewport
region. It is the physical surface that the Views draw into, and shall
control:

- its size, its position on screen, and whether it is shown;
- when it is refreshed, either each engine frame or on request from
  application code, with the rendering parameters of REQ-CSW-RC-001;
- reading back the rendered image, for an offscreen Window; and
- the input that it receives, such as keyboard and mouse input, which it
  shall be able to deliver as an input source of REQ-CSW-CV-009 through a
  `CSW.Input` of REQ-CSW-API-014.

A `CSW.Window` shall be engine-neutral and shall expose no engine type. Each
engine realization shall map it to its native target, for example:

| CSW | Unity | Unreal | Gizmo3D |
| --- | --- | --- | --- |
| On-screen `CSW.Window` | `Display`, with `Camera.targetDisplay` | `UGameViewportClient` and its `FViewport`, or an `SWindow` | `gzWindow` |
| Offscreen `CSW.Window` | `RenderTexture`, with `Camera.targetTexture` | `UTextureRenderTarget2D`, rendered by `USceneCaptureComponent2D` | `gzImageRender` or `gzTextureRender` |
| Refresh | Player loop, or `Camera.Render` on request | Engine tick, or `CaptureScene` on request | `refreshWindow` |

Until the CSW repository provides `CSW.Window`, it shall be defined locally as
required by REQ-CSW-API-003.

### REQ-CSW-API-011: CSW.Path

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-016, UC-CSW-021
- **Verification:** API review of the C# and C++ language surfaces, plus tests
  that evaluate an open linear path, a closed spline path, a ground-clamped
  path, and a path relative to a moving world object at given distances

A path that a position can follow shall be realized as the class `Path` in the
namespace `CSW`, with the qualified name `CSW.Path` in C# and `CSW::Path` in
C++, as the vgPath of Vega is. A `CSW.Path` shall be an ordered list of path
points. Each point shall be a `CSW.Position` of REQ-CSW-API-004, floating or
ground-clamped, and may state an orientation, a speed, and a time. A
`CSW.Path` shall:

- connect its points with linear or spline segments, chosen for the whole path
  or for each segment;
- be open or closed, so that a closed path loops back to its first point;
- have a static origin in the map, or a dynamic origin in a Reference Frame of
  REQ-CSW-CV-003 carried by a moving world object, as Vega paths can be
  object-relative or player-relative;
- return the position, the direction, and the orientation at a given distance
  along the path, and its total length; and
- be shared by several navigators without being copied.

A `CSW.Path` shall be engine-neutral. Until the CSW repository provides
`CSW.Path`, it shall be defined locally as required by REQ-CSW-API-003.

### REQ-CSW-API-012: CSW.Navigator

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-016, UC-CSW-021
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario with an Observer and 100 world objects moved by
  navigators along shared paths, with waypoint events, a pause, a reset, and a
  replay that gives the same positions for the same simulation times

The traversal of a path shall be realized as the class `Navigator` in the
namespace `CSW`, with the qualified name `CSW.Navigator` in C# and
`CSW::Navigator` in C++, as the vgNavigator of Vega is. A `CSW.Navigator`
shall move one `CSW.Position` of REQ-CSW-API-004 along one `CSW.Path` of
REQ-CSW-API-011, and with it the Observer, Camera, `CSW.Player` of
REQ-CSW-API-013, or other world object that uses that position. It shall:

- start, stop, pause, and reset to the start of the path, and run forward or
  backward, once or in a loop;
- move at the speeds of the path points or at a speed set by the caller;
- orient the position along the path direction or by the orientations of the
  path points;
- advance with the simulation time and not with the frame rate, so that the
  same simulation time gives the same position;
- report its progress, as the distance along the path and the current
  segment; and
- raise an event when it reaches a path point or the end of the path; and
- accept playback control, such as start, stop, pause, speed, and direction,
  from a `CSW.MotionModel` of REQ-CSW-API-015 that reads a `CSW.Input` of
  REQ-CSW-API-014, without taking its position from the input.

A `CSW.Navigator` shall be engine-neutral. Until the CSW repository provides
`CSW.Navigator`, it shall be defined locally as required by
REQ-CSW-API-003.

### REQ-CSW-API-013: CSW.Player

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-009, UC-CSW-011, UC-CSW-021
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization with a ground-clamped car, a
  navigated ship that carries an Observer and a second Player, and an aircraft
  moved by application code, each checked against its `CSW.Position`

A moving world object that the application controls shall be realized as the
class `Player` in the namespace `CSW`, with the qualified name `CSW.Player` in
C# and `CSW::Player` in C++, as the vgPlayer of Vega is. A `CSW.Player` shall
have a pose, that is, a `CSW.Position` of REQ-CSW-API-004 and an orientation,
and the placement mode of that position shall decide how the Player follows
the ground: floating, ground-clamped, or navigated. A `CSW.Player` shall be
able to be moved:

- by application code that sets its pose directly;
- by a `CSW.Navigator` of REQ-CSW-API-012 along a `CSW.Path` of
  REQ-CSW-API-011, as a vgPlayer is moved by a vgNavigator;
- by a `CSW.MotionModel` of REQ-CSW-API-015; and
- by a simulation or an external system, such as a DIS or HLA entity.

A `CSW.Player` shall carry a Reference Frame of REQ-CSW-CV-003, so that
Observers of REQ-CSW-API-008, Cameras, objects, and other Players can be
attached to it, or to a named part of it such as a cockpit or a deck, and follow
it in the same refresh, as vgObservers and vgObjects are attached to a
vgPlayer. It may have a visual model, and shall be usable without one. It
shall be available as an entity through the Object API of REQ-CSW-OE-001.
When a Player is removed, the objects attached to it shall return to the world
Reference Frame at their last valid global pose, and the reason shall be
reported.

A `CSW.Player` shall be engine-neutral and shall expose no engine type. An
engine realization shall place an engine object at the Player's pose, for
example a Unity `GameObject`, an Unreal `AActor`, or a Gizmo3D `gzTransform`.
Until the CSW repository provides `CSW.Player`, it shall be defined locally as
required by REQ-CSW-API-003.

### REQ-CSW-API-014: CSW.Input

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-014, UC-CSW-016
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization in which one recorded input
  sequence, and the same input from mouse and keyboard and from a joystick,
  steers an Observer through its MotionModel and pauses, resumes, and changes
  the speed of a `CSW.Navigator`, with the same result for each source

The input sources of REQ-CSW-CV-009 shall be realized as the class `Input` in
the namespace `CSW`, with the qualified name `CSW.Input` in C# and
`CSW::Input` in C++. A `CSW.Input` is a source of input, as the vgIDev input
device of Vega and the `gzInputProvider` of Gizmo3D are. It shall:

- be fed by a device, such as a mouse, a keyboard, a joystick, a SpaceMouse,
  or a head tracker, by the input that a `CSW.Window` of REQ-CSW-API-010
  receives, by a network command, or by a recording;
- turn device input into named semantic actions, such as move, look, zoom,
  speed, pause, and reset, through a binding that the application can change
  at runtime, as a `gzInputAction` binds a key to an action;
- deliver the actions to one or more receivers, such as a `CSW.MotionModel`
  of REQ-CSW-API-015 or application code, in a defined order, so that a
  receiver can consume an action; and
- be recordable and replayable, with each action stamped with its simulation
  time.

A `CSW.MotionModel` of REQ-CSW-API-015 shall take the movement of its Observer
or `CSW.Player` from the actions of its input, as a vgMotion model takes it
from a vgIDev. A `CSW.Navigator` of REQ-CSW-API-012 shall be connected to
input through a `CSW.MotionModel` and shall take only playback control from
it, such as start, stop, pause, reset, speed, and direction; the position
shall still come from its `CSW.Path`, so that the same simulation time and the
same recorded actions give the same position.

A `CSW.Input` shall be engine-neutral and shall expose no engine type. Each
engine realization shall map it to its native input, for example:

| CSW | Unity | Unreal | Gizmo3D | Vega |
| --- | --- | --- | --- | --- |
| `CSW.Input`, the source | `InputDevice` of the Input System, or `PlayerInput` | `IInputDevice`, or `UEnhancedInputLocalPlayerSubsystem` | `gzInputProvider`, such as `gzWindow` | vgIDev |
| Semantic action and binding | `InputAction` and `InputActionMap` | `UInputAction` and `UInputMappingContext` | `gzInputAction` | Motion-model input channels |
| Receiver | Action callbacks | `UEnhancedInputComponent` bindings | `gzInputInterface` | vgMotion |

The Unity realization is `CSW.Unity.Input` as required by REQ-CSW-API-007,
and it shall refer to the legacy Unity class by its qualified name
`UnityEngine.Input`. Until the CSW repository provides `CSW.Input`, it shall
be defined locally as required by REQ-CSW-API-003.

### REQ-CSW-API-015: CSW.MotionModel

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-014, UC-CSW-015, UC-CSW-016, UC-CSW-017, UC-CSW-018,
  UC-CSW-019
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization that drives an Observer and a
  `CSW.Player` with each built-in model type from one `CSW.Input`, resets
  them, switches type at runtime, and controls a `CSW.Navigator`

The MotionModel of REQ-CSW-CV-009 shall be realized as the class
`MotionModel` in the namespace `CSW`, with the qualified name
`CSW.MotionModel` in C# and `CSW::MotionModel` in C++. A `CSW.MotionModel`
shall be the same as the vgMotion of Vega: it turns the actions of one or more
`CSW.Input` objects of REQ-CSW-API-014 into the movement of the Observer of
REQ-CSW-API-008 or the `CSW.Player` of REQ-CSW-API-013 that it is attached
to, as a vgMotion is attached to a vgObserver or a vgPlayer.

It shall provide the model types of vgMotion:

- the simple types Spin, Drive, UFO, Warp, Fly, Trackball, and Walk;
- the complex types Flight Simulator, which models a fixed-wing aircraft,
  Missile, which models a powered and guided missile, and PickFly;
- Device Direct, which takes the pose directly from an input device; and
- user-defined types, which application layers add as required by
  REQ-CSW-CV-016.

In addition to the types of Vega, CSW shall provide:

- FlyingCarpet, the flying-carpet MotionModel of REQ-CSW-CV-011;
- Rotorcraft, which models a drone or a helicopter that can hover, climb,
  and move sideways; and
- Ballistic, which models an unpowered projectile on a ballistic trajectory
  from an initial position, direction, and speed, under gravity and drag.

Flight Simulator, Missile, Rotorcraft, and Ballistic form the vehicle types,
which simulate the dynamics of a vehicle. The other types are viewing types,
which move the Observer or Player for looking at the world without vehicle
dynamics. The flying carpet is a viewing type and shall not be merged with a
vehicle type. The term ballistic is used only for unpowered flight, so drones
and fixed-wing aircraft are not ballistic types.

The UFO type is the UFO MotionModel of REQ-CSW-CV-010, and Spin and Trackball
are variants of the orbital MotionModel of REQ-CSW-CV-012.

A `CSW.MotionModel` shall:

- have an initial position and a reset position, each a `CSW.Position` of
  REQ-CSW-API-004 with an orientation, and return to the reset position on
  request;
- support ground clamping through the ground-clamped placement mode of
  REQ-CSW-API-004 and the surface placement of REQ-CSW-CV-006, with a constant
  height above the ground, and a step height and a step depth for the Walk
  type;
- apply the limits and the collision of REQ-CSW-CV-014;
- have speed, acceleration, and turn rate settings, and advance with the
  simulation time, so that the same recorded input gives the same movement;
- be switchable at runtime to another type or another instance while the
  Observer or Player keeps its pose; and
- control a `CSW.Navigator` of REQ-CSW-API-012, by turning input actions into
  playback control, such as start, stop, pause, reset, speed, and direction,
  while the Navigator takes the position from its `CSW.Path`.

A `CSW.MotionModel` shall be engine-neutral and shall expose no engine type.
The exact property names of vgMotion have not been verified against the Vega
reference pages and shall be confirmed before the API is fixed. Until the CSW
repository provides `CSW.MotionModel`, it shall be defined locally as
required by REQ-CSW-API-003.

### REQ-CSW-API-016: CSW.MapControl

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-005, UC-CSW-020, UC-CSW-022
- **Verification:** API review of the C# and C++ language surfaces, plus a
  reference scenario in each engine realization that opens a map, adds and
  removes map parts, reads the projection, the extent, and the maximum LOD,
  changes the LOD factor and the view distance, resets the map and the view,
  clears the map, and reports a map that fails to load

The map control of REQ-CSW-MM-001 and REQ-CSW-MM-002 shall be realized as the
class `MapControl` in the namespace `CSW`, with the qualified name
`CSW.MapControl` in C# and `CSW::MapControl` in C++. `CSW.MapControl` succeeds
the name MapManager.

A `CSW.MapControl` shall control the loading of maps through
`CSW.SceneManager`. It shall request, replace, and clear a map, and add and
remove URL-based map parts, by submitting SceneManager commands and receiving
their correlated responses as required by REQ-CSW-SM-007. It shall not load,
traverse, or build scene objects itself; SceneManager loads the map and the
engine realization builds what SceneManager sends.

A `CSW.MapControl` shall keep track of the map that it controls and expose:

- the projection: the CoordinateSystem from the map metadata and the Map
  Coordinate Context derived from it, with an event when it changes;
- the extent: the bounds of the map in its CoordinateSystem and as a
  geographic area, the aggregate map size, and the centroid;
- the maximum LOD, the LOD factor, and the view distance;
- the planar, globe, or patch topology;
- the map parts with their metadata, and the layers with their visibility and
  drawing policy;
- the load state, such as empty, loading, loaded, or failed, with the error
  of a failed load; and
- the map and view reset operations of REQ-CSW-MM-002.

The values shall be those that SceneManager reports for the loaded map, not
values assumed by the caller. Composing several maps and models into one world
follows REQ-CSW-WC-001 and REQ-CSW-WC-002. A `CSW.MapControl` shall be
engine-neutral and shall expose no engine type. Until the CSW repository
provides `CSW.MapControl`, it shall be defined locally as required by
REQ-CSW-API-003.

## SceneManager mechanism

The reviewed Unreal implementation provides the mechanism baseline:

```text
host command
  -> SceneManager input queue and worker processing
  -> traversal plus factory preparation
  -> typed output command buffers
  -> receiver-owned queue
  -> engine update polls and executes buffers
  -> engine object registry and correlated status/results
```

### REQ-CSW-SM-001: Typed asynchronous buffers

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario covering each buffer kind

SceneManager shall communicate Gizmo3D scene changes and status through
asynchronous command buffers. The buffer contract shall distinguish generic or
response, error, frame, new, update, and delete work.

### REQ-CSW-SM-002: Receiver handoff and engine polling

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of receiver handoff and execution in the
  engine context

A SceneManager receiver shall be callable from a SceneManager-owned thread and
shall be able to retain or copy, enqueue, and signal a received buffer. The
engine realization shall poll or fetch queued buffers and execute them in an
engine-approved update context rather than mutating engine objects in the
receiver callback.

### REQ-CSW-SM-003: Ordered and budgeted execution

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of execution order and budgets

The realization shall preserve buffer and command order while processing work.
It shall be able to limit frame and construction work per engine update and
leave unprocessed commands queued for a later update.

### REQ-CSW-SM-004: Scene-instance identity

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of PathID-based scene-instance identity

New, update, activation, and delete commands shall identify a scene instance by
the source node together with its PathID. Hierarchy commands shall also carry
the parent source node and ParentPathID. The realization shall use that complete
identity when registering, finding, updating, and removing an engine object.

### REQ-CSW-SM-005: Factory and preparation boundary

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-002, UC-CSW-003
- **Verification:** Reference scenario of factory preparation without
  engine-thread APIs

The realization shall select registered factories from the Gizmo3D node type
and its supported type hierarchy. Pre-build, update-preparation, and
pre-destroy hooks shall be allowed to run under the SceneManager edit lock on a
SceneManager thread and shall return or retain the prepared realization data
needed by later engine execution without calling engine-thread-only APIs.

### REQ-CSW-SM-006: New, update, and delete lifecycle

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Depends on:** REQ-CSW-SM-004, REQ-CSW-SM-005
- **Verification:** Reference scenario of the new, update, and delete lifecycle

A new command shall resolve its parent, create and build the engine object
through the selected factory, and register it under the complete scene-instance
identity. An update command shall resolve and update that same identity. A
delete command shall destroy the resolved engine object and remove its identity
registration.

### REQ-CSW-SM-007: Correlated requests and polling

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-004
- **Verification:** Reference scenario of request correlation and polling

An asynchronous request and its response shall carry the same command reference
identity. A realization shall be able to publish the response through its
native callback or event API and expose a polling operation that returns and
consumes the response for that identity.

### REQ-CSW-SM-008: Language surfaces

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** API review of the C++ and C# language surfaces

Unity shall consume this SceneManager mechanism through the C# CSW interface
and Unreal shall consume it through the C++ CSW interface.

## MapControl

### REQ-CSW-MM-001: Whole-map API

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-005, UC-CSW-020
- **Verification:** API review plus reference scenario

The map control, realized as `CSW.MapControl` by REQ-CSW-API-016, shall
provide the public whole-map operations for requesting a map and adding or
removing URL-based map parts. Map and MapPart scene realizations shall remain
hidden behind SceneManager, while the map control shall allow callers to
inspect map parts and their metadata.

### REQ-CSW-MM-002: Map information and control

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-005, UC-CSW-022
- **Verification:** API review plus reference scenario

The map control shall expose the map CoordinateSystem from metadata, aggregate map
size, centroid, maximum LOD or view distance, planar, globe, or patch topology,
layer visibility and drawing policy, and map and view reset operations.

## World composition

### REQ-CSW-WC-001: World content

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-020, UC-CSW-021
- **Verification:** API review plus reference scenario composing a world from
  two maps and two models and changing it at runtime

The application shall control what the synthetic world contains geometrically.
The world shall be able to contain several maps and several models, such as a
building, a vehicle, or a CAD model. Each item shall have a source, and each
model a placement given as a GeoPosition, a Global 3D Position, or a pose in a
Reference Frame, with orientation and scale. The application shall be able to
add, remove, replace, show, and hide items at runtime without restarting the
world. Each item shall report its state, such as loading, active, failed, or
removed, and the reason for a failure. All items shall share the Map
Coordinate Context of the world. An item in another coordinate system shall be
converted into it when CSW supports the conversion. Otherwise it shall be
rejected with the reason, while the other items stay active. A model is
geometry. When it also needs an identity, a state, or events in the
simulation, it shall be represented as a world object of REQ-CSW-OE-001.

### REQ-CSW-WC-002: Combined world geometry

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-020
- **Verification:** Reference scenario with a detailed map over a coarse map,
  checking the rendered surface, surface placement, and picking where they
  overlap

Where maps overlap, the application shall be able to set which map is shown,
for example a detailed city map over a coarse country map. When a map is
removed or hidden, the map below it shall be shown again. Rendering, surface
placement, picking, and collision shall all use the same combined geometry, so
that an Observer stands on the surface that is shown and a click hits what is
seen.

### REQ-CSW-WC-003: Population from application layers

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-021, UC-CSW-023
- **Verification:** Reference scenario in which an application layer adds
  static and moving objects, in a single call and in large batches, and checks
  that they are rendered, picked, collided with, and followed like built-in
  content

An application layer that uses an engine realization, such as CSWUnity, shall
be able to populate the synthetic world with its own objects in a simple way.
These can be static objects, such as trees or buildings, or moving objects,
such as vehicles, people, or aircraft. An object shall be addable from a model
source or from an engine-native object, such as a Unity GameObject, with a
placement as in REQ-CSW-WC-001. The application layer shall be able to add and
remove many objects in one call. It shall be able to move an object by updating
its pose, by giving it a path, or by connecting a MotionModel to it. Added
objects shall become part of the world in the same way as built-in content:
they are shown in Views, hit by picking, collided with, available through the
Object API and Event API of REQ-CSW-OE-001, and usable as Reference Frames that
Observers can follow or enter. Populating shall not require changes to CSW or
to the engine realization.

## Rendering control

### REQ-CSW-RC-001: External rendering parameters

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-022
- **Verification:** Reference scenario in which external code changes rendering
  parameters for the world and for one View at runtime in each engine
  realization, including an unsupported parameter

External code shall be able to control how the engine renders, through CSW and
without access to the internals of the engine. External code is an application
layer or another process that uses a command interface. CSW shall define named
parameters with the same meaning in every engine realization, such as LOD
factor, view distance, quality profile, shadows, fog and visibility, lighting,
wireframe and debug drawing, layer visibility, culling options, and frame-rate
limit. A parameter shall apply to the whole world or to a single View. An
engine realization may expose its own engine-specific parameters through the
same mechanism, under a name that shows the engine. External code shall be able
to read and change parameters at runtime, and a change shall take effect no
later than the next rendered frame without a restart. Each change shall report
the value that the engine actually applies. A parameter that the engine does
not support shall be reported as unsupported, and the other parameters shall
not be affected.

## Camera, View, Observer, and positions

### REQ-CSW-CV-001: Camera and View model

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-006, UC-CSW-007, UC-CSW-010, UC-CSW-011, UC-CSW-012,
  UC-CSW-013, UC-CSW-014, UC-CSW-015, UC-CSW-016, UC-CSW-017, UC-CSW-018,
  UC-CSW-019
- **Verification:** API review plus reference scenario

CSW shall support multiple Cameras and Views. A rendered View shall reference a
Camera and show what that Camera sees. A Camera shall describe the physical
camera model, position, direction, LookAt GeoPosition, and HeadUp policy.

An Observer shall combine four parts:

- a View, which shows what the Observer can see and hear;
- a Camera, which the Observer carries with an orientation relative to itself,
  and which the View is connected to;
- a MotionModel, which describes how the Observer can move, such as a map orbit
  around a selected map;
- input, which affects the movement through the MotionModel.

An Observer shall have a forward Camera and a View connected to it from the
start, so that it shows what it sees without further setup. The Observer is
realized as the class `CSW.Observer` of REQ-CSW-API-008.

### REQ-CSW-CV-002: Coordinate levels and Main Camera

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-007, UC-CSW-008, UC-CSW-011, UC-CSW-012, UC-CSW-013
- **Verification:** API review plus coordinate tests in each engine realization

The generic Camera API shall use GeoPosition and, where the GizmoSDK mapping is
known, Global 3D Position. It shall support geodetic latitude, longitude, and
ellipsoid height, Cartesian positions, ENU, and object-local positions. Engine
realizations may use their native coordinate systems below this boundary.
SceneManager shall expose a Main Camera for the primary area of interest while
allowing an engine View to use another Camera.

### REQ-CSW-CV-003: Observer Reference Frame

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-011, UC-CSW-012, UC-CSW-023
- **Verification:** API review plus reference scenario with nested moving
  Reference Frames

An Observer shall be placed in exactly one Reference Frame at a time. A
Reference Frame shall be the world frame of the map or a frame carried by a
world object or a part of it, such as a vehicle, a ship deck, an aircraft cabin,
a building floor, a room, or a ladder. Reference Frames shall be nestable, and
the pose of a Reference Frame relative to its parent may change every frame. The
Observer pose shall be expressed in its Reference Frame. A Reference Frame is a
spatial frame of the simulated world and is distinct from the Global 3D Frame,
the Local Tangent Frame, and the Local 3D Frame used for precision
localization.

### REQ-CSW-CV-004: Frame-dependent movement

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-011, UC-CSW-012
- **Verification:** Reference scenarios walking on a rolling deck, changing
  floors in a building, and climbing a ladder

A Reference Frame shall be able to supply the movement constraints and the
default MotionModel for Observers in it, such as walkable surfaces, floors and
rooms with their connections, or a ladder axis that allows movement only up and
down. The MotionModel shall move the Observer in the coordinates of its
Reference Frame, so that the motion of the frame itself, such as the roll of a
ship, is not treated as Observer input. Moving between Reference Frames, such
as from a deck onto a ladder or from one room to another, shall be an explicit
frame change.

### REQ-CSW-CV-005: Global Observer pose

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-011, UC-CSW-012, UC-CSW-013, UC-CSW-023
- **Verification:** Coordinate tests comparing the resolved global pose with
  the composed frame poses for each refresh

The pose of an Observer shall always be resolvable to a GeoPosition and a
Global 3D Position in the active Map Coordinate Context by composing its chain
of Reference Frames. The resolved global pose shall use the frame poses of the
same refresh. Attaching an Observer to a Reference Frame, detaching it, or
changing its Reference Frame shall preserve its global pose unless the caller
requests a new pose.

### REQ-CSW-CV-006: Surface placement

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-016, UC-CSW-020, UC-CSW-023
- **Verification:** Reference scenario placing an Observer on streamed terrain
  before and after the terrain detail has loaded

A caller shall be able to place an Observer on the ground or on another
surface of its Reference Frame by giving a horizontal position and a height
above that surface. The surface height shall be resolved asynchronously from
the streamed map or the frame geometry, and the placement shall report whether
it is resolved, provisional, or unavailable.

### REQ-CSW-CV-007: Observer-mounted Cameras

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-013, UC-CSW-018
- **Verification:** Reference scenario with a forward View and a downward View
  on an Observer attached to a rolling ship

An Observer shall be able to carry one or more Cameras, each mounted with an
offset and an orientation relative to the Observer, such as looking forward or
straight down. A mount shall either follow the Observer orientation or be
stabilized to the local vertical or north of the map. Each View shall be able
to reference one of these Cameras and show what that Camera sees. The global
pose of a mounted Camera shall be resolved from the global pose of the Observer
in the same refresh. Which of the Cameras drives streaming shall follow the
Main Camera rule of REQ-CSW-CV-002.

### REQ-CSW-CV-008: View as virtual rendering camera

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-013, UC-CSW-018, UC-CSW-022
- **Verification:** Reference scenario rendering one Observer Camera to a
  window and another to an offscreen image, with projection checks in each
  engine realization

A View shall define how the 3D graphics are rendered for its Camera, as a
virtual camera: the render target, such as a window, a viewport region, or an
offscreen image, its resolution and aspect, and the projection, with field of
view, near and far distances, and a finite or an infinite far plane. The
rendered frustum shall be derived from the Camera pose and this projection.
Each engine realization shall realize a View with its native rendering camera,
such as a Unity Camera, an Unreal camera or scene capture, or a Gizmo3D camera
and window, and shall report the projection that it actually renders with. The View is
realized as the class `CSW.View` of REQ-CSW-API-009, in a `CSW.Window` of
REQ-CSW-API-010.

### REQ-CSW-CV-009: Observer input

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-014, UC-CSW-015, UC-CSW-016, UC-CSW-017, UC-CSW-018,
  UC-CSW-019
- **Verification:** Reference scenario driving one Observer with mouse,
  keyboard, a position device, and recorded input

An Observer shall be able to be connected to one or more input sources, such as
a mouse, a keyboard, a joystick, a head tracker or another position device, a
network command, or a recording. Input sources shall deliver semantic input,
such as move, look, zoom, and speed, to the MotionModel of the Observer, so
that a MotionModel does not depend on a particular device. The input
connection shall be changeable at runtime. An input source is realized as
the class `CSW.Input` of REQ-CSW-API-014, and a MotionModel as the class
`CSW.MotionModel` of REQ-CSW-API-015.

### REQ-CSW-CV-010: UFO MotionModel

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-015
- **Verification:** Reference scenario hovering, looking around, and flying in
  all directions with mouse and keyboard

CSW shall provide a UFO MotionModel in which the Observer hovers freely in the
air. The user shall be able to look in any direction and to move forward,
backward, sideways, up, and down relative to the view. Without input, the
Observer shall keep its position and orientation.

### REQ-CSW-CV-011: Flying-carpet MotionModel

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-016
- **Verification:** Reference scenario steering a flight path over varying
  terrain with mouse and keyboard

CSW shall provide a flying-carpet MotionModel in which the Observer travels
along a flight path at a height above the ground, as if seated on a carpet.
The user shall be able to steer the path, its speed, and its height, and to
look around from the carpet. The carpet shall follow the terrain with the
surface placement of REQ-CSW-CV-006 and keep a minimum clearance. The flying
carpet is a viewing MotionModel without flight dynamics; it is the FlyingCarpet
type of REQ-CSW-API-015 and is separate from the Flight Simulator, Rotorcraft,
Missile, and Ballistic vehicle types.

### REQ-CSW-CV-012: Orbital MotionModel

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-017
- **Verification:** Reference scenario orbiting a house in the map, a position,
  and a moving object with mouse and keyboard

CSW shall provide an orbital MotionModel in which the Observer circles around
a focus, which is a map feature such as a house, a world object, or a position,
and keeps looking at it. The focus point shall stay in the center of the View.
The user shall be able to change the heading and the elevation angle around the
focus with simple input, such as a mouse drag or arrow keys, while the radial
distance stays constant. The distance shall change only when the user zooms.
When the focus is a moving object, the orbit shall follow it.

### REQ-CSW-CV-013: Focus selection in the map

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-017, UC-CSW-020, UC-CSW-021
- **Verification:** Reference scenario picking a house, a terrain position, and
  a world object in a View

CSW shall let the application select a focus by picking a pixel in a View or
by naming a map feature or a world object. The result shall identify what was
hit: a map feature such as a house, a world object, or terrain. It shall also
give the global position of the hit, a focus point such as the center of the
feature bounds, and the feature extent. The application can use the extent to
choose an orbit distance that frames the whole feature. Picking shall
use the streamed map data that is loaded at the time. When no data is hit, the
result shall say so.

### REQ-CSW-CV-014: MotionModel limits and collision

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-006, UC-CSW-017, UC-CSW-019
- **Verification:** Reference scenario orbiting a map and a house with
  elevation limits and collision switched on and off per geometry type

A MotionModel shall be able to limit how the Observer moves, and CSW shall
apply the limits the same way in every engine realization.

- **Angle limits.** An orbit shall have a maximum elevation angle of at most
  90°, which looks straight down on the ground. Input beyond the limit shall
  stop at it and shall never flip the View over. The application can also set
  a minimum elevation angle and limit the heading.
- **Distance limits.** The application can set a minimum and a maximum
  distance to the focus.
- **Ground limit.** The Observer can be kept from going below the ground,
  with a minimum clearance above the terrain.
- **Collision.** The application shall switch collision on or off for each
  geometry type, such as terrain, buildings, vegetation, water, and world
  objects. With collision off, the Observer passes through that geometry type.
  With collision on, an orbit moves in toward the focus to stay in front of the
  geometry, and returns to the chosen distance when the way is clear. Other
  MotionModels stop or slide along the geometry.

The Observer shall be able to report which limit or collision is active.

### REQ-CSW-CV-015: Observer hearing

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-018
- **Verification:** Reference scenario turning an Observer toward a sound
  source in each engine realization

An Observer shall hear the simulated world from where it is. Its View shall
present the sound heard at the pose of its Camera, so that sound sources in the
world have direction and distance relative to the Observer. When several Views
are shown, the application shall select which View the sound is heard from.
Each engine realization shall realize hearing with its native audio listener,
such as a Unity AudioListener or the Unreal audio listener.

### REQ-CSW-CV-016: Specialization in application layers

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-019
- **Verification:** Reference scenario replacing a built-in MotionModel, input
  source, and geometry interaction with ones written in an application layer

CSW shall provide basic MotionModels, input sources, and ways to interact with
the environment and its geometry. Application layers that use an engine
realization, such as CSWUnity, shall be able to specialize them. An application
layer shall be able to add its own MotionModels, input sources, and
interactions, such as walking with physics, driving on roads, or opening doors.
It shall connect them to an Observer in the same way as the built-in ones,
which have no special access. A specialization may use the features of its
engine, such as Unity physics or the Unity Input System. Specialized parts
shall keep the Observer rules of CSW: the Reference Frame, the global pose, the
connection between Camera and View, and the MotionModel limits. Specializing
shall not require changes to CSW or to the engine realization.

### REQ-CSW-CV-017: Frame positions

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-023
- **Verification:** Reference scenario with objects on Frame Positions while
  terrain detail streams in, a map is replaced, the origin is rebased, and the
  Reference Frame moves

A caller shall be able to define a Frame Position: a position, and optionally
an orientation, in a Reference Frame, such as the frame in which an Observer
moves. A caller shall be able to give it as a GeoPosition, as a Global 3D
Position, as a position relative to the Reference Frame, or as a height above a
surface of the frame. SceneManager shall own the Frame Position and keep it
valid when the map changes, such as when terrain detail streams in, a map is
loaded, unloaded, or replaced, the overlap priority changes, or the engine
origin is rebased. A Frame Position shall keep its global position, or its
height above the surface when it was defined on a surface, and shall follow its
Reference Frame when the frame moves. An engine-native object, a world object,
or an Observer shall be able to be attached to a Frame Position. The attached
object shall follow it in the same refresh, without the caller moving it. A
Frame Position shall report whether it is resolved, provisional, or
unavailable, and shall report each change of its resolved pose. When a Frame
Position is removed, its attached objects shall be detached at their current
global pose.

## Diagnostics

### REQ-CSW-DIAG-001: Message level

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Test in each engine realization that sends one message at
  each level, at several configured message levels, and checks which messages
  reach the engine log

CSW shall send a diagnostic message only when its level is at or above the
configured message level. A message below the configured level shall not be
sent, routed to the engine log, or passed to any other receiver, so that its
cost is limited to the level check. The application shall be able to set the
message level at startup and change it at runtime. CSW code and engine
realizations shall send their diagnostic messages through the same message
mechanism, with the GizmoSDK message levels, and shall not write directly to
the engine log, so that the configured level applies to every message. An
engine realization shall route a sent message to the engine log with a
severity that corresponds to its level. An exception that CSW reports shall
also be sent as a message with a level, and its stack trace may follow the
message to the engine log.

## Objects and events

### REQ-CSW-OE-001: Object API and Event API

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-009, UC-CSW-011, UC-CSW-017, UC-CSW-021
- **Verification:** API review

CSW shall expose an Object API and an Event API. Map objects shall be able to
represent entities and static objects. Their realization shall build on the
existing GizmoDistribution object and event mechanisms where applicable.

## Baseline constraints

### REQ-CSW-BASE-001: New target contract

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Design review

The target CSW contracts shall not require backward compatibility with the
obsolete CSW implementation and shall not require changes to GizmoSDK core.
Existing implementations may be used as informative mechanism references.

## Verification

Each requirement states its own verification method. Each engine realization
verifies these requirements in its own repository and references these IDs.

## Related Documentation

- [Generic CSW baseline use cases](use-cases.md)
- [Coordinate nomenclature and mapping](coordinate-nomenclature.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
