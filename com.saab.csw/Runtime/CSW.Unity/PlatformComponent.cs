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
// File			: PlatformComponent.cs
// Module		: CSW.Unity
// Description	: Default component that starts and stops the CSW platform
// Author		: Anders Modén
// Product		: GizmoBase 2.12.326
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
// AMO	180607	Created file        (2.9.1)
// AMO	261028	Removed legacy SceneManager camera and map URL wiring.	(2.12.327)
// AMO	261030	Removed an unused tracer field.	(2.12.327)
// AMO	261103	Removed the never-called Android asset binding and disabled debug station code.	(2.12.327)
// AMO	261104	Renamed from Saab.Unity.Initializer.Initializer; starts and stops CSW.Unity.Platform.	(2.12.327)
//
//******************************************************************************

using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Default scene component that calls Platform.Start when enabled and Platform.Stop when disabled.</summary>
	/// <remarks>
	/// Applications that control the platform from code call Platform.Start and Platform.Stop directly instead.
	/// The component only stops a platform that its own OnEnable started, and the stop is deferred until the
	/// last session reference is released.
	/// </remarks>
	[DefaultExecutionOrder(-3000)]
	[DisallowMultipleComponent]
	public sealed class PlatformComponent : MonoBehaviour
	{
		public PlatformSettings Settings = new PlatformSettings();

		private bool _startedPlatform;

		private void OnEnable()
		{
			_startedPlatform = Platform.Start(Settings);
		}

		private void Update()
		{
			Platform.Update();
		}

		private void OnDisable()
		{
			if (!_startedPlatform)
				return;
			_startedPlatform = false;
			Platform.Stop();
		}
	}
}
