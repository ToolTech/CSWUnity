# Design

This directory contains the functional design of CSWUnity features.

## Designs

- [Removed legacy architecture](current-architecture.md)
- [Next-generation architecture](next-generation-architecture.md)
- [Scene control and threaded realization](scene-control-and-threading.md)
- [Synthetic world](synthetic-world.md)
- [External object integration](object-integration.md)
- [Movement models](movement-models.md)
- [Rendering](rendering.md)
- [Streaming Map pipeline](streaming-map-pipeline.md)
- [Runtime initialization](runtime-initialization.md)

## Authoring designs

Designs translate approved requirements into Unity functionality within CSW,
built on the C# CSW SceneManager and GizmoSDK. Each design must make the
responsibilities and boundaries of the host project, Unity, CSWUnity, CSW
SceneManager, GizmoSDK, and surrounding CSW components explicit.

Each design document should describe:

- context and goals
- related requirements
- proposed solution
- Unity components and lifecycle
- GizmoSDK C# and CSW integration
- components, interfaces, and responsibilities
- data and control flow
- error handling and diagnostics
- performance and compatibility considerations
- alternatives and trade-offs
- risks and open questions
