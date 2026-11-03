# CSWUnity Implementation Status

## Purpose

This ledger records the implementation and verification progress of CSWUnity
requirements, separately from their approval status, as rule 4 in
[Status: Pending and Accepted][standard-status] requires. Accepted does not
mean realized, and a realized requirement is not thereby Accepted. The ledger
covers the Accepted requirements in
[CSW SceneManager Integration][integration]. Pending requirements are not
tracked here.

## Status Values

| Status | Meaning |
| --- | --- |
| Realized | The code meets the requirement, and the cited evidence covers it. |
| Partly realized | The code meets part of the requirement. The notes name the missing part. |
| Planned | Not realized. Work is intended within the milestone of the requirement. |
| Deferred | Not realized. Work is postponed beyond the milestone of the requirement. |
| Unverified | Code exists, but no evidence yet shows that it meets the requirement. |

## Baseline

| Item | Value |
| --- | --- |
| Date | 2026-10-29 |
| Code | Commit `b00f444` |
| Packages | `CSW.SceneManager` and `cswSceneManager` `1.1.1.1-phase1.20261022.204509`, GizmoSDK `2.12.347.1` |
| Reference revisions | [Reference revisions][integration-revisions] |

Evidence names code by type and member and tests by method. Test names
without a fixture prefix are in `CswSceneCommandTests`. The ledger records
where the evidence is, not a test run. Test runs are recorded as milestone
completion evidence in [Milestones](milestones.md).

| Type or fixture | File |
| --- | --- |
| `CswMapStreamerHost` | [CswMapStreamerHost.cs][host] |
| `MapManager` | [MapManager.cs][manager] |
| `SceneCommandInbox`, `SceneManagerSession` | [SceneCommandInbox.cs][inbox] |
| `SceneCommandPump` | [SceneCommandPump.cs][pump] |
| `UnityScene` | [UnityScene.cs][scene] |
| `SceneInstanceKey` | [SceneInstanceHandle.cs][key] |
| `SceneCoordinateContext` | [SceneCoordinateContext.cs][coordinates] |
| `CameraBridge` | [CameraBridge.cs][camera] |
| `SceneResources` | [SceneResources.cs][resources] |
| `GeometryBuilderBase` | [GeometryBuilder.cs][builder] |
| `GeometryHelper` | [NodeGeometryHelper.cs][geometry] |
| `SceneManagerSettings` | [SceneManagerSettings.cs][settings] |
| `CswSceneCommandTests` (EditMode, `MAPSTREAMER.TESTS`) | [CswSceneCommandTests.cs][command-tests] |
| `CswNativeMapTests` (PlayMode, `MAPSTREAMER.PLAYMODE.TESTS`) | [CswNativeMapTests.cs][map-tests] |
| `CswSceneResourceTests` (PlayMode, `MAPSTREAMER.PLAYMODE.TESTS`) | [CswSceneResourceTests.cs][resource-tests] |

## CSW SceneManager Integration

Of the 41 requirements, 7 are superseded and keep their rows for
traceability. Of the other 34, 21 are Realized, 10 are Partly realized, and 3
are Planned.

