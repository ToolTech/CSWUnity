# CSWUnity

CSWUnity is the Unity realization of Saab's streaming-map capabilities. It
combines Unity runtime components with the managed CSW SceneManager and the
managed/native GizmoSDK dependency graph.

The repository contains:

- the reusable `com.saab.map-streamer` Unity package;
- a Unity example and validation project;
- locked CSW/GizmoSDK dependency staging;
- runtime and editor tests;
- architecture, requirements, and integration documentation; and
- CDW installation guides.

## Setup baseline

The current Windows setup baseline is:

| Component | Version or target |
| --- | --- |
| Unity project | `6000.3.5f2` |
| CSW managed package | `CSW.SceneManager` `1.1.1.1` |
| GizmoSDK packages | `2.12.347.1` |
| Native target | Windows x64 Release |
| Staging tool | .NET 8-compatible SDK |

Exact NuGet versions and content hashes are committed in
[`build/nuget-staging/packages.lock.json`](build/nuget-staging/packages.lock.json).

## Quick start

1. Clone the repository.
2. Open PowerShell in the repository root.
3. Stage the locked CSW and GizmoSDK dependencies:

   ```powershell
   dotnet run --project tools\CSWUnity.NuGetStager -- stage
   ```

4. Verify the generated Unity package:

   ```powershell
   dotnet run --project tools\CSWUnity.NuGetStager -- verify
   ```

5. Add `projects\com.saab.map-streamer` to Unity Hub.
6. Open the project with the Unity version specified in
   `ProjectSettings\ProjectVersion.txt`.
7. Open `Assets\Scenes\ExampleScene.unity` and enter Play mode.

The example project already references the generated local package:

```text
artifacts\unity-packages\com.saab.map-streamer.dependencies
```

Run the staging command before opening Unity after a fresh clone or after
changing dependency versions.

## Dependency staging

`tools\CSWUnity.NuGetStager` is the only supported dependency-installation
path. It replaces the previous Visual Studio installer and DLL-copy scripts.

The stager:

- restores an exact locked NuGet graph;
- lets NuGet resolve the transitive package closure;
- materializes assets for each enabled runtime identifier;
- validates required managed assemblies and native libraries;
- verifies native processor architecture;
- places assets in a generated UPM package;
- generates deterministic Unity `PluginImporter` metadata;
- records installed, disabled, and unavailable platforms; and
- owns and verifies every generated file by SHA-256.

The generated package currently contains:

### Managed assemblies

- `SceneManager.dll`
- `GizmoBase.dll`
- `Gizmo3D.dll`
- `GizmoDistribution.dll`
- `Coordinate.dll`

### Windows x64 native libraries

- CSW SceneManager and bridge libraries;
- GizmoSDK Base, Graph, Image, Database, System, Distribution, and Coordinate
  libraries; and
- the corresponding native bridge libraries.

To remove only tool-owned generated output:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- clean
```

The tool refuses to replace or clean an output containing modified or unowned
files.

## Platform profiles

Platform selection is declared in
[`build/nuget-staging/staging.json`](build/nuget-staging/staging.json).

| Platform | Current status | Reason |
| --- | --- | --- |
| Windows x64 | Installed and verified | Complete CSW and GizmoSDK NuGet closure |
| Android ARMv7 | Disabled | Complete CSW native closure is not published |
| Android ARM64 | Disabled | Complete CSW native closure is not published |
| Linux x64 | Disabled | GizmoSDK package family and complete CSW closure must be selected |
| Linux ARM64 | Disabled | GizmoSDK package family and complete CSW closure must be selected |
| macOS ARM64 | Disabled | Current NuGet coverage is incomplete |
| iOS ARM64 | Disabled | No complete NuGet/static-link consumption model is published |

An enabled required platform fails staging if any package or native asset is
missing. Missing dependencies are never silently skipped.

The effective supported set is the intersection of:

1. platforms enabled by the staging profile;
2. complete CSW package assets;
3. complete GizmoSDK package assets; and
4. Unity-supported build targets.

The generated `supported-platforms.json` records the result for each build.

## Validation

Run the staging-tool tests with:

```powershell
dotnet test tools\CSWUnity.NuGetStager.Tests\CSWUnity.NuGetStager.Tests.csproj --configuration Release
```

The Windows baseline has also been validated by:

- importing the generated package in Unity on Windows x64;
- confirming that Unity recognizes all 15 native plugins; and
- successfully initializing and uninitializing `GizmoBase`, `Gizmo3D`,
  `GizmoDistribution`, `Coordinate`, and `CSW.Platform`.

## Repository layout

```text
build\nuget-staging\                 Locked dependency project and profile
com.saab.map-streamer\               Reusable Unity package
docs\                                Requirements, designs, and how-to guides
projects\com.saab.map-streamer\      Unity example and validation project
tools\CSWUnity.NuGetStager\          NuGet-to-UPM staging tool
tools\CSWUnity.NuGetStager.Tests\    Staging-tool tests
tutorial\howto\                      CDW installation and demo guides
artifacts\                            Generated local output; not committed
```

## Documentation

- [Documentation index](docs/README.md)
- [Stage NuGet dependencies for Unity](docs/howto/stage-nuget-dependencies.md)
- [Integrate CSWUnity into a host project](docs/howto/integrate-into-host-project.md)
- [Current architecture](docs/design/current-architecture.md)
- [Next-generation architecture](docs/design/next-generation-architecture.md)
- [CDW installation](tutorial/howto/CDW_Installation.md)
- [CDW Map Streamer demo](tutorial/howto/CDW_map_streamer_DEMO.md)

## Demo controls

| Key | Action |
| --- | --- |
| `W`, `A`, `S`, `D` | Move |
| `Space`, `Ctrl` | Move vertically |
| Arrow keys | Rotate |
| `Shift` | Increase movement speed |

## Licensing and contact

The open source code and documentation in this repository are provided under
the repository's applicable LGPL terms. Saab-provided native binaries have
separate licensing terms.

For commercial use and licensing questions, contact:
`anders.moden@saabgroup.com`.
