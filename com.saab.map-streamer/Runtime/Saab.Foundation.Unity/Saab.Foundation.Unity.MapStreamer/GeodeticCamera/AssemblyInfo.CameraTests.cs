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
// Information Class:  COMPANY UNCLASSIFIED
// Defence Secrecy:     NOT CLASSIFIED
// Export Control:      NOT EXPORT CONTROLLED
//
//
// File         : AssemblyInfo.CameraTests.cs
// Module       :
// Description  : Grants camera and MapStreamer test assemblies internal access.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA  260909  Created file
// AMO	261022	Granted MapStreamer test assemblies internal access.	(2.12.327)
//
//******************************************************************************

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Saab.Application.Unity.MapEditor.Tests.EditMode")]
[assembly: InternalsVisibleTo("MAPSTREAMER.TESTS")]
[assembly: InternalsVisibleTo("MAPSTREAMER.PLAYMODE.TESTS")]