| ID | Status | Evidence | Notes |
| --- | --- | --- | --- |
| [REQ-CSWU-SM-007][sm-007] | Realized | `SceneCommandPump.Capture` and `SceneCommandPump.Publish` change the Unity realization only as received commands direct; review found no native scene graph traversal in the runtime. `CswNativeMapTests.RealMapRendersNavigatesLeavesScopeReturnsAndReloads` | |
| [REQ-CSWU-SM-008][sm-008] | Partly realized | [Deviations][deviations], reviewed against both reference revisions on 2026-10-29 | The differences open in REQ-CSWU-SM-011, REQ-CSWU-SM-012, REQ-CSWU-SM-021, REQ-CSWU-SM-022, REQ-CSWU-SM-030, REQ-CSWU-SM-039, and REQ-CSWU-SM-040 are not recorded deviations and remain defects until they are realized. Parity for `gzRoiNode` is Unverified (REQ-CSWU-SM-025). |
| [REQ-CSWU-SM-009][sm-009] | Realized | `SceneCommandPump.Pump` captures under `TryLockEdit(0)` and publishes after `NodeLock.UnLock`; `SceneCommandPump.CaptureNode` releases native wrappers with `ReleaseAlreadyLocked`; `SceneResources` keeps native addresses only as keys. `FrameChangesShareOneBufferLockAndPublishAfterUnlock`, `BusyGraphLockDefersTheOwnedBufferWithoutConsumingCommands` | |
| [REQ-CSWU-SM-010][sm-010] | Realized | `SceneCommandInbox.Receive` only sets the delete mode, enqueues, and signals; `UnityScene.RequireMainThread` rejects other threads. `ForeignThreadCannotRealizeUnityObjects` | |
| [REQ-CSWU-SM-011][sm-011] | Partly realized | `CswMapStreamerHost.StartStreaming` acquires `CSW.Platform` and then the Coordinate platform and calls `StopStreaming` on failure; the `SceneManagerSession` constructor creates the SceneManager, adds the receiver, queues `Initialize(false)` and `SetLoaders`, and calls `Run(true)`; `MapManager.OpenMap` submits `AddNewMap` only after the session exists. `CswNativeMapTests.RealMapRendersNavigatesLeavesScopeReturnsAndReloads`, `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` | Step 2 is missing (REQ-CSWU-SM-012). No EditMode test checks the startup commands. |
| [REQ-CSWU-SM-012][sm-012] | Planned | None | `SceneManagerSession` does not call `EnableCapabilities`. |
| [REQ-CSWU-SM-013][sm-013] | Realized | `SceneCommandInbox.Receive`, with limits of 256 buffers and 262144 commands from the `SceneCommandInbox` constructor defaults; an overflow releases the rejected buffer and records a fault that `SceneCommandPump.Pump` raises. `OverflowFaultsExplicitlyAndReleasesRejectedAndRetainedBuffers`, `InboxActivityDoesNotLoseAnArrivalBeforeWaiting`, `InboxWaitReleasesItsMonitorAndTakesNoSdkGraphLock` | |
| [REQ-CSWU-SM-014][sm-014] | Realized | `SceneCommandInbox.Receive` sets `CommandBufferDeleteMode.EDIT_LOCK`; `SceneCommandPump.Pump` releases consumed buffers under edit access; `SceneCommandInbox.DrainAlreadyLocked` and `SceneCommandPump.Dispose` release queued and partially consumed buffers under `NodeLock.WaitLockEdit`. `PartialBufferRetainsOnlyItsUnconsumedNativeCommands`, `OverflowFaultsExplicitlyAndReleasesRejectedAndRetainedBuffers`, `CallbackReleaseFailureIsContainedAndEveryOwnedBufferIsRetried` | |
| [REQ-CSWU-SM-015][sm-015] | Realized | `SceneCommandPump.Pump` and `SceneCommandPump.Capture`: `SceneCommandResponse` yields no change, and the default case throws `NotSupportedException`. `PartialBufferRetainsOnlyItsUnconsumedNativeCommands`, `HierarchyPublicationPrecedesDependentCapturesInTheSameBuffer` | The unsupported-type fault is verified by code review only. |
| [REQ-CSWU-SM-016][sm-016] | Realized | `SceneCommandPump.Pump` uses `TryLockEdit(0)`; `SceneManagerSession.Submit` and `SceneManagerSession.SubmitFrame` take no edit access; `MapManager.Tick` rejects frame synchronization while the caller holds edit access. `BusyGraphLockDefersTheOwnedBufferWithoutConsumingCommands`, `ControlSubmissionDoesNotWaitForForeignSdkEditLock`, `FrameSubmissionDoesNotWaitAndNativeWorkerReturnsItsFence` | |
| [REQ-CSWU-SM-017][sm-017] | Partly realized | `MapManager.MaximumCommandsPerTick`, `BuildBudget`, `SynchronizeFrames`, `FrameWaitTimeout`, and `FinishFrame`; `CswMapStreamerHost` sets the first four from its `MaximumCommandsPerTick` field and from `Settings`. `SynchronousDrainStopsAtItsFenceWithoutConsumingFutureUpdates`, `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick`, `CswNativeMapTests.SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates` | Superseded by REQ-CSWU-SM-045 (AD-CSWU-004), which REQ-CSWU-SM-046 (AD-CSWU-005) supersedes. The limits do not split a frame, and frame synchronization is pipelined: a synchronized update consumes the refresh that the previous update submitted, so the refresh for the camera pose of a Unity frame is consumed only in the next update. |
| [REQ-CSWU-SM-018][sm-018] | Realized | `MapManager.Tick` submits a refresh only when none is outstanding, the coordinate context is configured, and a camera pose exists; `MapManager.HandleFrame` clears the outstanding refresh. `FramesRequireMatchingInputRefreshFenceBeforePublishing`, `FrameSubmissionDoesNotWaitAndNativeWorkerReturnsItsFence` | Superseded by REQ-CSWU-SM-042 (AD-CSWU-003). |
| [REQ-CSWU-SM-019][sm-019] | Realized | `SceneCommandPump.Publish` checks `StartFrame` and `EndFrame` against the expected refresh, applies the collected transforms and then the activations, and skips changes that `IsCurrent` rejects; `UnityScene.CommitFrame`. `FrameCommitIsAtomicAndScopeGatePreservesChildLodOff`, `FramesRequireMatchingInputRefreshFenceBeforePublishing`, `DeferredFrameChangesCannotCrossOccurrenceLifetimeOrParent`, `NewBranchUnderVisibleMapWaitsForItsFrameCommit` | |
| [REQ-CSWU-SM-020][sm-020] | Realized | `SceneManagerSession.AppendFrameInput` submits `PositionCamera` before `RefreshScene` in one buffer; `CameraBridge.Sample`. `FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch`, `AxisMappingAndGlobalDoubleSubtractionHaveNumericReferenceValues`, `SubmittedProjectionSurvivesHardcodedNativeFrameFov` | |
| [REQ-CSWU-SM-021][sm-021] | Partly realized | `SceneManagerSession.AppendFrameInput` submits `CameraSettings` with the near and far distances of the Unity camera in every refresh input. `FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch` | The `CameraSettings` command accepts an infinite far distance, but `SceneManagerSession.AppendFrameInput` always submits the finite far distance, and the host has no setting to select an infinite far distance. |
| [REQ-CSWU-SM-022][sm-022] | Partly realized | `SceneManagerSession.AppendFrameInput` submits `SetOmniTraverse` and `SetLodFactor` in every refresh input; `SceneManagerSettings.Default` turns omni traversal on; `CswMapStreamerHost.Update` applies `Settings.OmniView` at runtime. `FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch`, `CameraOptionsDefaultToCswUnrealOmniWithAsynchronousFrames` | The level-of-detail factor stays at the `CameraBridge.Sample` default of 1.0; the host has no setting to change it. |
| [REQ-CSWU-SM-023][sm-023] | Realized | `SceneInstanceKey`; `SceneCommandPump.CaptureInstance` and `SceneCommandPump.CaptureNode` build keys from the native node and the full `PathID` and `ParentPathID`. `IdentityPreservesFullPathAndNativeNode`, `SingleChildParentAndLoaderMayShareTheSamePath`, `SharedNativeNodeHasIndependentOccurrencesAndParents` | |
| [REQ-CSWU-SM-024][sm-024] | Realized | `SceneCommandPump.CaptureNode` resolves the parent through `UnityScene.Parent`, which faults when the parent is missing; `UnityScene.Create` and `UnityScene.Update`. `HierarchyPublicationPrecedesDependentCapturesInTheSameBuffer`, `InvalidTransformCannotAllocateAnOrphanOrConsumeAPooledHandle` | The missing-parent fault is verified by code review only. |
| [REQ-CSWU-SM-025][sm-025] | Partly realized | `SceneCommandPump.CaptureNode` tests the node type in the fixed order `Geometry`, `DynamicLoader`, `Lod`, `Crossboard`, otherwise group, and reads the local transform of every `Transform`. `CswSceneResourceTests.ResourceCompatibilityIncludesUniformColorAndBuilderTemplate` | `gzRoiNode` is realized through the transform path. Parity with `UCSWRoiNode` is Unverified. |
| [REQ-CSWU-SM-026][sm-026] | Realized | `SceneCommandPump.CaptureNode` resolves the builder and captures mesh and state through `SceneResources.Capture` under edit access; `UnityScene.Create`, `UnityScene.Update`, and `UnityScene.PublishResources` run after `NodeLock.UnLock`. `FrameChangesShareOneBufferLockAndPublishAfterUnlock` | Meshes and textures are produced during capture; see [Current Implementation Notes](#current-implementation-notes). |
| [REQ-CSWU-SM-027][sm-027] | Realized | `UnityScene.Create` faults for a duplicate; `UnityScene.Update` faults for a missing occurrence and a cycle; `UnityScene.Delete` removes descendants first and ignores an unregistered identity; `UnityScene.SetActivation` faults for a missing occurrence. `HiddenDeleteRecyclesChildBeforeParentWithoutTouchingAnotherBranch`, `DeferredFrameChangesCannotCrossOccurrenceLifetimeOrParent` | The fault paths are verified by code review only. |
| [REQ-CSWU-SM-028][sm-028] | Partly realized | `SceneCommandPump.CaptureNode` turns a transform-only `UpdateNode` into a transform change. `CswSceneResourceTests.ScopeHideAndTransformOnlyReentryDoNotRebuildOrFreeSharedGeometry` | The GizmoSDK C# `Node` (`Gizmo3D` 2.12.347.1) does not expose the update ID that the native `gzNode` provides through `getUpdateID`, so `SceneResources.Capture` rebuilds mesh and state for every other `UpdateNode`. |
| [REQ-CSWU-SM-029][sm-029] | Realized | `UnityScene.Create` creates an occurrence under the root locally inactive; `UnityScene.SetActivation` changes only visibility. `FrameCommitIsAtomicAndScopeGatePreservesChildLodOff`, `CswSceneResourceTests.ScopeHideAndTransformOnlyReentryDoNotRebuildOrFreeSharedGeometry` | |
| [REQ-CSWU-SM-030][sm-030] | Partly realized | `SceneCommandPump.Publish` configures `SceneCoordinateContext` from `GeoInfo` before later captures; `SceneCoordinateContext.Configure` faults for a system that it cannot parse. `CoordinateContextUsesTypedWgs84ConversionAndTrueProjectedOrientation` | An empty coordinate system leaves the context unconfigured, so `MapManager.Tick` never submits a refresh for a plain geometry map. |
| [REQ-CSWU-SM-031][sm-031] | Realized | `MapManager.OpenMap` and `MapManager.Clear` dispose the session and the pump before `UnityScene.Clear` advances the generation; `SceneCommandPump.Pump` rejects a stale generation. `CswNativeMapTests.RepeatedRealMapLifetimesDrainPendingOutputBeforeReloadAndRestart`, `StaleGenerationCannotPublishAndShutdownIsIdempotent` | Superseded by REQ-CSWU-SM-039 (AD-CSWU-002). |
| [REQ-CSWU-SM-032][sm-032] | Partly realized | `SceneCommandPump.Capture` and `SceneCommandPump.Publish` handle `ErrorMessage` in buffer order; `MapManager.HandleError` faults for `FATAL` errors and for errors that reference the map request or the outstanding refresh, and logs other errors as warnings. `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` | Superseded by REQ-CSWU-SM-040 (AD-CSWU-002). The reports carry the message, and warnings the reference ID, but not the native message level. |
| [REQ-CSWU-SM-033][sm-033] | Planned | None | Milestone M3. The C# SceneManager lacks the ground-clamp and intersection commands. |
| [REQ-CSWU-SM-034][sm-034] | Realized | `SceneManagerSession.Dispose` stops accepting, calls `Shutdown` without edit access, detaches and disposes the receiver and drains the queue under `NodeLock.WaitLockEdit`, and then disposes the SceneManager; `SceneCommandPump.Dispose`; `MapManager.Clear` releases the Unity realization afterwards; `CswMapStreamerHost.StopStreaming` releases the Coordinate platform before `CSW.Platform`; before it releases them, the last host that holds the CSW platform stops the dynamic loaders with `DynamicLoaderManager.StopManager` and waits for their loads in progress. `StaleGenerationCannotPublishAndShutdownIsIdempotent`, `CallbackReleaseFailureIsContainedAndEveryOwnedBufferIsRetried`, `CswNativeMapTests.RepeatedRealMapLifetimesDrainPendingOutputBeforeReloadAndRestart`, `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` | Superseded by REQ-CSWU-SM-047 (AD-CSWU-005), which adds the loader stop that `CswMapStreamerHost.StopStreaming` performs. |
| [REQ-CSWU-SM-035][sm-035] | Realized | API review of `CswMapStreamerHost` on 2026-10-29. `CswNativeMapTests.RealMapRendersNavigatesLeavesScopeReturnsAndReloads`, `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` | |
| [REQ-CSWU-SM-036][sm-036] | Realized | `SceneManagerSession` is the only caller of `AddSingleCommand` and `AddCommandBuffer` in the runtime; `Submit` and `SubmitFrame` throw `ObjectDisposedException` after disposal; `Dispose` returns at once when the session is already disposed. `ControlSubmissionDoesNotWaitForForeignSdkEditLock`, `FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch`, `OverflowFaultsExplicitlyAndReleasesRejectedAndRetainedBuffers`, `StaleGenerationCannotPublishAndShutdownIsIdempotent` | |
| [REQ-CSWU-SM-037][sm-037] | Realized | API review of `SceneInstanceKey` on 2026-10-29. `IdentityPreservesFullPathAndNativeNode` | |
| [REQ-CSWU-SM-038][sm-038] | Realized | API review of `GeometryBuilderBase` on 2026-10-29; `SceneCommandPump.CaptureNode` calls `CanBuild` under edit access, in list order, only for geometry with a non-zero effective mask, and faults when no builder accepts. `CswSceneResourceTests.ResourceCompatibilityIncludesUniformColorAndBuilderTemplate`, `CswSceneResourceTests.ZeroEffectiveMaskRetainsStructureWithoutBuildingOrFaulting` | The no-builder fault is verified by code review only. |
| [REQ-CSWU-SM-039][sm-039] | Planned | None | `MapManager.OpenMap` stops the running session, starts a new one for each map, and submits one `AddNewMap`. The host has no map list, and the runtime submits neither `SetMapUrls` nor `ClearMaps`. |
| [REQ-CSWU-SM-040][sm-040] | Partly realized | `SceneCommandPump.Capture` and `SceneCommandPump.Publish` handle `ErrorMessage` in buffer order; `MapManager.HandleError` faults for `FATAL` errors and for errors that reference the outstanding refresh, and logs other errors as warnings with their reference ID. | `MapManager.HandleError` also faults for an error that references the map request, so a rejected map faults instead of producing a warning, and `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` asserts that fault. Fault reports carry only the message, warnings carry the message and the reference ID, and neither carries the native message level. |
| [REQ-CSWU-SM-041][sm-041] | Partly realized | `SceneManagerSettings.SynchronizeFrames`; `CswMapStreamerHost.Update` applies it to `MapManager` and `CameraBridge` in every frame; `CameraBridge.PresentPose` presents the pose and projection of the committed refresh when synchronized and the requested pose otherwise, from `CswMapStreamerHost.LateUpdate` and, when synchronized, again before the view camera renders. `SynchronizedCameraPresentsOnlyTheCommittedFrustumWithoutGraphAccess`, `CameraRotationDoesNotWaitForNativeEndFrameOrGraphAccess`, `FrameCommitDoesNotReplaceNewerLiveCameraRotation`, `ResetSynchronizedCameraReleasesItsProjectionAndOldMapPose`, `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick` | Superseded by REQ-CSWU-SM-044 (AD-CSWU-003). In unsynchronized presentation, `CameraBridge.Commit` writes the field of view and the near and far distances of the committed refresh into the borrowed camera after the next refresh has been sampled. A projection that the host sets once is therefore replaced by the projection of an older refresh and can alternate between the two on later refreshes. No test covers a projection change in unsynchronized presentation. `SceneManagerSettings.Default` selects unsynchronized presentation; REQ-CSWU-POS-003 (Pending) proposes a synchronized default. |
| [REQ-CSWU-SM-042][sm-042] | Realized | `MapManager.Tick` submits a refresh whenever none is outstanding, the coordinate context is configured, and the host passes a camera sample; it does not compare the camera with the camera of the previous refresh. In unsynchronized presentation, `CswMapStreamerHost.Update` samples the camera with `CameraBridge.Sample` and calls `MapManager.Tick`, which submits the next refresh in the same call in which `SceneCommandPump.Pump` consumes the `EndFrame` and `MapManager.HandleFrame` clears the outstanding refresh. In synchronized presentation, `CswMapStreamerHost.LateUpdate` calls `MapManager.Tick`, which first completes the refresh that the previous update submitted with `MapManager.FinishFrame` and then submits the next refresh, so it submits one refresh for each Unity frame. `SceneCommandPump.ExpectFrame` faults for a second outstanding refresh. `FramesRequireMatchingInputRefreshFenceBeforePublishing`, `FrameSubmissionDoesNotWaitAndNativeWorkerReturnsItsFence`, `CswNativeMapTests.RealMapRendersNavigatesLeavesScopeReturnsAndReloads` (32 committed refreshes with a stationary camera in unsynchronized presentation), `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick` (a synchronized update commits the refresh that the previous update submitted and submits the next one, and refreshes are committed with a stationary camera after synchronized presentation is selected again) | That a camera change does not by itself submit a refresh is verified by code review only. |
| [REQ-CSWU-SM-043][sm-043] | Partly realized | `SceneCommandPump.Pump` processes buffers and commands in arrival order and keeps the unconsumed rest of a partial buffer for the next Unity update; `SceneCommandPump.Publish` collects the transforms and activations of a frame, applies them only when it consumes `EndFrame`, and skips only the changes that `IsCurrent` rejects; `UnityScene.Create` publishes the captured resources of a new occurrence before it marks it ready, and `UnityScene.CommitFrame` makes new occurrences visible only at a frame commit. `FrameCommitIsAtomicAndScopeGatePreservesChildLodOff`, `NewBranchUnderVisibleMapWaitsForItsFrameCommit`, `PartialBufferRetainsOnlyItsUnconsumedNativeCommands` | No test checks after every Unity update that a transition that the budgets split keeps either the closing level or every replacing level visible, and no automated test checks the rendered view for holes. Temporary diagnostics that were not committed counted no hole pixels in the main view with omni traversal, also while the camera turned at 360°/s, and counted holes with frustum traversal while the camera turned. Visibility is hierarchical; see deviation 20 in [Deviations][deviations]. |
| [REQ-CSWU-SM-044][sm-044] | Partly realized | `SceneManagerSettings.SynchronizeFrames`; `CswMapStreamerHost.Update` applies it to `MapManager` and `CameraBridge` in every frame; `CameraBridge.PresentPose` presents the pose and projection of the committed refresh when synchronized and the requested pose otherwise, from `CswMapStreamerHost.LateUpdate` and, when synchronized, again before the view camera renders. In synchronized presentation, `MapManager.FinishFrame` completes a refresh when its `EndFrame` is consumed, without waiting for loading. `SynchronizedCameraPresentsOnlyTheCommittedFrustumWithoutGraphAccess`, `CameraRotationDoesNotWaitForNativeEndFrameOrGraphAccess`, `FrameCommitDoesNotReplaceNewerLiveCameraRotation`, `ResetSynchronizedCameraReleasesItsProjectionAndOldMapPose`, `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick` | In unsynchronized presentation, `CameraBridge.Commit` writes the field of view and the near and far distances of the committed refresh into the borrowed camera after the next refresh has been sampled. A projection that the host sets once is therefore replaced by the projection of an older refresh and can alternate between the two on later refreshes. No test covers a projection change in unsynchronized presentation. `SceneManagerSettings.Default` selects unsynchronized presentation; REQ-CSWU-POS-003 (Pending) proposes a synchronized default. |
| [REQ-CSWU-SM-045][sm-045] | Partly realized | `SceneCommandPump.Pump` checks the command and time limits only outside a frame and continues through the `EndFrame` of a started frame; `SceneCommandPump.CountsTowardLimit` exempts the transforms and activations of a frame from the command limit. `MapManager.Tick` and `MapManager.FinishFrame`. `FrameExceedingBothLimitsIsConsumedWholeWhileOutputOutsideFramesRespectsThem`, `SynchronousDrainStopsAtItsFenceWithoutConsumingFutureUpdates`, `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick`, `CswNativeMapTests.SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates` | Superseded by REQ-CSWU-SM-046 (AD-CSWU-005). The budgets and whole frames are realized as recorded for REQ-CSWU-SM-046. Frame synchronization is pipelined: a synchronized update consumes the refresh that the previous update submitted, so the refresh for the camera pose of a Unity frame is consumed only in the next update. |
| [REQ-CSWU-SM-046][sm-046] | Realized | `SceneCommandPump.Pump` checks the command and time limits only outside a frame, that is, before a command outside a frame and before a `StartFrame`; after it has captured a `StartFrame`, it continues through the `EndFrame` of the frame without checking them. `SceneCommandPump.CountsTowardLimit` exempts the transforms and activations of a frame from the command limit, and the time of a frame counts toward the time budget. `SceneCommandPump.Pump` leaves the unconsumed work queued, and `SceneCommandPump.Publish` applies the transforms and activations of a frame together at its `EndFrame`. `MapManager.MaximumCommandsPerTick`, `BuildBudget`, `SynchronizeFrames`, and `FrameWaitTimeout`; `CswMapStreamerHost` sets them from its `MaximumCommandsPerTick` field and from `Settings`, the timeout from `SceneManagerSettings.FrameWaitTimeout` (default 5 s) through `CswMapStreamerHost.ToFrameWaitTimeout` when it creates the `MapManager` and in every `Update`. In synchronized presentation, `CswMapStreamerHost.LateUpdate` calls `MapManager.Tick`, which runs one bounded `SceneCommandPump.Pump`, then completes the outstanding refresh with `MapManager.FinishFrame`, which repeats bounded slices and waits for SceneManager until it consumes the `EndFrame` and throws `TimeoutException` when the timeout of the update expires, and then submits the next refresh with the sampled camera; `MapManager.Fail` faults the manager and releases the session and the Unity realization. `FrameExceedingBothLimitsIsConsumedWholeWhileOutputOutsideFramesRespectsThem`, `FrameCommitIsAtomicAndScopeGatePreservesChildLodOff`, `NewBranchUnderVisibleMapWaitsForItsFrameCommit`, `SynchronousDrainStopsAtItsFenceWithoutConsumingFutureUpdates`, `PartialBufferRetainsOnlyItsUnconsumedNativeCommands`, `CameraOptionsDefaultToCswUnrealOmniWithAsynchronousFrames`, `CswNativeMapTests.RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick`, `CswNativeMapTests.SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates` | A started frame continues in a later Unity update only when its `EndFrame` has not yet arrived, or when `TryLockEdit(0)` fails after a barrier command or a buffer boundary inside the frame (REQ-CSWU-SM-016). The native `refreshScene` queues each frame as one buffer without barrier commands, so native output causes neither case. `FrameCommitIsAtomicAndScopeGatePreservesChildLodOff` and `NewBranchUnderVisibleMapWaitsForItsFrameCommit` cover an `EndFrame` that arrives in a later update; the failed edit access inside a frame is verified by code review only. `CswMapStreamerHost.ToFrameWaitTimeout` turns a timeout setting that is not positive, or that exceeds the largest millisecond wait, into zero, so the next synchronized `MapManager.Tick` faults. SceneManager processes the outstanding refresh while Unity renders, so a host-driven update seldom waits; `CswNativeMapTests.SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates` therefore forces the timeout with back-to-back `MapManager.Tick` calls and a timeout of one tick. |
| [REQ-CSWU-SM-047][sm-047] | Realized | `SceneManagerSession.Dispose` stops accepting, calls `Shutdown` without edit access, detaches and disposes the receiver and drains the queue under `NodeLock.WaitLockEdit`, and then disposes the SceneManager; `SceneCommandPump.Dispose`; `MapManager.Clear` releases the Unity realization afterwards; `CswMapStreamerHost.StopStreaming` releases the Coordinate platform before `CSW.Platform`; before it releases them, the last host that holds the CSW platform stops the dynamic loaders with `DynamicLoaderManager.StopManager(true, 0, true)`, which waits for their loads in progress, without edit access. `StaleGenerationCannotPublishAndShutdownIsIdempotent`, `CallbackReleaseFailureIsContainedAndEveryOwnedBufferIsRetried`, `CswNativeMapTests.RepeatedRealMapLifetimesDrainPendingOutputBeforeReloadAndRestart` (checks with `DynamicLoaderManager.IsManagerRunning` that the loaders run in each session and have stopped after `StopStreaming`), `CswNativeMapTests.MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain` | The loader stop is needed because `CSW.Platform.Uninitialize` of the referenced CSW release uninitializes the database managers before Gizmo3D stops the loaders, so a load in progress fails; see [Owner attention][owner-attention]. The loader manager is process-wide, so another holder of the CSW platform has no loading until a host starts a new session. The host counts only the platform references of hosts, and a failed `CSW.Platform.Uninitialize` leaves that count unchanged, so a later host can skip the loader stop. No test checks that a load was in progress. |

## Current Implementation Notes

- **Main-thread geometry export.** `SceneResources.Capture` exports geometry
  through `GeometryHelper.Build`, which calls `PrepareMeshExport` with `TRIS`
  and fills the mesh through `Mesh.AllocateWritableMeshData` on the Unity main
  thread while edit access is held. Textures are captured in the same step.
  Because `EnableCapabilities` is not called (REQ-CSWU-SM-012), primitive
  conversion and indexing also run in this step. This main-thread work under
  edit access is a candidate cause of frame stalls; it has not been measured
  separately.
- **Budgets.** The host passes `MaximumCommandsPerTick` (default 128) and
  `Settings.MaxBuildTime` (default 0.012 s) to `MapManager`, whose own defaults
  are 128 commands and 4 ms. The limits apply only outside frames, so they
  never split a frame, and the transforms and activations of a frame do not
  count toward the command limit. Every other command counts, including
  `DeleteNode`, `StartFrame`, and `EndFrame`. The time of a frame counts
  toward the time budget of its update, so a large frame can exceed the
  budget, and the next command then waits for the next update. Whether
  `DeleteNode` shall count, and whether the default limit shall be 128
  commands or the 1000 of CSWUnreal, is open; see deviation 7 in
  [Deviations][deviations]. Numeric targets remain open in
  [OD-CSWU-002](architecture/next-generation-architecture.md#open-decisions).
  In synchronized presentation, `MapManager.FinishFrame` repeats bounded
  slices until it consumes the `EndFrame` of the outstanding refresh, so the
  wait for SceneManager can make a synchronized update exceed the time budget
  up to `Settings.FrameWaitTimeout` (default 5 s).
- **Fault paths without tests.** No automated test covers a missing parent, a
  geometry that no builder accepts, a duplicate occurrence, a hierarchy cycle,
  an update or activation for an unregistered identity, or an unsupported
  command type. Code review covers these paths in `UnityScene` and
  `SceneCommandPump`.

## Related Documentation

- [CSW SceneManager Integration requirements][integration]
- [CSWUnreal SceneManager reference](../design/cswunreal-scenemanager-reference.md)
- [Milestones](milestones.md)
- [Requirements standard](standard.md)
- [Requirements index](README.md)

[standard-status]: standard.md#7-status-pending-and-accepted
[integration]: architecture/csw-scenemanager-integration.md
[integration-revisions]: architecture/csw-scenemanager-integration.md#reference-revisions
[deviations]: ../design/cswunreal-scenemanager-reference.md#deviations
[owner-attention]: ../design/cswunreal-scenemanager-reference.md#owner-attention
[host]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/CswMapStreamerHost.cs
[manager]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/MapManager.cs
[inbox]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneCommandInbox.cs
[pump]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneCommandPump.cs
[scene]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/UnityScene.cs
[key]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneInstanceHandle.cs
[coordinates]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneCoordinateContext.cs
[camera]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/CameraBridge.cs
[resources]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneResources.cs
[builder]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/GeometryBuilder.cs
[geometry]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/NodeGeometryHelper.cs
[settings]: ../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/SceneManagerSettings.cs
[command-tests]: ../../com.saab.map-streamer/Tests/Runtime/CswSceneCommandTests.cs
[map-tests]: ../../com.saab.map-streamer/Tests/Runtime/PlayMode/CswNativeMapTests.cs
[resource-tests]: ../../com.saab.map-streamer/Tests/Runtime/PlayMode/CswSceneResourceTests.cs
[sm-007]: architecture/csw-scenemanager-integration.md#req-cswu-sm-007-scenemanager-authority
[sm-008]: architecture/csw-scenemanager-integration.md#req-cswu-sm-008-cswunreal-parity
[sm-009]: architecture/csw-scenemanager-integration.md#req-cswu-sm-009-edit-access-discipline
[sm-010]: architecture/csw-scenemanager-integration.md#req-cswu-sm-010-scenemanager-threads
[sm-011]: architecture/csw-scenemanager-integration.md#req-cswu-sm-011-session-startup-order
[sm-012]: architecture/csw-scenemanager-integration.md#req-cswu-sm-012-geometry-capabilities
[sm-013]: architecture/csw-scenemanager-integration.md#req-cswu-sm-013-receiver-handoff
[sm-014]: architecture/csw-scenemanager-integration.md#req-cswu-sm-014-buffer-release
[sm-015]: architecture/csw-scenemanager-integration.md#req-cswu-sm-015-ordered-typed-processing
[sm-016]: architecture/csw-scenemanager-integration.md#req-cswu-sm-016-non-blocking-edit-access-during-updates
[sm-017]: architecture/csw-scenemanager-integration.md#req-cswu-sm-017-per-update-budgets-and-frame-synchronization
[sm-018]: architecture/csw-scenemanager-integration.md#req-cswu-sm-018-demand-driven-refresh
[sm-019]: architecture/csw-scenemanager-integration.md#req-cswu-sm-019-frame-consistent-presentation
[sm-020]: architecture/csw-scenemanager-integration.md#req-cswu-sm-020-camera-pose-before-refresh
[sm-021]: architecture/csw-scenemanager-integration.md#req-cswu-sm-021-camera-traversal-settings
[sm-022]: architecture/csw-scenemanager-integration.md#req-cswu-sm-022-streaming-settings
[sm-023]: architecture/csw-scenemanager-integration.md#req-cswu-sm-023-occurrence-identity
[sm-024]: architecture/csw-scenemanager-integration.md#req-cswu-sm-024-parent-resolution
[sm-025]: architecture/csw-scenemanager-integration.md#req-cswu-sm-025-realizer-selection
[sm-026]: architecture/csw-scenemanager-integration.md#req-cswu-sm-026-capture-under-edit-access
[sm-027]: architecture/csw-scenemanager-integration.md#req-cswu-sm-027-occurrence-lifecycle
[sm-028]: architecture/csw-scenemanager-integration.md#req-cswu-sm-028-redundant-rebuild-avoidance
[sm-029]: architecture/csw-scenemanager-integration.md#req-cswu-sm-029-activation-without-rebuild
[sm-030]: architecture/csw-scenemanager-integration.md#req-cswu-sm-030-geoinfo-coordinate-context
[sm-031]: architecture/csw-scenemanager-integration.md#req-cswu-sm-031-map-change-and-clear
[sm-032]: architecture/csw-scenemanager-integration.md#req-cswu-sm-032-error-buffers
[sm-033]: architecture/csw-scenemanager-integration.md#req-cswu-sm-033-correlated-asynchronous-requests
[sm-034]: architecture/csw-scenemanager-integration.md#req-cswu-sm-034-session-shutdown-order
[sm-035]: architecture/csw-scenemanager-integration.md#req-cswu-sm-035-cswmapstreamerhost-component
[sm-036]: architecture/csw-scenemanager-integration.md#req-cswu-sm-036-scenemanagersession-owner
[sm-037]: architecture/csw-scenemanager-integration.md#req-cswu-sm-037-sceneinstancekey-identity
[sm-038]: architecture/csw-scenemanager-integration.md#req-cswu-sm-038-geometrybuilderbase-extension-point
[sm-039]: architecture/csw-scenemanager-integration.md#req-cswu-sm-039-map-list
[sm-040]: architecture/csw-scenemanager-integration.md#req-cswu-sm-040-error-buffers
[sm-041]: architecture/csw-scenemanager-integration.md#req-cswu-sm-041-camera-synchronization-modes
[sm-042]: architecture/csw-scenemanager-integration.md#req-cswu-sm-042-continuous-refresh
[sm-043]: architecture/csw-scenemanager-integration.md#req-cswu-sm-043-hole-free-level-of-detail-transitions
[sm-044]: architecture/csw-scenemanager-integration.md#req-cswu-sm-044-camera-synchronization-modes
[sm-045]: architecture/csw-scenemanager-integration.md#req-cswu-sm-045-per-update-budgets-whole-frames-and-frame-synchronization
[sm-046]: architecture/csw-scenemanager-integration.md#req-cswu-sm-046-per-update-budgets-whole-frames-and-pipelined-frame-synchronization
[sm-047]: architecture/csw-scenemanager-integration.md#req-cswu-sm-047-session-shutdown-order
