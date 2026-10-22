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
// File         : Program.cs
// Module       : CSWUnity.NuGetStager
// Description  : Command-line entry point for Unity dependency staging.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

namespace CswUnity.NuGetStager;

internal static class Program
{
    private const string DefaultConfigurationPath = @"build\nuget-staging\staging.json";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            CommandLineOptions options = CommandLineOptions.Parse(args);

            if (options.ShowHelp)
            {
                WriteUsage();
                return 0;
            }

            StagingConfiguration configuration = StagingConfiguration.Load(options.ConfigurationPath);
            StagingService service = new(configuration);

            switch (options.Command)
            {
                case "stage":
                    StageResult result = await service.StageAsync();
                    WritePlatformReport(result.Platforms);
                    Console.WriteLine();
                    Console.WriteLine($"Unity package staged at: {result.OutputDirectory}");
                    return 0;

                case "verify":
                    VerificationResult verification = service.Verify();
                    WritePlatformReport(verification.Platforms);
                    Console.WriteLine();
                    Console.WriteLine($"Verified {verification.FileCount} owned files in {verification.OutputDirectory}");
                    return 0;

                case "clean":
                    service.Clean();
                    Console.WriteLine($"Removed staged Unity package: {configuration.OutputDirectoryPath}");
                    return 0;

                default:
                    throw new StagingException($"Unsupported command '{options.Command}'.");
            }
        }
        catch (StagingException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static void WritePlatformReport(IReadOnlyCollection<PlatformReport> platforms)
    {
        WritePlatformGroup("INSTALLED", platforms, PlatformStatus.Installed);
        WritePlatformGroup("DISABLED BY PROFILE", platforms, PlatformStatus.Disabled);
        WritePlatformGroup("UNAVAILABLE", platforms, PlatformStatus.Unavailable);
    }

    private static void WritePlatformGroup(
        string heading,
        IEnumerable<PlatformReport> platforms,
        PlatformStatus status)
    {
        PlatformReport[] selected = platforms.Where(platform => platform.Status == status).ToArray();

        if (selected.Length == 0)
            return;

        Console.WriteLine(heading);

        foreach (PlatformReport platform in selected)
        {
            string detail = string.IsNullOrWhiteSpace(platform.Detail)
                ? string.Empty
                : $" - {platform.Detail}";

            Console.WriteLine($"  {platform.DisplayName}{detail}");
        }

        Console.WriteLine();
    }

    private static void WriteUsage()
    {
        Console.WriteLine("CSWUnity NuGet Stager");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools\\CSWUnity.NuGetStager -- <stage|verify|clean> [--config <path>]");
        Console.WriteLine();
        Console.WriteLine($"Default configuration: {DefaultConfigurationPath}");
    }

    private sealed record CommandLineOptions(string Command, string ConfigurationPath, bool ShowHelp)
    {
        public static CommandLineOptions Parse(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
                return new CommandLineOptions(string.Empty, DefaultConfigurationPath, true);

            string command = args[0].ToLowerInvariant();
            string configurationPath = DefaultConfigurationPath;

            for (int index = 1; index < args.Length; index++)
            {
                if (args[index] != "--config")
                    throw new StagingException($"Unknown argument '{args[index]}'.");

                if (++index >= args.Length)
                    throw new StagingException("Missing path after --config.");

                configurationPath = args[index];
            }

            return new CommandLineOptions(command, configurationPath, false);
        }
    }
}
