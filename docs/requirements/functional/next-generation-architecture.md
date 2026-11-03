# Requirements: Next-Generation Architecture Behavior

## Purpose

This document holds the Behavior requirements of the
[Next-Generation Architecture](../architecture/next-generation-architecture.md)
area. The purpose, scope, and verification of the area are described in that
document.

## Scope

The scope of the area is described in
[Next-Generation Architecture](../architecture/next-generation-architecture.md#scope).
This document contains requirements of the following groups:

- Unity realization and extension
- Performance and scalability
- Reliability and validation

## Unity realization and extension

### REQ-CSWU-REAL-003: Pre-start registration

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-REAL-001
- **Verification:** EditMode test proving that missing or ambiguous
  registrations produce a configuration error before streaming starts

Required realizers and factories shall be validated before streaming starts.
Missing or ambiguous registrations shall produce a configuration error rather
than silently dropping supported content.

## Performance and scalability

### REQ-CSWU-PERF-007: Cancellation of stale work

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M2
- **Use cases:** None
- **Depends on:** REQ-CSWU-ARCH-006
- **Verification:** EditMode test of stale lifetime rejection before resource
  creation (intended fixture: `SceneCommandTests`)

Queued realization work for a deleted, replaced, or recycled scene instance
shall be cancelled or rejected before resource creation.

### REQ-CSWU-PERF-008: Instrumentation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Verification:** PlayMode test that reads each listed profiler marker and
  counter

CSWUnity shall expose profiler markers and counters for at least queue depth,
buffer wait time, decode time, main-thread apply time, build counts, stale work,
resource-cache use, loaded instances, dynamic-loader activity, and failures.

## Reliability and validation

### REQ-CSWU-QUAL-001: Structured failures

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-016
- **Verification:** EditMode failure-cleanup tests plus PlayMode map-failure
  tests checking distinct failure categories and originating command or source
  identity (intended fixtures: `SceneCommandTests`, `NativeMapTests`)

Initialization, configuration, source loading, protocol, realization, resource,
and shutdown failures shall be distinguishable and preserve their originating
command or source identity.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Next-Generation Architecture](../architecture/next-generation-architecture.md#verification).

## Related Documentation

- [Next-Generation Architecture requirements](../architecture/next-generation-architecture.md)
- [Next-generation architecture design](../../design/next-generation-architecture.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
