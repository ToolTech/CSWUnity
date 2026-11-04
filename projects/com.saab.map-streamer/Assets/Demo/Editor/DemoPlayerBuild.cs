//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: DemoPlayerBuild.cs
// Description	: Builds the Windows demo player with its configuration and launcher
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Added the demo player build with config.xml and the Start Demo.cmd launcher.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.Demo.	(2.12.327)
//
//*****************************************************************************

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CSW.Unity.Demo
{
	/// <summary>Builds the Windows demo player into Builds/MapStreamer-Demo with config.xml and Start Demo.cmd.</summary>
	/// <remarks>
	/// The launcher starts the player with -csw-demo in the player folder, where the player reads config.xml.
	/// Scripts build the player with Unity -batchmode -quit -projectPath and
	/// -executeMethod Saab.Foundation.Unity.MapStreamer.Demo.DemoPlayerBuild.Build.
	/// </remarks>
	public static class DemoPlayerBuild
	{
		private const string PlayerFolder = "Builds/MapStreamer-Demo";

		[MenuItem("MapStreamer/Build Demo Player")]
		public static void Build()
		{
			var executable = PlayerSettings.productName + ".exe";
			var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
			{
				scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
				locationPathName = Path.Combine(PlayerFolder, executable),
				target = BuildTarget.StandaloneWindows64
			});
			if (report.summary.result != BuildResult.Succeeded)
				throw new BuildFailedException("The demo player build ended with " + report.summary.result + ".");
			File.Copy("config.xml", Path.Combine(PlayerFolder, "config.xml"), true);
			File.WriteAllText(Path.Combine(PlayerFolder, "Start Demo.cmd"),
				"@echo off\r\n" +
				"rem Starts the demo flight. Extra arguments override its defaults, for example -csw-demo-speed 120.\r\n" +
				"cd /d \"%~dp0\"\r\n" +
				"start \"\" \"" + executable + "\" -csw-demo %*\r\n");
			Debug.Log("Demo player built in " + Path.GetFullPath(PlayerFolder) + ".");
		}
	}
}
