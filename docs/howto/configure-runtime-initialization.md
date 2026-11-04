# Configure runtime initialization

**Applies to:** Current implementation (`MapControl`)

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
2. Add the `CSW.Unity.PlatformComponent` component and set its **Settings**.
3. Add `CSW.Unity.MapControl`.
4. Assign the host's `ViewCamera` and at least one geometry builder to
   `Builders`.
5. Set **Map Url**, or leave it empty to use `SceneManager/MapUrl` from
   `config.xml`.
6. Provide `config.xml` at the local registry location resolved by GizmoSDK if
   the application uses it.
7. Enter Play mode.

The component's `OnEnable` calls `CSW.Unity.Platform.Start` with its settings,
which initializes GizmoBase, routes GizmoSDK messages to the Unity console, sets
the message level, installs the GizmoSDK monitor and selects `config.xml` as the
local registry. All `OnEnable` calls in the scene complete before the host's
`Start`, which takes a platform reference with `CSW.Unity.Platform.Acquire`,
resolves the map URL and opens the map in a native CSW SceneManager session.
The first reference initializes the CSW and Coordinate platforms. See
[Integrate CSWUnity into a host project](integrate-into-host-project.md) for the
complete composition and lifecycle.

## Start the platform from code

An application that does not use the component controls the platform through
the static class `CSW.Unity.Platform`:

```csharp
CSW.Unity.Platform.Start(new CSW.Unity.PlatformSettings { LocalRegistry = "config.xml" });
// Call CSW.Unity.Platform.Update() once per Unity update for monitoring.
CSW.Unity.Platform.Stop();
```

A second `Start` is a no-op that returns `false` and keeps the first settings.
A session takes a reference with `Acquire` and disposes it when it stops; if no
caller has started the platform, `Acquire` starts it with default settings.
`Stop` releases the platform services immediately, or when the last reference
is disposed. The component stops only a platform that it started. GizmoBase
stays initialized until the process ends, because GizmoSDK does not register
its message callback again once GizmoBase has been uninitialized.

## Select the map URL

The host uses the first non-blank value of:

1. the URL passed to `control.StartStreaming(url)`;
2. the host's **Map Url**; and
3. the GizmoSDK user key `SceneManager/MapUrl` in `config.xml`.

A **Map Url** entered in the component therefore takes precedence over
`config.xml`; clear it to use the configured URL. The configured URL is read
even when **Read Configuration** is disabled. That option only controls the
other `config.xml` overrides, currently `SceneManager/DynamicLoaders`.

The URL may contain GizmoSDK variables, which GizmoSDK expands when it loads
the map: `%NAME%` is replaced by the environment variable `NAME`, and `$(key)`
by the GizmoSDK registry key `key`. For example,
`<MapUrl>%CSWUNITY_TEST_MAP%</MapUrl>` loads the map that the environment
variable `CSWUNITY_TEST_MAP` names, so each machine can select its own map
without a change of `config.xml`.

## Verify initialization

- The Unity console contains GizmoSDK messages. GizmoSDK only sends messages
  at or above **Message Level** (default `PERF_DEBUG`); raise it to `NOTICE` or
  `WARNING` to reduce the output. The level also filters CSWUnity's own
  diagnostics, which are sent as GizmoSDK messages from `CSW.Unity`.
- `MapControl.State` reaches `Running`.
- The configured map begins loading.
- No host startup exception or `CSW MapControl:` error is reported.

## Android

The platform configures the default registry in the application's data
directory and acquires a Wi-Fi multicast lock unless **Android Default Registry** or
**Android Multicast Lock** is cleared. The application must have the
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
| GizmoSDK messages are missing | Lower **Message Level**; GizmoSDK filters messages below it before they reach Unity |
| Android discovery fails | Verify Wi-Fi state, permissions, multicast support, and the `wlan0` monitor configuration |

## Related documentation

- [Integrate CSWUnity into a host project](integrate-into-host-project.md)
- [Target runtime initialization](../design/runtime-initialization.md)
