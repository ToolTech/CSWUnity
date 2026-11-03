# Requirements: Coordinate Nomenclature and Mapping

## Purpose

This document defines platform-independent coordinate terms shared by the core
use cases.

## Scope

The terms define the target design contract and describe current GizmoSDK
capabilities. A statement about the target contract is not evidence that the
current `gzCoordinate` API implements every stage. Current ownership and known
implementation differences are stated explicitly.

This document is hosted content of the CSW owner (owner code `CSW`) until the
CSW repository has its own requirements root. Its requirement IDs keep the owner
code `CSW`. The requirements apply to CSWUnity, CSWUnreal, and other viewer
realizations. Definitions, tables, equations, and explanatory text are context
for the requirements. Each requirement states its kind. The CSW owner has no
milestones yet, so every requirement is `Unassigned`.

## Terminology use

### REQ-CSW-COORD-001: Shared coordinate terminology

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review of viewer documentation

CSWUnity, CSWUnreal, and other viewer realizations shall use these terms when
describing equivalent behavior.

## Coordinate model

```text
GeoPosition <-> Global3DPositionD

MapOriginGeoPosition -> OriginGlobal3D
                            |
                            +-> may define LocalOriginOffsetD

Global3DPositionD - LocalOriginOffsetD -> Global3DOffsetD
Global3DOffsetD -> local basis -> checked float -> Local3DPositionF
Local3DPositionF <-> Viewer Coordinate Mapping <-> named viewer position
```

The current GizmoSDK `gzCoordinate` API owns the first conversion. The current
GizmoSDK and CSW implementations perform localization in consumer code rather
than through a common `gzCoordinate` Local 3D API. A viewer adapter such as
CSWUnity or CSWUnreal owns the final conversion and native position
interpretation.

| Stage | Input and output | Target owner | Current implementation |
| --- | --- | --- | --- |
| Coordinate conversion and Global 3D mapping | `GeoPosition` and double-precision Global 3D Position | Coordinate service | GizmoSDK `gzCoordinate` |
| Origin-relative localization | Global 3D Position and single-precision Local 3D Position, with an explicit Local 3D Frame | Shared coordinate/localization boundary | Consumer code, including map, ROI, and scene integrations |
| Viewer Coordinate Mapping | Local 3D Position and a specifically named native viewer position | Viewer adapter | CSWUnity or CSWUnreal integration |

The following conceptual identifiers are used in equations and design reviews.
They do not prescribe language-specific wrapper types:

| Identifier | Required meaning |
| --- | --- |
| `MapOriginGeoPosition` | CRS-associated map anchor |
| `Global3DPositionD` | double-precision point in the named Global 3D Frame |
| `OriginGlobal3D` | Map Origin mapped into that Global 3D Frame as a double-precision point |
| `LocalOriginOffsetD` | double-precision displacement locating a named Local 3D Frame origin |
| `Global3DOffsetD` | double-precision displacement from the Local Origin Offset to a Global 3D Position |
| `Local3DPositionF` | single-precision point in one Local 3D Frame identity and generation |
| `ViewerPosition` | placeholder that must be replaced by a native frame-specific name at a viewer API |

### REQ-CSW-COORD-002: Explicit localization contract

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Design review of the localization contract

The target shared coordinate boundary shall make that localization contract
explicit.

### REQ-CSW-COORD-003: Distinguishable conversion stages

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Design review plus per-stage tests

The stages may be composed for efficient execution, but their semantics,
precision boundaries, and ownership shall remain distinguishable.

## Coordinate context

### Coordinate Reference System

A Coordinate Reference System (CRS) defines how coordinate values relate to
the Earth. The logical CRS description includes:

- coordinate-system type;
- horizontal datum and reference ellipsoid;
- projection when applicable;
- vertical datum or height model; and
- metadata required by that coordinate system.

In GizmoSDK, `gzCoordSystem` contains datum, projection, and coordinate-system
type. `gzCoordSystemMetaData` supplies additional values such as UTM zone and
hemisphere or a flat-earth geographic origin. The height model is associated
with the selected datum.

### Map Coordinate Context

A Map Coordinate Context is the complete information required to interpret
positions for one map or a compatible set of maps. It comprises:

- the CRS and its metadata;
- the map's 3D mapping and axis convention;
- the Map Origin `GeoPosition` for a georeferenced map, or an explicit
  declaration that the map is not georeferenced; and
