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
// File         : Configuration.cs
// Module       : CSWUnity.NuGetStager
// Description  : Staging profile model and validation.
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
using System.Text.Json.Serialization;

namespace CswUnity.NuGetStager;

internal sealed class StagingConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public int SchemaVersion { get; init; }
    public string Profile { get; init; } = string.Empty;
    public string OutputDirectory { get; init; } = string.Empty;
    public string WorkDirectory { get; init; } = string.Empty;
    public UnityPackageConfiguration UnityPackage { get; init; } = new();
    public List<DependencySetConfiguration> DependencySets { get; init; } = [];
    public List<PlatformConfiguration> Platforms { get; init; } = [];

    [JsonIgnore]
    public string ConfigurationDirectory { get; private set; } = string.Empty;

    [JsonIgnore]
    public string OutputDirectoryPath => ResolvePath(OutputDirectory);

    [JsonIgnore]
    public string WorkDirectoryPath => ResolvePath(WorkDirectory);

    public static StagingConfiguration Load(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new StagingException($"Configuration file does not exist: {fullPath}");

        StagingConfiguration? configuration;

        try
        {
            configuration = JsonSerializer.Deserialize<StagingConfiguration>(
                File.ReadAllText(fullPath),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new StagingException($"Invalid staging configuration '{fullPath}': {exception.Message}", exception);
        }

        if (configuration is null)
            throw new StagingException($"Staging configuration is empty: {fullPath}");

        configuration.ConfigurationDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new StagingException($"Cannot resolve configuration directory: {fullPath}");

        configuration.Validate();
        return configuration;
    }

    public DependencySetConfiguration GetDependencySet(string id)
    {
        return DependencySets.SingleOrDefault(
                   dependencySet => string.Equals(dependencySet.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new StagingException($"Platform references unknown dependency set '{id}'.");
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new StagingException("Configuration contains an empty path.");

        return Path.GetFullPath(Path.Combine(ConfigurationDirectory, path));
    }

    private void Validate()
    {
        if (SchemaVersion != 1)
            throw new StagingException($"Unsupported staging configuration schema {SchemaVersion}. Expected 1.");

        if (string.IsNullOrWhiteSpace(Profile))
            throw new StagingException("The staging profile name is required.");

        PathSafety.ValidateGeneratedDirectory(OutputDirectoryPath, "outputDirectory");
        PathSafety.ValidateGeneratedDirectory(WorkDirectoryPath, "workDirectory");

        if (PathComparer.Equals(OutputDirectoryPath, WorkDirectoryPath))
            throw new StagingException("outputDirectory and workDirectory must be different.");

        UnityPackage.Validate();

        EnsureUniqueIds(DependencySets.Select(value => value.Id), "dependency set");
        EnsureUniqueIds(Platforms.Select(value => value.Id), "platform");

        if (DependencySets.Count == 0)
            throw new StagingException("At least one dependency set is required.");

        if (Platforms.Count == 0)
            throw new StagingException("At least one platform is required.");

        foreach (DependencySetConfiguration dependencySet in DependencySets)
        {
            dependencySet.Validate();

            string projectPath = ResolvePath(dependencySet.Project);

            if (!File.Exists(projectPath))
                throw new StagingException($"Dependency project does not exist: {projectPath}");

            string lockFilePath = Path.Combine(
                Path.GetDirectoryName(projectPath) ?? ConfigurationDirectory,
                "packages.lock.json");

            if (!File.Exists(lockFilePath))
                throw new StagingException($"Dependency lock file does not exist: {lockFilePath}");
        }

        foreach (PlatformConfiguration platform in Platforms)
        {
            platform.Validate();

            if (!platform.Enabled)
                continue;

            _ = GetDependencySet(platform.DependencySet);
        }

        if (!Platforms.Any(platform => platform.Enabled))
            throw new StagingException("At least one platform must be enabled.");
    }

    private static void EnsureUniqueIds(IEnumerable<string> values, string description)
    {
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);

        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new StagingException($"A {description} has an empty id.");

            if (!unique.Add(value))
                throw new StagingException($"Duplicate {description} id '{value}'.");
        }
    }
}

internal sealed class UnityPackageConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Unity { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Version)
            || string.IsNullOrWhiteSpace(DisplayName)
            || string.IsNullOrWhiteSpace(Unity)
            || string.IsNullOrWhiteSpace(Author))
        {
            throw new StagingException("unityPackage requires name, version, displayName, unity, and author.");
        }
    }
}

internal sealed class DependencySetConfiguration
{
    public string Id { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string AnchorAssembly { get; init; } = string.Empty;
    public List<string> RequiredManagedAssemblies { get; init; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Project))
            throw new StagingException($"Dependency set '{Id}' has no project.");

        if (string.IsNullOrWhiteSpace(AnchorAssembly))
            throw new StagingException($"Dependency set '{Id}' has no anchorAssembly.");

        if (RequiredManagedAssemblies.Count == 0)
            throw new StagingException($"Dependency set '{Id}' has no requiredManagedAssemblies.");
    }
}

internal sealed class PlatformConfiguration
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public bool Required { get; init; }
    public string DependencySet { get; init; } = string.Empty;
    public string RuntimeIdentifier { get; init; } = string.Empty;
    public string Configuration { get; init; } = "Release";
    public string MsbuildPlatform { get; init; } = "AnyCPU";
    public string NativeDestination { get; init; } = string.Empty;
    public string NativePeMachine { get; init; } = string.Empty;
    public List<string> RequiredNativeLibraries { get; init; } = [];
    public List<UnityPlatformConfiguration> UnityPlatforms { get; init; } = [];
    public string Note { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new StagingException($"Platform '{Id}' has no displayName.");

        if (Required && !Enabled)
            throw new StagingException($"Platform '{Id}' cannot be required while disabled.");

        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(DependencySet)
            || string.IsNullOrWhiteSpace(RuntimeIdentifier)
            || string.IsNullOrWhiteSpace(NativeDestination))
        {
            throw new StagingException(
                $"Enabled platform '{Id}' requires dependencySet, runtimeIdentifier, and nativeDestination.");
        }

        if (!string.Equals(Configuration, "Release", StringComparison.OrdinalIgnoreCase))
        {
            throw new StagingException(
                $"Phase 1 only stages Release native assets. Platform '{Id}' requests '{Configuration}'.");
        }

        if (RequiredNativeLibraries.Count == 0)
            throw new StagingException($"Enabled platform '{Id}' has no requiredNativeLibraries.");

        if (UnityPlatforms.Count == 0)
            throw new StagingException($"Enabled platform '{Id}' has no Unity PluginImporter platforms.");
    }
}

internal sealed class UnityPlatformConfiguration
{
    public string Group { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public Dictionary<string, string> Settings { get; init; } = new(StringComparer.Ordinal);
}

internal static class PathSafety
{
    public static void ValidateGeneratedDirectory(string path, string propertyName)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new StagingException($"Cannot resolve {propertyName}: {path}");

        if (PathComparer.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar)))
            throw new StagingException($"{propertyName} cannot be a filesystem root: {fullPath}");

        if (fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length < 4)
            throw new StagingException($"{propertyName} is too broad for generated output: {fullPath}");
    }
}

internal static class PathComparer
{
    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool Equals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Comparison);
    }
}

internal sealed class StagingException : Exception
{
    public StagingException(string message)
        : base(message)
    {
    }

    public StagingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
