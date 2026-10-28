//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneManagerSettings.cs
// Description	: Native CSW SceneManager traversal and build settings
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261028	Moved the command-driven SceneManager settings out of the removed legacy SceneManager component.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{
	/// <summary>Settings applied to the native CSW SceneManager and its Unity build budget.</summary>
	[Serializable]
	public struct SceneManagerSettings
	{
		[Tooltip("Maximum time in seconds spent per Unity frame on building scene resources.")]
		public double MaxBuildTime;
		[Tooltip("Number of native dynamic loader threads. config.xml SceneManager/DynamicLoaders overrides this value when configuration is read.")]
		public byte DynamicLoaders;
		public IntersectMaskValue IntersectMask;
		[Tooltip("Traverse and select LODs in all directions instead of only the SceneManager camera frustum. Enabled by default, like CSWUnreal; frustum traversal leaves map outside the camera frustum unloaded. Unity rendering still uses its own culling.")]
		public bool OmniView;
		[Tooltip("Finish the matching native frame and present its camera pose/projection before rendering. May wait and exceed the per-tick build budget.")]
		public bool SynchronizeFrames;

		public static readonly SceneManagerSettings Default = new SceneManagerSettings
		{
			MaxBuildTime = 0.012,
			DynamicLoaders = 4,
			IntersectMask = IntersectMaskValue.ALL,
			OmniView = true,
			SynchronizeFrames = false,
		};
	}
}
