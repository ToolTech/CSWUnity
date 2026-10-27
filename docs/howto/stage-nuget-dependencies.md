# Stage NuGet dependencies for Unity

**Status:** Development phase 1; authoritative dependency setup

## Goal

Restore a locked CSW and GizmoSDK NuGet graph, validate its Windows x64
contents, and generate a Unity Package Manager-compatible dependency package
used by the CSWUnity example project.

The phase-1 dependency baseline is:

- `CSW.SceneManager` and native `cswSceneManager`
  `1.1.1.1-phase1.20261022.204509`, restored as a matching local package pair;
- GizmoSDK `2.12.347.1`, selected transitively by `CSW.SceneManager`;
- `GizmoDistribution` `2.12.347.1`, retained because the current CSWUnity
  implementation uses it; and
- Windows x64 Release native binaries.

Exact package versions and hashes are recorded in
`build\nuget-staging\packages.lock.json`.

## Prerequisites

- A .NET 8-compatible SDK.
- The .NET Framework 4.8 targeting pack for the locked `net48` dependency project.
- Access to the GizmoSDK package feed and the local CSW package pair below.
- Unity 6000 or later for package validation.

The repository's NuGet credentials remain user- or build-agent-owned. They are
not stored in the staging configuration.

## Restore the local phase-1 package pair

The current SceneManager output contract requires these local, unpublished
packages from the native SceneManager build:

```text
CSW.SceneManager.1.1.1.1-phase1.20261022.204509.nupkg
cswSceneManager.1.1.1.1-phase1.20261022.204509.nupkg
```

Obtain both packages from the native build owner and place them in an accessible
local NuGet feed. The assembly/file version remains `1.1.1.1`; that version alone
does not identify the new command contract. Do not fall back to the stable
`1.1.1.1` packages or mix a managed assembly, native library, bridge, or headers
from different builds. Native consumers must rebuild against the matching
headers because the `UpdateNode` layout has changed.

This pair also corrects callback registration lifetime across platform
reinitialization. Garbage collection of a previous platform lifetime must not
disconnect the next session's command output. Reliable stop/restart in one
process therefore requires this corrected pair, not just the new output types.

Before staging on a machine without the packages cached, restore the locked
graph explicitly. Replace both feed placeholders with accessible directories
or URLs:

```powershell
$cswFeed = '<local CSW package feed>'
$gizmoFeed = '<GizmoSDK package feed>'

dotnet restore build\nuget-staging\GizmoDependencies.csproj --runtime win-x64 --locked-mode `
    --source $cswFeed --source $gizmoFeed
```

Passing `--source` overrides the configured source list, so include every feed
needed by the locked graph. In particular, the SDK source must supply
`GizmoDistribution` and `gzDistribution` `2.12.347.1`: a feed prepared only for
native SceneManager consumers can omit these retained Unity dependencies.
An existing package cache can hide an incomplete source list. If one local
feed contains all 13 locked packages, a single `--source` is sufficient.

This restore populates the standard NuGet package cache used by the stager.
Then run `setup.bat` as described below. Neither the setup launcher nor the
stager has a feed-selection option; do not pass `--source` to them. No
machine-specific feed path or credentials need to be committed.

## Stage the package

From the repository root, run:

```bat
setup.bat
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

Windows users can double-click `setup.bat`. Git Bash users can run
`./setup.sh`. Both launchers stage and verify the generated package. The
current profile supports Windows x64, so `setup.sh` rejects Linux and macOS
until complete platform profiles are available.

