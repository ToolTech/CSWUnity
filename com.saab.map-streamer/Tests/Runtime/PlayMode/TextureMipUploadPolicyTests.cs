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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Interop;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public class TextureMipUploadPolicyTests
    {
        [TearDown]
        public void TearDown()
        {
            Texture2DCache.Clear();
            Texture2DCache.ResetDiagnostics();
        }

        [Test]
        public void GraphicsPoolKeyContainsExactlyPhysicalLayout()
        {
            var first = Texture2DCache.GetOrCreateTexture(
                32, 16, GraphicsFormat.R8G8B8A8_UNorm, 1);
            Texture2DCache.Free(first);

            var exact = Texture2DCache.GetOrCreateTexture(
                32, 16, GraphicsFormat.R8G8B8A8_UNorm, 1);
            Assert.That(exact, Is.SameAs(first));

            var differentWidth = Texture2DCache.GetOrCreateTexture(
                31, 16, GraphicsFormat.R8G8B8A8_UNorm, 1);
            var differentHeight = Texture2DCache.GetOrCreateTexture(
                32, 15, GraphicsFormat.R8G8B8A8_UNorm, 1);
            var differentFormat = Texture2DCache.GetOrCreateTexture(
                32, 16, GraphicsFormat.R8G8B8A8_SRGB, 1);
            var differentMipCount = Texture2DCache.GetOrCreateTexture(
                32, 16, GraphicsFormat.R8G8B8A8_UNorm, 2);
            Assert.That(differentWidth, Is.Not.SameAs(exact));
            Assert.That(differentHeight, Is.Not.SameAs(exact));
            Assert.That(differentFormat, Is.Not.SameAs(exact));
            Assert.That(differentMipCount, Is.Not.SameAs(exact));

            Texture2DCache.Discard(exact);
            Texture2DCache.Discard(differentWidth);
            Texture2DCache.Discard(differentHeight);
            Texture2DCache.Discard(differentFormat);
            Texture2DCache.Discard(differentMipCount);
        }

        [Test]
        public void EligibleTextureRemainsReadableAndReusable()
        {
            var texture = CreateR8Texture();
            Assert.That(texture.isReadable, Is.True);
            texture.Apply(false, false);
            Texture2DCache.Free(texture);

            var recycled = CreateR8Texture();
            Assert.That(recycled, Is.SameAs(texture));
            Assert.That(recycled.isReadable, Is.True);
            Texture2DCache.Discard(recycled);
        }

        [Test]
        public void GeneratedMipChainRemainsReadableAndReusable()
        {
            var texture = Texture2DCache.GetOrCreateTexture(
                4,
                4,
                GraphicsFormat.R8_UNorm,
                3);

            var baseLevel = texture.GetPixelData<byte>(0);
            for (var index = 0; index < baseLevel.Length; index++)
                baseLevel[index] = byte.MaxValue;
            texture.Apply(true, false);

            AssertMipValue(texture, 1, byte.MaxValue);
            AssertMipValue(texture, 2, byte.MaxValue);
            Texture2DCache.Free(texture);

            var recycled = Texture2DCache.GetOrCreateTexture(
                4,
                4,
                GraphicsFormat.R8_UNorm,
                3);
            Assert.That(recycled, Is.SameAs(texture));

            baseLevel = recycled.GetPixelData<byte>(0);
            for (var index = 0; index < baseLevel.Length; index++)
                baseLevel[index] = 0;
            recycled.Apply(true, false);

            AssertMipValue(recycled, 1, 0);
            AssertMipValue(recycled, 2, 0);
            Texture2DCache.Discard(recycled);
        }

        [TestCase(GraphicsFormat.R8G8B8_SRGB, 1)]
        [TestCase(GraphicsFormat.R8G8B8_SRGB, 3)]
        [TestCase(GraphicsFormat.R8G8B8_UNorm, 1)]
        [TestCase(GraphicsFormat.R8G8B8_UNorm, 3)]
        public void Rgb24UploadRemainsSampleableAndRecyclable(
            GraphicsFormat format,
            int mipCount)
        {
            var texture = Texture2DCache.GetOrCreateTexture(
                7, 3, format, mipCount);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
            Assert.That(texture.graphicsFormat, Is.EqualTo(format));
            Assert.That(texture.mipmapCount, Is.EqualTo(mipCount));

            for (var mip = 0; mip < mipCount; mip++)
            {
                var data = texture.GetPixelData<byte>(mip);
                for (var index = 0; index < data.Length; index++)
                    data[index] = index % 3 == 0 ? byte.MaxValue : (byte)0;
            }
            texture.Apply(false, false);
            AssertSampledColor(texture, Color.red);
            Texture2DCache.Free(texture);

            var recycled = Texture2DCache.GetOrCreateTexture(
                7, 3, format, mipCount);
            Assert.That(recycled, Is.SameAs(texture));
            var baseData = recycled.GetPixelData<byte>(0);
            for (var index = 0; index < baseData.Length; index++)
                baseData[index] = index % 3 == 1 ? byte.MaxValue : (byte)0;
            recycled.Apply(mipCount > 1, false);
            for (var mip = 0; mip < mipCount; mip++)
            {
                var data = recycled.GetPixelData<byte>(mip);
                for (var index = 0; index < data.Length; index++)
                    Assert.That(
                        data[index],
                        Is.EqualTo(index % 3 == 1 ? byte.MaxValue : (byte)0));
            }
            AssertSampledColor(recycled, Color.green);
            Texture2DCache.Discard(recycled);
        }

        [TestCase(GraphicsFormat.R8G8B8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8_SRGB)]
        public void Rgb24UploadPreservesColorSpaceAndOpaqueAlpha(GraphicsFormat format)
        {
            var texture = Texture2DCache.GetOrCreateTexture(7, 3, format, 1);
            try
            {
                var data = texture.GetPixelData<byte>(0);
                var color = new byte[] { 128, 64, 192 };
                for (var index = 0; index < data.Length; index++)
                    data[index] = color[index % 3];
                texture.Apply(false, false);

                var expected = new Color(128f / 255f, 64f / 255f, 192f / 255f);
                if (GraphicsFormatUtility.IsSRGBFormat(format))
                    expected = expected.linear;
                AssertSampledColor(texture, expected);
            }
            finally
            {
                Texture2DCache.Discard(texture);
            }
        }

        [TestCase(GraphicsFormat.R8_UNorm)]
        [TestCase(GraphicsFormat.R8G8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8A8_SRGB)]
        [TestCase(GraphicsFormat.B8G8R8A8_UNorm)]
        [TestCase(GraphicsFormat.R32_SFloat)]
        [TestCase(GraphicsFormat.R32G32_SFloat)]
        [TestCase(GraphicsFormat.R16G16B16A16_SFloat)]
        [TestCase(GraphicsFormat.R32G32B32A32_SFloat)]
        [TestCase(GraphicsFormat.RGBA_DXT1_SRGB)]
        [TestCase(GraphicsFormat.RGBA_DXT3_SRGB)]
        [TestCase(GraphicsFormat.RGBA_DXT5_SRGB)]
        public void TextureCreationPreservesPhysicalStorage(
            GraphicsFormat format)
        {
            Assume.That(SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample));
            Assert.That(
                TextureExportInterop.IsFormatSupportedForSampling(format, false),
                Is.True);

            var texture = Texture2DCache.GetOrCreateTexture(8, 8, format, 4);
            try
            {
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.graphicsFormat, Is.EqualTo(format));
                Assert.That(texture.mipmapCount, Is.EqualTo(4));
                if (TextureExportInterop.TryGetTextureFormat(format, out var legacyFormat))
                    Assert.That(texture.format, Is.EqualTo(legacyFormat));
                Assert.That(
                    (ulong)texture.GetRawTextureData<byte>().Length,
                    Is.EqualTo(GraphicsFormatUtility.ComputeMipChainSize(8, 8, format, 4)));
            }
            finally
            {
                Texture2DCache.Discard(texture);
            }
        }

        [TestCase(GraphicsFormat.R8G8B8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8_SRGB)]
        public void UnityTextureCreationPreservesRgb24Storage(GraphicsFormat storageFormat)
        {
            Assert.That(
                TextureExportInterop.IsFormatSupportedForSampling(storageFormat, true),
                Is.True);
            var texture = TextureExportInterop.CreateTexture(7, 3, storageFormat, 1);
            try
            {
                Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB24));
                Assert.That(texture.graphicsFormat, Is.EqualTo(storageFormat));
                Assert.That(texture.GetRawTextureData<byte>().Length, Is.EqualTo(7 * 3 * 3));
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        [TestCase(GraphicsFormat.R8G8B8A8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8_UNorm)]
        [TestCase(GraphicsFormat.R16G16B16_UNorm)]
        public void SamplingSupportDoesNotImplyLinearFiltering(GraphicsFormat format)
        {
            Assert.That(
                TextureExportInterop.IsFormatSupportedForSampling(format, false),
                Is.True);
            var field = typeof(TextureExportInterop).GetField(
                "SupportedGraphicsFormats", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            var capabilities =
                (Dictionary<(GraphicsFormat, GraphicsFormatUsage), bool>)field.GetValue(null);
            var samplingFormat = SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample)
                ? format
                : SystemInfo.GetCompatibleFormat(format, GraphicsFormatUsage.Sample);
            var key = (samplingFormat, GraphicsFormatUsage.Linear);
            var hadValue = capabilities.TryGetValue(key, out var previous);
            // Exercise missing filter support independently of the test GPU.
            capabilities[key] = false;
            try
            {
                Assert.That(TextureExportInterop.IsFormatSupportedForSampling(format, false), Is.True);
                Assert.That(TextureExportInterop.IsFormatSupportedForSampling(format, true), Is.False);
            }
            finally
            {
                if (hadValue)
                    capabilities[key] = previous;
                else
                    capabilities.Remove(key);
            }
        }

        [Test]
        public void NoFormatIsNeverUploadable()
        {
            Assert.That(
                TextureExportInterop.IsFormatSupportedForSampling(GraphicsFormat.None, false),
                Is.False);
        }

        [Test]
        public void UnityTextureCreationAcceptsPackedFloatStorage()
        {
            const GraphicsFormat format = GraphicsFormat.E5B9G9R9_UFloatPack32;
            Assert.That(TextureExportInterop.IsFormatSupportedForSampling(format, true), Is.True);
            var texture = Texture2DCache.GetOrCreateTexture(7, 3, format, 1);
            try
            {
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.format, Is.EqualTo(TextureFormat.RGB9e5Float));
                Assert.That(texture.graphicsFormat, Is.EqualTo(format));
                var data = texture.GetPixelData<uint>(0);
                Assert.That(data.Length, Is.EqualTo(7 * 3));
                const uint packed = (15U << 27) | (384U << 18) | (256U << 9) | 128U;
                for (var i = 0; i < data.Length; i++)
                    data[i] = packed;
                texture.Apply(false, false);
                AssertSampledColor(texture, new Color(0.25f, 0.5f, 0.75f));
            }
            finally
            {
                Texture2DCache.Discard(texture);
            }
        }

        [TestCase(GraphicsFormat.RGBA_ETC2_UNorm, 1, 0f)]
        [TestCase(GraphicsFormat.RGBA_ETC2_UNorm, 4, 0f)]
        [TestCase(GraphicsFormat.RGBA_ETC2_SRGB, 1, 0f)]
        [TestCase(GraphicsFormat.RGBA_ETC2_SRGB, 4, 0f)]
        [TestCase(GraphicsFormat.RGB_ETC2_UNorm, 1, 1f)]
        [TestCase(GraphicsFormat.RGB_ETC2_SRGB, 1, 1f)]
        [TestCase(GraphicsFormat.RGB_A1_ETC2_UNorm, 1, 1f)]
        [TestCase(GraphicsFormat.RGB_A1_ETC2_SRGB, 1, 1f)]
        [TestCase(GraphicsFormat.R_EAC_UNorm, 1, 1f)]
        [TestCase(GraphicsFormat.RG_EAC_UNorm, 1, 1f)]
        public void UnityCanDecompressUnsupportedGpuFormats(
            GraphicsFormat format,
            int mipCount,
            float expectedAlpha)
        {
            Assert.That(TextureExportInterop.IsFormatSupportedForSampling(format, true), Is.True);
            var texture = Texture2DCache.GetOrCreateTexture(8, 8, format, mipCount);
            try
            {
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.graphicsFormat, Is.EqualTo(format));
                for (var mip = 0; mip < mipCount; mip++)
                {
                    var data = texture.GetPixelData<byte>(mip);
                    Assert.That(
                        (ulong)data.Length,
                        Is.EqualTo(GraphicsFormatUtility.ComputeMipmapSize(
                            Math.Max(1, 8 >> mip), Math.Max(1, 8 >> mip), format)));
                    for (var i = 0; i < data.Length; i++)
                        data[i] = 0;
                }
                texture.Apply(false, false);
                AssertSampledColor(texture, new Color(0f, 0f, 0f, expectedAlpha));
                Assert.That(texture.graphicsFormat, Is.EqualTo(format));
                Assert.That(
                    (ulong)texture.GetPixelData<byte>(0).Length,
                    Is.EqualTo(GraphicsFormatUtility.ComputeMipmapSize(8, 8, format)));
                Texture2DCache.Free(texture);
                var recycled = Texture2DCache.GetOrCreateTexture(8, 8, format, mipCount);
                Assert.That(recycled, Is.SameAs(texture));
                texture = recycled;
            }
            finally
            {
                Texture2DCache.Discard(texture);
            }
        }

        [TestCase(GraphicsFormat.R_EAC_SNorm, TextureFormat.EAC_R_SIGNED)]
        [TestCase(GraphicsFormat.RG_EAC_SNorm, TextureFormat.EAC_RG_SIGNED)]
        public void UnavailableSignedEacDecompressionFailsWithDiagnostics(
            GraphicsFormat format,
            TextureFormat legacyFormat)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 ||
                SystemInfo.SupportsTextureFormat(legacyFormat))
                Assert.Ignore("Requires the Direct3D 11 backend without native signed EAC support.");
            var createdBefore = new TextureManager().Diagnostics.Created;
            LogAssert.Expect(
                LogType.Error,
                $"Texture creation failed. '{legacyFormat}' is not supported on this platform. " +
                "Use 'SystemInfo.SupportsTextureFormat' C# API to check format support.");
            LogAssert.Expect(LogType.Error, $"Unity failed to create a texture with storage format {format}.");
            Assert.That(Texture2DCache.GetOrCreateTexture(8, 8, format, 1), Is.Null);
            Assert.That(new TextureManager().Diagnostics.Created, Is.EqualTo(createdBefore));
        }

        [Test]
        public void ClearResetsAccountingAndOwnership()
        {
            var manager = new TextureManager();
            var texture = CreateR8Texture();
            Texture2DCache.Free(texture);
            Assert.That(manager.Diagnostics.CachedBytes, Is.GreaterThan(0));

            Texture2DCache.Clear();

            Assert.That(manager.Diagnostics.CachedBytes, Is.Zero);
            Assert.That(manager.Diagnostics.CachedTextures, Is.Zero);
            var replacement = CreateR8Texture();
            Assert.That(replacement, Is.Not.SameAs(texture));
            Texture2DCache.Discard(replacement);
        }

        [Test]
        public void FreeReturnsTextureButDiscardNeverReusesIt()
        {
            var first = CreateR8Texture();
            Texture2DCache.Free(first);
            var reused = CreateR8Texture();
            Assert.That(reused, Is.SameAs(first));

            Texture2DCache.Discard(reused);
            var replacement = CreateR8Texture();
            Assert.That(replacement, Is.Not.SameAs(reused));
            Texture2DCache.Discard(replacement);
        }

        [Test]
        public void TextureManagerDiagnosticsUseConstantTimeCounters()
        {
            Texture2DCache.Clear();
            Texture2DCache.ResetDiagnostics();
            var manager = new TextureManager();
            var texture = CreateR8Texture();
            var key = new IntPtr(42);
            Assert.That(manager.TryAdd(key, texture, null), Is.True);
            Assert.That(manager.TryGet(key, out _, out _), Is.True);

            var diagnostics = manager.Diagnostics;
            Assert.That(diagnostics.ActiveTextures, Is.EqualTo(1));
            Assert.That(diagnostics.ActiveReferences, Is.EqualTo(2));
            Assert.That(diagnostics.Created, Is.EqualTo(1));
            Assert.That(diagnostics.CachedTextures, Is.Zero);

            Assert.That(manager.Free(texture), Is.True);
            Assert.That(manager.Free(texture), Is.True);
            diagnostics = manager.Diagnostics;
            Assert.That(diagnostics.ActiveTextures, Is.Zero);
            Assert.That(diagnostics.ActiveReferences, Is.Zero);
            Assert.That(diagnostics.CachedTextures, Is.EqualTo(1));
            Assert.That(diagnostics.CachedBytes, Is.GreaterThan(0));
            Assert.That(
                diagnostics.PeakCachedBytes,
                Is.EqualTo(diagnostics.CachedBytes));

            var reused = CreateR8Texture();
            Assert.That(reused, Is.SameAs(texture));
            diagnostics = manager.Diagnostics;
            Assert.That(diagnostics.CachedTextures, Is.Zero);
            Assert.That(diagnostics.CachedBytes, Is.Zero);
            Assert.That(diagnostics.Reused, Is.EqualTo(1));

            Texture2DCache.Discard(reused);
            diagnostics = manager.Diagnostics;
            Assert.That(diagnostics.Discarded, Is.EqualTo(1));
            Assert.That(diagnostics.Destroyed, Is.EqualTo(1));
        }

        [Test]
        public void TextureManagerReleasesEachAcquiredReferenceExactlyOnce()
        {
            var manager = new TextureManager();
            var texture = CreateR8Texture();
            var key = new IntPtr(42);
            Assert.That(manager.TryAdd(key, texture, null), Is.True);
            Assert.That(manager.TryGet(key, out var cached, out _), Is.True);
            Assert.That(cached, Is.SameAs(texture));

            Assert.That(manager.Free(texture), Is.True);
            Assert.That(manager.TryGet(key, out cached, out _), Is.True);
            Assert.That(cached, Is.SameAs(texture));
            Assert.That(manager.Free(texture), Is.True);
            Assert.That(manager.Free(texture), Is.True);
            Assert.That(manager.TryGet(key, out _, out _), Is.False);
        }

        [Test]
        public void ApplyFalsePreservesEveryMip()
        {
            var texture = new Texture2D(
                8, 8, GraphicsFormat.R8_UNorm, 4,
                TextureCreationFlags.None);
            try
            {
                var expected = new byte[texture.mipmapCount][];
                for (var mip = 0; mip < texture.mipmapCount; mip++)
                {
                    var data = texture.GetPixelData<byte>(mip);
                    expected[mip] = new byte[data.Length];
                    for (var index = 0; index < data.Length; index++)
                    {
                        var value = (byte)(mip * 37 + index * 11);
                        data[index] = value;
                        expected[mip][index] = value;
                    }
                }

                texture.Apply(false, false);

                for (var mip = 0; mip < texture.mipmapCount; mip++)
                    CollectionAssert.AreEqual(
                        expected[mip],
                        texture.GetPixelData<byte>(mip).ToArray());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        [Test]
        public void BuildDiagnosticsAccumulateAndResetCoherently()
        {
            SceneManager.ResetTotalBuildDiagnostics();
            var resetVersion = GetSceneManagerStaticField<long>(
                "_buildDiagnosticsResetVersion");
            InvokeRecorder("RecordMeshBuild", 512, Stopwatch.Frequency);
            InvokeRecorder(
                "RecordTextureBuild",
                1024,
                Stopwatch.Frequency / 2);

            var mesh = SceneManager.MeshTotalDiagnostics;
            var texture = SceneManager.TextureTotalDiagnostics;
            Assert.That(mesh.TotalMeshes, Is.EqualTo(1));
            Assert.That(mesh.TotalBytes, Is.EqualTo(512));
            Assert.That(mesh.TotalTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(mesh.WorstTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(texture.TotalTextures, Is.EqualTo(1));
            Assert.That(texture.TotalBytes, Is.EqualTo(1024));
            Assert.That(
                texture.TotalTime.TotalSeconds,
                Is.EqualTo(0.5).Within(1.0 / Stopwatch.Frequency));
            Assert.That(
                texture.WorstTime.TotalSeconds,
                Is.EqualTo(0.5).Within(1.0 / Stopwatch.Frequency));
            SceneManager.ResetTotalBuildDiagnostics();
            Assert.That(SceneManager.MeshTotalDiagnostics.TotalMeshes, Is.Zero);
            Assert.That(
                SceneManager.MeshTotalDiagnostics.WorstTime,
                Is.EqualTo(TimeSpan.Zero));
            Assert.That(
                SceneManager.TextureTotalDiagnostics.TotalTextures,
                Is.Zero);
            Assert.That(
                GetSceneManagerStaticField<long>(
                    "_buildDiagnosticsResetVersion"),
                Is.EqualTo(resetVersion + 1));
        }

        private static void AssertSampledColor(Texture2D texture, Color expected)
        {
            var previousTarget = RenderTexture.active;
            var target = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                readback.ReadPixels(
                    new Rect(0, 0, texture.width, texture.height), 0, 0);
                var actual = readback.GetPixel(0, 0);
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
                Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.01f));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(readback);
            }
        }

        private static void InvokeRecorder(
            string methodName,
            long bytes,
            long ticks)
        {
            var method = typeof(SceneManager).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { bytes, ticks });
        }

        private static Texture2D CreateR8Texture()
        {
            return Texture2DCache.GetOrCreateTexture(
                16,
                16,
                GraphicsFormat.R8_UNorm,
                1);
        }

        private static void AssertMipValue(
            Texture2D texture,
            int mipLevel,
            byte expected)
        {
            var data = texture.GetPixelData<byte>(mipLevel);
            for (var index = 0; index < data.Length; index++)
                Assert.That(data[index], Is.EqualTo(expected));
        }

        private static T GetSceneManagerStaticField<T>(string name)
        {
            return (T)typeof(SceneManager).GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

    }
}
