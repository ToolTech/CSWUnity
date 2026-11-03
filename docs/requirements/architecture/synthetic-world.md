# Requirements: Synthetic World

## Purpose

CSWUnity shall provide a Unity realization framework for the synthetic-world
capabilities defined by CSW. The framework must distinguish portable world
semantics from optional Unity rendering and effects.

The CSW capability taxonomy is broader than the currently implemented APIs.
CSWUnity shall not claim production support for a capability until its semantic
contract, data source, Unity realization, and validation profile are identified.

## Scope

This document covers capability maturity, world context and time, the
environment contract, Unity realization, and sensor integration. This document
holds the Principle, Mechanism, and Class requirements of the area. Its Behavior
requirements are in
[Synthetic World Behavior](../functional/synthetic-world.md).

## Capability maturity

### REQ-CSWU-WORLD-001: Maturity levels

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Review of the maturity level declared for each capability

Each synthetic-world capability shall publish one of these maturity levels:

1. **Defined:** a versioned semantic contract exists;
2. **Connected:** at least one provider or adapter supplies the contract;
3. **Realized:** a Unity component or asset realizes it;
4. **Validated:** behavior and performance pass a published profile.

## World context and time

### REQ-CSWU-TIME-001: Authoritative time

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Test proving that realization uses the CSW world clock
  rather than `Time.time`

CSWUnity shall consume an explicit CSW world or simulation clock. Unity
`Time.time` shall not be assumed to be authoritative for simulation or
externally supplied state.

### REQ-CSWU-TIME-002: Time representation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test of the time fields, with simulation time and UTC kept
  separate

World updates shall identify source time, receive time, clock identity, and
sequence where available. Simulation time and UTC time shall not be conflated.

### REQ-CSWU-WORLD-003: Shared world context

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Integration test of the shared world context

Map, coordinate, object, environment, movement, query, sensor, and rendering
services shall use the same world identity, time context, coordinate context,
and Local 3D Frame identity and generation.

## Environment contract

### REQ-CSWU-ENV-001: Environment provider

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** UC-CSWU-004, UC-CSWU-010
- **Verification:** Provider review plus test of time- and region-valid samples

CSWUnity shall consume environment state through a versioned, engine-neutral
provider contract. The contract shall support snapshots or samples valid for a
specified time, position or region, coordinate frame, and altitude range.

### REQ-CSWU-ENV-002: Units and frames

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Schema review of environment units and frames

Every environment value shall define units, coordinate frame, spatial
validity, temporal validity, source, quality, and authority where applicable.

### REQ-CSWU-ENV-003: Gravity

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Schema review plus physics-behavior test of gravity

The environment contract shall represent gravity magnitude or vector and its
frame. A Unity physics realization shall document whether it follows this
value or uses a host override.

### REQ-CSWU-ENV-004: Wind

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test proving that the same wind state drives a CSW physical
  consumer and a Unity visual consumer

The environment contract shall represent at least a wind vector. It shall be
extensible to spatial fields, altitude layers, gusts, turbulence, uncertainty,
and forecast validity.

Unity wind visuals shall not become the authoritative wind used by CSW
simulation models.

### REQ-CSWU-ENV-005: Atmosphere

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Schema review of the atmosphere fields

The environment contract shall be able to represent atmospheric temperature,
pressure, density, humidity, and derived values required by participating
simulation, sensor, or presentation models.

### REQ-CSWU-ENV-006: Visibility

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test proving that presentation and sensor consumers receive
  the same visibility state

The environment contract shall represent visibility or extinction information
with enough semantics for presentation and sensor consumers to apply the same
world state.

### REQ-CSWU-ENV-007: Precipitation

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Schema review and test of the precipitation fields

The environment contract shall support typed precipitation state, intensity,
phase, direction or fall velocity, and validity.

### REQ-CSWU-ENV-008: Clouds and sky

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test of cloud and sky state without a specific rendering
  implementation

The environment contract shall support cloud and sky state without requiring
a particular Unity sky or volumetric-cloud implementation.

### REQ-CSWU-ENV-009: Lighting and time of day

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Test of the lighting and time-of-day inputs

The environment contract shall support the solar, lunar, ambient, and
time-of-day inputs required by installed lighting realizations.

### REQ-CSWU-ENV-010: Water and sea state

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Schema review of the water and sea-state fields

The environment contract shall support water level and optional wave, current,
surface, and sea-state information where provided by CSW.

### REQ-CSWU-ENV-011: Surface and vegetation state

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Schema review of the surface and vegetation fields

The environment contract shall be extensible to surface condition, terrain
material, snow or wetness, vegetation state, and other map-related
environmental data.

### REQ-CSWU-ENV-012: Partial support

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M5
- **Use cases:** UC-CSWU-004, UC-CSWU-010
- **Verification:** Test proving that unsupported fields are reported without
  blocking streaming

An environment provider may omit unsupported fields. Consumers shall declare
required and optional fields and apply a documented unavailable-data policy.

## Unity realization

### REQ-CSWU-WORLD-004: Optional realization modules

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** UC-CSWU-010
- **Verification:** Test installing the core package without weather, water,
  cloud, or sensor assets

Sky, sun, clouds, fog, rain, snow, wind effects, water, vegetation,
post-processing, and sensor effects shall be optional Unity components/assets
selected through feature profiles.

### REQ-CSWU-WORLD-005: Render-pipeline adapters

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** UC-CSWU-010
- **Verification:** Design review plus test replacing a render-pipeline adapter

Environment semantics shall remain independent of Unity render pipelines.
URP, HDRP, built-in, or future render-pipeline implementations shall be
replaceable adapters.

### REQ-CSWU-WORLD-007: Physical and visual separation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test proving that visual approximations are not fed back
  into physical state

Physical models, sensor models, and visual effects may derive different outputs
from the same environment snapshot. Visual approximations shall be identified
and shall not feed back as authoritative simulation state unless explicitly
configured.

## Sensor integration

### REQ-CSWU-SENSOR-001: Sensor context

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Test of the sensor context inputs

A synthetic sensor realization shall receive pose, world time, coordinate
context, and declared environment inputs.

### REQ-CSWU-SENSOR-002: Modality extensions

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Package review proving that modality extensions add no core
  dependency

The architecture shall allow optional visual, infrared, radar, radio, and audio
presentation modules without adding those dependencies to the core map
streaming package.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Synthetic World Behavior](../functional/synthetic-world.md), is verified when
the following acceptance criteria hold:

- A wind update can drive a CSW physical consumer and a Unity visual consumer
  from the same timestamped environment state.
- A recorded environment sequence replays deterministically at its semantic
  state boundary.
- Unsupported weather fields are reported without preventing map and object
  streaming.
- At least one selected release profile validates time, wind, visibility, and
  lighting integration.
- Installing the core package does not require a specific weather, water,
  cloud, or sensor rendering asset.

## Related Documentation

- [Synthetic World Behavior requirements](../functional/synthetic-world.md)
- [Synthetic world architecture](../../design/synthetic-world.md)
- [Rendering architecture](../../design/rendering.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
- [CSW capability architecture](https://saab.ghe.com/saab/CSW)