- a stable identity or revision for that context.

A position value without its applicable context is ambiguous.

## Position terms

### GeoPosition

A `GeoPosition` is a coordinate value associated with a CRS. It identifies the
same geographic location independently of which supported representation is
used.

`GeoPosition` is the collective domain term. It is not synonymous with a
geodetic position and is not a Unity or Unreal position.

| Representation | Meaning | Current GizmoSDK form |
| --- | --- | --- |
| Geodetic Position | Latitude, longitude, and height | `gzLatPos` |
| Geocentric Position | Earth-centered, Earth-fixed Cartesian XYZ in metres | `gzCartPos` |
| Projected Position | Northing, easting, and height in a named projection | `gzProjPos` |
| UTM Position | Zone, hemisphere, northing, easting, and height | `gzUTMPos` |
| MGRS Reference | UTM-derived grid-zone and grid-square reference with encoded precision | `gzString` through the MGRS API |
| Flat-Earth Position | Local ENU position relative to a geographic origin | `gzVec3D` with flat-earth metadata |

GizmoSDK geodetic latitude and longitude use radians internally. Presentation
formats such as degrees or degree-minute-second strings do not change the
underlying `GeoPosition`.

### Global 3D Frame

A Global 3D Frame is the double-precision Cartesian XYZ frame produced by the
3D mapping for a Map Coordinate Context. It is engine-neutral.

The word *global* means that values are not relative to a Local Origin Offset,
map node, ROI node, or viewer object. It does not mean that every map uses the
same numerical frame.

### Global 3D Position

A Global 3D Position is a double-precision Cartesian XYZ value in a specific
Global 3D Frame. It is the canonical position representation used by the core
camera and scene model.

The typed GizmoSDK `gzCoordinate::get3DCoordinate()` overloads convert a
geodetic or geocentric `GeoPosition` to a Global 3D Position in a selected map
context. The typed `gzCoordinate::getGlobalCoordinate()` overloads perform the
reverse conversion. The selected CRS and metadata define the context, and the
axis swizzle applies to all currently supported map types except geocentric raw
XYZ.

MGRS is supported by the stateful conversion API through conversion to or from
UTM. It is not a separate geometric projection and is not supported as a map
type in the typed Global 3D overloads.

#### REQ-CSW-COORD-004: Dynamic overload conversions

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Review plus precision tests comparing dynamic and typed
  overloads

Dynamic overloads have different packing behavior and shall not be assumed to
provide the same datum conversions as the typed overloads.

#### REQ-CSW-COORD-005: Global 3D context association

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** API review proving that Global 3D Positions carry their Map
  Coordinate Context

The same geographic location can have different Global 3D Position values in
maps that use different coordinate systems or mappings. A Global 3D Position
is therefore not self-describing and shall remain associated with its Map
Coordinate Context.

### Map Origin

A Map Origin is a `GeoPosition` supplied with a map. It geographically anchors
the map and remains associated with the map's CRS and metadata.

When a numerical Global 3D value is required, the Map Origin is mapped through
the same Map Coordinate Context as every other `GeoPosition`:

```text
OriginGlobal3D = ToGlobal3D(MapOriginGeoPosition)
```

#### REQ-CSW-COORD-006: Non-georeferenced maps

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Review plus test with a non-georeferenced map

A deliberately non-georeferenced Cartesian map shall declare that status and
start in an explicitly named 3D frame; it shall not invent a `GeoPosition` or
call an unreferenced XYZ value a Map Origin.

#### REQ-CSW-COORD-007: Map Origin qualification

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Naming review of documentation and APIs

The Map Origin `GeoPosition` and Origin Global 3D Position are related but are
not the same value or type. Documentation and APIs shall not call the mapped XYZ
value the Map Origin without qualification.

### Local 3D Frame and Local Origin Offset

A Local 3D Frame is an explicitly identified origin-relative frame used to keep
3D values near zero and suitable for single-precision storage. It has:

- a stable frame identity;
- a generation that changes when its origin is rebased;
- a double-precision Local Origin Offset locating its origin in the Global 3D
  Frame; and
- a declared basis and unit relationship to the Global 3D Frame.

