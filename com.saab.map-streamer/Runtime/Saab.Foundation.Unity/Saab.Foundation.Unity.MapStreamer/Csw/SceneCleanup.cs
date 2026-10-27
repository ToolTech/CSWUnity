//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCleanup.cs
// Description	: Complete independent cleanup steps and report every failure
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added ordered cleanup that completes independent resource releases before reporting failures.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	internal sealed class SceneCleanup
	{
		private readonly List<Exception> _failures = new List<Exception>();

		internal void Attempt(Action action)
		{
			try
			{
				action();
			}
			catch (Exception exception)
			{
				_failures.Add(exception);
			}
		}

		internal void Complete(string message)
		{
			if (_failures.Count != 0)
				throw new AggregateException(message, _failures);
		}
	}
}
