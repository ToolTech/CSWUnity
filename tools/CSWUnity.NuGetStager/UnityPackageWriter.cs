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
// File         : UnityPackageWriter.cs
// Module       : CSWUnity.NuGetStager
// Description  : Deterministic Unity package and PluginImporter generation.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CswUnity.NuGetStager;

internal sealed class UnityPackageWriter
{
    internal const string OwnershipManifestFileName = ".cswunity-staging-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _packageRoot;

    public UnityPackageWriter(string packageRoot)
    {
        _packageRoot = packageRoot;
        Directory.CreateDirectory(_packageRoot);
    }

    public void WritePackageJson(UnityPackageConfiguration configuration)
    {
        var package = new
        {
            name = configuration.Name,
            version = configuration.Version,
            displayName = configuration.DisplayName,
            description = configuration.Description,
            unity = configuration.Unity,
            author = new
            {
                name = configuration.Author
            }
        };

        WriteJsonAsset("package.json", package);
    }

    public void WritePlatformReport(string profile, IReadOnlyCollection<PlatformReport> platforms)
    {
        var report = new
        {
            schemaVersion = 1,
            profile,
            platforms = platforms.Select(platform => new
            {
                id = platform.Id,
                displayName = platform.DisplayName,
                requested = platform.Requested,
                required = platform.Required,
                runtimeIdentifier = platform.RuntimeIdentifier,
                status = platform.Status.ToString().ToLowerInvariant(),
                detail = platform.Detail
            })
        };

        WriteJsonAsset("supported-platforms.json", report);
    }

    public void CopyManagedAssembly(string sourcePath)
    {
        string relativePath = Path.Combine("Runtime", "Managed", Path.GetFileName(sourcePath));
        CopyFile(sourcePath, relativePath);
        WriteText($"{relativePath}.meta", BuildManagedPluginMeta(relativePath));
    }

    public void CopyNativeLibrary(string sourcePath, PlatformConfiguration platform)
    {
        string relativePath = Path.Combine(platform.NativeDestination, Path.GetFileName(sourcePath));
        CopyFile(sourcePath, relativePath);
        WriteText($"{relativePath}.meta", BuildNativePluginMeta(relativePath, platform));
    }

