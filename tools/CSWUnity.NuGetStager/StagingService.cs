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
// File         : StagingService.cs
// Module       : CSWUnity.NuGetStager
// Description  : Locked NuGet materialization, staging, verification, and cleanup.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

using System.Text.Json;

namespace CswUnity.NuGetStager;

internal enum PlatformStatus
{
    Installed,
    Disabled,
    Unavailable
}

internal sealed record PlatformReport(
    string Id,
    string DisplayName,
    bool Requested,
    bool Required,
    string RuntimeIdentifier,
    PlatformStatus Status,
    string Detail);

internal sealed record StageResult(string OutputDirectory, IReadOnlyCollection<PlatformReport> Platforms);

internal sealed record VerificationResult(
    string OutputDirectory,
    int FileCount,
    IReadOnlyCollection<PlatformReport> Platforms);

internal sealed class StagingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StagingConfiguration _configuration;

    public StagingService(StagingConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<StageResult> StageAsync()
    {
        Directory.CreateDirectory(_configuration.WorkDirectoryPath);

        string runRoot = Path.Combine(
            _configuration.WorkDirectoryPath,
            $"run-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");
        string packageRoot = Path.Combine(runRoot, "package");
        UnityPackageWriter writer = new(packageRoot);
        List<PlatformReport> reports = [];

        try
        {
            foreach (PlatformConfiguration platform in _configuration.Platforms)
            {
                if (!platform.Enabled)
                {
                    reports.Add(new PlatformReport(
                        platform.Id,
                        platform.DisplayName,
                        false,
                        false,
                        platform.RuntimeIdentifier,
                        PlatformStatus.Disabled,
                        platform.Note));
                    continue;
                }

                PlatformAssets assets;

                try
                {
                    assets = await MaterializeAsync(platform, runRoot);
                }
                catch (StagingException exception) when (!platform.Required)
                {
                    reports.Add(new PlatformReport(
                        platform.Id,
                        platform.DisplayName,
                        true,
                        false,
                        platform.RuntimeIdentifier,
                        PlatformStatus.Unavailable,
                        exception.Message));

                    continue;
                }
                catch (StagingException exception)
                {
                    reports.Add(new PlatformReport(
                        platform.Id,
                        platform.DisplayName,
                        true,
                        true,
                        platform.RuntimeIdentifier,
                        PlatformStatus.Unavailable,
                        exception.Message));

                    throw new StagingException(
                        $"Required platform '{platform.DisplayName}' is unavailable: {exception.Message}",
                        exception);
                }

                foreach (string managedAssembly in assets.ManagedAssemblies)
                    writer.CopyManagedAssembly(managedAssembly);

                foreach (string nativeLibrary in assets.NativeLibraries)
                    writer.CopyNativeLibrary(nativeLibrary, platform);

                reports.Add(new PlatformReport(
                    platform.Id,
                    platform.DisplayName,
                    true,
                    platform.Required,
                    platform.RuntimeIdentifier,
                    PlatformStatus.Installed,
                    $"{assets.ManagedAssemblies.Count} managed, {assets.NativeLibraries.Count} native"));
            }

            if (!reports.Any(report => report.Status == PlatformStatus.Installed))
                throw new StagingException("No configured platform could be staged.");

            writer.WritePackageJson(_configuration.UnityPackage);
            writer.WritePlatformReport(_configuration.Profile, reports);
            writer.WriteFolderMetas();
            writer.WriteOwnershipManifest(_configuration.Profile, reports);

            _ = VerifyDirectory(packageRoot);
            PublishPackage(packageRoot, _configuration.OutputDirectoryPath);
            VerificationResult verification = Verify();

            return new StageResult(verification.OutputDirectory, verification.Platforms);
        }
        finally
        {
            if (Directory.Exists(runRoot))
                Directory.Delete(runRoot, true);
        }
    }

    public VerificationResult Verify()
    {
        return VerifyDirectory(_configuration.OutputDirectoryPath);
    }

    public void Clean()
    {
        string outputDirectory = _configuration.OutputDirectoryPath;

        if (!Directory.Exists(outputDirectory))
            return;

        _ = VerifyDirectory(outputDirectory);
        Directory.Delete(outputDirectory, true);
    }

    private async Task<PlatformAssets> MaterializeAsync(
        PlatformConfiguration platform,
        string runRoot)
    {
        DependencySetConfiguration dependencySet = _configuration.GetDependencySet(platform.DependencySet);
        string projectPath = _configuration.ResolvePath(dependencySet.Project);
        string platformRoot = Path.Combine(runRoot, platform.Id);
        string intermediateDirectory = EnsureTrailingSeparator(Path.Combine(platformRoot, "obj"));
        string outputDirectory = Path.Combine(platformRoot, "output");

        Directory.CreateDirectory(platformRoot);

        await ProcessRunner.RunAsync(
            "dotnet",
            [
                "restore",
                projectPath,
                "--locked-mode",
                "--runtime",
                platform.RuntimeIdentifier,
                "--property:BaseIntermediateOutputPath=" + intermediateDirectory,
                "--property:RestorePackagesWithLockFile=true",
                "--nologo"
            ],
            _configuration.ConfigurationDirectory,
            $"NuGet restore for {platform.DisplayName}");

        await ProcessRunner.RunAsync(
            "dotnet",
            [
                "build",
                projectPath,
                "--configuration",
                platform.Configuration,
                "--runtime",
                platform.RuntimeIdentifier,
                "--no-restore",
                "--output",
                outputDirectory,
                "--property:BaseIntermediateOutputPath=" + intermediateDirectory,
                "--property:Platform=" + platform.MsbuildPlatform,
                "--property:AppendRuntimeIdentifierToOutputPath=false",
                "--nologo"
            ],
            _configuration.ConfigurationDirectory,
            $"dependency materialization for {platform.DisplayName}");

        string[] candidates = Directory
            .EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                dependencySet.AnchorAssembly,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<string> managedAssemblies = [];
        List<string> nativeLibraries = [];

        foreach (string candidate in candidates)
        {
            BinaryInspection inspection = BinaryInspector.Inspect(candidate);

            if (inspection.Kind == BinaryKind.Managed)
            {
                managedAssemblies.Add(candidate);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(platform.NativePeMachine)
                && !string.Equals(
                    inspection.Machine,
                    platform.NativePeMachine,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new StagingException(
                    $"Native library '{Path.GetFileName(candidate)}' has machine '{inspection.Machine}', "
                    + $"expected '{platform.NativePeMachine}'.");
            }

            nativeLibraries.Add(candidate);
        }

        ValidateRequiredFiles(
            dependencySet.RequiredManagedAssemblies,
            managedAssemblies,
            platform.DisplayName,
            "managed assembly");
        ValidateRequiredFiles(
            platform.RequiredNativeLibraries,
            nativeLibraries,
            platform.DisplayName,
            "native library");

        return new PlatformAssets(managedAssemblies, nativeLibraries);
    }

    private static void ValidateRequiredFiles(
        IEnumerable<string> requiredNames,
        IEnumerable<string> actualPaths,
        string platform,
        string description)
    {
        HashSet<string> actualNames = actualPaths
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] missing = requiredNames
            .Where(required => !actualNames.Contains(required))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new StagingException(
                $"{platform} is missing required {description}s: {string.Join(", ", missing)}");
        }
    }

    private static VerificationResult VerifyDirectory(string packageRoot)
    {
        if (!Directory.Exists(packageRoot))
            throw new StagingException($"Staged package does not exist: {packageRoot}");

        string manifestPath = Path.Combine(packageRoot, UnityPackageWriter.OwnershipManifestFileName);

        if (!File.Exists(manifestPath))
            throw new StagingException($"Ownership manifest is missing: {manifestPath}");

        StagingManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<StagingManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new StagingException($"Invalid ownership manifest: {manifestPath}", exception);
        }

        if (manifest is null || manifest.SchemaVersion != 1)
            throw new StagingException($"Unsupported ownership manifest: {manifestPath}");

        Dictionary<string, OwnedFile> expected = manifest.Files.ToDictionary(
            file => file.Path,
            StringComparer.OrdinalIgnoreCase);
        string[] actual = Directory
            .EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                UnityPackageWriter.OwnershipManifestFileName,
                StringComparison.Ordinal))
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(packageRoot, path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] unowned = actual.Where(path => !expected.ContainsKey(path)).ToArray();
        string[] missing = expected.Keys.Where(path => !actual.Contains(path, StringComparer.OrdinalIgnoreCase)).ToArray();

        if (unowned.Length > 0)
            throw new StagingException($"Staged package contains unowned files: {string.Join(", ", unowned)}");

        if (missing.Length > 0)
            throw new StagingException($"Staged package is missing owned files: {string.Join(", ", missing)}");

        foreach ((string relativePath, OwnedFile file) in expected)
        {
            string fullPath = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string actualHash = UnityPackageWriter.ComputeSha256(fullPath);

            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new StagingException($"Staged file has changed: {relativePath}");
        }

        PlatformReport[] failedRequired = manifest.Platforms
            .Where(platform => platform.Required && platform.Status != PlatformStatus.Installed)
            .ToArray();

        if (failedRequired.Length > 0)
        {
            throw new StagingException(
                "Required platforms are not installed: "
                + string.Join(", ", failedRequired.Select(platform => platform.DisplayName)));
        }

        return new VerificationResult(
            packageRoot,
            manifest.Files.Count,
            manifest.Platforms);
    }

    private static void PublishPackage(string sourceDirectory, string outputDirectory)
    {
        PathSafety.ValidateGeneratedDirectory(outputDirectory, "outputDirectory");
        Directory.CreateDirectory(Path.GetDirectoryName(outputDirectory)!);

        if (!Directory.Exists(outputDirectory))
        {
            Directory.Move(sourceDirectory, outputDirectory);
            return;
        }

        _ = VerifyDirectory(outputDirectory);

        string backupDirectory = outputDirectory + $".backup-{Guid.NewGuid():N}";
        Directory.Move(outputDirectory, backupDirectory);

        try
        {
            Directory.Move(sourceDirectory, outputDirectory);
            Directory.Delete(backupDirectory, true);
        }
        catch
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, true);

            if (Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, outputDirectory);

            throw;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
               + Path.DirectorySeparatorChar;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private sealed record PlatformAssets(
        IReadOnlyCollection<string> ManagedAssemblies,
        IReadOnlyCollection<string> NativeLibraries);
}
