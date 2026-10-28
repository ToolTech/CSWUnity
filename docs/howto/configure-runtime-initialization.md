# Configure runtime initialization

**Applies to:** Current implementation (`CswMapStreamerHost`)

## Goal

Configure a Unity scene so CSWUnity initializes GizmoSDK, resolves the map URL
and starts command-driven Streaming Map loading.

## Prerequisites

- The CSWUnity package and its GizmoSDK, Coordinate and CSW SceneManager
  dependencies are installed as described in
  [Stage NuGet dependencies](stage-nuget-dependencies.md).
- The scene contains the Unity camera that presents the map.
- A Streaming Map URL is available for the host's **Map Url** or in
  `config.xml`.

## Configure the scene

1. Create or select the GameObject that owns the Streaming Map runtime.
2. Add the `Saab.Unity.Initializer.Initializer` component.
3. Add `Saab.Foundation.Unity.MapStreamer.Csw.CswMapStreamerHost`.
4. Assign the host's `ViewCamera` and at least one geometry builder to
   `Builders`.
5. Set **Map Url**, or leave it empty to use `SceneManager/MapUrl` from
   `config.xml`.
6. Provide `config.xml` at the local registry location resolved by GizmoSDK if
   the application uses it.
7. Enter Play mode.

The initializer's `Awake` initializes GizmoBase, routes GizmoSDK messages to the
Unity console, installs the GizmoSDK monitor and selects `config.xml` as the
local registry. All `Awake` calls in the scene complete before the host's
`Start`, which initializes the CSW and Coordinate platforms, resolves the map
URL and opens the map in a native CSW SceneManager session. See
[Integrate CSWUnity into a host project](integrate-into-host-project.md) for the
complete composition and lifecycle.

## Select the map URL

The host uses the first non-blank value of:

1. the URL passed to `host.StartStreaming(url)`;
2. the host's **Map Url**; and
3. the GizmoSDK user key `SceneManager/MapUrl` in `config.xml`.

A **Map Url** entered in the component therefore takes precedence over
`config.xml`; clear it to use the configured URL. The configured URL is read
even when **Read Configuration** is disabled. That option only controls the
other `config.xml` overrides, currently `SceneManager/DynamicLoaders`.

## Verify initialization

- The Unity console contains GizmoSDK startup messages.
- `host.Manager.State` reaches `Running`.
- The configured map begins loading.
- No host startup exception or `CSW Map Streamer:` error is reported.

## Android

The initializer configures the default registry in the application's data
directory and acquires a Wi-Fi multicast lock. The application must have the
Android network permissions required by its deployment environment.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| The map does not load | Verify the host's **Map Url**, or clear it and verify `SceneManager/MapUrl` in `config.xml` |
| A different map loads | A non-empty **Map Url** takes precedence over `config.xml` |
| Startup reports `No map URL` | Set **Map Url** or `SceneManager/MapUrl` in `config.xml` |
| The host disables itself at startup | Read the logged exception; `ViewCamera` and at least one builder are required |
| Geometry is missing | Verify the entries in the host's `Builders` |
| GizmoSDK configuration is ignored | Verify that `config.xml` is available at the local registry location |
| Android discovery fails | Verify Wi-Fi state, permissions, multicast support, and the `wlan0` monitor configuration |

## Related documentation

- [Integrate CSWUnity into a host project](integrate-into-host-project.md)
- [Target runtime initialization](../design/runtime-initialization.md)
