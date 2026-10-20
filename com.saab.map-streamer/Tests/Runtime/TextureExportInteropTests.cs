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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Interop;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public class TextureExportInteropTests
    {
        [Test]
        public void AbiMatchesDeployedTextureExportContract()
        {
            Assert.That(TextureExport.AbiVersion, Is.EqualTo(1));
            Assert.That(
                Marshal.SizeOf(GetNestedType("Request")),
                Is.EqualTo(12));
            Assert.That(Marshal.SizeOf<TextureExport.Info>(), Is.EqualTo(32));
            Assert.That(
                typeof(TextureExport.Info)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(field => field.Name),
                Is.EqualTo(new[]
                {
                    "Version",
                    "StructSize",
                    "ImageFormat",
                    "ComponentType",
                    "Components",
                    "Width",
                    "Height",
                    "LevelCount",
                }));
            Assert.That(
                Marshal.SizeOf<TextureExport.LevelDestination>(),
                Is.EqualTo(40));
            Assert.That(
                typeof(TextureExport).GetNestedType(
                    "NativeLevelDestination",
                    BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                Marshal.SizeOf(GetNestedType("NativeDestination")),
                Is.EqualTo(IntPtr.Size == 8 ? 24 : 20));
            Assert.That(
                typeof(TextureExport)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method =>
                        method.Name.StartsWith(
                            "TextureExport_",
                            StringComparison.Ordinal))
                    .Select(method => method.Name),
                Is.EquivalentTo(new[]
                {
                    "TextureExport_prepare",
                    "TextureExport_copyPrepared",
                }));

            Assert.That(
                Enum.GetValues(typeof(TextureExport.Status))
                    .Cast<TextureExport.Status>()
                    .Select(value => (uint)value),
                Is.EqualTo(Enumerable.Range(0, 15).Select(value => (uint)value)));
            Assert.That(
                Enum.GetNames(typeof(TextureExport.Status)),
                Is.EqualTo(new[]
                {
                    "Success",
                    "NullTexture",
                    "BadVersion",
                    "BadSize",
                    "UnsupportedRequest",
                    "UnsupportedFormat",
                    "MissingLocalData",
                    "IncompleteMipChain",
                    "InvalidDestination",
                    "DestinationTooSmall",
                    "InternalFailure",
                    "BadStride",
                    "InvalidLevelCoverage",
                    "Overflow",
                    "InconsistentMetadata",
                }));
        }

        [Test]
        public void PublicApiIsInfoOnlyAndUsesOrderedDestinations()
        {
            Assert.That(
                typeof(TextureExport.Prepared)
                    .GetProperties(
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.DeclaredOnly)
                    .Select(property => property.Name),
                Is.EqualTo(new[] { "Info" }));
            Assert.That(
                typeof(TextureExport)
                    .GetNestedTypes(BindingFlags.Public)
                    .Select(type => type.Name),
                Is.EquivalentTo(new[]
                {
                    "Status",
                    "SourceSelection",
                    "Info",
                    "LevelDestination",
                    "Destination",
                    "Prepared",
                }));

            Assert.That(
                typeof(TextureExport.Destination).GetConstructor(
                    new[] { typeof(int) }),
                Is.Not.Null);
            Assert.That(
                typeof(TextureExport.Destination).GetMethod(
                    "SetLevelDestination",
                    new[]
                    {
                        typeof(int),
                        typeof(TextureExport.LevelDestination).MakeByRefType(),
                    }),
                Is.Not.Null);
            Assert.That(
                typeof(TextureExport.Prepared).GetMethod(
                    "Copy",
                    new[] { typeof(TextureExport.Destination) }),
                Is.Not.Null);
        }

        [Test]
        public void LevelDestinationIsReadonlyAndHasExactShape()
        {
            var descriptor = new TextureExport.LevelDestination(
                new IntPtr(0x1234),
                128,
                7,
                16,
                64);

            Assert.That(descriptor.Pointer, Is.EqualTo(new IntPtr(0x1234)));
            Assert.That(descriptor.Capacity, Is.EqualTo(128));
            Assert.That(descriptor.Offset, Is.EqualTo(7));
            Assert.That(descriptor.RowPitch, Is.EqualTo(16));
            Assert.That(descriptor.Span, Is.EqualTo(64));
            Assert.That(
                typeof(TextureExport.LevelDestination)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance),
                Is.Empty);
            Assert.That(
                typeof(TextureExport.LevelDestination)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(field => field.Name),
                Is.EqualTo(new[]
                {
                    "Pointer",
                    "Capacity",
                    "Offset",
                    "RowPitch",
                    "Span",
                }));
            Assert.That(
                typeof(TextureExport.LevelDestination)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .All(field => field.IsInitOnly),
                Is.True);
            Assert.That(
                typeof(TextureExport.LevelDestination)
                    .StructLayoutAttribute.Pack,
                Is.EqualTo(8));

            var destination = new TextureExport.Destination(1);
            Assert.DoesNotThrow(() =>
                destination.SetLevelDestination(0, in descriptor));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                destination.SetLevelDestination(1, in descriptor));
        }

        [Test]
        public void ValidateInfoRejectsBadRootAbiAndDimensions()
        {
            var info = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.UNSIGNED_BYTE,
                1,
                4,
                4,
                1);

            info.Version++;
            AssertStatus(
                info,
                TextureExport.SourceSelection.BaseOnly,
                1,
                TextureExport.Status.BadVersion);

            info = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.UNSIGNED_BYTE,
                1,
                4,
                4,
                1);
            info.StructSize--;
            AssertStatus(
                info,
                TextureExport.SourceSelection.BaseOnly,
                1,
                TextureExport.Status.BadSize);

            info = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.UNSIGNED_BYTE,
                1,
                0,
                4,
                1);
            AssertStatus(
                info,
                TextureExport.SourceSelection.BaseOnly,
                1,
                TextureExport.Status.InconsistentMetadata);
        }

        [TestCase(1U, 1U, 1U)]
        [TestCase(7U, 5U, 3U)]
        [TestCase(512U, 512U, 10U)]
        [TestCase(1024U, 512U, 11U)]
        public void CompleteSelectionRequiresCanonicalMipCount(
            uint width,
            uint height,
            uint levelCount)
        {
            var info = CreateInfo(
                ImageFormat.COMPRESSED_RGB_S3TC_DXT1,
                ComponentType.UNSIGNED_BYTE,
                3,
                width,
                height,
                levelCount);
            AssertStatus(
                info,
                TextureExport.SourceSelection.CompleteMipChain,
                0,
                TextureExport.Status.Success);

            if (levelCount > 1)
            {
                info.LevelCount--;
                AssertStatus(
                    info,
                    TextureExport.SourceSelection.CompleteMipChain,
                    0,
                    TextureExport.Status.IncompleteMipChain);
            }
        }

        [Test]
        public void CompleteMipSelectionIsSupportedForR8()
        {
            var r8 = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.UNSIGNED_BYTE,
                1,
                8,
                8,
                4);
            AssertStatus(
                r8,
                TextureExport.SourceSelection.CompleteMipChain,
                1,
                TextureExport.Status.Success);
        }

        [TestCase(ImageFormat.COMPRESSED_RGB_S3TC_DXT1, ComponentType.UNSIGNED_BYTE, 3U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RGBA_S3TC_DXT1, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.LUMINANCE, ComponentType.UNSIGNED_BYTE, 1U, 1U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.RGBA, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.RGBA, ComponentType.HALF_FLOAT, 4U, 0U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.RGBA, ComponentType.FLOAT, 4U, 0U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.RGB, ComponentType.UNSIGNED_BYTE, 3U, 0U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.BGR, ComponentType.UNSIGNED_BYTE, 3U, 0U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.COMPRESSED_RGBA_S3TC_DXT3, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RGBA8_ETC2, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RGB8_ETC2, ComponentType.UNSIGNED_BYTE, 3U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RGB8_A1_ETC2, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RGBA_S3TC_DXT5, ComponentType.UNSIGNED_BYTE, 4U, 0U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_R11_EAC, ComponentType.UNSIGNED_BYTE, 1U, 1U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.COMPRESSED_RG11_EAC, ComponentType.UNSIGNED_BYTE, 2U, 1U, TextureExport.SourceSelection.CompleteMipChain)]
        [TestCase(ImageFormat.LUMINANCE, ComponentType.FLOAT, 1U, 1U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.LUMINANCE_ALPHA, ComponentType.UNSIGNED_BYTE, 2U, 1U, TextureExport.SourceSelection.BaseOnly)]
        [TestCase(ImageFormat.LUMINANCE_ALPHA, ComponentType.FLOAT, 2U, 1U, TextureExport.SourceSelection.BaseOnly)]
        public void MapsEveryUnitySupportedPhysicalTuple(
            ImageFormat imageFormat,
            ComponentType componentType,
            uint components,
            uint unit,
            TextureExport.SourceSelection selection)
        {
            var info = CreateInfo(
                imageFormat,
                componentType,
                components,
                1,
                1,
                1);

            var status = TextureExportInterop.ValidateInfo(
                in info,
                selection,
                unit,
                out var mapping);

            Assert.That(status, Is.EqualTo(TextureExport.Status.Success));
            Assert.That(mapping.ImageFormat, Is.EqualTo(imageFormat));
            Assert.That(mapping.ComponentType, Is.EqualTo(componentType));
            Assert.That(mapping.Components, Is.EqualTo(components));
            Assert.That(mapping.GraphicsFormat, Is.Not.EqualTo(GraphicsFormat.None));
        }

        [Test]
        public void RgbaUnsignedByteMappingUsesOnlyPhysicalTuple()
        {
            var info = CreateInfo(
                ImageFormat.RGBA,
                ComponentType.UNSIGNED_BYTE,
                4,
                1,
                1,
                1);

            var status = TextureExportInterop.ValidateInfo(
                in info,
                TextureExport.SourceSelection.BaseOnly,
                0,
                out var mapping);

            Assert.That(status, Is.EqualTo(TextureExport.Status.Success));
            Assert.That(mapping.GraphicsFormat,
                Is.EqualTo(GraphicsFormat.R8G8B8A8_SRGB));
        }

        [Test]
        public void UnsupportedPhysicalTupleIsTerminal()
        {
            var info = CreateInfo(
                ImageFormat.RGBA,
                ComponentType.UNSIGNED_SHORT,
                4,
                1,
                1,
                1);
            AssertStatus(
                info,
                TextureExport.SourceSelection.BaseOnly,
                0,
                TextureExport.Status.UnsupportedFormat);
        }

        [TestCase(ImageFormat.COLOR_INDEX, ComponentType.UNSIGNED_BYTE, 1U)]
        [TestCase(ImageFormat.DEPTH_COMPONENT, ComponentType.FLOAT, 1U)]
        [TestCase(ImageFormat.ABGR, ComponentType.UNSIGNED_BYTE, 4U)]
        [TestCase(ImageFormat.RGBA, ComponentType.UNSIGNED_SHORT, 4U)]
        [TestCase(ImageFormat.RGBA, ComponentType.UNSIGNED_BYTE, 3U)]
        public void ExplicitlyRejectsTuplesWithoutUnityDestinationMapping(
            ImageFormat imageFormat,
            ComponentType componentType,
            uint components)
        {
            var info = CreateInfo(
                imageFormat,
                componentType,
                components,
                1,
                1,
                1);

            AssertStatus(
                info,
                TextureExport.SourceSelection.BaseOnly,
                0,
                TextureExport.Status.UnsupportedFormat);
        }

        [Test]
        public void DataFormatsAreAllowedOnUsedTextureUnits()
        {
            var scalarOnMain = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.FLOAT,
                1,
                1,
                1,
                1);
            AssertStatus(
                scalarOnMain,
                TextureExport.SourceSelection.BaseOnly,
                0,
                TextureExport.Status.Success);

            var redOnOptionalUnit = CreateInfo(
                ImageFormat.LUMINANCE,
                ComponentType.UNSIGNED_BYTE,
                1,
                1,
                1,
                1);
            AssertStatus(
                redOnOptionalUnit,
                TextureExport.SourceSelection.BaseOnly,
                1,
                TextureExport.Status.Success);
        }

        [Test]
        public void CreatesMeasuredR8Destination()
        {
            var texture = new Texture2D(
                7,
                3,
                GraphicsFormat.R8_UNorm,
                TextureCreationFlags.None);
            try
            {
                var info = CreateInfo(
                    ImageFormat.LUMINANCE,
                    ComponentType.UNSIGNED_BYTE,
                    1,
                    7,
                    3,
                    1);
                var status = TextureExportInterop.ValidateInfo(
                    in info,
                    TextureExport.SourceSelection.BaseOnly,
                    1,
                    out var mapping);
                Assert.That(status, Is.EqualTo(TextureExport.Status.Success));

                status = TextureExportInterop.CreateDestination(
                    texture,
                    in info,
                    in mapping,
                    out var destination,
                    out var rawTextureBytes);

                Assert.That(status, Is.EqualTo(TextureExport.Status.Success));
                Assert.That(destination, Is.Not.Null);
                Assert.That(rawTextureBytes, Is.EqualTo(21));

                var nativeLevels = (Array)typeof(TextureExport.Destination)
                    .GetField(
                        "levels",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(destination);
                Assert.That(nativeLevels.Length, Is.EqualTo(1));
                var nativeLevel = nativeLevels.GetValue(0);
                var nativeType = nativeLevel.GetType();
                Assert.That(
                    (IntPtr)nativeType.GetField("Pointer").GetValue(nativeLevel),
                    Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(
                    (ulong)nativeType.GetField("Capacity").GetValue(nativeLevel),
                    Is.EqualTo(21));
                Assert.That(
                    (ulong)nativeType.GetField("Offset").GetValue(nativeLevel),
                    Is.Zero);
                Assert.That(
                    (ulong)nativeType.GetField("RowPitch").GetValue(nativeLevel),
                    Is.EqualTo(7));
                Assert.That(
                    (ulong)nativeType.GetField("Span").GetValue(nativeLevel),
                    Is.EqualTo(21));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(0U, GraphicsFormat.R8G8B8_SRGB)]
        [TestCase(1U, GraphicsFormat.R8G8B8_UNorm)]
        public void CreatesPackedRgb24DestinationsForCompatibleUpload(
            uint unit,
            GraphicsFormat graphicsFormat)
        {
            var texture = new Texture2D(
                7, 3, TextureFormat.RGB24, 3,
                linear: unit != 0);
            try
            {
                var info = CreateInfo(
                    ImageFormat.RGB,
                    ComponentType.UNSIGNED_BYTE,
                    3,
                    7,
                    3,
                    3);
                Assert.That(
                    TextureExportInterop.ValidateInfo(
                        in info,
                        TextureExport.SourceSelection.CompleteMipChain,
                        unit,
                        out var mapping),
                    Is.EqualTo(TextureExport.Status.Success));
                Assert.That(mapping.GraphicsFormat, Is.EqualTo(graphicsFormat));
                Assert.That(
                    TextureExportInterop.IsFormatSupportedForSampling(
                        graphicsFormat, true),
                    Is.True);
                Assert.That(
                    TextureExportInterop.CreateDestination(
                        texture,
                        in info,
                        in mapping,
                        out var destination,
                        out var rawTextureBytes),
                    Is.EqualTo(TextureExport.Status.Success));
                Assert.That(destination, Is.Not.Null);
                Assert.That(rawTextureBytes, Is.EqualTo(63 + 9 + 3));
                Assert.That(texture.GetPixelData<byte>(0).Length, Is.EqualTo(63));
                Assert.That(texture.GetPixelData<byte>(1).Length, Is.EqualTo(9));
                Assert.That(texture.GetPixelData<byte>(2).Length, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CreatesOpaqueContiguousCompressedDestinations()
        {
            var texture = new Texture2D(
                8,
                8,
                GraphicsFormat.RGBA_DXT1_UNorm,
                TextureCreationFlags.MipChain);
            try
            {
                var info = CreateInfo(
                    ImageFormat.COMPRESSED_RGB_S3TC_DXT1,
                    ComponentType.UNSIGNED_BYTE,
                    3,
                    8,
                    8,
                    4);
                Assert.That(
                    TextureExportInterop.ValidateInfo(
                        in info,
                        TextureExport.SourceSelection.CompleteMipChain,
                        1,
                        out var mapping),
                    Is.EqualTo(TextureExport.Status.Success));

                Assert.That(
                    TextureExportInterop.CreateDestination(
                        texture,
                        in info,
                        in mapping,
                        out var destination,
                        out var rawTextureBytes),
                    Is.EqualTo(TextureExport.Status.Success));

                var levels = (Array)typeof(TextureExport.Destination)
                    .GetField(
                        "levels",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(destination);
                Assert.That(levels.Length, Is.EqualTo(4));
                foreach (var level in levels)
                {
                    var levelType = level.GetType();
                    Assert.That(
                        (ulong)levelType.GetField("RowPitch").GetValue(level),
                        Is.EqualTo(
                            (ulong)levelType.GetField("Span").GetValue(level)));
                }
                Assert.That(rawTextureBytes, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CreateDestinationRejectsMismatchedMetadata()
        {
            var texture = new Texture2D(
                4,
                4,
                GraphicsFormat.R8_UNorm,
                TextureCreationFlags.None);
            try
            {
                var info = CreateInfo(
                    ImageFormat.LUMINANCE,
                    ComponentType.UNSIGNED_BYTE,
                    1,
                    4,
                    4,
                    1);
                Assert.That(
                    TextureExportInterop.ValidateInfo(
                        in info,
                        TextureExport.SourceSelection.BaseOnly,
                        1,
                        out var mapping),
                    Is.EqualTo(TextureExport.Status.Success));
                info.ImageFormat = ImageFormat.LUMINANCE_ALPHA;
                info.Components = 2;

                Assert.That(
                    TextureExportInterop.CreateDestination(
                        texture,
                        in info,
                        in mapping,
                        out _,
                        out _),
                    Is.EqualTo(TextureExport.Status.InconsistentMetadata));
            }

            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(GraphicsFormat.R8_UNorm, TextureFormat.R8)]
        [TestCase(GraphicsFormat.R8G8_UNorm, TextureFormat.RG16)]
        [TestCase(GraphicsFormat.R8G8B8_UNorm, TextureFormat.RGB24)]
        [TestCase(GraphicsFormat.R8G8B8_SRGB, TextureFormat.RGB24)]
        [TestCase(GraphicsFormat.R16G16B16_UNorm, TextureFormat.RGB48)]
        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, TextureFormat.RGBA32)]
        [TestCase(GraphicsFormat.B8G8R8A8_UNorm, TextureFormat.BGRA32)]
        [TestCase(GraphicsFormat.R32_SFloat, TextureFormat.RFloat)]
        [TestCase(GraphicsFormat.R32G32_SFloat, TextureFormat.RGFloat)]
        [TestCase(GraphicsFormat.R16G16B16A16_SFloat, TextureFormat.RGBAHalf)]
        [TestCase(GraphicsFormat.R32G32B32A32_SFloat, TextureFormat.RGBAFloat)]
        [TestCase(GraphicsFormat.E5B9G9R9_UFloatPack32, TextureFormat.RGB9e5Float)]
        [TestCase(GraphicsFormat.RGBA_DXT1_SRGB, TextureFormat.DXT1)]
        [TestCase(GraphicsFormat.RGBA_DXT5_SRGB, TextureFormat.DXT5)]
        [TestCase(GraphicsFormat.RGBA_ETC2_SRGB, TextureFormat.ETC2_RGBA8)]
        public void LegacyTextureFormatPreservesPhysicalStorage(
            GraphicsFormat storageFormat,
            TextureFormat expectedLegacyFormat)
        {
            Assert.That(
                TextureExportInterop.TryGetTextureFormat(storageFormat, out var legacyFormat),
                Is.True);
            Assert.That(legacyFormat, Is.EqualTo(expectedLegacyFormat));
            Assert.That(
                GraphicsFormatUtility.GetGraphicsFormat(
                    legacyFormat, GraphicsFormatUtility.IsSRGBFormat(storageFormat)),
                Is.EqualTo(storageFormat));
        }

        [TestCase(GraphicsFormat.None)]
        [TestCase(GraphicsFormat.B8G8R8_UNorm)]
        [TestCase(GraphicsFormat.B8G8R8_SRGB)]
        [TestCase(GraphicsFormat.RGBA_DXT3_UNorm)]
        [TestCase(GraphicsFormat.RGBA_DXT3_SRGB)]
        [TestCase(GraphicsFormat.R32_UInt)]
        public void LegacyTextureFormatRejectsMissingOrInexactMappings(GraphicsFormat storageFormat)
        {
            Assert.That(
                TextureExportInterop.TryGetTextureFormat(storageFormat, out _),
                Is.False);
        }

        [Test]
        public void NoFormatCannotCreateTexture()
        {
            Assert.Throws<ArgumentException>(
                () => TextureExportInterop.CreateTexture(4, 4, GraphicsFormat.None, 1));
        }

        [Test]
        public void BroaderUnityCompatibilityDoesNotEnableUnsupportedNativeSourceLayouts()
        {
            var info = CreateInfo(
                ImageFormat.RGB, ComponentType.UNSIGNED_SHORT, 3, 7, 3, 1);
            AssertStatus(
                info, TextureExport.SourceSelection.BaseOnly, 0,
                TextureExport.Status.UnsupportedFormat);
        }

        [TestCase(GraphicsFormat.R8G8B8A8_UNorm, 4, 4)]
        [TestCase(GraphicsFormat.R8G8B8_UNorm, 8, 2)]
        public void CreateDestinationRejectsDifferentPhysicalTextureLayout(
            GraphicsFormat format,
            int width,
            int height)
        {
            var texture = new Texture2D(
                width, height, GraphicsFormatUtility.GetTextureFormat(format), 1, true);
            try
            {
                var info = CreateInfo(
                    ImageFormat.RGB, ComponentType.UNSIGNED_BYTE, 3, 4, 4, 1);
                Assert.That(
                    TextureExportInterop.ValidateInfo(
                        in info, TextureExport.SourceSelection.BaseOnly, 1, out var mapping),
                    Is.EqualTo(TextureExport.Status.Success));
                Assert.That(
                    TextureExportInterop.CreateDestination(
                        texture, in info, in mapping, out var destination, out var bytes),
                    Is.EqualTo(TextureExport.Status.InconsistentMetadata));
                Assert.That(destination, Is.Null);
                Assert.That(bytes, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Type GetNestedType(string name)
        {
            var type = typeof(TextureExport).GetNestedType(
                name,
                BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static TextureExport.Info CreateInfo(
            ImageFormat imageFormat,
            ComponentType componentType,
            uint components,
            uint width,
            uint height,
            uint levelCount)
        {
            return new TextureExport.Info
            {
                Version = TextureExport.AbiVersion,
                StructSize = (uint)Marshal.SizeOf<TextureExport.Info>(),
                Width = width,
                Height = height,
                LevelCount = levelCount,
                ImageFormat = imageFormat,
                ComponentType = componentType,
                Components = components,
            };
        }

        private static void AssertStatus(
            TextureExport.Info info,
            TextureExport.SourceSelection selection,
            uint unit,
            TextureExport.Status expected)
        {
            Assert.That(
                TextureExportInterop.ValidateInfo(
                    in info,
                    selection,
                    unit,
                    out _),
                Is.EqualTo(expected));
        }
    }
}
