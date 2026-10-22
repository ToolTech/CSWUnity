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
// File         : ProcessRunner.cs
// Module       : CSWUnity.NuGetStager
// Description  : External process execution with explicit diagnostics.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

using System.Diagnostics;

namespace CswUnity.NuGetStager;

internal static class ProcessRunner
{
    public static async Task RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        string description)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };

        if (!process.Start())
            throw new StagingException($"Failed to start {description}.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await standardOutput;
        string error = await standardError;

        if (process.ExitCode == 0)
            return;

        string detail = string.Join(
            Environment.NewLine,
            new[] { output.Trim(), error.Trim() }.Where(value => value.Length > 0));

        throw new StagingException(
            $"{description} failed with exit code {process.ExitCode}."
            + (detail.Length == 0 ? string.Empty : $"{Environment.NewLine}{detail}"));
    }
}