The Local Origin Offset is a double-precision 3D displacement from the Global
3D Frame origin to the Local 3D Frame origin. It is not a `GeoPosition`.
For a map-anchored local frame it can be derived from
`OriginGlobal3D`. A nested ROI, node, or camera-relative frame can use a
different Global 3D Position as its Local Origin Offset.

Localization is defined by:

```text
Global3DOffsetD =
    Global3DPositionD - LocalOriginOffsetD

Local3DWorkingD =
    GlobalToLocalBasisD.TransformVector(Global3DOffsetD)

Local3DPositionF =
    CheckedFloat(Local3DWorkingD)

Global3DPositionD =
    LocalOriginOffsetD
    + LocalToGlobalBasisD.TransformVector(Double(Local3DPositionF))
```

Global 3D Offset is the double-precision displacement produced by the
subtraction. It becomes a Local 3D Position only after it is expressed in the
declared local basis and narrowed to the local storage type. Translation-only
localization uses the identity basis and preserves Global 3D axes and units.

The inverse reconstructs the Global 3D Position within the published local
round-trip tolerance; float storage makes the numerical conversion non-exact.

#### REQ-CSW-COORD-008: Double-precision subtraction

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Precision tests of subtraction before narrowing

The subtraction shall occur in double precision. Implementations shall not
narrow the two Global 3D operands before subtracting them.

#### REQ-CSW-COORD-009: Checked narrowing

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Tests with non-finite and out-of-range values

Conversion to single precision shall be checked for finite values and supported
range.

### Local 3D Position

A Local 3D Position is a single-precision Cartesian XYZ position in one named
Local 3D Frame. It is meaningless without that frame's identity and generation.

`Local3DPositionF` is the shared storage and exchange boundary.

The term does not imply ENU orientation, Unity coordinates, Unreal coordinates,
or a node-parent transform. Those meanings require an explicitly named frame or
the subsequent Viewer Coordinate Mapping.

Current `gzCoordinate` APIs do not expose this Local 3D Position contract.
Existing GizmoSDK consumers commonly subtract double-precision positions and
then cast the resulting offset to `gzVec3`; that behavior is precedent for the
precision order, not a shared API contract.

#### REQ-CSW-COORD-010: Local frame isolation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Test rejecting values from mixed frames or generations

Local 3D Positions from different frames or generations shall not be directly
compared, subtracted, or combined.

#### REQ-CSW-COORD-011: Local working values

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** API review of local working values

An implementation may retain an explicitly named double-precision local working
value or compose Global-to-Viewer mapping without materializing the float
intermediate. Such an optimization shall preserve the same frame semantics and
shall not expose the double value as `Local3DPositionF`.

### Local Tangent Frame

A Local Tangent Frame is the East-North-Up (ENU) orientation at a
`GeoPosition`. It supplies local directions for heading, movement, normals,
and orientation without changing the position's CRS.

`gzCoordinate::get3DOrientation()` supplies the map orientation used by the
current API. For geocentric maps it returns the location-dependent ENU basis.
For other recognized map types it returns the fixed orientation for the
default east/up/south map axes. It does not accept or automatically follow a
custom axis swizzle.

### Viewer Coordinate System

A Viewer Coordinate System is the coordinate system and terminology native to
a viewer or rendering engine. Unity and Unreal define different axes, units,
precision behavior, transform hierarchies, and world/local terminology.

Core use cases use *Viewer Position* only as a generic placeholder.

#### REQ-CSW-COORD-012: Native viewer terms

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review per viewer specialization

A viewer specialization shall use its established native term, such as Unity
World Position or Unreal World Position, and define how it relates to Local 3D
Position and, through localization, Global 3D Position.

### Viewer Coordinate Mapping

A Viewer Coordinate Mapping is the explicit, bidirectional transformation
between a Local 3D Frame and a specifically named position frame in a Viewer
Coordinate System. It is owned by the viewer adapter and may include:

- axis permutation and sign changes;
- handedness conversion;
- unit scaling;
- a viewer root, level, actor, or component transform; and
- separate transformation rules for positions, vectors, normals, and
  orientations.

A viewer mapping does not select the Map Origin, change the Local Origin
Offset, or change the underlying CRS or `GeoPosition`.

### Viewer Transform

A Viewer Transform is the resolved affine transform for one Map Coordinate
Context, Local 3D Frame generation, and specifically named viewer target frame.
Its conceptual decomposition is:

