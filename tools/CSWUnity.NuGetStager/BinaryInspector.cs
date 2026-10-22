//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s)
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been supplied.
//
// Information Class:  COMPANY UNCLASSIFIED
// Defence Secrecy:    NOT CLASSIFIED
// Export Control:     NOT EXPORT CONTROLLED
//
// File         : BinaryInspector.cs
// Module       : CSWUnity.NuGetStager
// Description  : Managed/native PE classification and architecture inspection.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

using System.Reflection.PortableExecutable;

namespace CswUnity.NuGetStager;

internal enum BinaryKind
{
    Managed,
    Native
}

internal sealed record BinaryInspection(BinaryKind Kind, string Machine);

internal static class BinaryInspector
{
    public static BinaryInspection Inspect(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader reader = new(stream);

            if (reader.PEHeaders.PEHeader is null)
                throw new StagingException($"File is not a valid PE image: {path}");

            BinaryKind kind = reader.HasMetadata ? BinaryKind.Managed : BinaryKind.Native;
            return new BinaryInspection(kind, reader.PEHeaders.CoffHeader.Machine.ToString());
        }
        catch (BadImageFormatException exception)
        {
            throw new StagingException($"File is not a valid managed or native PE image: {path}", exception);
        }
    }
}