    public void WriteFolderMetas()
    {
        string[] directories = Directory
            .EnumerateDirectories(_packageRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar))
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (string directory in directories)
        {
            string relativePath = Path.GetRelativePath(_packageRoot, directory);
            WriteText($"{relativePath}.meta", BuildFolderMeta(relativePath));
        }
    }

    public StagingManifest WriteOwnershipManifest(
        string profile,
        IReadOnlyCollection<PlatformReport> platforms)
    {
        List<OwnedFile> files = Directory
            .EnumerateFiles(_packageRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                OwnershipManifestFileName,
                StringComparison.Ordinal))
            .Select(path => new OwnedFile(
                NormalizeRelativePath(Path.GetRelativePath(_packageRoot, path)),
                ComputeSha256(path)))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();

        StagingManifest manifest = new()
        {
            SchemaVersion = 1,
            Profile = profile,
            Platforms = platforms.ToList(),
            Files = files
        };

        string manifestPath = Path.Combine(_packageRoot, OwnershipManifestFileName);
        WriteJson(manifestPath, manifest);
        return manifest;
    }

    public static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string CreateDeterministicGuid(string relativePath)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $"CSWUnity.NuGetStager|{NormalizeRelativePath(relativePath).ToLowerInvariant()}");
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    public static void WriteJson<T>(string path, T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        WriteUtf8(path, json);
    }

    private void WriteJsonAsset<T>(string relativePath, T value)
    {
        string fullPath = GetSafePath(relativePath);
        WriteJson(fullPath, value);
        WriteText($"{relativePath}.meta", BuildDefaultImporterMeta(relativePath));
    }

    private void CopyFile(string sourcePath, string relativePath)
    {
        string destinationPath = GetSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            string sourceHash = ComputeSha256(sourcePath);
            string destinationHash = ComputeSha256(destinationPath);

            if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal))
            {
                throw new StagingException(
                    $"Conflicting staged asset '{NormalizeRelativePath(relativePath)}' has different contents.");
            }

            return;
        }

        File.Copy(sourcePath, destinationPath);
    }

    private void WriteText(string relativePath, string content)
    {
        WriteUtf8(GetSafePath(relativePath), content);
    }

    private string GetSafePath(string relativePath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_packageRoot, relativePath));
        string rootWithSeparator = _packageRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        StringComparison comparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootWithSeparator, comparison))
            throw new StagingException($"Generated asset escapes package root: {relativePath}");

        return fullPath;
    }

    private static void WriteUtf8(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllText(path, normalized + (normalized.EndsWith("\r\n", StringComparison.Ordinal) ? string.Empty : "\r\n"),
            new UTF8Encoding(false));
    }

    private static string BuildFolderMeta(string relativePath)
    {
        return $$"""
            fileFormatVersion: 2
            guid: {{CreateDeterministicGuid(relativePath)}}
            folderAsset: yes
            DefaultImporter:
              externalObjects: {}
              userData:
              assetBundleName:
              assetBundleVariant:
            """;
    }

    private static string BuildDefaultImporterMeta(string relativePath)
    {
        return $$"""
            fileFormatVersion: 2
            guid: {{CreateDeterministicGuid(relativePath)}}
            DefaultImporter:
              externalObjects: {}
              userData:
              assetBundleName:
              assetBundleVariant:
            """;
    }

    private static string BuildManagedPluginMeta(string relativePath)
    {
        return $$"""
            fileFormatVersion: 2
            guid: {{CreateDeterministicGuid(relativePath)}}
            PluginImporter:
              externalObjects: {}
              serializedVersion: 2
              iconMap: {}
              executionOrder: {}
              defineConstraints: []
              isPreloaded: 0
              isOverridable: 0
              isExplicitlyReferenced: 0
              validateReferences: 1
              platformData:
              - first:
                  Any:
                second:
                  enabled: 1
                  settings: {}
              - first:
                  Editor: Editor
                second:
                  enabled: 1
                  settings:
                    CPU: AnyCPU
                    DefaultValueInitialized: true
                    OS: AnyOS
              userData:
              assetBundleName:
              assetBundleVariant:
            """;
    }

    private static string BuildNativePluginMeta(string relativePath, PlatformConfiguration platform)
    {
        StringBuilder builder = new();
        builder.AppendLine("fileFormatVersion: 2");
        builder.AppendLine($"guid: {CreateDeterministicGuid(relativePath)}");
        builder.AppendLine("PluginImporter:");
        builder.AppendLine("  externalObjects: {}");
        builder.AppendLine("  serializedVersion: 2");
        builder.AppendLine("  iconMap: {}");
        builder.AppendLine("  executionOrder: {}");
        builder.AppendLine("  defineConstraints: []");
        builder.AppendLine("  isPreloaded: 0");
        builder.AppendLine("  isOverridable: 0");
        builder.AppendLine("  isExplicitlyReferenced: 0");
        builder.AppendLine("  validateReferences: 1");
        builder.AppendLine("  platformData:");
        builder.AppendLine("  - first:");
        builder.AppendLine("      Any:");
        builder.AppendLine("    second:");
        builder.AppendLine("      enabled: 0");
        builder.AppendLine("      settings: {}");

        foreach (UnityPlatformConfiguration unityPlatform in platform.UnityPlatforms)
        {
            builder.AppendLine("  - first:");
            builder.AppendLine($"      {unityPlatform.Group}: {unityPlatform.Target}");
            builder.AppendLine("    second:");
            builder.AppendLine($"      enabled: {(unityPlatform.Enabled ? 1 : 0)}");

            if (unityPlatform.Settings.Count == 0)
            {
                builder.AppendLine("      settings: {}");
                continue;
            }

            builder.AppendLine("      settings:");

            foreach ((string key, string value) in unityPlatform.Settings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                builder.AppendLine($"        {key}: {value}");
        }

        builder.AppendLine("  userData:");
        builder.AppendLine("  assetBundleName:");
        builder.AppendLine("  assetBundleVariant:");
        return builder.ToString();
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}

internal sealed class StagingManifest
{
    public int SchemaVersion { get; init; }
    public string Profile { get; init; } = string.Empty;
    public List<PlatformReport> Platforms { get; init; } = [];
    public List<OwnedFile> Files { get; init; } = [];
}

internal sealed record OwnedFile(string Path, string Sha256);