```text
Local3DWorkingD =
    GlobalToLocalTransformD.TransformPoint(Global3DPositionD)

Local3DPositionF =
    CheckedFloat(Local3DWorkingD)

ViewerPosition =
    LocalToViewerTransform.TransformPoint(Local3DPositionF)

GlobalToViewerTransform =
    LocalToViewerTransform * GlobalToLocalTransformD

ViewerToGlobalTransform =
    inverse(GlobalToViewerTransform)
```

The full `GlobalToViewerTransform` therefore contains the Local Origin Offset as
its translation term after the viewer's linear mapping is taken into account.

The mathematical transform pair can be invertible, while conversion through a
stored Local 3D Position or viewer-native float value remains subject to the
declared round-trip tolerance.

A Local Origin Rebase changes the Local Origin Offset and frame generation. It
changes Local 3D and viewer positions but does not modify the authoritative
`GeoPosition`, Global 3D Position, Map Origin, or object identity.

The current viewer realizations use different mappings:

| Viewer | Current mapping responsibility |
| --- | --- |
| CSWUnity | Resolves map- and ROI-relative Local 3D Positions, maps GizmoSDK XYZ to Unity axes, and uses camera-relative presentation to keep Unity transforms near zero |
| CSWUnreal | Maps origin-relative GizmoSDK XYZ to a specifically identified Unreal frame according to coordinate type, applies Unreal world-unit scaling, and incorporates the `UCSWScene` root transform |

These rows describe existing realization strategies, not mandatory behavior for
all future viewers.

#### REQ-CSW-COORD-013: Single offset application

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Test proving that the offset is applied once

The offset shall not be applied a second time when an implementation
materializes Local 3D Position as an intermediate value.

#### REQ-CSW-COORD-014: Semantic transform kinds

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Point, vector, normal, and orientation transform tests

Positions use the full affine transform. Offset and direction vectors use the
applicable linear part without translation. Normals use the mathematically
correct normal transform, and orientations are converted by transforming their
basis. These operations shall not be replaced by point transformation.

### CSWUnity mapping

The earlier CSWUnity map API, which the command-driven realization replaces,
already applied the intended precision order, but its names did not
consistently match this document:

- `Map._origin` was a double-precision mapped value derived from the database
  origin `GeoPosition`;
- `Map.WorldToGlobal()` subtracted that value, so the result called *global* by
  that API could already be origin-relative;
- `MapPos.position` was a `Vec3D` working value relative either to the map or
  to a node context;
- `Map.ToLocal()` subtracted `RoiNode.Position` in double precision; and
- `MapPos.LocalPosition` converted the resulting value to `Vec3` float.

`MapPos.LocalPosition` returned a bare `Vec3` without an explicit Local 3D
Frame identity, generation, or checked narrowing result. The associated node
supplied part of the context indirectly.

The logical point mapping between the default GizmoSDK Local 3D axes and Unity
is:

| Local 3D component | Unity component |
| --- | --- |
| `X` | `+X` |
| `Y` | `+Y` |
| `Z` | `-Z` |

Vector and camera-matrix helpers perform the Z-axis conversion explicitly.
Scene content can receive the equivalent conversion through its parent
hierarchy. Distances currently use one Unity unit per GizmoSDK map unit.
The earlier `MapUtil.MapToUnity()` helper attached an object below its ROI
transform and copied the float Local 3D Position into Unity Transform Local
Position.

The Unity camera remains near the active camera-relative Local 3D Frame origin.
Its Global 3D Position is sent separately to scene traversal, while streamed
scene transforms and shader offsets provide the camera-relative presentation.

#### REQ-CSW-COORD-015: Precision order at new API boundaries

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Naming review plus precision tests at new API boundaries

The target contract shall preserve that subtract-before-narrowing order while
replacing the overloaded *global*, *origin*, and *local* names at new API
boundaries.

#### REQ-CSW-COORD-016: Explicit frame token

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Test of frame tokens and failed precision conversion

New APIs shall carry the frame token explicitly and report failed precision
conversion.

### Current CSWUnreal mapping

The current CSWUnreal implementation does not expose the complete conceptual
chain as one routine or define an explicit Local 3D Position type. Its effective
precision layers are:

