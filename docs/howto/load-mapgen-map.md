# Load a MapGen GZD map

**Applies to:** Map generation and the current CSWUnity runtime

The MapGen generation and publication steps remain valid for the target
architecture. The Unity configuration steps use the command-driven
`CswMapStreamerHost`.

## Goal

Generate and publish a GZD map with CSWMapGenerator, then load it in a Unity
scene through CSWUnity.

## Prerequisites

- Access to [CSWMapGenerator](https://saab.ghe.com/saab/CSWMapGenerator).
- A MapGen installation with a connected server and at least one SceneBuilder
  worker.
- Source datasets accepted by MapGen.
- A CSWUnity scene configured according to
  [Configure runtime initialization](configure-runtime-initialization.md).
- Compatible CSWMapGenerator, GizmoSDK, CSWUnity, and Unity versions.

## Generate and publish the map

1. In MapGen, upload and prepare the required source datasets.
2. Create a project and select the datasets.
3. Define the build extent, coordinate system, and required output settings.
4. Start the build and wait until the result appears under **Generated maps**.
5. Preview the generated map in MapEditor when the output type supports it.
6. Publish the generated GZD map to a local path or server destination
   accessible to the target CSWUnity runtime.

The detailed MapGen workflow is maintained in the
[CSWMapGenerator documentation](https://saab.ghe.com/saab/CSWMapGenerator/tree/main/docs).

## Configure CSWUnity

1. Copy the published map URL.
2. Set the URL in the host's **Map Url** field in the Unity Inspector, or leave
   the field empty and set the GizmoSDK user key `SceneManager/MapUrl` in
   `config.xml`. A non-empty **Map Url** takes precedence.
3. Verify that the host's `Builders` contain builders for the node types
   required by the map.
4. Enter Play mode.

The host resolves the URL in `Start` and opens it in a native CSW SceneManager
session. GizmoSDK loads and streams the GZD scene graph below CSW SceneManager,
which sends ordered commands that CSWUnity realizes as Unity objects.

## Verify the result

- The map loads without a `CSW Map Streamer:` error, and `host.Manager.State`
  reaches `Running`.
- The expected terrain, geometry, textures, and asset instances appear.
- Camera movement updates streamed content and levels of detail.
- Geospatial position and orientation match the generated map.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| The URL cannot be opened | Verify publication, permissions, connectivity, and the required GizmoSDK URL adapter |
| The native session reports a map load error | Verify the CSWMapGenerator and GizmoSDK version combination |
| Some content is absent | Verify MapGen mappings and the configured CSWUnity node builders |
| Position or orientation is incorrect | Verify the MapGen coordinate-system settings and CSWUnity map origin |
| A different map loads | A non-empty **Map Url** on the host takes precedence over `SceneManager/MapUrl` in `config.xml` |

## Related documentation

- [Streaming Map input requirements](../requirements/streaming-map-input.md)
- [Streaming Map pipeline design](../design/streaming-map-pipeline.md)
- [Integrate CSWUnity into a host project](integrate-into-host-project.md)
