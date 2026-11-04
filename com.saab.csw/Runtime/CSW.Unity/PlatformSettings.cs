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
// File			: PlatformSettings.cs
// Module		: CSW.Unity
// Description	: Settings for the CSW platform startup in Unity
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
// AMO	261104	Created file from the Initializer settings.	(2.12.327)
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;
using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Settings that Platform.Start applies once per platform start.</summary>
	[Serializable]
	public sealed class PlatformSettings
	{
		[Tooltip("Lowest GizmoSDK message level that is routed to the Unity log.")]
		public MessageLevel MessageLevel = MessageLevel.PERF_DEBUG;
		[Tooltip("Local registry file. When empty, no local registry is set.")]
		public string LocalRegistry = "config.xml";
		[Tooltip("Install the GizmoSDK monitor. It is installed at most once per process.")]
		public bool InstallMonitor = true;
		[Tooltip("Monitor URL. When empty, the platform default URL is used.")]
		public string MonitorUrl = "";
		[Tooltip("Run a performance tracer for all threads.")]
		public bool EnableTracing;
		[Tooltip("Trace native allocations and publish memory values to the monitor. Heavy load.")]
		public bool EnableMemoryMonitoring;
		[Tooltip("Publish the frame rate to the monitor.")]
		public bool EnableFrameRateMonitoring;
		[Tooltip("Keep the screen awake while the platform is started.")]
		public bool PreventScreenSleep = true;
		[Tooltip("Android: use the application files folder for the GizmoSDK default registry.")]
		public bool AndroidDefaultRegistry = true;
		[Tooltip("Android: hold a Wi-Fi multicast lock while the platform is started.")]
		public bool AndroidMulticastLock = true;

		/// <summary>Returns an independent copy.</summary>
		public PlatformSettings Clone()
		{
			return (PlatformSettings)MemberwiseClone();
		}
	}
}
