//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: MapControlTests.cs
// Description	: Map URL precedence of CSW.Unity.MapControl
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261028	Covered explicit, component and config.xml map URL precedence.	(2.12.327)
// AMO	261103	Renamed from CswMapStreamerHostTests into the Csw test namespace.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.Tests.	(2.12.327)
// AMO	261104	Renamed from MapStreamerHostTests for CSW.Unity.MapControl.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;
using System;
using NUnit.Framework;
using UnityEngine;

namespace CSW.Unity.Tests
{
	public sealed class MapControlTests
	{
		private int _configurationReads;

		[SetUp]
		public void SetUp()
		{
			_configurationReads = 0;
		}

		[Test]
		public void ExplicitUrlWinsWithoutReadingConfiguration()
		{
			Assert.That(MapControl.ResolveMapUrl("explicit.gzd", "component.gzd", Configured("config.gzd")),
				Is.EqualTo("explicit.gzd"));
			Assert.That(_configurationReads, Is.Zero);
		}

		[Test]
		public void ComponentUrlWinsWithoutReadingConfiguration()
		{
			Assert.That(MapControl.ResolveMapUrl(null, "component.gzd", Configured("config.gzd")),
				Is.EqualTo("component.gzd"));
			Assert.That(MapControl.ResolveMapUrl(" ", "component.gzd", Configured("config.gzd")),
				Is.EqualTo("component.gzd"));
			Assert.That(_configurationReads, Is.Zero);
		}

		[Test]
		public void BlankComponentUrlUsesConfiguration()
		{
			Assert.That(MapControl.ResolveMapUrl(null, "", Configured("config.gzd")),
				Is.EqualTo("config.gzd"));
			Assert.That(MapControl.ResolveMapUrl("", "  ", Configured("config.gzd")),
				Is.EqualTo("config.gzd"));
			Assert.That(_configurationReads, Is.EqualTo(2));
		}

		[Test]
		public void MissingUrlFailsWithBothSourcesNamed()
		{
			foreach (var configured in new[] { null, "", " " })
			{
				var exception = Assert.Throws<InvalidOperationException>(() =>
					MapControl.ResolveMapUrl(null, null, Configured(configured)));
				Assert.That(exception.Message, Does.Contain("MapUrl").And.Contain("config.xml"));
			}
			Assert.That(_configurationReads, Is.EqualTo(3));
		}

		[Test]
		public void MapControlWithoutSessionReportsNoMapAndStopsIdempotently()
		{
			var gameObject = new GameObject("Map control");
			try
			{
				var control = gameObject.AddComponent<MapControl>();
				control.StopStreaming();
				control.StopStreaming();
				Assert.That(control.State, Is.EqualTo(MapControlState.Stopped));
				Assert.That(control.LoadedMapUrl, Is.Null);
				Assert.That(control.Error, Is.Null);
				Assert.That(control.CompletedFrames, Is.Zero);
				Assert.That(control.Camera, Is.Null);
				Assert.Throws<InvalidOperationException>(() => control.OpenMap("map.gzd"));
				Assert.Throws<InvalidOperationException>(() => control.StartStreaming("map.gzd"),
					"Starting without a view camera must fail before acquiring the platform.");
				Assert.That(control.State, Is.EqualTo(MapControlState.Stopped));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		private Func<string> Configured(string url)
		{
			return () =>
			{
				_configurationReads++;
				return url;
			};
		}
	}
}
