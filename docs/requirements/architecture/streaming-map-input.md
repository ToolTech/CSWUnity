# Requirements: Streaming Map Input Architecture

## Purpose

This document holds the Principle requirements of the
[Streaming Map Input](../functional/streaming-map-input.md) area. The purpose,
scope, and verification of the area are described in that document.

## Scope

The scope of the area is described in
[Streaming Map Input](../functional/streaming-map-input.md#scope).

## Requirements

### REQ-CSWU-MAP-002: Format ownership

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-012
- **Verification:** Design and code review proving that CSWUnity contains no GZD
  parser

GizmoSDK and CSW SceneManager shall own source-format resolution, parsing,
native scene traversal, and dynamic loading. CSWUnity shall not implement a
separate GZD parser.

### REQ-CSWU-MAP-003: Format-neutral Unity boundary

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-012
- **Verification:** Test format provider that emits the common scene contract
  without changes to Unity builders

The scene-change contract consumed by CSWUnity shall describe common scene,
resource, coordinate, and lifecycle semantics without exposing GZD-specific
types to Unity realizers.

A future streamed format shall be addable below this boundary without
rewriting the CSWUnity frame scheduler or Unity object lifecycle.

### REQ-CSWU-MAP-010: Version compatibility

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Release review of the validated version combination

Each supported release shall identify a validated combination of Unity,
CSWUnity, C# CSW SceneManager, GizmoSDK, and CSWMapGenerator versions.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Streaming Map Input](../functional/streaming-map-input.md#verification).

## Related Documentation

- [Streaming Map Input requirements](../functional/streaming-map-input.md)
- [Streaming Map pipeline](../../design/streaming-map-pipeline.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
- [CSW architecture and SceneManager](https://saab.ghe.com/saab/CSW)
- [CSWMapGenerator](https://saab.ghe.com/saab/CSWMapGenerator)
