//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: FactoryTests.cs
// Description	: Factory registry and most specific type selection tests
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Covered CSW.Unity.Factory registration, removal and selection.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CSW.Unity.Tests
{
	public sealed class FactoryTests
	{
		private sealed class TestFactory : NodeFactory
		{
		}

		private readonly List<Node> _nodes = new List<Node>();

		[OneTimeSetUp]
		public void InitializeNative()
		{
			Assert.That(CSW.Platform.Initialize(), Is.True);
		}

		[OneTimeTearDown]
		public void UninitializeNative()
		{
			Assert.That(CSW.Platform.Uninitialize(), Is.True);
		}

		[TearDown]
		public void TearDown()
		{
			NodeLock.WaitLockEdit();
			try
			{
				foreach (var node in _nodes)
					node.ReleaseAlreadyLocked();
				_nodes.Clear();
			}
			finally { NodeLock.UnLock(); }
		}

		[Test]
		public void BuiltInFactoriesRealizeTheCswUnrealTypesAndTheNotifiedKinds()
		{
			Assert.That(Factory.Get("gzNode"), Is.TypeOf<NodeFactory>());
			Assert.That(Factory.Get("gzGroup"), Is.SameAs(Factory.Get("gzNode")));
			Assert.That(Factory.Get("gzTransform"), Is.TypeOf<TransformFactory>());
			Assert.That(Factory.Get("gzRoiNode"), Is.TypeOf<RoiNodeFactory>());
			Assert.That(Factory.Get("gzGeometry"), Is.TypeOf<GeometryFactory>());
			Assert.That(Factory.Get("gzLod"), Is.TypeOf<LodFactory>());
			Assert.That(Factory.Get("gzCrossboard"), Is.TypeOf<CrossboardFactory>());
			Assert.That(Factory.Get("gzDynamicLoader"), Is.TypeOf<DynamicLoaderFactory>());
			Assert.That(Factory.Get("gzRefNode"), Is.Null);
		}

		[Test]
		public void SelectsTheFactoryOfTheMostSpecificTypeThatHasOne()
		{
			Assert.That(Factory.Select(Create(() => new Group("Group"))), Is.SameAs(Factory.Get("gzGroup")));
			Assert.That(Factory.Select(Create(() => new GizmoSDK.Gizmo3D.Transform("Transform"))),
				Is.SameAs(Factory.Get("gzTransform")));
			Assert.That(Factory.Select(Create(() => new RoiNode("RoiNode"))), Is.SameAs(Factory.Get("gzRoiNode")));
			Assert.That(Factory.Select(Create(() => new Roi("Roi"))), Is.SameAs(Factory.Get("gzTransform")));
			Assert.That(Factory.Select(Create(() => new Geometry("Geometry"))), Is.SameAs(Factory.Get("gzGeometry")));
			Assert.That(Factory.Select(Create(() => new Lod("Lod"))), Is.SameAs(Factory.Get("gzLod")));
			Assert.That(Factory.Select(Create(() => new Crossboard("Crossboard"))),
				Is.SameAs(Factory.Get("gzCrossboard")));
			Assert.That(Factory.Select(Create(() => new DynamicLoader("Loader"))),
				Is.SameAs(Factory.Get("gzDynamicLoader")));
			Assert.That(Factory.Select(Create(() => new RefNode("RefNode"))), Is.SameAs(Factory.Get("gzGroup")));
			Assert.Throws<ArgumentNullException>(() => Factory.Select(null));
		}

		[Test]
		public void RegistrationAndRemovalChangeTheSelectionOfLaterOccurrences()
		{
			var reference = Create(() => new RefNode("RefNode"));
			Assert.That(Factory.Select(reference), Is.SameAs(Factory.Get("gzGroup")));
			var factory = new TestFactory();
			try
			{
				Assert.That(Factory.Register("gzRefNode", factory), Is.True);
				Assert.That(Factory.Get("gzRefNode"), Is.SameAs(factory));
				Assert.That(Factory.Select(reference), Is.SameAs(factory));
				LogAssert.Expect(LogType.Warning, "Multiple CSW.Unity.Factory registrations for type (gzRefNode)");
				Assert.That(Factory.Register("gzRefNode", new TestFactory()), Is.False);
				Assert.That(Factory.Get("gzRefNode"), Is.SameAs(factory));
			}
			finally
			{
				Assert.That(Factory.Unregister("gzRefNode"), Is.True);
			}
			Assert.That(Factory.Get("gzRefNode"), Is.Null);
			Assert.That(Factory.Select(reference), Is.SameAs(Factory.Get("gzGroup")));
			LogAssert.Expect(LogType.Warning, "No CSW.Unity.Factory registered for type (gzRefNode)");
			Assert.That(Factory.Unregister("gzRefNode"), Is.False);
			Assert.Throws<ArgumentException>(() => Factory.Register("", factory));
			Assert.Throws<ArgumentNullException>(() => Factory.Register("gzRefNode", null));
		}

		private Node Create(Func<Node> create)
		{
			NodeLock.WaitLockEdit();
			try
			{
				var node = create();
				_nodes.Add(node);
				return node;
			}
			finally { NodeLock.UnLock(); }
		}
	}
}