| Layer | Current representation | Meaning |
| --- | --- | --- |
| Geographic input and map origin | Coordinate-specific dynamic value or `gzLatPos` | `GeoPosition` plus CRS context |
| Coordinate-service output | `gzVec3D` | double-precision Global 3D Position |
| Scene and ROI anchors | `gzDoubleXYZ`, `gzMatrix4D`, and `FVector3d` | double-precision hierarchy anchors and transforms |
| Child transforms and mesh vertices | `gzMatrix4`, `gzVec3`, and `FVector3f` | already-local single-precision scene payload |
| Public mapped point | `FVector3d` | double-precision position interpreted by the `UCSWScene` root mapping |

Core CSW currently reads the database Map Origin representation and converts it
to a Global 3D Position before sending `cswSceneCommandGeoInfo`. Consequently,
CSWUnreal receives the derived value in `command->getOrigin()`, not the original
Map Origin `GeoPosition`.

The generic `cswVector*::UEVector*()` and `GZVector*()` templates copy components
and convert scalar types. `cswMatrix4_*::UEMatrix4()` and `GZMatrix4()` adapt
matrix storage and vector convention. These templates do not define coordinate
semantics, change axes or handedness, scale units, or apply an origin.

Semantic mapping is instead hard-coded in named matrix factories and selected
by the runtime `CoordType` switch. Let `p = (x,y,z)` be a GizmoSDK point,
`o = (ox,oy,oz)` the first-map double Global 3D origin, and `s` Unreal
`WorldToMeters`. The current forward mappings are:

```text
General geometry:
    u = s * (-(z - oz), x - ox, y - oy)

Projected or UTM:
    u = s * (x - ox, z - oz, y - oy)
```

The corresponding inverse mappings add `o` after applying inverse axis and
unit mapping. `UCSWScene` currently obtains the reverse point mapping by
inverting the selected complete forward matrix, not by using the standalone
`UE_2_GZ*()` axis helpers.

For streamed content, the effective hierarchy is approximately:

```text
Unreal world position =
    s * AxisMap(
        ROI anchor double
        + child transform or vertex float
        - first-map origin double)
```

The root performs first-map-origin subtraction, axis and handedness conversion,
and `WorldToMeters` scaling in double precision. ROI anchors remain double.
Child transforms and mesh vertices are already float before CSWUnreal receives
them, so the upstream Global-to-ROI-local conversion is outside this plugin.

The helpers named `GZ_2_UE_Local()` and `UE_2_GZ_Local()` operate on
`gzVec3D`/`FVector3d`; they are not the Local 3D Position contract defined here.
They manually add or remove root-relative translation and do not account for a
complete parent transform with custom rotation or scale.

The current mapping switch implements general geometry and projected/UTM
content. Geodetic, geocentric, and Flat Earth paths fall through an
unimplemented/default path despite unused matrix helpers, and MGRS has no
dedicated Unreal mapping case.

The current camera path maps Unreal camera position to Global 3D Position by
removing root-relative translation and applying the inverse selected matrix. It
does not implement the opposite core-to-Unreal camera placement direction and
does not account for arbitrary parent rotation or scale.

#### REQ-CSW-COORD-017: Transport origin naming

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Review of transport field names

New transport contracts shall reflect that distinction in their field names and
retain the geographic anchor in the Map Coordinate Context.

#### REQ-CSW-COORD-018: Explicit unsupported-mapping failure

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Tests proving explicit failure of unsupported mappings

Unsupported mappings shall fail explicitly rather than silently use identity or
general-geometry behavior.

#### REQ-CSW-COORD-019: Coherent mapping rebuild

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Mapping tests in the Unreal realization that change
  `WorldToMeters`, the custom origin, and the root transform

Changing `WorldToMeters`, using a custom origin, or changing the root transform
shall rebuild one coherent forward/inverse mapping. A custom-origin option
shall not accidentally disable axis, handedness, and unit mapping together with
automatic origin subtraction.

#### REQ-CSW-COORD-020: Shared camera transform pair

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Comparison of camera, point-helper, and streamed-content
  transforms

Camera and public point helpers shall use the same complete transform pair as
streamed content.

#### REQ-CSW-COORD-021: CSWUnreal mapping snapshot

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Design review of the mapping snapshot plus independent
  mapping tests in the Unreal realization

The CSWUnreal specialization shall therefore:

- name every public point parameter and result by semantic frame and precision,
  rather than by scalar or engine type alone;
- treat component-copy and matrix-layout templates as representation adapters,
  not coordinate mappings;
