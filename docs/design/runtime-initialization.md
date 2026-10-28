# Runtime initialization design

**Status:** Proposed target design

## Context

Initialization bridges the Unity host lifecycle to the engine-neutral C# CSW
SceneManager and GizmoSDK. Unlike the removed legacy initialization, the target
has no single scene object that discovers other components through
`FindObjectOfType`.

## Runtime components

| Component | Responsibility |
| --- | --- |
| Runtime host | Owns lifecycle state and coordinates startup/shutdown |
| Configuration asset | Serializes map, realization, feature, resource, and performance settings |
| Platform service | Acquires and releases the GizmoSDK platform |
| SceneManager adapter | Creates C# CSW SceneManager, submits commands, and owns receiver registration |
| Command ingress | Transfers output buffers into bounded CSWUnity queues |
| Frame coordinator | Applies completed frames on the Unity main thread |
| Platform adapters | Provide optional Android or deployment-specific services |

## Startup sequence

1. The host supplies explicit component references and configuration.
2. CSWUnity validates required assemblies, native plugins, realizers, feature
   assets, map sources, and render-pipeline compatibility.
3. The platform service acquires GizmoSDK initialization.
4. CSWUnity creates the C# CSW SceneManager.
5. The command receiver is registered before manager processing starts.
6. CSWUnity starts SceneManager processing.
7. CSWUnity submits initialization, capability, loader, and camera configuration
   commands.
8. Successful initialization moves the runtime to `Running`.
9. CSWUnity submits configured map-source commands.
10. Unity begins submitting camera snapshots and frame refresh commands.

The exact command class names may evolve with the managed API. Their ordering
and completion semantics are part of the contract.

## Receiver callback

The receiver callback:

1. validates runtime generation and shutdown state;
2. acquires or copies the minimum required payload under the documented CSW
   buffer lock;
3. enqueues the payload in a bounded queue;
4. releases the lock; and
5. returns without invoking Unity APIs.

Exceptions are converted into structured diagnostics. They are not swallowed
and do not leave a buffer locked.

## Configuration

Configuration is split by ownership:

| Configuration | Owner |
| --- | --- |
| Map URLs and retry policy | Host project or map-source asset |
| Loader, LOD, and source capabilities | SceneManager configuration |
| Camera and Local 3D Frame | Camera/coordinate adapter |
| Builders and fallbacks | Realization profile |
| Mesh, texture, material, and object budgets | Resource profile |
| Shader and GPU features | Optional feature profiles |
| Main-thread time and item budgets | Performance profile |
| Logging and telemetry | Diagnostics profile |

Host-supplied runtime values override serialized defaults through a documented
precedence chain. Phase 1 resolves the map URL from the `StartStreaming`
argument, then the host's serialized `MapUrl`, then `SceneManager/MapUrl` in
`config.xml`; see
[Configure runtime initialization](../howto/configure-runtime-initialization.md#select-the-map-url).

## Shutdown sequence

1. Change state to `Stopping` and reject new host commands.
2. Stop frame refresh submission.
3. Apply the configured cancel-or-drain policy to ingress and realization
   queues.
4. Shut down optional feature modules.
5. Release realized Unity instances and shared resources.
6. Remove the CSW SceneManager receiver.
7. Request SceneManager shutdown and wait for completion within a configured
   timeout;
8. dispose SceneManager and command payload leases;
9. release platform-specific services; and
10. release the GizmoSDK platform reference and publish `Stopped`.

Timeout or release failure produces a structured shutdown failure and remains
observable to the host.

## Platform-specific setup

Android registry, network interface, multicast, and Java binding behavior is
implemented by an optional Android platform adapter. It is activated by the
host configuration and kept outside the common SceneManager and realization
assemblies.

## Related requirements

- [Runtime initialization requirements](../requirements/runtime-initialization.md)
- [Next-generation architecture requirements](../requirements/next-generation-architecture.md)
