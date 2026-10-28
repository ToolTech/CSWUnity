# How-to guides

This directory contains task-oriented guides for developers and users of
CSWUnity.

## Guides

- [Integrate CSWUnity into a host project](integrate-into-host-project.md)
- [Stage NuGet dependencies for Unity](stage-nuget-dependencies.md)
- [Configure runtime initialization](configure-runtime-initialization.md)
- [Load a MapGen GZD map](load-mapgen-map.md)

The guides describe the command-driven `CswMapStreamerHost`, which is the
package's only streaming lane. The former Unity-owned `SceneManager` lane was
removed in 5.0.

## Authoring guides

Guides should explain how to configure, integrate, operate, or troubleshoot
CSWUnity functionality in Unity and the surrounding CSW environment through
the C# CSW SceneManager and GizmoSDK.

Each guide should include:

- goal and prerequisites
- applicable Unity, GizmoSDK, and CSW versions
- step-by-step instructions
- expected result
- troubleshooting information
- related requirements and design documents
