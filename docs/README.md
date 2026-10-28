# CSWUnity documentation

CSWUnity is the reusable Unity platform realization of the CSW Streaming Map
presentation capability. It is delivered as Unity components and assets that
can be installed and composed in another project that has selected Unity as
its application architecture.

The next generation of CSWUnity shall use the engine-neutral C# CSW
SceneManager as its streaming core. CSWUnity adapts the SceneManager command
and buffer model to Unity objects, resources, cameras, coordinates, and
rendering without moving Unity dependencies into CSW.

The package also provides extension points for CSW world time and environment
state, externally updated world objects, interchangeable movement models, and
optional synthetic-world rendering features.

GizmoSDK remains the foundation below CSW SceneManager. It loads and streams
GZD maps today and may support additional streamed formats in the future. GZD
maps are generated and published by the CSW World Asset Maker functionality
maintained in
[CSWMapGenerator](https://saab.ghe.com/saab/CSWMapGenerator).

The CSW architecture, the C# CSW SceneManager under development, and the C++
SceneManager used by CSWUnreal are maintained in the
[CSW](https://saab.ghe.com/saab/CSW) repository. The C++/CSWUnreal path is the
behavioral and architectural reference for the next C# realization, not a
runtime dependency of CSWUnity.

## Architecture status

- Streaming runs through the command-driven `CswMapStreamerHost`. The C# CSW
  SceneManager owns map loading, traversal and LOD selection, and CSWUnity
  realizes its ordered commands with the retained builder, pooling and optional
  shader-module patterns.
- The former Unity-owned `SceneManager` lane, its camera controllers and node
  utilities were removed in 5.0. They are recorded in
  [Removed legacy architecture](design/current-architecture.md) as migration
  input.
- Requirements describe the target architecture. Phase 1 does not yet replace
  the removed geodetic navigation and synchronous map queries.

## Documentation principles

- All documentation is written in English.
- Functionality is described from the CSWUnity user's perspective.
- Designs follow the CSW SceneManager command and buffer principles used by
  CSWUnreal, implemented through C# and Unity.
- Requirements remain implementation-independent where possible.
- Designs identify which responsibilities belong to the host Unity project,
  CSWUnity, CSW SceneManager, GizmoSDK, and other CSW components.
- Cross-repository dependencies use stable GHE links rather than local
  checkout paths.

## Documentation areas

- [Requirements](requirements/README.md)
- [Design](design/README.md)
- [How-to guides](howto/README.md)
