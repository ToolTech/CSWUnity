# Stage NuGet dependencies for Unity

**Status:** Development phase 1; authoritative dependency setup

## Goal

Restore a locked CSW and GizmoSDK NuGet graph, validate its Windows x64
contents, and generate a Unity Package Manager-compatible dependency package
used by the CSWUnity example project.

The phase-1 dependency baseline is:

- `CSW.SceneManager` `1.1.1.1`;
- GizmoSDK `2.12.347.1`, selected transitively by `CSW.SceneManager`;
- `GizmoDistribution` `2.12.347.1`, retained because the current CSWUnity
  implementation uses it; and
- Windows x64 Release native binaries.

Exact package versions and hashes are recorded in
`build\nuget-staging\packages.lock.json`.

## Prerequisites

- A .NET 8-compatible SDK.
- Access to the configured Saab NuGet feed.
- Unity 6000 or later for package validation.

The repository's NuGet credentials remain user- or build-agent-owned. They are
not stored in the staging configuration.

## Stage the package

From the repository root, run:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- stage
```

The command:

1. restores the locked dependency graph for `win-x64`;
2. materializes package assets through an SDK-style dependency project;
3. requires the CSW package's `Platform=x64` contract;
4. distinguishes managed and native PE files;
5. verifies every required managed assembly and native library;
6. verifies that native binaries use the AMD64 machine type;
7. generates deterministic Unity `.meta` files;
8. writes a platform capability report; and
9. publishes the generated package to:

```text
artifacts\unity-packages\com.saab.map-streamer.dependencies
```

Staging uses a temporary directory below `artifacts\nuget-staging`. The final
package is replaced only after the new output passes verification.

## Verify or clean

Verify every owned file and its SHA-256 hash:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- verify
```

Remove only the generated package recorded by its ownership manifest:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- clean
```

The tool refuses to replace or clean an output directory containing unowned or
modified files.

## Install in a Unity project

Add the generated package as a local UPM dependency:

```json
{
  "dependencies": {
    "com.saab.map-streamer.dependencies": "file:<path-to-repository>/artifacts/unity-packages/com.saab.map-streamer.dependencies"
  }
}
```

The repository no longer contains a second plugin installation path. Rerun the
stager whenever the locked dependency graph changes.

Phase 1 has been validated by importing the generated package in Unity
6000.3.26f1 on Windows x64 and successfully initializing and uninitializing:

- `GizmoSDK.GizmoBase.Platform`;
- `GizmoSDK.Gizmo3D.Platform`;
- `GizmoSDK.GizmoDistribution.Platform`;
- `GizmoSDK.Coordinate.Platform`; and
- `CSW.Platform`.

## Configure included platforms

`build\nuget-staging\staging.json` separates requested platforms from available
package assets. Each platform has:

- `enabled`: include the target in the current profile;
- `required`: fail staging when the enabled target is incomplete;
- `runtimeIdentifier`: NuGet RID used for restore;
- `dependencySet`: top-level dependency project;
- Unity plugin destination and importer settings; and
- required native libraries.

An enabled required target fails explicitly when any dependency is unavailable.
A disabled target is listed in `supported-platforms.json` as not included and
does not run restore.

The effective platform support is the intersection of:

1. platforms enabled by the profile;
2. complete CSW package assets;
3. complete GizmoSDK package assets; and
4. Unity-supported targets.

The current profile enables only Windows x64. Android, Linux, macOS, and iOS
remain visible but disabled until their complete dependency sets and Unity
import rules are implemented and verified.

## Update package versions

1. Change the exact versions in
   `build\nuget-staging\GizmoDependencies.csproj`.
2. Regenerate the lock file:

   ```powershell
   dotnet restore build\nuget-staging\GizmoDependencies.csproj --runtime win-x64 --force-evaluate
   ```

3. Review `packages.lock.json` and confirm one compatible CSW/GizmoSDK graph.
4. Run the stager tests.
5. Stage and verify the Unity package.
6. Run a Unity import and platform-initialization smoke test before declaring a
   new dependency baseline.

## Extending the setup

Add another target only after its full CSW and GizmoSDK dependency closure is
published and its Unity importer settings have passed import and player-build
validation on the corresponding build host. Disabled targets remain visible in
the generated capability report.
