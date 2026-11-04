//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: Platform.cs
// Module		: CSW.Unity
// Description	: Process-wide CSW platform startup in Unity
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created file from Initializer and the platform part of MapStreamerHost.	(2.12.327)
// AMO	261104	Added SendMessage and SendException that send CSWUnity diagnostics at their message level.	(2.12.327)
//
//******************************************************************************

using System;
using CSW.Unity.StreamingMap;
using GizmoSDK.GizmoBase;
using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Owns all platform startup of a Unity application that uses CSW.</summary>
	/// <remarks>
	/// Start initializes GizmoBase, message routing, the registries, the monitor and the optional platform services;
	/// a second Start is a no-op. Sessions call Acquire instead of initializing platforms; the first reference
	/// initializes CSW.Platform and the Coordinate platform, and the last released reference stops the dynamic
	/// loaders and releases the Coordinate platform before CSW.Platform. Acquire starts the platform with default
	/// settings when no caller has started it. Stop releases the started services once no reference remains.
	/// The GizmoBase reference taken by the first Start is kept for the process, as by the former Initializer,
	/// because GizmoSDK does not register its message callback again after GizmoBase reaches zero references.
	/// Call every member on the Unity main thread.
	/// </remarks>
	public static class Platform
	{
		private const int TracerStartUpdate = 10;
		private const int MonitorInterval = 30;
		// GZ_MESSAGE_ENCODING_NO_TRANSLATE | GZ_MESSAGE_ENCODING_NO_FORMAT: the text is sent as is.
		private const MessageLevel PlainText = (MessageLevel)0x0600;

		/// <summary>Sender of the diagnostic messages of CSWUnity.</summary>
		internal const string MessageSender = "CSW.Unity";

		private static readonly object _routingLock = new object();
		private static bool _routing;

		private static PlatformSettings _settings;
		private static bool _started;
		// Process-lifetime GizmoBase reference; see the class remarks.
		private static bool _baseInitialized;
		private static bool _stopRequested;
		private static bool _monitorInstalled;
		private static int _references;
		private static int _previousSleepTimeout;
		private static int _updates;
		private static double _frameTime;
		private static double _frameDuration;
		private static PerformanceTracer _tracer;
#if UNITY_ANDROID
		private static AndroidJavaObject _multicastLock;
#endif

		/// <summary>Receives the name of each platform initialization and release step; used by tests.</summary>
		internal static Action<string> StepTrace;

		/// <summary>True between a Start and the release of its services.</summary>
		public static bool IsStarted => _started;

		/// <summary>Number of session references to CSW.Platform and the Coordinate platform.</summary>
		public static int References => _references;

		/// <summary>True when Stop has been called and the services are released when the last reference is.</summary>
		public static bool IsStopPending => _stopRequested;

		/// <summary>Copy of the settings applied by the current start, or null when stopped.</summary>
		public static PlatformSettings Settings => _settings?.Clone();

		/// <summary>Starts the platform services with the given settings, or the defaults when null.</summary>
		/// <returns>True when this call started the platform; false when it was already started.</returns>
		/// <remarks>A Start while a Stop is pending cancels the pending stop and keeps the applied settings.</remarks>
		public static bool Start(PlatformSettings settings = null)
		{
			if (_started)
			{
				_stopRequested = false;
				return false;
			}
			var applied = settings?.Clone() ?? new PlatformSettings();
			if (!_baseInitialized)
			{
				if (!GizmoSDK.GizmoBase.Platform.Initialize())
					throw new InvalidOperationException("GizmoBase platform initialization failed.");
				_baseInitialized = true;
			}
			_settings = applied;
			_started = true;
			_stopRequested = false;
			_updates = 0;
			_frameTime = 0;
			_frameDuration = 0;
			try
			{
				StartRouting();
				Message.SetMessageLevel(applied.MessageLevel);
				_previousSleepTimeout = Screen.sleepTimeout;
				if (applied.PreventScreenSleep)
					Screen.sleepTimeout = SleepTimeout.NeverSleep;
				if (applied.EnableMemoryMonitoring)
					MemoryControl.DebugMem(true);
				if (applied.InstallMonitor && !_monitorInstalled)
				{
					InstallMonitor(applied.MonitorUrl);
					_monitorInstalled = true;
				}
				StartAndroidServices(applied);
				if (!string.IsNullOrWhiteSpace(applied.LocalRegistry))
					KeyDatabase.SetLocalRegistry(applied.LocalRegistry);
			}
			catch
			{
				StopServices();
				throw;
			}
			return true;
		}

		/// <summary>Requests a stop; the services are released now, or when the last session reference is released.</summary>
		public static void Stop()
		{
			if (!_started)
				return;
			_stopRequested = true;
			if (_references == 0)
				StopServices();
		}

		/// <summary>Returns a session reference that keeps CSW.Platform and the Coordinate platform initialized.</summary>
		/// <remarks>Dispose the reference after the session has released every native object it owns.</remarks>
		public static PlatformReference Acquire()
		{
			if (!_started)
				Start();
			if (_references == 0)
			{
				Trace("CSW.Platform.Initialize");
				if (!CSW.Platform.Initialize())
					throw new InvalidOperationException("CSW.Platform initialization failed.");
				Trace("Coordinate.Platform.Initialize");
				if (!GizmoSDK.Coordinate.Platform.Initialize())
				{
					CSW.Platform.Uninitialize();
					throw new InvalidOperationException("Coordinate platform initialization failed.");
				}
			}
			_references++;
			return new PlatformReference();
		}

		/// <summary>Publishes the enabled monitor values and starts the delayed tracer; call once per Unity update.</summary>
		public static void Update()
		{
			if (!_started)
				return;
			var time = GizmoSDK.GizmoBase.Time.SystemSeconds;
			if (_frameTime > 0)
				_frameDuration = 0.999 * _frameDuration + 0.001 * (time - _frameTime);
			_frameTime = time;
			_updates++;
			if (_settings.EnableTracing && _tracer == null && _updates >= TracerStartUpdate)
			{
				_tracer = new PerformanceTracer();
				_tracer.AddAll();
				_tracer.Run();
			}
			if (_updates % MonitorInterval != 0)
				return;
			if (_settings.EnableMemoryMonitoring)
			{
				GizmoSDK.GizmoBase.Monitor.AddValue("mem", (double)MemoryControl.GetAllocMem());
				GizmoSDK.GizmoBase.Monitor.AddValue("internal", (double)MemoryControl.GetAllocMem(0, 0, false, true));
				GizmoSDK.GizmoBase.Monitor.AddValue("dyn", (double)MemoryControl.GetAllocMem(66666));
			}
			if (_settings.EnableFrameRateMonitoring && _frameDuration > 0)
				GizmoSDK.GizmoBase.Monitor.AddValue("fps", 1 / _frameDuration);
		}

		internal static void Release()
		{
			if (_references <= 0)
				throw new InvalidOperationException("No platform reference is held.");
			var cleanup = new SceneCleanup();
			if (_references == 1)
			{
				cleanup.Attempt(() =>
				{
					Trace("DynamicLoaderManager.StopManager");
					GizmoSDK.Gizmo3D.DynamicLoaderManager.StopManager(true, 0, true);
				});
				cleanup.Attempt(() =>
				{
					Trace("Coordinate.Platform.Uninitialize");
					if (!GizmoSDK.Coordinate.Platform.Uninitialize())
						throw new InvalidOperationException("Coordinate platform shutdown failed.");
				});
				cleanup.Attempt(() =>
				{
					Trace("CSW.Platform.Uninitialize");
					if (!CSW.Platform.Uninitialize())
						throw new InvalidOperationException("CSW platform shutdown failed.");
				});
			}
			_references--;
			if (_references == 0 && _stopRequested)
				cleanup.Attempt(StopServices);
			cleanup.Complete("Platform release failed.");
		}

		private static void StopServices()
		{
			var cleanup = new SceneCleanup();
			cleanup.Attempt(() =>
			{
				var tracer = _tracer;
				_tracer = null;
				if (tracer != null)
				{
					tracer.Stop(true);
					tracer.Dispose();
				}
			});
			cleanup.Attempt(StopAndroidServices);
			cleanup.Attempt(() =>
			{
				if (_settings?.EnableMemoryMonitoring == true)
					MemoryControl.DebugMem(false);
			});
			cleanup.Attempt(() =>
			{
				if (_settings?.PreventScreenSleep == true)
					Screen.sleepTimeout = _previousSleepTimeout;
			});
			StopRouting();
			_settings = null;
			_started = false;
			_stopRequested = false;
			cleanup.Complete("Platform stop failed.");
		}

		private static void InstallMonitor(string url)
		{
			if (!string.IsNullOrWhiteSpace(url))
				GizmoSDK.GizmoBase.Monitor.InstallMonitor(url);
			else
#if UNITY_ANDROID
				GizmoSDK.GizmoBase.Monitor.InstallMonitor("udp::45454?nic=${wlan0}");
#else
				GizmoSDK.GizmoBase.Monitor.InstallMonitor();
#endif
		}

		private static void StartAndroidServices(PlatformSettings settings)
		{
#if UNITY_ANDROID
			if (settings.AndroidDefaultRegistry)
				KeyDatabase.SetDefaultRegistry($"/data/data/{Application.identifier}/files/gizmosdk.reg");
			if (settings.AndroidMulticastLock)
			{
				var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
				var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
				_multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "lock");
				_multicastLock.Call("setReferenceCounted", true);
				_multicastLock.Call("acquire");
				Message.Send(Message.GIZMOSDK, MessageLevel.DEBUG, "MultiCast Lock acquired");
			}
#endif
		}

		private static void StopAndroidServices()
		{
#if UNITY_ANDROID
			var multicastLock = _multicastLock;
			_multicastLock = null;
			if (multicastLock != null)
			{
				multicastLock.Call("release");
				multicastLock.Dispose();
			}
#endif
		}

		private static void Trace(string step)
		{
			StepTrace?.Invoke(step);
		}

		/// <summary>Sends a CSWUnity diagnostic message through GizmoSDK.</summary>
		/// <remarks>
		/// GizmoSDK drops a message below the current message level, which Start sets and which is NOTICE
		/// before any start, and routes the others to the Unity log with their severity. The text is neither
		/// formatted nor translated. CSWUnity code sends its diagnostics here instead of writing to the Unity
		/// log, so that the message level applies to them. Callable from any thread.
		/// </remarks>
		internal static void SendMessage(MessageLevel level, string message)
		{
			StartRouting();
			Message.Send(MessageSender, (level & MessageLevel.LEVEL_MASK) | PlainText, message);
		}

		/// <summary>Sends an exception, with its type and stack trace, as a FATAL CSWUnity diagnostic message.</summary>
		internal static void SendException(Exception exception)
		{
			SendMessage(MessageLevel.FATAL, exception.ToString());
		}

		private static void StartRouting()
		{
			lock (_routingLock)
			{
				if (_routing)
					return;
				Message.OnMessage += RouteMessage;
				_routing = true;
			}
		}

		private static void StopRouting()
		{
			lock (_routingLock)
			{
				if (!_routing)
					return;
				Message.OnMessage -= RouteMessage;
				_routing = false;
			}
		}

		internal static void RouteMessage(string sender, MessageLevel level, string message)
		{
			switch (level & MessageLevel.LEVEL_MASK)
			{
				case MessageLevel.WARNING:
					Debug.LogWarning(message);
					break;

				case MessageLevel.FATAL:
					Debug.LogError(message);
					break;

				case MessageLevel.ASSERT:
					Debug.LogAssertion(message);
					break;

				default:
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
					break;
			}
		}
	}

	/// <summary>A session reference to CSW.Platform and the Coordinate platform; dispose it once.</summary>
	public sealed class PlatformReference : IDisposable
	{
		private bool _released;

		internal PlatformReference()
		{
		}

		/// <summary>True after the reference has been released.</summary>
		public bool IsReleased => _released;

		/// <summary>Releases the reference; the last released reference releases the session platforms.</summary>
		public void Dispose()
		{
			if (_released)
				return;
			_released = true;
			Platform.Release();
		}
	}
}
