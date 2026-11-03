# Requirements: Synthetic World Behavior

## Purpose

This document holds the Behavior requirements of the
[Synthetic World](../architecture/synthetic-world.md) area. The purpose, scope,
and verification of the area are described in that document.

## Scope

The scope of the area is described in
[Synthetic World](../architecture/synthetic-world.md#scope). This document
contains requirements of the following groups:

- Capability maturity
- World context and time
- Unity realization
- Sensor integration

## Capability maturity

### REQ-CSWU-WORLD-002: Capability report

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M5
- **Use cases:** UC-CSWU-004
- **Verification:** Test of the runtime and editor capability report

At runtime and in the Unity editor, CSWUnity shall report which capabilities
are defined, connected, realized, enabled, unsupported, or degraded.

## World context and time

### REQ-CSWU-TIME-003: Time control

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M5
- **Use cases:** UC-CSWU-013
- **Verification:** Test of pause, resume, step, replay, and external-clock
  control

The host shall be able to pause, resume, step, replay, or follow an external
clock when the selected CSW time provider supports those operations.

## Unity realization

### REQ-CSWU-WORLD-006: Interpolation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Test of interpolation by world time without changing the
  source state

Visual environment changes shall support configurable interpolation based on
world time. Interpolation shall not rewrite authoritative source state.

## Sensor integration

### REQ-CSWU-SENSOR-003: Output metadata

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M5
- **Use cases:** None
- **Verification:** Test of the sensor output metadata

Sensor outputs shall identify modality, source pose and time, projection or
field of view, resolution, and environment assumptions.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Synthetic World](../architecture/synthetic-world.md#verification).

## Related Documentation

- [Synthetic World requirements](../architecture/synthetic-world.md)
- [Synthetic world architecture](../../design/synthetic-world.md)
- [Rendering architecture](../../design/rendering.md)
- [Operator use cases](../use-cases/operator.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
- [CSW capability architecture](https://saab.ghe.com/saab/CSW)
