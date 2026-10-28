//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswMapStreamerHostTests.cs
// Description	: Map URL precedence for the command-driven streaming host
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261028	Covered explicit, component and config.xml map URL precedence.	(2.12.327)
//
//*****************************************************************************

using System;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Csw;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
	public sealed class CswMapStreamerHostTests
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
			Assert.That(CswMapStreamerHost.ResolveMapUrl("explicit.gzd", "component.gzd", Configured("config.gzd")),
				Is.EqualTo("explicit.gzd"));
			Assert.That(_configurationReads, Is.Zero);
		}

		[Test]
		public void ComponentUrlWinsWithoutReadingConfiguration()
		{
			Assert.That(CswMapStreamerHost.ResolveMapUrl(null, "component.gzd", Configured("config.gzd")),
				Is.EqualTo("component.gzd"));
			Assert.That(CswMapStreamerHost.ResolveMapUrl(" ", "component.gzd", Configured("config.gzd")),
				Is.EqualTo("component.gzd"));
			Assert.That(_configurationReads, Is.Zero);
		}

		[Test]
		public void BlankComponentUrlUsesConfiguration()
		{
			Assert.That(CswMapStreamerHost.ResolveMapUrl(null, "", Configured("config.gzd")),
				Is.EqualTo("config.gzd"));
			Assert.That(CswMapStreamerHost.ResolveMapUrl("", "  ", Configured("config.gzd")),
				Is.EqualTo("config.gzd"));
			Assert.That(_configurationReads, Is.EqualTo(2));
		}

		[Test]
		public void MissingUrlFailsWithBothSourcesNamed()
		{
			foreach (var configured in new[] { null, "", " " })
			{
				var exception = Assert.Throws<InvalidOperationException>(() =>
					CswMapStreamerHost.ResolveMapUrl(null, null, Configured(configured)));
				Assert.That(exception.Message, Does.Contain("MapUrl").And.Contain("config.xml"));
			}
			Assert.That(_configurationReads, Is.EqualTo(3));
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