The equivalent direct command for build agents and other hosts is:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- stage
```

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

Keep the managed/native CSW versions paired and retain the exact GizmoSDK and
`GizmoDistribution` baseline unless a coordinated SDK update is intended. Use
a new prerelease version when a local package payload changes; do not overwrite
an existing package version or replace individual generated DLLs.

## Local same-version debug builds

A producer may intentionally rebuild the same package IDs and versions during
local debugging. These bytes are not the immutable phase-1 release baseline,
even when their assembly versions are unchanged. A normal restore can reuse
older bytes from the global NuGet cache.

Keep such a debug cohort separate: freeze the complete managed/native package
closure into a private feed, use a separate dependency project with
`RestoreSources` and `RestorePackagesPath` pointing to that feed and an initially
empty private cache, and generate its own `packages.lock.json`. Do not clear or
overwrite the global cache or rewrite the accepted repository lock file.

Use the existing `--config` option with a debug staging configuration whose
dependency set points to that private project:

```powershell
dotnet run --project tools\CSWUnity.NuGetStager -- stage --config '<debug staging.json>'
dotnet run --project tools\CSWUnity.NuGetStager -- verify --config '<debug staging.json>'
```

The configuration may publish to the example's normal generated dependency
package, after saving the previous package and closing any editor using it.
Stage the whole closure and compare the generated DLL hashes with the frozen
archives; do not replace individual DLLs. The generated ownership manifest's
profile identifies the local debug cohort. Running ordinary `setup.bat` stages
the repository's accepted baseline again, not this private debug cohort.

The current same-version debug delivery is Windows x64 only. Its managed
assemblies declaring .NET Framework 4.8 under `lib\netstandard2.0` are not proof
of .NET Standard compatibility; the staging project's `net48` target does not
correct that producer packaging issue or establish support for other platforms.

## Thread-safe command handoff

The non-waiting command lane requires a later native ownership correction:
`cswCommandBuffer` and `cswSceneCommand` derive from `gzThreadSafeReference`,
not plain `gzReference`. The native input queue mutex does not protect the
caller's subsequent release against the worker's reference operations.
Without the fix, concurrent submission/consumption can lose references and
eventually fast-fail even when graph-bearing output is consumed under EDIT.

The earlier performance-validation cohort uses the private matching CSW package pair
`1.1.1.1-streaming.20261023.1` with the unchanged frozen GizmoSDK/Distribution
`2.12.347.1` closure. Both Debug and Release native core/bridge binaries are
built against matching SDK headers and import libraries. The managed assembly
and binary version remain unchanged; this is not a product release or a
package publication.

Stage this complete closure through a separate locked dependency project and
`--config`, as above. The stager's work directory must be on the same volume
as its output so that final publication can use an atomic directory move.
Do not replace individual generated DLLs, mutate the shared NuGet cache, or
infer compatibility from the unchanged assembly/file version. Native C++
consumers must rebuild with the updated CSW headers.

The repository's accepted phase-1 lock is retained as historical baseline.
Ordinary `setup.bat` restores that baseline; it does not select the newer
private performance cohort and must not be used to restage this validation
configuration.

## Shutdown-safe SDK handoff

The subsequent real-map lifecycle investigation found a separate SDK deadlock:
library shutdown joined a deallocator while retaining the global graph EDIT
lock, and that worker's loader destructor needed EDIT to release pending loads.
The SDK correction keeps ordinary and final cleanup protected, releases EDIT
around both blocking joins, and gives each existing post-join incoming-queue
cleanup its own edit guard. Call library shutdown outside any caller-owned
graph guard; the fix cannot release an outer caller's lock.

The private Windows x64 validation closure is now:

| Packages | Exact version |
| --- | --- |
| `gzGraph`, `gzSystem` | `2.12.347.2-shutdown.20261023.1` |
| `cswSceneManager`, `CSW.SceneManager` | `1.1.1.1-streaming.20261023.4` |
| Remaining SDK managed, bridge and native packages, including Distribution | `2.12.347.1` |

The CSW DLL/PDB/LIB/header payloads are byte-identical to the preceding
thread-safe command cohort. Their new package identities retarget exact
dependencies; the managed README also describes the new closure. The native
CSW package pins the new Graph/System identities, and the managed CSW package
pins the matching native CSW identity. Directly overriding Graph/System while
keeping the old exact CSW constraints produces `NU1608`; do not suppress the
warning or stage that inconsistent graph.

Keep this delivery in a separate locked `net48` dependency project, private
feed and private package cache. Require a warning-free restore and exactly
13 resolved packages for both the target framework and `win-x64` graph.
Stage and verify through that project's private `--config`, on the same
output volume as described above. The generated profile is
`shutdown-safe-streaming-windows-x64-20261023`.

The complete staged output still has 49 owned files, five managed DLLs and
15 native DLLs. Only `gzGraph64.dll` and `gzSystem64.dll` differ from the
preceding runtime cohort. Match their hashes to the tested producer output
and match all 20 DLL hashes in the rebuilt Unity player to staged files.
Debug and Release native package payloads have matching DLL/PDB/LIB inputs;
no other platform is included or validated by this private handoff.

The producer's permanent loader/shutdown cases pass in both maintained
Windows workspaces and configurations. Real-map eight-lifetime checks pass
in the injected ExampleScene and a plain host; two full 64-case PlayMode runs
also finish naturally with exit code 0. A passed result XML followed by a
debugger-induced or otherwise abnormal exit is not equivalent evidence.
The complete 118-case EditMode suite also passes with a natural exit code 0.
Neither these lifecycle results nor a smooth individual rotation run prove
continuous displayed 60 fps.

The repository's phase-1 project and lock remain the historical baseline.
Ordinary `setup.bat` does **not** select this shutdown-safe closure. Shared
SDK outputs, original package archives and the global NuGet cache must not
be overwritten to reproduce it.

## Extending the setup

Add another target only after its full CSW and GizmoSDK dependency closure is
published and its Unity importer settings have passed import and player-build
validation on the corresponding build host. Disabled targets remain visible in
the generated capability report.
