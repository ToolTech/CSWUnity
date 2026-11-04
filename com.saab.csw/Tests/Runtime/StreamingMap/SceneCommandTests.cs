//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCommandTests.cs
// Description	: Native command ownership and camera-relative realization tests
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Command ownership, frame and hierarchy regressions	(2.12.327)
// AMO	261023	Covered foreign SDK lock contention, frame input batches and unlocked slice publication.	(2.12.327)
// AMO	261023	Covered live pose, two-ROI reference continuity and map reset during foreign SDK ownership.	(2.12.327)
// AMO	261026	Covered Omni input, notification handoff, fence-bounded draining and synchronized camera projection.	(2.12.327)
// AMO	261028	Covered CSWUnreal-compatible Omni defaults.	(2.12.327)
// AMO	261028	Removed the legacy NodeHandle check.	(2.12.327)
// AMO	261030	Covered whole frames that exceed both per-update limits.	(2.12.327)
// AMO	261030	Covered the synchronized frame wait timeout default and setting conversion.	(2.12.327)
// AMO	261103	Covered capture wrapper release under edit access with a finalizer pass during held edit access.	(2.12.327)
// AMO	261103	Asserted committed visibility of active occurrences and covered visibility after reparenting.	(2.12.327)
// AMO	261103	Covered native finite/infinite projections, zoom-scaled LOD factors and presentation without frustum write-back.	(2.12.327)
// AMO	261103	Renamed from CswSceneCommandTests into the Csw test namespace.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.Tests.	(2.12.327)
// AMO	261104	Moved the axis mapping test to ConversionTests.	(2.12.327)
// AMO	261104	Uses CSW.Unity.Camera and CameraFrame.	(2.12.327)
// AMO	261104	Uses CSW.Unity.MapControl and the internal MapSession.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;
using System;
using System.Collections.Generic;
using ManualResetEventSlim = System.Threading.ManualResetEventSlim;
using Stopwatch = System.Diagnostics.Stopwatch;
using Thread = System.Threading.Thread;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using NUnit.Framework;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Object = UnityEngine.Object;

namespace CSW.Unity.Tests
{
	public sealed class SceneCommandTests
	{
		private GameObject _parent;
		private GameObject _cameraObject;
		private SceneCoordinateContext _coordinates;
		private UnityScene _scene;
		private SceneCommandInbox _inbox;
		private SceneCommandPump _pump;
		private readonly List<Node> _nodes = new List<Node>();
		private readonly List<State> _states = new List<State>();

		[OneTimeSetUp]
		public void InitializeNative()
		{
			Assert.That(CSW.Platform.Initialize(), Is.True);
			Assert.That(GizmoSDK.Coordinate.Platform.Initialize(), Is.True);
		}

		[OneTimeTearDown]
		public void UninitializeNative()
		{
			Assert.That(GizmoSDK.Coordinate.Platform.Uninitialize(), Is.True);
			Assert.That(CSW.Platform.Uninitialize(), Is.True);
		}

		[SetUp]
		public void SetUp()
		{
			_parent = new GameObject("Host-owned parent");
			_cameraObject = new GameObject("Host-owned camera");
			var camera = _cameraObject.AddComponent<UnityEngine.Camera>();
			_coordinates = new SceneCoordinateContext();
			_scene = new UnityScene(_parent.transform, camera, _coordinates);
			_inbox = new SceneCommandInbox();
			_pump = new SceneCommandPump(_scene, _inbox, Array.Empty<GeometryBuilderBase>());
		}

		[TearDown]
		public void TearDown()
		{
			_pump.Dispose();
			_scene.Dispose();
			_coordinates.Dispose();
			NodeLock.WaitLockEdit();
			try
			{
				foreach (var state in _states)
					state.ReleaseAlreadyLocked();
				foreach (var node in _nodes)
					node.ReleaseAlreadyLocked();
				_states.Clear();
				_nodes.Clear();
			}
			finally
			{
				NodeLock.UnLock();
			}
			Object.DestroyImmediate(_cameraObject);
			Object.DestroyImmediate(_parent);
		}

		[Test]
		public void IdentityPreservesFullPathAndNativeNode()
		{
			var first = new SceneInstanceKey(new IntPtr(1), ulong.MaxValue);
			var second = new SceneInstanceKey(new IntPtr(2), ulong.MaxValue);
			var third = new SceneInstanceKey(new IntPtr(1), long.MaxValue);
			var dictionary = new Dictionary<SceneInstanceKey, int>
			{
				[first] = 1, [second] = 2, [third] = 3
			};
			Assert.That(first.PathId, Is.EqualTo(ulong.MaxValue));
			Assert.That(dictionary.Count, Is.EqualTo(3));
			Assert.That(dictionary[new SceneInstanceKey(new IntPtr(1), ulong.MaxValue)], Is.EqualTo(1));
		}

		[Test]
		public void SingleChildParentAndLoaderMayShareTheSamePath()
		{
			var parent = Group("Map");
			var child = Group("Loader");
			const ulong path = 0xF123456789ABCDEF;
			Enqueue(
				new SceneCommandNewNode(parent, path, null, 0, null),
				new SceneCommandNewNode(child, path, parent, path, null));
			Pump();
			var parentHandle = Handle(parent, path);
			var childHandle = Handle(child, path);
			Assert.That(_scene.InstanceCount, Is.EqualTo(2));
			Assert.That(childHandle.transform.parent, Is.SameAs(parentHandle.transform));
			Assert.That(childHandle, Is.Not.SameAs(parentHandle));
			Assert.That(parentHandle.IsVisible, Is.False);
			Assert.That(childHandle.IsLocallyActive, Is.True);
			Assert.That(childHandle.IsVisible, Is.False);
			Assert.That(childHandle.gameObject.activeInHierarchy, Is.True,
				"An occurrence is activated hidden when it is created (REQ-CSWU-PERF-012).");
		}

		[Test]
		public void SharedNativeNodeHasIndependentOccurrencesAndParents()
		{
			var left = Group("Left");
			var right = Group("Right");
			var shared = Group("Shared");
			Enqueue(
				new SceneCommandNewNode(left, 1, null, 0, null),
				new SceneCommandNewNode(right, 2, null, 0, null),
				new SceneCommandNewNode(shared, ulong.MaxValue, left, 1, null),
				new SceneCommandNewNode(shared, 0x8000000000000001, right, 2, null),
				new SceneCommandActivation(shared, ulong.MaxValue, Activation.OFF));
			Pump();
			var a = Handle(shared, ulong.MaxValue);
			var b = Handle(shared, 0x8000000000000001);
			Assert.That(a, Is.Not.SameAs(b));
			Assert.That(a.transform.parent, Is.SameAs(Handle(left, 1).transform));
			Assert.That(b.transform.parent, Is.SameAs(Handle(right, 2).transform));
			Assert.That(a.IsVisible, Is.False);
			Assert.That(b.IsLocallyActive, Is.True);
		}

		[Test]
		public void FrameCommitIsAtomicAndScopeGatePreservesChildLodOff()
		{
			var root = Group("Map");
			var child = Group("Lod child");
			Enqueue(
				new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandActivation(root, 1, Activation.INIT),
				new SceneCommandNewNode(child, 2, root, 1, null));
			Pump();
			var rootHandle = Handle(root, 1);
			var childHandle = Handle(child, 2);
			var global = new Vec3D(6378137, 123.5, 12.25);
			var frame = Frame(global);
			_pump.ExpectFrame(42, frame);
			Enqueue(
				new SceneCommandStartFrame(42, 100, global),
				new SceneCommandUpdateNode(root, 1, null, 0, null,
					Matrix4x4.Translate(new Vector3(10, 20, 30)).CopyMatrix4()),
				new SceneCommandActivation(child, 2, Activation.OFF),
				new SceneCommandActivation(root, 1, Activation.ON));

			Assert.That(_pump.Pump(2, TimeSpan.FromSeconds(1)), Is.EqualTo(4));
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(Vector3.zero));
			Assert.That(rootHandle.IsVisible, Is.False);
			Assert.That(childHandle.IsLocallyActive, Is.True);
			Assert.That(_scene.HasCommittedFrame, Is.False);
			Enqueue(new SceneCommandEndFrame(42));
			Pump();
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(new Vector3(10, 20, 30)));
			Assert.That(rootHandle.IsVisible, Is.True);
			Assert.That(childHandle.IsVisible, Is.False);
			Assert.That(_scene.CommittedFrame.GlobalPosition.x, Is.EqualTo(global.x));
			Assert.That(_scene.CommittedFrame.VerticalFieldOfView, Is.EqualTo(67).Within(0.001));

