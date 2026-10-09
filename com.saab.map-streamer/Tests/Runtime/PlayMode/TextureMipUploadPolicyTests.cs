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
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
