/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s)
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied.
 *
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using NUnit.Framework;
using CSW.Unity.StreamingMap;
using CSW.Utility;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CSW.Unity.Tests
{
    public sealed class FoliageModuleTests
    {
        [Test]
        public void DisabledCapabilitiesSkipFoliageInitialization()
        {
            var previousCaps = GfxCaps.CurrentCaps;
            var gameObject = new GameObject(nameof(FoliageModuleTests));
            try
            {
                GfxCaps.CurrentCaps = Capability.None;
                var module = gameObject.AddComponent<FoliageModule>();
                var hiZDepthPyramid =
                    gameObject.AddComponent<BuiltInHiZDepthPyramid>();
                module.HiZDepthPyramid = hiZDepthPyramid;
                module.Features = new List<FeatureSet>
                {
                    new FeatureSet
                    {
                        SettingsType = SettingsFeatureType.Trees,
                    },
                    new FeatureSet
                    {
                        SettingsType = SettingsFeatureType.Grass,
                    },
                };

                InvokeStart(module);

                Assert.That(module.Features, Has.All.Matches<FeatureSet>(
                    feature => !feature.Enabled));
                Assert.That(GetField<bool>(module, "_hasEnabledFeatures"),
                    Is.False);
                Assert.That(hiZDepthPyramid.enabled, Is.False);
                Assert.That(GetField<int[]>(module, "_mappingTable"), Is.Null);
                Assert.That(module.Features, Has.All.Matches<FeatureSet>(
                    feature => feature.FoliageFeature == null &&
                               feature.InderectBuffer == null &&
                               feature.FoliageMaterial == null));
            }
            finally
            {
                GfxCaps.CurrentCaps = previousCaps;
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(0.3f, 100000f, false, ExpectedResult = true)]
        [TestCase(0.3f, 1000000f, false, ExpectedResult = true)]
        [TestCase(0.3f, 100000f, true, ExpectedResult = false)]
        public bool HiZOcclusionRequiresAFiniteFarPlane(
            float nearDistance, float farDistance, bool infiniteFar)
        {
            return FoliageModule.HasFiniteFarPlane(Camera.NativeProjection(
                60f, 16f / 9f, nearDistance, farDistance, infiniteFar));
        }

        private static void InvokeStart(FoliageModule module)
        {
            typeof(FoliageModule).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(module, null);
        }

        private static T GetField<T>(FoliageModule module, string name)
        {
            return (T)typeof(FoliageModule).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(module);
        }
    }
}