- resolve coordinate type, Local Origin Offset, axis mapping, handedness,
  `WorldToMeters`, root hierarchy, and inverse into one immutable mapping
  snapshot with a generation;
- use that same snapshot for streamed content, public point conversion, spatial
  queries, and both camera directions;
- retain ROI anchors and origin arithmetic in double precision while keeping
  float child payload explicitly bound to its ROI or Local 3D Frame;
- reject unsupported coordinate types and stale generations explicitly; and
- validate forward/inverse point, vector, normal, and orientation mappings
  independently.

### Unreal Engine georeferencing terminology

Epic's `FGeographicCoordinates` represents the geodetic case specifically; it
is not equivalent to the broader core `GeoPosition` term.
`FCartesianCoordinates` carries double-precision projected or ECEF values.

Epic's Flat Planet and Round Planet modes select how the Unreal Engine CRS is
related to projected, geographic, and ECEF coordinate systems. They are Unreal
viewer configuration terms, not additional core `GeoPosition`
representations.

#### REQ-CSW-COORD-022: Unreal georeferencing terminology

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review of the Unreal specialization

The Unreal specialization shall preserve Epic's established terminology from
[Georeferencing a Level in Unreal Engine](https://dev.epicgames.com/documentation/unreal-engine/georeferencing-a-level-in-unreal-engine):

| Core term | Unreal Engine term | Relationship |
| --- | --- | --- |
| Viewer Coordinate System | Unreal Engine CRS | The level coordinate system used by actors |
| Geodetic Position | Geographic coordinates in a Geographic CRS | Latitude, longitude, and altitude |
| Geocentric Position | Cartesian coordinates in a Geocentric CRS | Earth-Centered, Earth-Fixed coordinates |
| Projected Position | Cartesian coordinates in a Projected CRS | Easting, northing, and up in the selected projection |
| Viewer georeference anchor | Level Origin | Associates the Unreal Engine CRS origin with a geographic, projected, or planet-center location |
| Runtime origin adjustment | World Origin offset or shifting | Changes the engine origin for precision without changing the georeferenced location |
| Local Tangent Frame | Tangent frame or ENU vectors | East, north, and up expressed in the Unreal Engine CRS |

#### REQ-CSW-COORD-023: Viewer mapping implementation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Documentation review

The built-in Unreal Georeferencing plugin and the current CSWUnreal
`UCSWScene` adapter are separate implementations. Both can be described through
the same core boundary, but documentation shall state which implementation
performs the Viewer Coordinate Mapping.

### Viewer Position and viewer-specific origins

A Viewer Position is a position represented in a viewer's engine coordinate
system after applying the Viewer Coordinate Mapping.

Core terminology uses Local 3D Frame and Local Origin Offset for origin-relative
precision handling.

Unity may use camera-relative presentation to keep single-precision transforms
near zero. Unreal may use a map-centered root transform and large-world
coordinate support. These are viewer specializations, not properties of
`GeoPosition`, Map Origin, Global 3D Position, or the core camera.

In an Unreal specialization, the configured Level Origin establishes the
geographic association of the Unreal Engine CRS. A World Origin shift is a
separate runtime precision operation. Their combined effect determines the
current mapping to Unreal World Positions; neither changes the authoritative
`GeoPosition` or Global 3D Position.

#### REQ-CSW-COORD-024: Native Viewer Position term

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review of viewer specializations

A viewer specialization shall replace this generic term with the native frame
actually used, such as Unity Transform Local Position, Unity World Position,
Unreal World Position, or Unreal Component Relative Position.

#### REQ-CSW-COORD-025: Viewer-specific origins

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Documentation review

*Viewer Origin*, *Level Origin*, *World Origin*, and similar terms are retained
only when they name a specific viewer concept. Documentation shall state how
each such origin contributes to the Local Origin Offset, Viewer Transform, or
native transform hierarchy.

## GizmoSDK coordinate-system variants

`gzCoordType` identifies the representation and conversion rules of a
coordinate system:

| `gzCoordType` | Core meaning |
| --- | --- |
| `GZ_COORDTYPE_GEOCENTRIC` | Geocentric Cartesian coordinates |
| `GZ_COORDTYPE_GEODETIC` | Geodetic latitude, longitude, and height |
| `GZ_COORDTYPE_PROJECTED` | Coordinates in a named flat projection |
| `GZ_COORDTYPE_UTM` | UTM coordinates with zone and hemisphere metadata |
| `GZ_COORDTYPE_MGRS` | UTM-derived Military Grid Reference System representation |
| `GZ_COORDTYPE_FLATEARTH` | Local Cartesian coordinates around a geographic origin |

The coordinate-system type is not the same as a 3D mapping or a viewer
coordinate system.

## Mapping to Global 3D

### Coordinate conversion

Coordinate conversion changes the representation or CRS while preserving the
geographic location. Examples include geodetic-to-geocentric,
geodetic-to-projected, geodetic-to-UTM, UTM-to-MGRS, datum transformation, and
height-model transformation.

### Global 3D mapping

Global 3D mapping embeds a coordinate representation into Cartesian XYZ using
`gzCoordSystem`, `gzCoordSystemMetaData`, and `gzCoordinateSwizzle`.

The current typed mapping behavior is:

| Coordinate-system type | Global 3D interpretation |
| --- | --- |
| Geocentric | The actual geocentric `X,Y,Z` in metres; swizzle is ignored |
| Geodetic | Longitude, altitude, and latitude arranged by the selected swizzle |
| Projected | Easting, height, and northing arranged by the selected swizzle |
| UTM | Easting, height, and northing arranged by the selected swizzle; zone and hemisphere come from metadata |
| Flat Earth | Local ENU components arranged by the selected swizzle |
| MGRS | No direct mapping; resolve the MGRS Reference to UTM first |

For forward UTM mapping, the converted position must match the zone and
hemisphere in the map metadata or conversion fails.

For a geocentric Map Coordinate Context, Geocentric Position and Global 3D
Position have identical numerical XYZ components and units. They remain
distinct terms only because Geocentric Position is a CRS-associated
`GeoPosition` representation, while Global 3D Position is the value consumed
by the core camera and scene interfaces.

`gzCoordinateMapping` separately declares names for flat, spherical, geodetic,
geocentric, sinusoidal, trapezoidal, Web Mercator, Lambert, UTM, RT90,
SWEREF99, and general projected mappings. The current `gzCoordinate` plugin
does not consume that enum in coordinate conversion or Global 3D mapping.
Those enum values are therefore not evidence that each named mapping is
implemented.

### Axis mapping and swizzle

An axis mapping, called a swizzle by `gzCoordinate`, assigns semantic
coordinate components to XYZ axes and signs. It does not change datum or
projection.

Geocentric positions retain their raw Cartesian XYZ order in the typed Global
3D conversion API and bypass the swizzle.

The GizmoSDK default map swizzle is:

| Semantic component | Global 3D axis |
| --- | --- |
| Longitude or easting | `+X` |
| Altitude or height | `+Y` |
| Latitude or northing | `-Z` |

For a conventional flat projected map this gives `X = east`, `Y = up`, and
`Z = south`.

The GizmoSDK ENU swizzle is:

| ENU component | Axis |
| --- | --- |
| East | `+X` |
| North | `+Y` |
| Up | `+Z` |

Custom swizzles are bit masks.

#### REQ-CSW-COORD-026: ENU swizzle distinction

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology and mapping review

The ENU swizzle is used to express a local tangent frame. It shall not be
confused with the default Global 3D map axes.

#### REQ-CSW-COORD-027: Validated swizzle profiles

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Review of the swizzle profiles used by core integrations

The current implementation does not validate that a custom mask is complete and
one-to-one, so core integrations should use a validated mapping profile rather
than pass arbitrary masks.

### Position and vector transformations

#### REQ-CSW-COORD-028: Position-only translation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Position, direction, offset, normal, and orientation
  transformation tests

Position conversion may include translation by a declared Local Origin Offset.
That translation shall be applied only to a position. Direction, offset, normal,
and orientation conversion shall apply only their applicable linear, normal, or
basis operations.

#### REQ-CSW-COORD-029: Distinct adapter operations

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** API review of the coordinate and viewer adapters

Shared coordinate and viewer adapters shall therefore expose semantically
distinct operations rather than route every value through one point-conversion
helper.

#### REQ-CSW-COORD-030: Checked conversion calls

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Code review plus failure tests

All conversion calls returning a success flag shall be checked. Failed
conversion does not establish a usable output value or guarantee rollback of
intermediate state.

## Camera relationship

The core camera stores a Camera Pose in the active Global 3D Frame. Geographic
input is converted from `GeoPosition` before it updates the camera. Geographic
output is converted from the camera's Global 3D Position on request.

Heading, pitch, ground following, and geodetic orbit may use the Local Tangent
Frame, but this does not make the camera class responsible for coordinate
conversion.

The viewer adapter maps the Camera Pose to an engine camera and maps scene
content into the same viewer frame. Moving a Unity scene around a camera is a
precision implementation detail and is not core camera movement.

## Terminology rules

The following rules are requirements in the compact form of the requirements
standard. Each rule states its status and kind, and all rules share these
fields:

- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review of documentation and APIs

The rules are:

- REQ-CSW-COORD-031 [Pending, Principle]: Use `GeoPosition` for the
  CRS-associated collective position concept.
- REQ-CSW-COORD-032 [Pending, Principle]: Use Geodetic Position only for
  latitude, longitude, and height.
- REQ-CSW-COORD-033 [Pending, Principle]: Use Global 3D Position for the
  engine-neutral XYZ value used by the core camera and scene.
- REQ-CSW-COORD-034 [Pending, Principle]: Use Map Origin only for the map's
  CRS-associated `GeoPosition`.
- REQ-CSW-COORD-035 [Pending, Principle]: Use Origin Global 3D Position for its
  mapped double-precision XYZ value.
- REQ-CSW-COORD-036 [Pending, Principle]: Use Local Origin Offset for the
  double-precision 3D displacement locating a named Local 3D Frame origin.
- REQ-CSW-COORD-037 [Pending, Principle]: Use Global 3D Offset for the
  double-precision displacement from that origin to a Global 3D Position; it is
  a vector, not a position.
- REQ-CSW-COORD-038 [Pending, Principle]: Use Local 3D Position only for a
  single-precision position associated with an explicit Local 3D Frame identity
  and generation.
- REQ-CSW-COORD-039 [Pending, Principle]: Qualify other local positions as
  node-local, ENU-local, actor-relative, or component-relative and name their
  frame.
- REQ-CSW-COORD-040 [Pending, Principle]: Do not use *world position* in core
  use cases. Unity World Position and Unreal World Position are
  platform-specific terms.
- REQ-CSW-COORD-041 [Pending, Principle]: Preserve each viewer's established
  coordinate nomenclature in its specialization and state its Viewer Coordinate
  Mapping explicitly.
- REQ-CSW-COORD-042 [Pending, Principle]: Use Viewer Transform for the composed
  runtime transform and document its Global-to-Local and Local-to-Viewer stages.
- REQ-CSW-COORD-043 [Pending, Principle]: Never apply an origin translation to a
  direction, offset, normal, or orientation.
- REQ-CSW-COORD-044 [Pending, Principle]: Do not use coordinate conversion,
  projection, 3D mapping, axis swizzle, and localization, viewer mapping, or
  origin rebasing as synonyms.

## Existing implementation terminology

Some earlier CSWUnity APIs used *world* for geographic conversion, *global* for
values that could already have a map-origin value subtracted, and *local* for
both double-precision ROI-relative working values and their float
representations. Those names describe an earlier implementation and are not
normative core terminology.

Current CSWUnreal names also require semantic qualification:

| Existing name | Actual current meaning |
| --- | --- |
| `cswSceneCommandGeoInfo::Origin` | Global 3D Position derived from the map origin before the command reaches CSWUnreal |
| `ModelOriginX/Y/Z` | Scaled Unreal-space double coordinates cached by `UCSWScene` |
| `GZ_2_UE_Local()` | Double-precision GizmoSDK point to root-relative Unreal point helper, not Global-to-Local 3D |
| `UEVector*()` / `GZVector*()` | Component and scalar-type adapters without coordinate semantics |
| a parameter named `local` in `GZ_2_UE()` | Often a Global 3D map point rather than a Local 3D Position |

### REQ-CSW-COORD-045: Terminology in new artifacts

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Terminology review of new use cases and APIs

New use cases and future APIs shall use the terms in this document and shall
state the applicable Map Coordinate Context explicitly.

## Verification

Each requirement states its own verification method. Each viewer realization
verifies the requirements that apply to it in its own repository and references
these IDs.

## Related Documentation

- [Generic CSW baseline requirements](requirements.md)
- [Generic CSW baseline use cases](use-cases.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
