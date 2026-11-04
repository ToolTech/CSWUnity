//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: PlatformTests.cs
// Description	: Start, reference counting, message routing and release order of CSW.Unity.Platform
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Covered two start and stop cycles with two sessions sharing the platform.	(2.12.327)
// AMO	261104	Covered the message level, plain text and exceptions of SendMessage and SendException.	(2.12.327)
//
//*****************************************************************************

using System.Collections;
using System.Collections.Generic;
using GizmoSDK.GizmoBase;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CSW.Unity.Tests
{
	public sealed class PlatformTests
	{
		private static readonly string[] CycleSteps =
		{
			"CSW.Platform.Initialize",
			"Coordinate.Platform.Initialize",
			"DynamicLoaderManager.StopManager",
			"Coordinate.Platform.Uninitialize",
			"CSW.Platform.Uninitialize"
		};

		private readonly List<string> _steps = new List<string>();

		[SetUp]
		public void SetUp()
		{
			Assume.That(Platform.References, Is.Zero, "Another fixture holds a platform reference.");
			Platform.Stop();
			Assert.That(Platform.IsStarted, Is.False);
			_steps.Clear();
			Platform.StepTrace = _steps.Add;
		}

		[TearDown]
		public void TearDown()
		{
			Platform.StepTrace = null;
		}

		[UnityTest]
		public IEnumerator TwoSessionsShareThePlatformAcrossTwoStartStopCycles()
		{
			for (var cycle = 0; cycle < 2; cycle++)
			{
				_steps.Clear();
				var settings = new PlatformSettings
				{
					InstallMonitor = false,
					PreventScreenSleep = false
				};
				Assert.That(Platform.Start(settings), Is.True);
				Assert.That(Platform.Start(settings), Is.False, "A second start is a no-op.");
				var first = Platform.Acquire();
				var second = Platform.Acquire();
				Assert.That(Platform.References, Is.EqualTo(2));

				yield return null;

				Platform.Stop();
				Assert.That(Platform.IsStarted, Is.True, "Stop waits for the last session reference.");
				Assert.That(Platform.IsStopPending, Is.True);
				first.Dispose();
				first.Dispose();
				Assert.That(Platform.References, Is.EqualTo(1));
				Assert.That(_steps, Has.No.Member("CSW.Platform.Uninitialize"));
				second.Dispose();
				Assert.That(Platform.References, Is.Zero);
				Assert.That(Platform.IsStarted, Is.False);
				Assert.That(_steps, Is.EqualTo(CycleSteps), $"cycle {cycle}");
			}
		}

		[Test]
		public void DeliversGizmoMessagesAtOrAboveTheMessageLevelAcrossRestarts()
		{
			var received = new List<string>();
			Application.LogCallback capture = (text, stack, type) =>
			{
				if (text.Contains("CSW level filter"))
					received.Add($"{type}:{text}");
			};
			Application.logMessageReceived += capture;
			try
			{
				for (var cycle = 0; cycle < 2; cycle++)
				{
					received.Clear();
					Assert.That(Platform.Start(new PlatformSettings
					{
						InstallMonitor = false,
						PreventScreenSleep = false,
						MessageLevel = MessageLevel.WARNING
					}), Is.True);
					LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex($"CSW level filter warning {cycle}"));
					Message.Send("CSWTest", MessageLevel.NOTICE, $"CSW level filter notice {cycle}");
					Message.Send("CSWTest", MessageLevel.WARNING, $"CSW level filter warning {cycle}");
					Platform.Stop();

					Assert.That(received, Has.Count.EqualTo(1), $"Cycle {cycle}: only the warning passes the level.");
					Assert.That(received[0], Does.StartWith("Warning:"));
				}
			}
			finally
			{
				Application.logMessageReceived -= capture;
				Platform.Stop();
			}
		}

		[Test]
		public void SendsCswDiagnosticsUnformattedOnlyAtOrAboveTheMessageLevel()
		{
			var received = new List<string>();
			Application.LogCallback capture = (text, stack, type) =>
			{
				if (text.Contains("CSW diagnostic"))
					received.Add($"{type}:{text}");
			};
			Application.logMessageReceived += capture;
			try
			{
				Assert.That(Platform.Start(new PlatformSettings
				{
					InstallMonitor = false,
					PreventScreenSleep = false,
					MessageLevel = MessageLevel.WARNING
				}), Is.True);
				LogAssert.Expect(LogType.Warning, "CSW diagnostic warning 100% {0} %s");
				LogAssert.Expect(LogType.Error,
					new System.Text.RegularExpressions.Regex("InvalidOperationException: CSW diagnostic exception"));
				LogAssert.Expect(LogType.Warning, "CSW diagnostic after stop");
				Platform.SendMessage(MessageLevel.DEBUG, "CSW diagnostic debug");
				Platform.SendMessage(MessageLevel.NOTICE, "CSW diagnostic notice");
				Platform.SendMessage(MessageLevel.WARNING, "CSW diagnostic warning 100% {0} %s");
				try
				{
					throw new System.InvalidOperationException("CSW diagnostic exception");
				}
				catch (System.InvalidOperationException exception)
				{
					Platform.SendException(exception);
				}
				Platform.Stop();
				Platform.SendMessage(MessageLevel.NOTICE, "CSW diagnostic notice after stop");
				Platform.SendMessage(MessageLevel.WARNING, "CSW diagnostic after stop");

				Assert.That(received, Has.Count.EqualTo(3), string.Join("\n", received));
				Assert.That(received[0], Is.EqualTo("Warning:CSW diagnostic warning 100% {0} %s"),
					"The text must be sent without GizmoSDK formatting.");
				Assert.That(received[1], Does.StartWith("Error:System.InvalidOperationException: CSW diagnostic exception"));
				Assert.That(received[1], Does.Contain(nameof(SendsCswDiagnosticsUnformattedOnlyAtOrAboveTheMessageLevel)),
					"The exception must carry its stack trace.");
				Assert.That(received[2], Is.EqualTo("Warning:CSW diagnostic after stop"),
					"Diagnostics must still be routed, at the last message level, without a started platform.");
			}
			finally
			{
				Application.logMessageReceived -= capture;
				Platform.Stop();
				Message.SetMessageLevel(MessageLevel.NOTICE);
			}
		}

		[Test]
		public void RoutesGizmoMessagesToTheUnityLogWithTheirSeverity()
		{
			LogAssert.Expect(LogType.Log, "routed notice");
			LogAssert.Expect(LogType.Warning, "routed warning");
			LogAssert.Expect(LogType.Error, "routed fatal");
			LogAssert.Expect(LogType.Assert, "routed assert");
			Platform.RouteMessage("CSWTest", MessageLevel.NOTICE, "routed notice");
			Platform.RouteMessage("CSWTest", MessageLevel.WARNING, "routed warning");
			Platform.RouteMessage("CSWTest", MessageLevel.FATAL, "routed fatal");
			Platform.RouteMessage("CSWTest", MessageLevel.ASSERT, "routed assert");
		}

		[Test]
		public void AcquireStartsTheDefaultPlatformAndStopReleasesIt()
		{
			using (Platform.Acquire())
			{
				Assert.That(Platform.IsStarted, Is.True);
				Assert.That(Platform.Settings.LocalRegistry, Is.EqualTo("config.xml"));
			}
			Assert.That(Platform.IsStarted, Is.True, "Releasing a session does not stop the platform.");
			Platform.Stop();
			Assert.That(Platform.IsStarted, Is.False);
			Assert.That(_steps, Is.EqualTo(CycleSteps));
		}
	}
}
