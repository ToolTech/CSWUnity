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
// AMO	261030	Added the synchronized frame wait timeout and described pipelined synchronization.	(2.12.327)
// AMO	261103	Added the infinite far plane, LOD factor and LOD reference field of view settings.	(2.12.327)
// AMO	261103	Moved into the Csw namespace with the other CSW SceneManager code.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.StreamingMap.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace CSW.Unity.StreamingMap
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
		[Tooltip("Lock rendering to processed native frames like CSWUnreal's synchronized tick: each update first finishes the frame of the refresh submitted in the previous update and presents its camera pose/projection, one update behind the requested camera, then submits the next refresh. May wait and exceed the per-tick build budget.")]
		public bool SynchronizeFrames;
		[Tooltip("Maximum time in seconds a synchronized update waits for the previous update's native frame. Expiration faults the map session instead of rendering a partial frame; a value that is not positive and finite faults synchronized updates.")]
		public double FrameWaitTimeout;
		[Tooltip("Use GizmoSDK's infinite far plane: the SceneManager does not cull beyond the far plane and the Unity camera renders with the same infinite projection. Off by default because depth-based effects such as shadows, fog and foliage occlusion still use the camera's finite far plane.")]
		public bool InfiniteFarPlane;
		[Tooltip("LOD factor submitted to the SceneManager; larger values keep higher detail farther away. A value that is not positive and finite is treated as 1.")]
		public float LodFactor;
		[Tooltip("Vertical field of view in degrees at which LodFactor applies unscaled. When set, the submitted factor is multiplied by the zoom tan(reference/2)/tan(fov/2), GizmoSDK's effective LOD factor, with and without OmniView. Zero disables zoom scaling, like CSWUnreal.")]
		[Range(0, 179)]
		public float LodReferenceFieldOfView;

		public static readonly SceneManagerSettings Default = new SceneManagerSettings
		{
			MaxBuildTime = 0.012,
			DynamicLoaders = 4,
			IntersectMask = IntersectMaskValue.ALL,
			OmniView = true,
			SynchronizeFrames = false,
			FrameWaitTimeout = 5,
			InfiniteFarPlane = false,
			LodFactor = 1,
			LodReferenceFieldOfView = 0,
		};
	}
}
