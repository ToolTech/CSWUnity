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
// File         : StagerTests.cs
// Module       : CSWUnity.NuGetStager.Tests
// Description  : Focused staging configuration and binary validation tests.
// Author       : Anders Modén
// Product      : CSWUnity 5.0.0
//
// Revision History...
//
// Who Date   Description
// AMO 261022 Created file (5.0.1)
//
//******************************************************************************

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CswUnity.NuGetStager.Tests;

[TestClass]
public sealed class StagerTests
{
    [TestMethod]
    public void DeterministicGuidUsesNormalizedCaseInsensitiveAssetPath()
    {
        string windowsPath = UnityPackageWriter.CreateDeterministicGuid(@"Runtime\Managed\GizmoBase.dll");
        string unityPath = UnityPackageWriter.CreateDeterministicGuid("runtime/managed/gizmobase.dll");

        Assert.AreEqual(windowsPath, unityPath);
        Assert.AreEqual(32, windowsPath.Length);
    }

    [TestMethod]
    public void BinaryInspectorRecognizesManagedAssembly()
    {
        BinaryInspection inspection = BinaryInspector.Inspect(typeof(StagerTests).Assembly.Location);

        Assert.AreEqual(BinaryKind.Managed, inspection.Kind);
    }

    [TestMethod]
    public void BinaryInspectorRecognizesWindowsNativeLibrary()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("The phase-1 native binary test requires Windows.");

        string kernel32 = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        BinaryInspection inspection = BinaryInspector.Inspect(kernel32);

        Assert.AreEqual(BinaryKind.Native, inspection.Kind);
    }

    [TestMethod]
    public void ConfigurationRejectsRequiredDisabledPlatform()
    {
        using TemporaryDirectory temporary = new();
        string projectPath = Path.Combine(temporary.Path, "Dependencies.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />", Encoding.UTF8);
        File.WriteAllText(Path.Combine(temporary.Path, "packages.lock.json"), "{}", Encoding.UTF8);

        string configurationPath = Path.Combine(temporary.Path, "staging.json");
        File.WriteAllText(
            configurationPath,
            """
            {
              "schemaVersion": 1,
              "profile": "test",
              "outputDirectory": "generated/output/package",
              "workDirectory": "generated/work/temp",
              "unityPackage": {
                "name": "com.saab.test",
                "version": "1.0.0",
                "displayName": "Test",
                "unity": "6000.0",
                "author": "SAAB AB"
              },
              "dependencySets": [
                {
                  "id": "dependencies",
                  "project": "Dependencies.csproj",
                  "anchorAssembly": "Dependencies.dll",
                  "requiredManagedAssemblies": [ "Managed.dll" ]
                }
              ],
              "platforms": [
                {
                  "id": "disabled",
                  "displayName": "Disabled",
                  "enabled": false,
                  "required": true
                }
              ]
            }
            """,
            Encoding.UTF8);

        StagingException exception = Assert.ThrowsException<StagingException>(
            () => StagingConfiguration.Load(configurationPath));

        StringAssert.Contains(exception.Message, "cannot be required while disabled");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"cswunity-stager-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
