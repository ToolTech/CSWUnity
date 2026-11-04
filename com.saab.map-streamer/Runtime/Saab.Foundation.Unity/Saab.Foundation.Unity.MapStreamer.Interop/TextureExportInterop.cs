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
using System.Runtime.InteropServices;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;


namespace Saab.Foundation.Unity.MapStreamer.Interop
{
    public readonly struct TextureExportUnityFormat
    {
        public readonly ImageFormat ImageFormat;
        public readonly ComponentType ComponentType;
        public readonly uint Components;
        public readonly GraphicsFormat GraphicsFormat;
        internal readonly bool IsCompressed;
        internal readonly uint BytesPerPixel;

        internal TextureExportUnityFormat(
            ImageFormat imageFormat,
            ComponentType componentType,
            uint components,
            GraphicsFormat graphicsFormat,
            bool isCompressed,
            uint bytesPerPixel)
        {
            ImageFormat = imageFormat;
            ComponentType = componentType;
            Components = components;
            GraphicsFormat = graphicsFormat;
            IsCompressed = isCompressed;
            BytesPerPixel = bytesPerPixel;
        }
    }

    public static class TextureExportInterop
    {
        private static readonly uint TextureExportInfoSize =
            (uint)Marshal.SizeOf<TextureExport.Info>();

        private static readonly Dictionary<
            (GraphicsFormat Format, GraphicsFormatUsage Usage),
            bool> SupportedGraphicsFormats =
                new Dictionary<
                    (GraphicsFormat Format, GraphicsFormatUsage Usage),
                    bool>();

        public static TextureExport.Status ValidateInfo(
            in TextureExport.Info info,
            TextureExport.SourceSelection requestedSelection,
            uint textureUnit,
            out TextureExportUnityFormat unityFormat)
        {
            unityFormat = default;
            if (info.Version != TextureExport.AbiVersion)
                return TextureExport.Status.BadVersion;
            if (info.StructSize < TextureExportInfoSize)
                return TextureExport.Status.BadSize;
            if (info.Width == 0 ||
                info.Height == 0 ||
                info.LevelCount == 0 ||
                info.Width > int.MaxValue ||
                info.Height > int.MaxValue ||
                info.LevelCount > int.MaxValue)
            {
                return TextureExport.Status.InconsistentMetadata;
            }
            var mappingStatus = TryGetUnityFormat(
                in info,
                textureUnit,
                out unityFormat);
            if (mappingStatus != TextureExport.Status.Success)
                return mappingStatus;

            var expectedLevelCount =
                requestedSelection == TextureExport.SourceSelection.BaseOnly
                    ? 1U
                    : CalculateMipCount(info.Width, info.Height);
            if (info.LevelCount != expectedLevelCount)
                return TextureExport.Status.IncompleteMipChain;
            return TextureExport.Status.Success;
        }