			Enqueue(new SceneCommandActivation(root, 1, Activation.OFF));
			Pump();
			Assert.That(rootHandle.IsVisible, Is.False);
			_pump.ExpectFrame(43, frame);
			Enqueue(
				new SceneCommandStartFrame(43, 101, global),
				new SceneCommandUpdateNode(root, 1, null, 0, null, Matrix4x4.identity.CopyMatrix4()),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandEndFrame(43));
			Pump();
			Assert.That(Handle(root, 1), Is.SameAs(rootHandle));
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(Vector3.zero));
			Assert.That(rootHandle.transform.localScale, Is.EqualTo(Vector3.one));
			Assert.That(rootHandle.transform.localRotation, Is.EqualTo(Quaternion.identity));
			Assert.That(rootHandle.IsVisible, Is.True);
			Assert.That(childHandle.IsVisible, Is.False);
			Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void FramesRequireMatchingInputRefreshFenceBeforePublishing(bool mismatchedEnd)
		{
			_pump.ExpectFrame(42, Frame(default));
			Assert.Throws<InvalidOperationException>(() => _pump.ExpectFrame(43, Frame(default)));
			if (mismatchedEnd)
				Enqueue(new SceneCommandStartFrame(42, 100, default), new SceneCommandEndFrame(43));
			else
				Enqueue(new SceneCommandStartFrame(43, 100, default));
			Assert.Throws<InvalidOperationException>(Pump);
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
			Assert.That(_scene.HasCommittedFrame, Is.False);
			Assert.Throws<InvalidOperationException>(() => _pump.ExpectFrame(44, Frame(default)));
		}

		[Test]
		public void HiddenDeleteRecyclesChildBeforeParentWithoutTouchingAnotherBranch()
		{
			var root = Group("Map");
			var child = Group("Child");
			var other = Group("Other map");
			Enqueue(
				new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandNewNode(child, 2, root, 1, null),
				new SceneCommandNewNode(other, 3, null, 0, null));
			Pump();
			var order = new List<SceneInstanceKey>();
			_scene.OnEnterPool += go => order.Add(go.GetComponent<SceneInstanceHandle>().Key);
			Enqueue(new SceneCommandDeleteNode(root, 1));
			Pump();
			Assert.That(order, Is.EqualTo(new[] { Key(child, 2), Key(root, 1) }));
			Assert.That(_scene.InstanceCount, Is.EqualTo(1));
			Assert.That(_scene.TryGetInstance(Key(other, 3), out _), Is.True);
			Assert.That(_scene.PooledObjectCount, Is.EqualTo(2));
		}

		[Test]
		public void PartialBufferRetainsOnlyItsUnconsumedNativeCommands()
		{
			var first = Group("First");
			var second = Group("Second");
			var firstReferences = first.GetReferenceCount();
			var secondReferences = second.GetReferenceCount();
			Enqueue(
				new SceneCommandNewNode(first, 1, null, 0, null),
				new SceneCommandNewNode(second, 2, first, 1, null));
			Assert.That(_pump.Pump(1, TimeSpan.FromSeconds(1)), Is.EqualTo(1));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
			Assert.That(_pump.PendingBuffers, Is.EqualTo(1));
			Assert.That(_scene.InstanceCount, Is.EqualTo(1));
			_pump.Dispose();
			Assert.That(first.GetReferenceCount(), Is.EqualTo(firstReferences));
			Assert.That(second.GetReferenceCount(), Is.EqualTo(secondReferences));
			Assert.That(_pump.PendingBuffers, Is.Zero);
		}

		[Test]
		public void CaptureLeavesNoWrapperWhoseFinalizerWaitsForEditAccess()
		{
			var root = Group("Map");
			var child = Group("Child");
			var state = EmptyState();
			var rootReferences = root.GetReferenceCount();
			var childReferences = child.GetReferenceCount();
			var stateReferences = state.GetReferenceCount();
			Enqueue(
				new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandNewNode(child, 2, root, 1, state),
				new SceneCommandUpdateNode(child, 2, root, 1, state),
				new SceneCommandActivation(child, 2, Activation.ON),
				new SceneCommandDeleteNode(child, 2));
			GC.Collect();
			GC.WaitForPendingFinalizers();
			Pump();
			Assert.That(_scene.InstanceCount, Is.EqualTo(1));
			Assert.That(root.GetReferenceCount(), Is.EqualTo(rootReferences));
			Assert.That(child.GetReferenceCount(), Is.EqualTo(childReferences));
			Assert.That(state.GetReferenceCount(), Is.EqualTo(stateReferences));
			Thread finalizers = null;
			NodeLock.WaitLockEdit();
			try
			{
				GC.Collect();
				finalizers = new Thread(GC.WaitForPendingFinalizers) { IsBackground = true };
				finalizers.Start();
				Assert.That(finalizers.Join(2000), Is.True,
					"A wrapper captured under edit access was left to a finalizer that waits for edit access.");
			}
			finally
			{
				NodeLock.UnLock();
				finalizers?.Join(5000);
			}
		}

		[Test]
		public void OverflowFaultsExplicitlyAndReleasesRejectedAndRetainedBuffers()
		{
			_pump.Dispose();
			_inbox = new SceneCommandInbox(1, 2);
			_pump = new SceneCommandPump(_scene, _inbox, Array.Empty<GeometryBuilderBase>());
			var first = Buffer(new SceneCommandResponse());
			var rejected = Buffer(new SceneCommandResponse());
			NodeLock.WaitLockEdit();
			try
			{
				_inbox.Receive(null, null, first);
				_inbox.Receive(null, null, rejected);
			}
			finally
			{
				NodeLock.UnLock();
			}
			Assert.That(rejected.IsValid(), Is.False);
			Assert.That(first.IsValid(), Is.True);
			Assert.That(_inbox.Fault, Does.Contain("overflow"));
			Assert.Throws<InvalidOperationException>(() => Pump());
			_pump.Dispose();
			Assert.That(first.IsValid(), Is.False);
		}

		[Test]
		public void StaleGenerationCannotPublishAndShutdownIsIdempotent()
		{
			Enqueue(new SceneCommandNewNode(Group("Old map"), 1, null, 0, null));
			_scene.Clear(5);
			Assert.Throws<InvalidOperationException>(() => Pump());
			_pump.Dispose();
			_scene.Dispose();
			_scene.Dispose();
			Assert.That(_parent, Is.Not.Null);
			Assert.That(_cameraObject, Is.Not.Null);
			Assert.That(_parent.transform.childCount, Is.Zero);
		}

		[Test]
		public void ForeignThreadCannotRealizeUnityObjects()
		{
			Exception failure = null;
			var thread = new Thread(() =>
			{
				try { _scene.TryGetInstance(SceneInstanceKey.Root, out _); }
				catch (Exception exception) { failure = exception; }
			});
			thread.Start();
			Assert.That(thread.Join(5000), Is.True);
			Assert.That(failure, Is.TypeOf<InvalidOperationException>());
			Assert.That(_scene.InstanceCount, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ControlSubmissionDoesNotWaitForForeignSdkEditLock(bool renderLock)
		{
			using (var session = new SceneManagerSession(1))
			{
				WithForeignGraphLock(() => session.Submit(new SceneCommandPositionCamera(
					new Vec3D(10, 20, 30), default, 90, 60)), renderLock);
			}
		}

		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(true, true)]
		public void FrameSubmissionDoesNotWaitAndNativeWorkerReturnsItsFence(bool renderLock, bool omniView)
		{
			_pump.Dispose();
			using (var session = new SceneManagerSession(1))
			{
				_inbox = session.Inbox;
				_pump = new SceneCommandPump(_scene, _inbox, Array.Empty<GeometryBuilderBase>());
				var camera = Frame(new Vec3D(10, 20, 30));
				uint refresh = 0;
				WithForeignGraphLock(() => refresh = session.SubmitFrame(camera, omniView), renderLock);
				_pump.ExpectFrame(refresh, camera);
				var timer = Stopwatch.StartNew();
				while (!_scene.HasCommittedFrame && timer.Elapsed < TimeSpan.FromSeconds(10))
				{
					Pump();
					if (!_scene.HasCommittedFrame)
						Thread.Sleep(1);
				}
				Assert.That(refresh, Is.Not.Zero);
				Assert.That(_scene.HasCommittedFrame, Is.True, _inbox.Fault);
				Assert.That(_scene.CommittedFrame.RefreshId, Is.EqualTo(refresh));
				Assert.That(_scene.CommittedFrame.GlobalPosition, Is.EqualTo(camera.GlobalPosition));
				Assert.That(_scene.CommittedFrame.VerticalFieldOfView, Is.EqualTo(camera.VerticalFieldOfView));
				Assert.That(NodeLock.IsLockedByMe(), Is.False);
			}
		}

		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(true, true)]
		public void FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch(bool omniView, bool infiniteFar)
		{
			var camera = new CameraFrame(new Vec3D(123.25, 456.5, 789.75),
				new Vec3(12, 34, 56), 123.5, 67, 1.5f, 0.3f, 50000, 600, 400, 2.5f, infiniteFar: infiniteFar);
			using (var buffer = new CommandBuffer())
			{
				uint refresh = 0;
				WithForeignGraphLock(() => refresh = SceneManagerSession.AppendFrameInput(buffer, camera, omniView));
				NodeLock.WaitLockEdit();
				try
				{
					Assert.That(buffer.BufferType, Is.EqualTo(CommandBuffer.CommandBufferType.GENERIC));
					Assert.That(buffer.BufferDeleteMode, Is.EqualTo(CommandBuffer.CommandBufferDeleteMode.UNLOCKED));
					Assert.That(buffer.Entries, Is.EqualTo(5));
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandSetOmniTraverse>());
						Assert.That(((SceneCommandSetOmniTraverse)command).OmniTraverse, Is.EqualTo(omniView));
					}
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandPositionCamera>());
						var position = (SceneCommandPositionCamera)command;
						Assert.That(position.Position, Is.EqualTo(camera.GlobalPosition));
						Assert.That(position.HPR, Is.EqualTo(camera.Hpr));
						Assert.That(position.HFOV, Is.EqualTo(camera.HorizontalFieldOfView));
						Assert.That(position.VFOV, Is.EqualTo(camera.VerticalFieldOfView));
					}
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandCameraSettings>());
						var settings = (SceneCommandCameraSettings)command;
						Assert.That(settings.NearDistance, Is.EqualTo(camera.NearDistance));
						Assert.That(settings.FarDistance, Is.EqualTo(camera.FarDistance));
						Assert.That(settings.InfiniteFar, Is.EqualTo(infiniteFar));
					}
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandSetLodFactor>());
						Assert.That(((SceneCommandSetLodFactor)command).LodFactor, Is.EqualTo(2.5f));
					}
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandRefreshScene>());
						var request = (SceneCommandRefreshScene)command;
						Assert.That(request.ID, Is.EqualTo(refresh));
						Assert.That(request.RenderTime, Is.EqualTo(camera.RenderTime));
						Assert.That(request.SizeX, Is.EqualTo(camera.Width));
						Assert.That(request.SizeY, Is.EqualTo(camera.Height));
						Assert.That(request.ScreenWidth, Is.EqualTo(camera.Width));
					}
					Assert.That(buffer.HasCommands, Is.False);
				}
				finally { NodeLock.UnLock(); }
			}
		}

		[Test]
		public void CameraOptionsDefaultToCswUnrealOmniWithAsynchronousFrames()
		{
			Assert.That(SceneManagerSettings.Default.OmniView, Is.True);
			Assert.That(SceneManagerSettings.Default.SynchronizeFrames, Is.False);
			Assert.That(SceneManagerSettings.Default.FrameWaitTimeout, Is.EqualTo(5));
			Assert.That(MapControl.ToFrameWaitTimeout(SceneManagerSettings.Default.FrameWaitTimeout),
				Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(MapControl.ToFrameWaitTimeout(1e-9), Is.EqualTo(TimeSpan.FromTicks(1)));
			foreach (var invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
				Assert.That(MapControl.ToFrameWaitTimeout(invalid), Is.EqualTo(TimeSpan.Zero),
					"An invalid timeout setting must fault synchronized updates instead of waiting unbounded.");
			Assert.That(SceneManagerSettings.Default.InfiniteFarPlane, Is.False,
				"Depth-based effects still use the finite far plane, so the finite frustum remains the default.");
			Assert.That(SceneManagerSettings.Default.LodFactor, Is.EqualTo(1));
			Assert.That(SceneManagerSettings.Default.LodReferenceFieldOfView, Is.Zero,
				"Without a reference field of view the LOD factor is submitted unscaled, like CSWUnreal.");
			Assert.That(MapControl.ToLodFactor(2.5f), Is.EqualTo(2.5f));
			foreach (var invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity })
				Assert.That(MapControl.ToLodFactor(invalid), Is.EqualTo(1),
					"An invalid LOD factor setting must submit the native default instead of faulting every update.");
			var bridge = new Camera(_scene.Camera, _coordinates);
			Assert.That(bridge.InfiniteFarPlane, Is.False);
			Assert.That(bridge.LodReferenceFieldOfView, Is.Zero);
			using (var manager = new MapSession(_scene, Array.Empty<GeometryBuilderBase>()))
			{
				Assert.That(manager.OmniView, Is.True);
				Assert.That(manager.SynchronizeFrames, Is.False);
				Assert.That(manager.FrameWaitTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
			}
		}

		[Test]
		public void SynchronousDrainStopsAtItsFenceWithoutConsumingFutureUpdates()
		{
			var later = Group("Later update");
			_pump.ExpectFrame(42, Frame(default));
			Enqueue(new SceneCommandStartFrame(42, 100, default),
				new SceneCommandEndFrame(42),
				new SceneCommandNewNode(later, 1, null, 0, null));
			Assert.That(_pump.Pump(int.MaxValue, TimeSpan.FromSeconds(1), 42), Is.EqualTo(2));
			Assert.That(_scene.CommittedFrame.RefreshId, Is.EqualTo(42));
			Assert.That(_scene.InstanceCount, Is.Zero);
			Assert.That(_pump.PendingBuffers, Is.EqualTo(1));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
			Pump();
			Assert.That(_scene.InstanceCount, Is.EqualTo(1));
		}

		[Test]
		public void InboxActivityDoesNotLoseAnArrivalBeforeWaiting()
		{
			var version = _inbox.ActivityVersion;
			Enqueue(new SceneCommandResponse());
			var timer = Stopwatch.StartNew();
			_inbox.WaitForActivity(version, 2000);
			Assert.That(timer.Elapsed.TotalMilliseconds, Is.LessThan(500));
			Assert.That(_inbox.ActivityVersion, Is.GreaterThan(version));
		}

		[Test]
		public void InboxWaitReleasesItsMonitorAndTakesNoSdkGraphLock()
		{
			var buffer = Buffer(new SceneCommandResponse());
			var version = _inbox.ActivityVersion;
			Exception failure = null;
			var thread = new Thread(() =>
			{
				try
				{
					Thread.Sleep(25);
					NodeLock.WaitLockEdit();
					try { _inbox.Receive(null, null, buffer); }
					finally { NodeLock.UnLock(); }
				}
				catch (Exception exception) { failure = exception; }
			}) { IsBackground = true };
			thread.Start();
			var timer = Stopwatch.StartNew();
			_inbox.WaitForActivity(version, 2000);
			var elapsed = timer.Elapsed;
			Assert.That(thread.Join(5000), Is.True);
			Assert.That(failure, Is.Null);
			Assert.That(elapsed.TotalMilliseconds, Is.LessThan(1500),
				"Graph access and the inbox monitor must remain available while the main thread waits.");
			Assert.That(_inbox.ActivityVersion, Is.GreaterThan(version));
			Assert.That(_inbox.PendingBuffers, Is.EqualTo(1));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
			Pump();
		}

		[Test]
		public void FrameChangesShareOneBufferLockAndPublishAfterUnlock()
		{
			var root = Group("Map");
			Enqueue(new SceneCommandNewNode(root, 1, null, 0, null));
			Pump();
			var commits = 0;
			_pump.FrameConsumed += _ =>
			{
				Assert.That(NodeLock.IsLockedByMe(), Is.False);
				commits++;
			};
			_pump.ExpectFrame(42, Frame(default));
			Enqueue(new SceneCommandStartFrame(42, 100, default),
				new SceneCommandUpdateNode(root, 1, null, 0, null,
					Matrix4x4.Translate(new Vector3(10, 20, 30)).CopyMatrix4()),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandResponse(),
				new SceneCommandEndFrame(42));
			Assert.That(_pump.Pump(128, TimeSpan.FromSeconds(1)), Is.EqualTo(5));
			Assert.That(_pump.LastLockAttempts, Is.EqualTo(1));
			Assert.That(commits, Is.EqualTo(1));
			Assert.That(Handle(root, 1).transform.localPosition, Is.EqualTo(new Vector3(10, 20, 30)));
			Assert.That(_pump.PendingBuffers, Is.Zero);
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
		}

		[Test]
		public void FrameExceedingBothLimitsIsConsumedWholeWhileOutputOutsideFramesRespectsThem()
		{
			var root = Group("Map");
			var first = Group("First tile");
			var second = Group("Second tile");
			var third = Group("Third tile");
			var fourth = Group("Fourth tile");
			var fifth = Group("Fifth tile");
			var sixth = Group("Sixth tile");
			Enqueue(new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandActivation(root, 1, Activation.INIT),
				new SceneCommandNewNode(first, 2, root, 1, null),
				new SceneCommandNewNode(second, 3, root, 1, null));
			Pump();
			_pump.ExpectFrame(42, Frame(default));
			Enqueue(new SceneCommandStartFrame(42, 100, default),
				new SceneCommandUpdateNode(root, 1, null, 0, null,
					Matrix4x4.Translate(new Vector3(10, 20, 30)).CopyMatrix4()),
				new SceneCommandActivation(first, 2, Activation.OFF),
				new SceneCommandActivation(second, 3, Activation.OFF),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandEndFrame(42));
			Enqueue(new SceneCommandNewNode(third, 4, null, 0, null));
			Enqueue(new SceneCommandNewNode(fourth, 5, null, 0, null));
			Enqueue(new SceneCommandStartFrame(43, 101, default),
				new SceneCommandUpdateNode(root, 1, null, 0, null,
					Matrix4x4.Translate(new Vector3(40, 50, 60)).CopyMatrix4()),
				new SceneCommandActivation(first, 2, Activation.ON),
				new SceneCommandActivation(second, 3, Activation.ON),
				new SceneCommandEndFrame(43));
			Enqueue(new SceneCommandNewNode(fifth, 6, null, 0, null));
			Enqueue(new SceneCommandNewNode(sixth, 7, null, 0, null));

			Assert.That(_pump.Pump(1, TimeSpan.Zero), Is.EqualTo(6));
			Assert.That(_pump.LastLockAttempts, Is.EqualTo(1));
			Assert.That(_scene.CommittedFrame.RefreshId, Is.EqualTo(42));
			Assert.That(Handle(root, 1).transform.localPosition, Is.EqualTo(new Vector3(10, 20, 30)));
			Assert.That(Handle(root, 1).IsVisible, Is.True);
			Assert.That(Handle(first, 2).IsVisible, Is.False);
			Assert.That(_scene.TryGetInstance(Key(third, 4), out _), Is.False);
			Assert.That(_pump.PendingBuffers, Is.EqualTo(5));

			Assert.That(_pump.Pump(1, TimeSpan.FromSeconds(10)), Is.EqualTo(1));
			Assert.That(_scene.TryGetInstance(Key(third, 4), out _), Is.True);
			Assert.That(_scene.TryGetInstance(Key(fourth, 5), out _), Is.False);
			Assert.That(_pump.Pump(int.MaxValue, TimeSpan.Zero), Is.EqualTo(1));
			Assert.That(_scene.TryGetInstance(Key(fourth, 5), out _), Is.True);
			Assert.That(_scene.CommittedFrame.RefreshId, Is.EqualTo(42));
			Assert.That(_pump.PendingBuffers, Is.EqualTo(3));

			_pump.ExpectFrame(43, Frame(default));
			Assert.That(_pump.Pump(3, TimeSpan.FromSeconds(10)), Is.EqualTo(6),
				"The transforms and activations of a frame must not count toward the command limit.");
			Assert.That(_scene.CommittedFrame.RefreshId, Is.EqualTo(43));
			Assert.That(Handle(root, 1).transform.localPosition, Is.EqualTo(new Vector3(40, 50, 60)));
			Assert.That(Handle(first, 2).IsVisible, Is.True);
			Assert.That(_scene.TryGetInstance(Key(fifth, 6), out _), Is.True);
			Assert.That(_scene.TryGetInstance(Key(sixth, 7), out _), Is.False);
			Pump();
			Assert.That(_scene.TryGetInstance(Key(sixth, 7), out _), Is.True);
			Assert.That(_pump.PendingBuffers, Is.Zero);
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void BusyGraphLockDefersTheOwnedBufferWithoutConsumingCommands(bool renderLock)
		{
			var parent = Group("Parent");
			var child = Group("Child");
			Enqueue(new SceneCommandNewNode(parent, 1, null, 0, null),
				new SceneCommandNewNode(child, 2, parent, 1, null));
			WithForeignGraphLock(() =>
			{
				Assert.That(_pump.Pump(128, TimeSpan.FromSeconds(10)), Is.Zero);
				Assert.That(_pump.LastLockAttempts, Is.EqualTo(1));
				Assert.That(_pump.PendingBuffers, Is.EqualTo(1));
				Assert.That(_scene.InstanceCount, Is.Zero);
			}, renderLock);
			Assert.That(_pump.Pump(1, TimeSpan.FromSeconds(1)), Is.EqualTo(1));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
			Assert.That(_pump.PendingBuffers, Is.EqualTo(1));
			Pump();
			Assert.That(_scene.InstanceCount, Is.EqualTo(2));
			Assert.That(Handle(child, 2).ParentKey, Is.EqualTo(Key(parent, 1)));
			Assert.That(_pump.PendingBuffers, Is.Zero);
		}

		[Test]
		public void HierarchyPublicationPrecedesDependentCapturesInTheSameBuffer()
		{
			var left = Group("Left");
			var right = Group("Right");
			var child = Group("Child");
			Enqueue(new SceneCommandNewNode(left, 1, null, 0, null),
				new SceneCommandNewNode(right, 2, null, 0, null),
				new SceneCommandNewNode(child, 3, left, 1, null),
				new SceneCommandUpdateNode(child, 3, right, 2, null),
				new SceneCommandUpdateNode(child, 3, right, 2, null,
					Matrix4x4.Translate(new Vector3(10, 20, 30)).CopyMatrix4()),
				new SceneCommandDeleteNode(left, 1));
			Pump();
			Assert.That(_scene.InstanceCount, Is.EqualTo(2));
			Assert.That(Handle(child, 3).ParentKey, Is.EqualTo(Key(right, 2)));
			Assert.That(Handle(child, 3).transform.localPosition, Is.EqualTo(new Vector3(10, 20, 30)));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
		}

		[Test]
		public void RawNativeHierarchyReceivesExactlyOneBasisReflection()
		{
			var parent = Group("Parent");
			var child = Group("Child");
			var parentMatrix = Matrix4x4.Translate(new Vector3(3, 5, 7));
			var childMatrix = Matrix4x4.TRS(new Vector3(11, 13, 17), Quaternion.Euler(19, 23, 29), Vector3.one);
			Enqueue(
				new SceneCommandNewNode(parent, 1, null, 0, null),
				new SceneCommandNewNode(child, 2, parent, 1, null),
				new SceneCommandUpdateNode(parent, 1, null, 0, null, parentMatrix.CopyMatrix4()),
				new SceneCommandUpdateNode(child, 2, parent, 1, null, childMatrix.CopyMatrix4()));
			Pump();
			var parentHandle = Handle(parent, 1);
			var childHandle = Handle(child, 2);
			Assert.That(parentHandle.transform.position, Is.EqualTo(new Vector3(3, 5, -7)));
			var vertex = new Vector3(31, 37, 41);
			var expected = (Conversion.UnityBasis * parentMatrix * childMatrix).MultiplyPoint3x4(vertex);
			Assert.That(Vector3.Distance(childHandle.transform.TransformPoint(vertex), expected), Is.LessThan(0.0001));
			var normal = new Vector3(0.1f, 0.4f, 0.7f).normalized;
			var expectedNormal = (Conversion.UnityBasis * parentMatrix * childMatrix).inverse.transpose.MultiplyVector(normal).normalized;
			var actualNormal = childHandle.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normal).normalized;
			Assert.That(Vector3.Distance(actualNormal, expectedNormal), Is.LessThan(0.00001));
		}

		[Test]
		public void InvalidTransformCannotAllocateAnOrphanOrConsumeAPooledHandle()
		{
			var first = new SceneNodeData
			{
				Key = new SceneInstanceKey(new IntPtr(101), 1),
				Parent = SceneInstanceKey.Root,
				Name = "Recyclable"
			};
			_scene.Create(first);
			_scene.Delete(first.Key);
			var childCount = _scene.Root.transform.childCount;
			var invalid = Matrix4x4.identity;
			invalid.m03 = float.NaN;
			var bad = new SceneNodeData
			{
				Key = new SceneInstanceKey(new IntPtr(102), 2),
				Parent = SceneInstanceKey.Root,
				Name = "Invalid",
				LocalTransform = invalid
			};
			Assert.Throws<InvalidOperationException>(() => _scene.Create(bad));
			Assert.That(_scene.InstanceCount, Is.Zero);
			Assert.That(_scene.PooledObjectCount, Is.EqualTo(1));
			Assert.That(_scene.Root.transform.childCount, Is.EqualTo(childCount));
			_scene.Clear(1);
			Assert.That(_scene.Root.transform.childCount, Is.EqualTo(childCount));
		}

		[Test]
		public void UninitializedCameraRotationLeavesTheBorrowedCameraUnchanged()
		{
			var bridge = new Camera(_scene.Camera, _coordinates);
			var rotation = Quaternion.Euler(10, 20, 30);
			_scene.Camera.transform.rotation = rotation;
			bridge.Present();
			Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
			bridge.SetPose(default, Quaternion.identity);
			bridge.ResetPose();
			bridge.Present();
			Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CameraRotationDoesNotWaitForNativeEndFrameOrGraphAccess(bool renderLock)
		{
			var bridge = new Camera(_scene.Camera, _coordinates);
			var position = new Vec3D(6378137, 123.5, 12.25);
			bridge.SetPose(position, Quaternion.identity);
			_pump.ExpectFrame(42, bridge.Sample(100));
			Enqueue(new SceneCommandStartFrame(42, 100, position));
			Pump();
			WithForeignGraphLock(() =>
			{
				for (var step = 1; step <= 90; step++)
				{
					var rotation = Quaternion.Euler(10, step, 0);
					bridge.SetPose(position, rotation);
					bridge.Present();
					Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
				}
			}, renderLock);
			Assert.That(_scene.HasCommittedFrame, Is.False);
			Assert.That(bridge.GlobalPosition, Is.EqualTo(position));
			Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(NodeLock.IsLockedByMe(), Is.False);
		}

		[Test]
		public void FrameCommitDoesNotReplaceNewerLiveCameraRotation()
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var bridge = new Camera(_scene.Camera, _coordinates);
			var position = new Vec3D(6378137, 123.5, 12.25);
			bridge.SetPose(position, Quaternion.Euler(10, 20, 0));
			var submitted = bridge.Sample(100);
			var latest = Quaternion.Euler(10, 50, 0);
			bridge.SetPose(position, latest);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			_scene.FrameCommitting += bridge.Commit;
			try
			{
				_scene.CommitFrame(submitted);
				Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, latest), Is.LessThan(0.05f));
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(_scene.CommittedFrame.Hpr, Is.EqualTo(submitted.Hpr));
				Assert.That(_scene.CommittedFrame.GlobalPosition, Is.EqualTo(position));
			}
			finally
			{
				_scene.FrameCommitting -= bridge.Commit;
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void SynchronizedCameraPresentsOnlyTheCommittedFrustumWithoutGraphAccess(bool renderLock)
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var bridge = new Camera(_scene.Camera, _coordinates) { SynchronizeFrames = true };
			var position = new Vec3D(6378137, 123.5, 12.25);
			var rotation = Quaternion.Euler(10, 20, 0);
			bridge.SetPose(position, rotation);
			var submitted = bridge.Sample(100);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			try
			{
				bridge.Commit(submitted);
				var latest = Quaternion.Euler(30, 150, 0);
				bridge.SetPose(position + new Vec3D(50, 60, 70), latest);
				_scene.Camera.fieldOfView = 95;
				_scene.Camera.aspect = 2;
				WithForeignGraphLock(() =>
				{
					bridge.Present();
					Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
					Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05));
					var expected = submitted.Projection;
					Assert.That(expected, Is.EqualTo(Matrix4x4.Perspective(submitted.VerticalFieldOfView,
						submitted.Aspect, submitted.NearDistance, submitted.FarDistance)));
					for (var i = 0; i < 16; i++)
						Assert.That(_scene.Camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001));
				}, renderLock);
				Assert.That(bridge.GlobalPosition, Is.EqualTo(position + new Vec3D(50, 60, 70)));
				Assert.That(Quaternion.Angle(bridge.Rotation, latest), Is.LessThan(0.05));
				bridge.SynchronizeFrames = false;
				bridge.Present();
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(new Vector3(50, 60, -70)));
				Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, latest), Is.LessThan(0.05));
				Assert.That(_scene.Camera.projectionMatrix[1, 1],
					Is.EqualTo(Matrix4x4.Perspective(95, 2, submitted.NearDistance, submitted.FarDistance)[1, 1])
						.Within(0.00001));
			}
			finally
			{
				bridge.ResetPose();
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		[Test]
		public void ResetSynchronizedCameraReleasesItsProjectionAndOldMapPose()
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var bridge = new Camera(_scene.Camera, _coordinates) { SynchronizeFrames = true };
			bridge.SetPose(new Vec3D(6378137, 0, 0), Quaternion.identity);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			try
			{
				bridge.Commit(bridge.Sample(100));
				bridge.ResetPose();
				_scene.Camera.fieldOfView = 95;
				Assert.That(_scene.Camera.projectionMatrix[1, 1],
					Is.EqualTo(Matrix4x4.Perspective(95, _scene.Camera.aspect,
						_scene.Camera.nearClipPlane, _scene.Camera.farClipPlane)[1, 1]).Within(0.00001));
				var sentinel = Quaternion.Euler(10, 20, 30);
				_scene.Camera.transform.rotation = sentinel;
				bridge.SetPose(new Vec3D(100, 200, 300), Quaternion.identity);
				bridge.Present();
				Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, sentinel), Is.LessThan(0.05));
			}
			finally
			{
				bridge.ResetPose();
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		[Test]
		public void ResetPoseDiscardsThePreviousMapReference()
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var bridge = new Camera(_scene.Camera, _coordinates);
			var position = new Vec3D(6378137, 123.5, 12.25);
			bridge.SetPose(position, Quaternion.identity);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			try
			{
				bridge.Commit(bridge.Sample(100));
				bridge.SetPose(position + new Vec3D(1, 2, 3), Quaternion.identity);
				bridge.Present();
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(new Vector3(1, 2, -3)));
				bridge.ResetPose();
				var sentinel = new Vector3(5, 7, 11);
				_scene.Camera.transform.position = sentinel;
				bridge.SetPose(new Vec3D(-1000000000, 2000000000, -3000000000), Quaternion.Euler(0, 45, 0));
				bridge.Present();
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(sentinel),
					"A new map must not use the previous map's committed reference.");
			}
			finally
			{
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void LivePoseAndBothRoiRootsRemainContinuousAcrossADeferredReferenceRebase(bool renderLock)
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var first = Group("First ROI");
			var second = Group("Second ROI");
			Enqueue(new SceneCommandNewNode(first, 1, null, 0, null),
				new SceneCommandNewNode(second, 2, null, 0, null));
			Pump();
			var firstHandle = Handle(first, 1);
			var secondHandle = Handle(second, 2);
			var firstOffset = new Vector3(32, 64, 128);
			var secondOffset = new Vector3(-48, 96, -160);
			_scene.SetTransform(firstHandle.Key, Matrix4x4.Translate(firstOffset));
			_scene.SetTransform(secondHandle.Key, Matrix4x4.Translate(secondOffset));
			var reference = new Vec3D(1000000000, -2000000000, 3000000000);
			var bridge = new Camera(_scene.Camera, _coordinates);
			bridge.SetPose(reference, Quaternion.identity);
			var submitted = bridge.Sample(100);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			_scene.FrameCommitting += bridge.Commit;
			try
			{
				_scene.CommitFrame(submitted);
				var shift = new Vector3(0.75f, -0.5f, 1.25f);
				var nextReference = reference + new Vec3D(shift.x, shift.y, shift.z);
				var frame = new CameraFrame(nextReference, default, 101, 60, 1, 1, 10000, 640, 480);
				_pump.ExpectFrame(43, frame);
				Enqueue(new SceneCommandStartFrame(43, 101, nextReference),
					new SceneCommandUpdateNode(first, 1, null, 0, null,
						Matrix4x4.Translate(firstOffset - shift).CopyMatrix4()),
					new SceneCommandUpdateNode(second, 2, null, 0, null,
						Matrix4x4.Translate(secondOffset - shift).CopyMatrix4()));
				Assert.That(_pump.Pump(3, TimeSpan.FromSeconds(1)), Is.EqualTo(3));
				Enqueue(new SceneCommandEndFrame(43));
				var committedWorldOffset = Shader.GetGlobalVector("_WorldOffset");
				WithForeignGraphLock(() =>
				{
					Assert.That(_pump.Pump(128, TimeSpan.FromSeconds(1)), Is.Zero);
					for (var step = 1; step <= 90; step++)
					{
						var delta = new Vec3D(step * 0.03125, step * 0.0625, step * 0.125);
						var rotation = Quaternion.Euler(10, step, 0);
						bridge.SetPose(reference + delta, rotation);
						bridge.Present();
						Assert.That(Vector3.Distance(_scene.Camera.transform.position,
							new Vector3(step * 0.03125f, step * 0.0625f, -step * 0.125f)),
							Is.LessThan(0.00001f), "Subtract doubles before converting the live displacement to floats.");
						Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
						Assert.That(firstHandle.transform.localPosition, Is.EqualTo(firstOffset));
						Assert.That(secondHandle.transform.localPosition, Is.EqualTo(secondOffset));
						Assert.That(Shader.GetGlobalVector("_WorldOffset"), Is.EqualTo(committedWorldOffset));
					}
				}, renderLock);
				var firstView = firstHandle.transform.position - _scene.Camera.transform.position;
				var secondView = secondHandle.transform.position - _scene.Camera.transform.position;
				Pump();
				Assert.That(firstHandle.transform.localPosition, Is.EqualTo(firstOffset - shift));
				Assert.That(secondHandle.transform.localPosition, Is.EqualTo(secondOffset - shift));
				Assert.That(Vector3.Distance(firstHandle.transform.position - _scene.Camera.transform.position,
					firstView), Is.LessThan(0.0001f), "The native rebase must not apply camera motion twice.");
				Assert.That(Vector3.Distance(secondHandle.transform.position - _scene.Camera.transform.position,
					secondView), Is.LessThan(0.0001f));
				Assert.That(_scene.CommittedFrame.GlobalPosition, Is.EqualTo(nextReference));
				Assert.That(Shader.GetGlobalVector("_WorldOffset").x,
					Is.EqualTo(nextReference.x % 5000).Within(0.00001));
				Assert.That(NodeLock.IsLockedByMe(), Is.False);
			}
			finally
			{
				_scene.FrameCommitting -= bridge.Commit;
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		[Test]
		public void SubmittedProjectionSurvivesHardcodedNativeFrameFov()
		{
			var submitted = new CameraFrame(default, default, 1, 67, 2, 1, 10000, 640, 480, 1.75f,
				infiniteFar: true);
			var committed = submitted.WithNativePose(123, 2, new Vec3D(1, 2, 3), new Vec3(4, 5, 6));
			Assert.That(committed.VerticalFieldOfView, Is.EqualTo(67));
			Assert.That(committed.HorizontalFieldOfView, Is.EqualTo(submitted.HorizontalFieldOfView));
			Assert.That(committed.Aspect, Is.EqualTo(2));
			Assert.That(committed.NearDistance, Is.EqualTo(1));
			Assert.That(committed.FarDistance, Is.EqualTo(10000));
			Assert.That(committed.InfiniteFar, Is.True, "The native frame must keep the submitted far plane mode.");
			Assert.That(committed.LodFactor, Is.EqualTo(1.75f));
			Assert.That(committed.Projection, Is.EqualTo(submitted.Projection));
			Assert.That(committed.GlobalPosition.x, Is.EqualTo(1));
			Assert.That(committed.Hpr.y, Is.EqualTo(5));
			Assert.That(committed.RefreshId, Is.EqualTo(123));
			Assert.Throws<ArgumentException>(() =>
				new CameraFrame(new Vec3D(double.NaN, 0, 0), default, 1, 60, 1, 1, 100, 1, 1));
		}

		[Test]
		public void FrameProjectionMatchesGzPerspCameraWithFiniteAndInfiniteFarPlanes()
		{
			const float near = 0.5f;
			const float far = 2000;
			var finite = new CameraFrame(default, default, 1, 50, 1.6f, near, far, 640, 400);
			var infinite = new CameraFrame(default, default, 1, 50, 1.6f, near, far, 640, 400,
				infiniteFar: true);
			// gzPerspCamera::getProjectionTransform with m_width = near * tan(hfov / 2), m_height = near * tan(vfov / 2).
			var width = 1 / Math.Tan(finite.HorizontalFieldOfView * Math.PI / 360);
			var height = 1 / Math.Tan(finite.VerticalFieldOfView * Math.PI / 360);
			var expectedFinite = GizmoProjection(width, height,
				-(far + near) / (double)(far - near), -2.0 * near * far / (far - near));
			var expectedInfinite = GizmoProjection(width, height, -0.995, -2.0 * near * 0.995);
			for (var i = 0; i < 16; i++)
			{
				Assert.That(finite.Projection[i], Is.EqualTo(expectedFinite[i]).Within(0.00001), $"finite[{i}]");
				Assert.That(infinite.Projection[i], Is.EqualTo(expectedInfinite[i]).Within(0.00001), $"infinite[{i}]");
			}
			Assert.That(finite.Projection, Is.EqualTo(Matrix4x4.Perspective(50, 1.6f, near, far)));
			Assert.That(ProjectedDepth(infinite.Projection, near), Is.EqualTo(-0.995f).Within(0.00001));
			Assert.That(ProjectedDepth(infinite.Projection, 1e12f), Is.EqualTo(0.995f).Within(0.0001).And.LessThan(1),
				"An infinite far plane keeps arbitrarily distant points inside the depth range.");
			Assert.That(ProjectedDepth(infinite.Projection, 2 * far), Is.LessThan(1));
			Assert.That(ProjectedDepth(finite.Projection, 2 * far), Is.GreaterThan(1),
				"A finite far plane clips beyond far like gzPerspCamera.");
			var distant = new Bounds(new Vector3(0, 0, -1e9f), Vector3.one * 1000);
			Assert.That(GeometryUtility.TestPlanesAABB(
				GeometryUtility.CalculateFrustumPlanes(infinite.Projection), distant), Is.True,
				"Unity culling must keep bounds beyond far with an infinite far plane.");
			Assert.That(GeometryUtility.TestPlanesAABB(
				GeometryUtility.CalculateFrustumPlanes(finite.Projection), distant), Is.False);
		}

		[Test]
		public void SampledLodFactorScalesWithZoomAgainstTheReferenceFieldOfView()
		{
			Assert.That(Camera.EffectiveLodFactor(2, 30, 0), Is.EqualTo(2));
			foreach (var disabled in new[] { -1, 180, float.NaN, float.PositiveInfinity })
				Assert.That(Camera.EffectiveLodFactor(2, 30, disabled), Is.EqualTo(2));
			Assert.That(Camera.EffectiveLodFactor(2, 60, 60), Is.EqualTo(2).Within(0.00001));
			var zoom = Math.Tan(Math.PI / 6) / Math.Tan(Math.PI / 12);
			Assert.That(Camera.EffectiveLodFactor(1, 30, 60), Is.EqualTo(zoom).Within(0.00001),
				"Halving the field of view must raise the LOD factor by GizmoSDK's zoom.");
			Assert.That(Camera.EffectiveLodFactor(1, 90, 60), Is.EqualTo(Math.Tan(Math.PI / 6)).Within(0.00001));
			var bridge = new Camera(_scene.Camera, _coordinates);
			bridge.SetPose(new Vec3D(6378137, 0, 0), Quaternion.identity);
			_scene.Camera.fieldOfView = 30;
			var unscaled = bridge.Sample(100, 1.5f);
			Assert.That(unscaled.LodFactor, Is.EqualTo(1.5f));
			Assert.That(unscaled.InfiniteFar, Is.False);
			bridge.LodReferenceFieldOfView = 60;
			bridge.InfiniteFarPlane = true;
			var zoomed = bridge.Sample(100, 1.5f);
			Assert.That(zoomed.LodFactor, Is.EqualTo(1.5 * zoom).Within(0.00001));
			Assert.That(zoomed.VerticalFieldOfView, Is.EqualTo(30));
			Assert.That(zoomed.InfiniteFar, Is.True);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CameraPresentsTheNativeFarPlaneModeWithoutWritingTheHostFrustum(bool synchronize)
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var camera = _scene.Camera;
			camera.aspect = 1.5f;
			camera.fieldOfView = 50;
			camera.nearClipPlane = 0.5f;
			camera.farClipPlane = 2000;
			var bridge = new Camera(camera, _coordinates)
			{
				SynchronizeFrames = synchronize, InfiniteFarPlane = true
			};
			var position = new Vec3D(6378137, 0, 0);
			bridge.SetPose(position, Quaternion.identity);
			var submitted = bridge.Sample(100);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			try
			{
				Assert.That(submitted.InfiniteFar, Is.True);
				bridge.Commit(submitted);
				AssertProjection(camera, submitted.Projection);
				Assert.That(camera.projectionMatrix.m22, Is.EqualTo(-Camera.InfiniteFarNudge));
				camera.fieldOfView = 70;
				camera.nearClipPlane = 1;
				camera.farClipPlane = 5000;
				var requested = Camera.NativeProjection(70, 1.5f, 1, 5000, true);
				bridge.Present();
				AssertProjection(camera, synchronize ? submitted.Projection : requested);
				var older = new CameraFrame(position, default, 99, 30, 1.5f, 2, 100, 640, 480);
				bridge.Commit(older);
				Assert.That(camera.fieldOfView, Is.EqualTo(70),
					"Committing a refresh must not write its frustum back over the host's camera settings.");
				Assert.That(camera.nearClipPlane, Is.EqualTo(1));
				Assert.That(camera.farClipPlane, Is.EqualTo(5000));
				AssertProjection(camera, synchronize ? older.Projection : requested);
				bridge.InfiniteFarPlane = false;
				bridge.Present();
				AssertProjection(camera, synchronize ? older.Projection : Matrix4x4.Perspective(70, 1.5f, 1, 5000));
				var next = bridge.Sample(101);
				Assert.That(next.VerticalFieldOfView, Is.EqualTo(70));
				Assert.That(next.NearDistance, Is.EqualTo(1));
				Assert.That(next.FarDistance, Is.EqualTo(5000));
				Assert.That(next.InfiniteFar, Is.False);
			}
			finally
			{
				bridge.ResetPose();
				Shader.SetGlobalVector("_WorldOffset", worldOffset);
				Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
			}
		}

		private static Matrix4x4 GizmoProjection(double width, double height, double m22, double m23)
		{
			var projection = Matrix4x4.zero;
			projection.m00 = (float)width;
			projection.m11 = (float)height;
			projection.m22 = (float)m22;
			projection.m23 = (float)m23;
			projection.m32 = -1;
			return projection;
		}

		private static float ProjectedDepth(Matrix4x4 projection, float distance)
		{
			var clip = projection * new Vector4(0, 0, -distance, 1);
			return clip.z / clip.w;
		}

		private static void AssertProjection(UnityEngine.Camera camera, Matrix4x4 expected)
		{
			for (var i = 0; i < 16; i++)
				Assert.That(camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001), $"projection[{i}]");
		}

		[TestCase("delete")]
		[TestCase("recreate")]
		[TestCase("reparent")]
		public void DeferredFrameChangesCannotCrossOccurrenceLifetimeOrParent(string operation)
		{
			var first = Group("First parent");
			var second = Group("Second parent");
			var child = Group("Changing occurrence");
			Enqueue(
				new SceneCommandNewNode(first, 1, null, 0, null),
				new SceneCommandNewNode(second, 2, null, 0, null),
				new SceneCommandNewNode(child, 3, first, 1, null),
				new SceneCommandActivation(child, 3, Activation.OFF));
			Pump();
			var lifetime = Handle(child, 3).Lifetime;
			_pump.ExpectFrame(101, Frame(default));
			Enqueue(
				new SceneCommandStartFrame(101, 0, default),
				new SceneCommandUpdateNode(child, 3, first, 1, null,
					Matrix4x4.Translate(new Vector3(17, 23, 31)).CopyMatrix4()),
				new SceneCommandActivation(child, 3, Activation.ON));
			Pump();
			if (operation == "delete" || operation == "recreate")
			{
				Enqueue(new SceneCommandDeleteNode(child, 3));
				Pump();
			}
			if (operation == "recreate")
			{
				Enqueue(
					new SceneCommandNewNode(child, 3, first, 1, null),
					new SceneCommandActivation(child, 3, Activation.OFF));
				Pump();
			}
			if (operation == "reparent")
			{
				Enqueue(new SceneCommandUpdateNode(child, 3, second, 2, null));
				Pump();
			}
			Enqueue(new SceneCommandEndFrame(101));
			Assert.DoesNotThrow(Pump);
			if (operation == "delete")
			{
				Assert.That(_scene.TryGetInstance(Key(child, 3), out _), Is.False);
				return;
			}
			var current = Handle(child, 3);
			Assert.That(current.transform.localPosition, Is.EqualTo(Vector3.zero));
			Assert.That(current.IsLocallyActive, Is.False);
			if (operation == "recreate")
				Assert.That(current.Lifetime, Is.Not.EqualTo(lifetime));
			else
				Assert.That(current.ParentKey, Is.EqualTo(Key(second, 2)));
		}

		[Test]
		public void NewBranchUnderVisibleMapWaitsForItsFrameCommit()
		{
			var root = Group("Visible map");
			var child = Group("Streamed branch");
			Enqueue(new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandActivation(root, 1, Activation.ON));
			Pump();
			_scene.CommitFrame(Frame(default));
			Assert.That(Handle(root, 1).IsVisible, Is.True);
			_pump.ExpectFrame(44, Frame(default));
			Enqueue(
				new SceneCommandStartFrame(44, 0, default),
				new SceneCommandNewNode(child, 2, root, 1, null),
				new SceneCommandActivation(child, 2, Activation.OFF));
			Assert.That(_pump.Pump(1, TimeSpan.Zero), Is.EqualTo(3));
			var branch = Handle(child, 2);
			Assert.That(branch.IsReady, Is.True);
			Assert.That(branch.IsPresented, Is.False);
			Assert.That(branch.IsVisible, Is.False);
			Assert.That(branch.gameObject.activeInHierarchy, Is.True);
			Enqueue(new SceneCommandEndFrame(44));
			Pump();
			Assert.That(branch.IsPresented, Is.True);
			Assert.That(branch.IsLocallyActive, Is.False);
			Assert.That(branch.IsVisible, Is.False);
		}

		[Test]
		public void ReparentingRecomputesTheVisibilityOfTheMovedSubtree()
		{
			var root = Group("Visible map");
			var shown = Group("Shown parent");
			var hidden = Group("Hidden parent");
			var moved = Group("Moved occurrence");
			var leaf = Group("Moved leaf");
			Enqueue(
				new SceneCommandNewNode(root, 1, null, 0, null),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandNewNode(shown, 2, root, 1, null),
				new SceneCommandNewNode(hidden, 3, root, 1, null),
				new SceneCommandActivation(hidden, 3, Activation.OFF),
				new SceneCommandNewNode(moved, 4, shown, 2, null),
				new SceneCommandNewNode(leaf, 5, moved, 4, null));
			Pump();
			_scene.CommitFrame(Frame(default));
			var movedHandle = Handle(moved, 4);
			var leafHandle = Handle(leaf, 5);
			Assert.That(leafHandle.IsVisible, Is.True);
			Enqueue(new SceneCommandUpdateNode(moved, 4, hidden, 3, null));
			Pump();
			Assert.That(movedHandle.IsVisible, Is.False);
			Assert.That(leafHandle.IsVisible, Is.False);
			Enqueue(new SceneCommandUpdateNode(moved, 4, shown, 2, null));
			Pump();
			Assert.That(movedHandle.IsVisible, Is.True);
			Assert.That(leafHandle.IsVisible, Is.True);
		}

		[Test]
		public void NonIdentityHostAttachmentIsRejectedBeforeOwningAnyUnityObjects()
		{
			_parent.transform.position = new Vector3(3, 5, 7);
			var count = _parent.transform.childCount;
			Assert.Throws<ArgumentException>(() =>
				new UnityScene(_parent.transform, _scene.Camera, _coordinates));
			Assert.That(_parent.transform.childCount, Is.EqualTo(count));
			_parent.transform.position = Vector3.zero;
		}

		[Test]
		public void CallbackReleaseFailureIsContainedAndEveryOwnedBufferIsRetried()
		{
			_pump.Dispose();
			var failures = 2;
			_inbox = new SceneCommandInbox(1, 10, buffer =>
			{
				if (failures-- > 0)
					throw new InvalidOperationException("Injected buffer release failure.");
				buffer.Dispose();
			});
			_pump = new SceneCommandPump(_scene, _inbox, Array.Empty<GeometryBuilderBase>());
			var first = Buffer(new SceneCommandResponse());
			var second = Buffer(new SceneCommandResponse());
			NodeLock.WaitLockEdit();
			try
			{
				_inbox.Receive(null, null, first);
				Assert.DoesNotThrow(() => _inbox.Receive(null, null, second));
			}
			finally { NodeLock.UnLock(); }
			Assert.That(_inbox.Fault, Does.Contain("overflow"));
			Assert.That(_inbox.PendingBuffers, Is.EqualTo(2));
			Assert.Throws<AggregateException>(_pump.Dispose);
			Assert.That(first.IsValid(), Is.True);
			Assert.That(second.IsValid(), Is.False);
			Assert.That(_pump.IsDisposed, Is.False);
			Assert.DoesNotThrow(_pump.Dispose);
			Assert.That(first.IsValid(), Is.False);
			Assert.That(_pump.IsDisposed, Is.True);
		}

		[Test]
		public void CoordinateContextUsesTypedWgs84ConversionAndTrueProjectedOrientation()
		{
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), default);
			var equator = _coordinates.FromGeodetic(new LatPos(0, 0, 0));
			Assert.That(equator.x, Is.EqualTo(6378137).Within(0.001));
			Assert.That(equator.y, Is.EqualTo(0).Within(0.001));
			Assert.That(equator.z, Is.EqualTo(0).Within(0.001));
			var expected = new LatPos(Math.PI / 3, Math.PI / 12, 123.456);
			var roundTrip = _coordinates.ToGeodetic(_coordinates.FromGeodetic(expected));
			Assert.That(roundTrip.Latitude, Is.EqualTo(expected.Latitude).Within(1e-9));
			Assert.That(roundTrip.Longitude, Is.EqualTo(expected.Longitude).Within(1e-9));
			Assert.That(roundTrip.Altitude, Is.EqualTo(expected.Altitude).Within(0.001));
			using (var system = new CoordinateSystem(
				Datum.GRS80_ELLIPSOID, FlatGaussProjection.SWEREF99, CoordinateType.PROJECTED))
				_coordinates.Configure(system.AsString(), default);
			var projected = _coordinates.FromGeodetic(expected);
			var orientation = _coordinates.Orientation(projected);
			Assert.That(orientation.Determinant(), Is.EqualTo(1).Within(1e-5));
			Assert.That(orientation.GetCol(0).Length(), Is.EqualTo(1).Within(1e-5));
			Assert.That(orientation.GetCol(1).Length(), Is.EqualTo(1).Within(1e-5));
			Assert.That(orientation.GetCol(2).Length(), Is.EqualTo(1).Within(1e-5));
			var before = _coordinates.Definition;
			Assert.Throws<ArgumentException>(() => _coordinates.Configure("not a coordinate system", default));
			Assert.That(_coordinates.Definition, Is.EqualTo(before));
		}

		private Group Group(string name)
		{
			NodeLock.WaitLockEdit();
			try
			{
				var group = new Group(name);
				_nodes.Add(group);
				return group;
			}
			finally { NodeLock.UnLock(); }
		}

		private State EmptyState()
		{
			NodeLock.WaitLockEdit();
			try
			{
				var state = new State();
				_states.Add(state);
				return state;
			}
			finally { NodeLock.UnLock(); }
		}

		private static void WithForeignGraphLock(Action action, bool renderLock = false)
		{
			using (var entered = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			{
				Exception failure = null;
				var thread = new Thread(() =>
				{
					try
					{
						if (renderLock)
							NodeLock.WaitLockRender();
						else
							NodeLock.WaitLockEdit();
						try
						{
							entered.Set();
							release.Wait(2000);
						}
						finally { NodeLock.UnLock(); }
					}
					catch (Exception exception)
					{
						failure = exception;
						entered.Set();
					}
				}) { IsBackground = true };
				thread.Start();
				try
				{
					Assert.That(entered.Wait(5000), Is.True, "The lock-owning worker did not start.");
					Assert.That(failure, Is.Null);
					var timer = Stopwatch.StartNew();
					action();
					timer.Stop();
					TestContext.WriteLine($"Work while another thread owns SDK {(renderLock ? "render" : "edit")} access: {timer.Elapsed.TotalMilliseconds:F3} ms");
					Assert.That(timer.Elapsed.TotalMilliseconds, Is.LessThan(500),
						"Main-thread command flow must not wait for the foreign SDK graph owner.");
				}
				finally
				{
					release.Set();
					Assert.That(thread.Join(5000), Is.True, "The lock-owning worker did not exit.");
				}
				Assert.That(failure, Is.Null);
			}
		}

		private static SceneInstanceKey Key(Node node, ulong path)
		{
			return new SceneInstanceKey(node.GetNativeReference(), path);
		}

		private SceneInstanceHandle Handle(Node node, ulong path)
		{
			Assert.That(_scene.TryGetInstance(Key(node, path), out var handle), Is.True);
			return handle;
		}

		private static CommandBuffer Buffer(params SceneCommand[] commands)
		{
			NodeLock.WaitLockEdit();
			try
			{
				var buffer = new CommandBuffer();
				foreach (var command in commands)
				{
					buffer.AddCommand(command);
					command.Dispose();
				}
				return buffer;
			}
			finally { NodeLock.UnLock(); }
		}

		private void Enqueue(params SceneCommand[] commands)
		{
			var buffer = Buffer(commands);
			NodeLock.WaitLockEdit();
			try { _inbox.Receive(null, null, buffer); }
			finally { NodeLock.UnLock(); }
		}

		private void Pump()
		{
			_pump.Pump(1000, TimeSpan.FromSeconds(1));
		}

		private static CameraFrame Frame(Vec3D position)
		{
			return new CameraFrame(position, default, 100, 67, 1.5f, 0.3f, 50000, 600, 400);
		}
	}
}
