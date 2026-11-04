//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: PresentedFrameHook.cs
// Description	: Test hook that runs after MapControl has presented the camera
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Calls back after MapControl.LateUpdate for fast-motion rendering analysis.	(2.12.327)
// AMO	261104	Draws an optional display texture over the game view for interactive runs.	(2.12.327)//
//*****************************************************************************

using System;
using UnityEngine;

namespace CSW.Unity.Tests
{
	/// <summary>Invokes Presented in LateUpdate after MapControl has presented the camera of the frame.</summary>
	[DefaultExecutionOrder(2000)]
	internal sealed class PresentedFrameHook : MonoBehaviour
	{
		public Action Presented;
		/// <summary>Optional texture drawn over the game view, so an interactive run can be watched.</summary>
		public Texture Display;

		private void LateUpdate()
		{
			Presented?.Invoke();
		}

		private void OnGUI()
		{
			if (Display != null && Event.current.type == EventType.Repaint)
				GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Display, ScaleMode.ScaleToFit, false);
		}
	}
}