        public static unsafe TextureExport.Status CreateDestination(
            Texture2D texture,
            in TextureExport.Info info,
            in TextureExportUnityFormat unityFormat,
            out TextureExport.Destination destination,
            out ulong rawTextureBytes)
        {
            destination = null;
            rawTextureBytes = 0;
            if (!texture ||
                info.Width == 0 || info.Height == 0 ||
                info.LevelCount == 0 || info.Width > int.MaxValue ||
                info.Height > int.MaxValue || info.LevelCount > int.MaxValue)
                return TextureExport.Status.InconsistentMetadata;

            if (unityFormat.ImageFormat != info.ImageFormat ||
                unityFormat.ComponentType != info.ComponentType ||
                unityFormat.Components != info.Components ||
                texture.graphicsFormat != unityFormat.GraphicsFormat ||
                texture.width != (int)info.Width ||
                texture.height != (int)info.Height ||
                texture.mipmapCount < (int)info.LevelCount)
                return TextureExport.Status.InconsistentMetadata;
            var rawData = texture.GetRawTextureData<byte>();
            var rawCapacity = (ulong)rawData.Length;
            var rawPointer =
                (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(rawData);
            if (rawCapacity == 0)
                return TextureExport.Status.DestinationTooSmall;
            if (rawPointer == IntPtr.Zero)
                return TextureExport.Status.InvalidDestination;
            var rawAddress = ToAddress(rawPointer);
            if (!TryAdd(rawAddress, rawCapacity, out var rawEnd))
                return TextureExport.Status.Overflow;

            var levelCount = (int)info.LevelCount;
            var builtDestination =
                new TextureExport.Destination(levelCount);
            var expectedWidth = info.Width;
            var expectedHeight = info.Height;
            for (var index = 0; index < levelCount; index++)
            {
                var mipData = texture.GetPixelData<byte>(index);
                var mipSpan = (ulong)mipData.Length;
                var mipPointer =
                    (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(mipData);
                var mipAddress = ToAddress(mipPointer);
                if (mipPointer == IntPtr.Zero ||
                    mipAddress < rawAddress ||
                    !TryAdd(mipAddress, mipSpan, out var mipEnd) ||
                    mipEnd > rawEnd)
                    return TextureExport.Status.InvalidDestination;

                ulong rowCount;
                ulong validBytesPerRow;
                if (unityFormat.IsCompressed)
                {
                    rowCount = 1;
                    validBytesPerRow = mipSpan;
                }
                else
                {
                    rowCount = expectedHeight;
                    validBytesPerRow =
                        (ulong)expectedWidth * unityFormat.BytesPerPixel;
                }
                if (rowCount == 0 ||
                    mipSpan < validBytesPerRow ||
                    mipSpan % rowCount != 0)
                    return TextureExport.Status.InvalidDestination;

                // Unity does not expose CPU row pitch. GetPixelData returns the
                // complete mip view, so accept only a uniform pitch proven by
                // exact divisibility by physical rows. Compressed payloads use
                // one opaque row, as required by the SDK copy contract.
                var rowPitch = mipSpan / rowCount;
                if (rowPitch < validBytesPerRow)
                    return TextureExport.Status.DestinationTooSmall;
                var levelDestination =
                    new TextureExport.LevelDestination(
                        rawPointer,
                        rawCapacity,
                        mipAddress - rawAddress,
                        rowPitch,
                        mipSpan);
                builtDestination.SetLevelDestination(
                    index,
                    in levelDestination);
                expectedWidth = Math.Max(1U, expectedWidth / 2U);
                expectedHeight = Math.Max(1U, expectedHeight / 2U);
            }

            destination = builtDestination;
            rawTextureBytes = rawCapacity;
            return TextureExport.Status.Success;
        }

        public static bool IsFormatSupportedForSampling(
            GraphicsFormat graphicsFormat,
            bool requireLinearFiltering)
        {
            var samplingFormat = graphicsFormat;
            if (!IsFormatSupported(graphicsFormat, GraphicsFormatUsage.Sample))
            {
                if (!TryGetTextureFormat(graphicsFormat, out var textureFormat) ||
                    (!SystemInfo.SupportsTextureFormat(textureFormat) &&
                        !GraphicsFormatUtility.IsCompressedFormat(graphicsFormat)))
                    return false;
                samplingFormat = SystemInfo.GetCompatibleFormat(
                    graphicsFormat, GraphicsFormatUsage.Sample);
            }

            return IsFormatSupported(samplingFormat, GraphicsFormatUsage.Sample) &&
                (!requireLinearFiltering ||
                    IsFormatSupported(samplingFormat, GraphicsFormatUsage.Linear));
        }

        public static Texture2D CreateTexture(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount)
        {
            if (format == GraphicsFormat.None)
                throw new ArgumentException("A source texture format is required.", nameof(format));
            return TryGetTextureFormat(format, out var textureFormat)
                ? new Texture2D(
                    width, height, textureFormat, mipCount,
                    linear: !GraphicsFormatUtility.IsSRGBFormat(format))
                : new Texture2D(
                    width, height, format, mipCount, TextureCreationFlags.None);
        }

        public static bool TryGetTextureFormat(
            GraphicsFormat graphicsFormat,
            out TextureFormat textureFormat)
        {
            textureFormat = default;
            if (graphicsFormat == GraphicsFormat.None)
                return false;

            var legacyFormat = GraphicsFormatUtility.GetTextureFormat(graphicsFormat);
            // The constructor must describe the source CPU bytes, regardless of
            // how Unity represents them on the GPU.
            if (!Enum.IsDefined(typeof(TextureFormat), legacyFormat) ||
                GraphicsFormatUtility.GetGraphicsFormat(
                    legacyFormat, GraphicsFormatUtility.IsSRGBFormat(graphicsFormat)) != graphicsFormat)
                return false;

            textureFormat = legacyFormat;
            return true;
        }

        private static bool IsFormatSupported(
            GraphicsFormat graphicsFormat,
            GraphicsFormatUsage usage)
        {
            if (graphicsFormat == GraphicsFormat.None)
                return false;

            var key = (graphicsFormat, usage);
            if (!SupportedGraphicsFormats.TryGetValue(
                key,
                out var supported))
            {
                supported = SystemInfo.IsFormatSupported(
                    graphicsFormat,
                    usage);
                SupportedGraphicsFormats.Add(key, supported);
            }

            return supported;
        }

        private static TextureExport.Status TryGetUnityFormat(
            in TextureExport.Info info,
            uint textureUnit,
            out TextureExportUnityFormat unityFormat)
        {
            unityFormat = default;
            if (!TryGetPhysicalLayout(
                    info.ImageFormat,
                    info.ComponentType,
                    info.Components,
                    out var isCompressed,
                    out var bytesPerPixel))
                return TextureExport.Status.UnsupportedFormat;

            GraphicsFormat graphicsFormat;
            switch (info.ImageFormat)
            {
                case ImageFormat.COMPRESSED_RGB_S3TC_DXT1:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGBA_DXT1_SRGB,
                        GraphicsFormat.RGBA_DXT1_UNorm);
                    break;
                case ImageFormat.COMPRESSED_RGBA_S3TC_DXT1:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGBA_DXT1_SRGB,
                        GraphicsFormat.RGBA_DXT1_UNorm);
                    break;
                case ImageFormat.RGB:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.R8G8B8_SRGB,
                        GraphicsFormat.R8G8B8_UNorm);
                    break;
                case ImageFormat.BGR:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.B8G8R8_SRGB,
                        GraphicsFormat.B8G8R8_UNorm);
                    break;
                case ImageFormat.RGBA:
                    if (info.ComponentType == ComponentType.UNSIGNED_BYTE)
                    {
                        graphicsFormat = SelectColorGraphicsFormat(
                            textureUnit,
                            GraphicsFormat.R8G8B8A8_SRGB,
                            GraphicsFormat.R8G8B8A8_UNorm);
                    }
                    else if (info.ComponentType == ComponentType.HALF_FLOAT)
                    {
                        graphicsFormat =
                            GraphicsFormat.R16G16B16A16_SFloat;
                    }
                    else
                    {
                        graphicsFormat =
                            GraphicsFormat.R32G32B32A32_SFloat;
                    }
                    break;
                case ImageFormat.LUMINANCE:
                    if (info.ComponentType == ComponentType.UNSIGNED_BYTE)
                    {
                        graphicsFormat = GraphicsFormat.R8_UNorm;
                    }
                    else
                    {
                        graphicsFormat = GraphicsFormat.R32_SFloat;
                    }
                    break;
                case ImageFormat.LUMINANCE_ALPHA:
                    if (info.ComponentType == ComponentType.UNSIGNED_BYTE)
                    {
                        graphicsFormat = GraphicsFormat.R8G8_UNorm;
                    }
                    else
                    {
                        graphicsFormat = GraphicsFormat.R32G32_SFloat;
                    }
                    break;
                case ImageFormat.COMPRESSED_RGBA_S3TC_DXT3:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGBA_DXT3_SRGB,
                        GraphicsFormat.RGBA_DXT3_UNorm);
                    break;
                case ImageFormat.COMPRESSED_RGBA8_ETC2:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGBA_ETC2_SRGB,
                        GraphicsFormat.RGBA_ETC2_UNorm);
                    break;
                case ImageFormat.COMPRESSED_RGB8_ETC2:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGB_ETC2_SRGB,
                        GraphicsFormat.RGB_ETC2_UNorm);
                    break;
                case ImageFormat.COMPRESSED_RGB8_A1_ETC2:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGB_A1_ETC2_SRGB,
                        GraphicsFormat.RGB_A1_ETC2_UNorm);
                    break;
                case ImageFormat.COMPRESSED_RGBA_S3TC_DXT5:
                    graphicsFormat = SelectColorGraphicsFormat(
                        textureUnit,
                        GraphicsFormat.RGBA_DXT5_SRGB,
                        GraphicsFormat.RGBA_DXT5_UNorm);
                    break;
                case ImageFormat.COMPRESSED_R11_EAC:
                    graphicsFormat = GraphicsFormat.R_EAC_UNorm;
                    break;
                case ImageFormat.COMPRESSED_RG11_EAC:
                    graphicsFormat = GraphicsFormat.RG_EAC_UNorm;
                    break;
                default:
                    return TextureExport.Status.UnsupportedFormat;
            }

            unityFormat = new TextureExportUnityFormat(
                info.ImageFormat,
                info.ComponentType,
                info.Components,
                graphicsFormat,
                isCompressed,
                bytesPerPixel);
            return TextureExport.Status.Success;
        }

        private static bool TryGetPhysicalLayout(
            ImageFormat imageFormat,
            ComponentType componentType,
            uint components,
            out bool isCompressed,
            out uint bytesPerPixel)
        {
            isCompressed = false;
            bytesPerPixel = 0;
            if (componentType == ComponentType.UNSIGNED_BYTE)
            {
                if (imageFormat == ImageFormat.RGB && components == 3)
                {
                    bytesPerPixel = 3;
                    return true;
                }
                if (imageFormat == ImageFormat.BGR && components == 3)
                {
                    bytesPerPixel = 3;
                    return true;
                }
                if (imageFormat == ImageFormat.RGBA && components == 4)
                {
                    bytesPerPixel = 4;
                    return true;
                }
                if (imageFormat == ImageFormat.LUMINANCE && components == 1)
                {
                    bytesPerPixel = 1;
                    return true;
                }
                if (imageFormat == ImageFormat.LUMINANCE_ALPHA &&
                    components == 2)
                {
                    bytesPerPixel = 2;
                    return true;
                }
                if ((imageFormat ==
                         ImageFormat.COMPRESSED_RGB_S3TC_DXT1 &&
                     components == 3) ||
                    (imageFormat ==
                         ImageFormat.COMPRESSED_RGBA_S3TC_DXT1 &&
                     components == 4) ||
                    (imageFormat ==
                         ImageFormat.COMPRESSED_RGBA_S3TC_DXT3 &&
                     components == 4) ||
                    (imageFormat ==
                         ImageFormat.COMPRESSED_RGBA_S3TC_DXT5 &&
                     components == 4) ||
                    (imageFormat == ImageFormat.COMPRESSED_RGB8_ETC2 &&
                     components == 3) ||
                    (imageFormat == ImageFormat.COMPRESSED_RGB8_A1_ETC2 &&
                     components == 4) ||
                    (imageFormat == ImageFormat.COMPRESSED_RGBA8_ETC2 &&
                     components == 4) ||
                    (imageFormat == ImageFormat.COMPRESSED_R11_EAC &&
                     components == 1) ||
                    (imageFormat == ImageFormat.COMPRESSED_RG11_EAC &&
                     components == 2))
                {
                    isCompressed = true;
                    return true;
                }
            }
            else if (componentType == ComponentType.HALF_FLOAT &&
                     imageFormat == ImageFormat.RGBA &&
                     components == 4)
            {
                bytesPerPixel = 8;
                return true;
            }
            else if (componentType == ComponentType.FLOAT)
            {
                if (imageFormat == ImageFormat.RGBA && components == 4)
                {
                    bytesPerPixel = 16;
                    return true;
                }
                if (imageFormat == ImageFormat.LUMINANCE && components == 1)
                {
                    bytesPerPixel = 4;
                    return true;
                }
                if (imageFormat == ImageFormat.LUMINANCE_ALPHA &&
                    components == 2)
                {
                    bytesPerPixel = 8;
                    return true;
                }
            }

            return false;
        }

        private static GraphicsFormat SelectColorGraphicsFormat(
            uint textureUnit,
            GraphicsFormat colorFormat,
            GraphicsFormat dataFormat)
        {
            return textureUnit == 0 && QualitySettings.activeColorSpace == ColorSpace.Linear
                ? colorFormat
                : dataFormat;
        }

        public static uint CalculateMipCount(uint width, uint height)
        {
            uint count = 1;
            while (width > 1 || height > 1)
            {
                width = Math.Max(1U, width / 2U);
                height = Math.Max(1U, height / 2U);
                count++;
            }
            return count;
        }

        private static ulong ToAddress(IntPtr pointer)
        {
            return unchecked((ulong)pointer.ToInt64());
        }

        private static bool TryAdd(ulong left, ulong right, out ulong result)
        {
            result = left + right;
            return result >= left;
        }

    }
}
