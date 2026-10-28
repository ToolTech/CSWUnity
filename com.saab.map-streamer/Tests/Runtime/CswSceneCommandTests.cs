//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswSceneCommandTests.cs
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
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using ManualResetEventSlim = System.Threading.ManualResetEventSlim;
using Stopwatch = System.Diagnostics.Stopwatch;
using Thread = System.Threading.Thread;
using CSW;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Csw;
using Saab.Unity.Extensions;
using UnityEngine;
using Camera = UnityEngine.Camera;
using Quaternion = UnityEngine.Quaternion;
using Object = UnityEngine.Object;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
	public sealed class CswSceneCommandTests
	{
		private GameObject _parent;
		private GameObject _cameraObject;
		private SceneCoordinateContext _coordinates;
		private UnityScene _scene;
		private SceneCommandInbox _inbox;
		private SceneCommandPump _pump;
		private readonly List<Node> _nodes = new List<Node>();

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
			var camera = _cameraObject.AddComponent<Camera>();
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
				foreach (var node in _nodes)
					node.ReleaseAlreadyLocked();
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
			Assert.That(parentHandle.gameObject.activeSelf, Is.False);
			Assert.That(childHandle.IsLocallyActive, Is.True);
			Assert.That(childHandle.gameObject.activeSelf, Is.False);
			Assert.That(childHandle.gameObject.activeInHierarchy, Is.False);
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
			Assert.That(a.gameObject.activeSelf, Is.False);
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
					Matrix4x4.Translate(new Vector3(10, 20, 30)).ToMatrix4()),
				new SceneCommandActivation(child, 2, Activation.OFF),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandEndFrame(42));

			Assert.That(_pump.Pump(2, TimeSpan.FromSeconds(1)), Is.EqualTo(2));
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(Vector3.zero));
			Assert.That(rootHandle.gameObject.activeSelf, Is.False);
			Assert.That(childHandle.IsLocallyActive, Is.True);
			Assert.That(_scene.HasCommittedFrame, Is.False);
			Pump();
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(new Vector3(10, 20, 30)));
			Assert.That(rootHandle.gameObject.activeSelf, Is.True);
			Assert.That(childHandle.gameObject.activeSelf, Is.False);
			Assert.That(_scene.CommittedFrame.GlobalPosition.x, Is.EqualTo(global.x));
			Assert.That(_scene.CommittedFrame.VerticalFieldOfView, Is.EqualTo(67).Within(0.001));

			Enqueue(new SceneCommandActivation(root, 1, Activation.OFF));
			Pump();
			Assert.That(rootHandle.gameObject.activeSelf, Is.False);
			_pump.ExpectFrame(43, frame);
			Enqueue(
				new SceneCommandStartFrame(43, 101, global),
				new SceneCommandUpdateNode(root, 1, null, 0, null, Matrix4x4.identity.ToMatrix4()),
				new SceneCommandActivation(root, 1, Activation.ON),
				new SceneCommandEndFrame(43));
			Pump();
			Assert.That(Handle(root, 1), Is.SameAs(rootHandle));
			Assert.That(rootHandle.transform.localPosition, Is.EqualTo(Vector3.zero));
			Assert.That(rootHandle.transform.localScale, Is.EqualTo(Vector3.one));
			Assert.That(rootHandle.transform.localRotation, Is.EqualTo(Quaternion.identity));
			Assert.That(childHandle.gameObject.activeSelf, Is.False);
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

		[TestCase(false)]
		[TestCase(true)]
		public void FrameInputContainsExactlyOneOrderedValueOnlyRefreshBatch(bool omniView)
		{
			var camera = new CameraRelativeFrame(new Vec3D(123.25, 456.5, 789.75),
				new Vec3(12, 34, 56), 123.5, 67, 1.5f, 0.3f, 50000, 600, 400);
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
						Assert.That(settings.InfiniteFar, Is.False);
					}
					using (var command = buffer.GetCommand())
					{
						Assert.That(command, Is.TypeOf<SceneCommandSetLodFactor>());
						Assert.That(((SceneCommandSetLodFactor)command).LodFactor, Is.EqualTo(camera.LodFactor));
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
			using (var manager = new MapManager(_scene, Array.Empty<GeometryBuilderBase>()))
			{
				Assert.That(manager.OmniView, Is.True);
				Assert.That(manager.SynchronizeFrames, Is.False);
				Assert.That(manager.FrameWaitTimeout, Is.GreaterThan(TimeSpan.Zero));
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
					Matrix4x4.Translate(new Vector3(10, 20, 30)).ToMatrix4()),
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
					Matrix4x4.Translate(new Vector3(10, 20, 30)).ToMatrix4()),
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
				new SceneCommandUpdateNode(parent, 1, null, 0, null, parentMatrix.ToMatrix4()),
				new SceneCommandUpdateNode(child, 2, parent, 1, null, childMatrix.ToMatrix4()));
			Pump();
			var parentHandle = Handle(parent, 1);
			var childHandle = Handle(child, 2);
			Assert.That(parentHandle.transform.position, Is.EqualTo(new Vector3(3, 5, -7)));
			var vertex = new Vector3(31, 37, 41);
			var expected = (_coordinates.Axes.Basis * parentMatrix * childMatrix).MultiplyPoint3x4(vertex);
			Assert.That(Vector3.Distance(childHandle.transform.TransformPoint(vertex), expected), Is.LessThan(0.0001));
			var normal = new Vector3(0.1f, 0.4f, 0.7f).normalized;
			var expectedNormal = (_coordinates.Axes.Basis * parentMatrix * childMatrix).inverse.transpose.MultiplyVector(normal).normalized;
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
			var rotation = Quaternion.Euler(10, 20, 30);
			_scene.Camera.transform.rotation = rotation;
			bridge.PresentPose();
			Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
			bridge.SetPose(default, Quaternion.identity);
			bridge.ResetPose();
			bridge.PresentPose();
			Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05f));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CameraRotationDoesNotWaitForNativeEndFrameOrGraphAccess(bool renderLock)
		{
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
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
					bridge.PresentPose();
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates) { SynchronizeFrames = true };
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
					bridge.PresentPose();
					Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
					Assert.That(Quaternion.Angle(_scene.Camera.transform.rotation, rotation), Is.LessThan(0.05));
					var expected = Matrix4x4.Perspective(submitted.VerticalFieldOfView, submitted.Aspect,
						submitted.NearDistance, submitted.FarDistance);
					for (var i = 0; i < 16; i++)
						Assert.That(_scene.Camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001));
				}, renderLock);
				Assert.That(bridge.GlobalPosition, Is.EqualTo(position + new Vec3D(50, 60, 70)));
				Assert.That(Quaternion.Angle(bridge.Rotation, latest), Is.LessThan(0.05));
				bridge.SynchronizeFrames = false;
				bridge.PresentPose();
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates) { SynchronizeFrames = true };
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
				bridge.PresentPose();
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
			var position = new Vec3D(6378137, 123.5, 12.25);
			bridge.SetPose(position, Quaternion.identity);
			var worldOffset = Shader.GetGlobalVector("_WorldOffset");
			var localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			try
			{
				bridge.Commit(bridge.Sample(100));
				bridge.SetPose(position + new Vec3D(1, 2, 3), Quaternion.identity);
				bridge.PresentPose();
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(new Vector3(1, 2, -3)));
				bridge.ResetPose();
				var sentinel = new Vector3(5, 7, 11);
				_scene.Camera.transform.position = sentinel;
				bridge.SetPose(new Vec3D(-1000000000, 2000000000, -3000000000), Quaternion.Euler(0, 45, 0));
				bridge.PresentPose();
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
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
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
				var frame = new CameraRelativeFrame(nextReference, default, 101, 60, 1, 1, 10000, 640, 480);
				_pump.ExpectFrame(43, frame);
				Enqueue(new SceneCommandStartFrame(43, 101, nextReference),
					new SceneCommandUpdateNode(first, 1, null, 0, null,
						Matrix4x4.Translate(firstOffset - shift).ToMatrix4()),
					new SceneCommandUpdateNode(second, 2, null, 0, null,
						Matrix4x4.Translate(secondOffset - shift).ToMatrix4()),
					new SceneCommandEndFrame(43));
				Assert.That(_pump.Pump(3, TimeSpan.FromSeconds(1)), Is.EqualTo(3));
				var committedWorldOffset = Shader.GetGlobalVector("_WorldOffset");
				WithForeignGraphLock(() =>
				{
					Assert.That(_pump.Pump(128, TimeSpan.FromSeconds(1)), Is.Zero);
					for (var step = 1; step <= 90; step++)
					{
						var delta = new Vec3D(step * 0.03125, step * 0.0625, step * 0.125);
						var rotation = Quaternion.Euler(10, step, 0);
						bridge.SetPose(reference + delta, rotation);
						bridge.PresentPose();
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
			var submitted = new CameraRelativeFrame(default, default, 1, 67, 2, 1, 10000, 640, 480);
			var committed = submitted.WithNativePose(123, 2, new Vec3D(1, 2, 3), new Vec3(4, 5, 6));
			Assert.That(committed.VerticalFieldOfView, Is.EqualTo(67));
			Assert.That(committed.HorizontalFieldOfView, Is.EqualTo(submitted.HorizontalFieldOfView));
			Assert.That(committed.Aspect, Is.EqualTo(2));
			Assert.That(committed.GlobalPosition.x, Is.EqualTo(1));
			Assert.That(committed.Hpr.y, Is.EqualTo(5));
			Assert.That(committed.RefreshId, Is.EqualTo(123));
			Assert.Throws<ArgumentException>(() =>
				new CameraRelativeFrame(new Vec3D(double.NaN, 0, 0), default, 1, 60, 1, 1, 100, 1, 1));
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
					Matrix4x4.Translate(new Vector3(17, 23, 31)).ToMatrix4()),
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
			Assert.That(Handle(root, 1).gameObject.activeInHierarchy, Is.True);
			_pump.ExpectFrame(44, Frame(default));
			Enqueue(
				new SceneCommandStartFrame(44, 0, default),
				new SceneCommandNewNode(child, 2, root, 1, null),
				new SceneCommandActivation(child, 2, Activation.OFF),
				new SceneCommandEndFrame(44));
			_pump.Pump(2, TimeSpan.FromSeconds(1));
			var branch = Handle(child, 2);
			Assert.That(branch.IsReady, Is.True);
			Assert.That(branch.IsPresented, Is.False);
			Assert.That(branch.gameObject.activeInHierarchy, Is.False);
			Pump();
			Assert.That(branch.IsPresented, Is.True);
			Assert.That(branch.IsLocallyActive, Is.False);
			Assert.That(branch.gameObject.activeInHierarchy, Is.False);
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
		public void AxisMappingAndGlobalDoubleSubtractionHaveNumericReferenceValues()
		{
			var axes = _coordinates.Axes;
			Assert.That(axes.ToUnityDirection(new Vec3(1, 0, 0)), Is.EqualTo(Vector3.right));
			Assert.That(axes.ToUnityDirection(new Vec3(0, 1, 0)), Is.EqualTo(Vector3.up));
			Assert.That(axes.ToUnityDirection(new Vec3(0, 0, 1)), Is.EqualTo(Vector3.back));
			var direction = new Vector3(1.25f, -2.5f, 3.75f);
			Assert.That(axes.ToUnityDirection(axes.ToNativeDirection(direction)), Is.EqualTo(direction));
			Assert.That(axes.ToUnityPosition(
				new Vec3D(1000000000.25, 2000000000.5, 3000000000.75),
				new Vec3D(1000000000, 2000000000, 3000000000)),
				Is.EqualTo(new Vector3(0.25f, 0.5f, -0.75f)));
			Assert.That(axes.ToUnityNormal(new Vec3(0, 0, 4)), Is.EqualTo(Vector3.back));
			Assert.That(axes.ReversesHandedness, Is.True);
			var rotation = Quaternion.Euler(21, 37, -13);
			Assert.That(Quaternion.Angle(rotation, axes.CameraRotation(axes.CameraHpr(rotation))), Is.LessThan(0.02f));
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

		private static CameraRelativeFrame Frame(Vec3D position)
		{
			return new CameraRelativeFrame(position, default, 100, 67, 1.5f, 0.3f, 50000, 600, 400);
		}
	}
}
