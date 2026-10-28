//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: NodeStateHelper.cs
// Module		:
// Description	: Helper for texture and state uploads
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// ZJP	200625	Created file                                        (2.10.6)
// AMO	261022	Added revision-aware copied texture metadata for command-driven scenes.	(2.12.327)
// AMO	261028	Recorded texture builds in the shared build diagnostics after removing the legacy SceneManager.	(2.12.327)
//
//******************************************************************************

// Framework
using System;
using Saab.Foundation.Unity.MapStreamer.Interop;

// Unity
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// GizmoSDK
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

using gzTexture = GizmoSDK.Gizmo3D.Texture;
using Texture = UnityEngine.Texture;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;

namespace Saab.Foundation.Unity.MapStreamer
{
    public struct StateBuildOutput
    {
        public Texture2D Texture;
        public Texture2D Feature;
        public Texture2D SurfaceHeight;

        public Matrix3D Feature_homography;
        public Matrix3D SurfaceHeight_homography;
    }

    public sealed class TextureImageInfo : IDisposable
    {
        public DynamicType homography;

        public DynamicType border_x;
        public DynamicType border_y;

        internal bool HasHomography;
        internal Matrix3D Homography;
        internal bool CopyOnly;

        public void Dispose()
        {
            homography?.Dispose();
            border_x?.Dispose();
            border_y?.Dispose();
            homography = null;
            border_x = null;
            border_y = null;
        }
    }


    public static class StateHelper
    {
        private static readonly ProfilerMarker _profilerMarker = new ProfilerMarker(ProfilerCategory.Render, "SM-ReadTexture");

        public static bool Build(State state, out StateBuildOutput output, TextureManager textureCache = null)
        {
            return Build(state, out output, textureCache, null);
        }

        internal static bool Build(
            State state,
            out StateBuildOutput output,
            TextureManager textureCache,
            Func<gzTexture, uint, bool, ulong> revision)
        {
            output = default;

            if (!ReadTextureFromState(state, out Texture2D texture, textureCache, 0, true, revision))
                return false;

            output.Texture = texture;

            if (ReadTextureFromState(
                    state,
                    out Texture2D feature,
                    textureCache,
                    out TextureImageInfo info,
                    1,
                    false,
                    revision))
            {
                if (info != null && info.HasHomography)
                {
                    output.Feature = feature;
                    output.Feature_homography = info.Homography;
                    if (textureCache == null)
                        info.Dispose();
                }
                else
                {
                    ReleaseOptionalTexture(textureCache, feature, info);
                    output.Feature = null;
                }
            }

            if (ReadTextureFromState(
                    state,
                    out Texture2D surface,
                    textureCache,
                    out info,
                    2,
                    false,
                    revision))
            {
                if (info != null && info.HasHomography)
                {
                    output.SurfaceHeight = surface;
                    output.SurfaceHeight_homography = info.Homography;
                    if (textureCache == null)
                        info.Dispose();
                }
                else
                {
                    ReleaseOptionalTexture(textureCache, surface, info);
                    output.SurfaceHeight = null;
                }
            }

            return true;
        }

        public static bool Build(State state, out Texture2D output, TextureManager textureCache = null)
        {
            output = default;

            if (!ReadTextureFromState(state, out Texture2D texture, textureCache))
                return false;

            output = texture;

            return true;
        }

        private static bool ReadTextureFromState(
            State state,
            out Texture2D result,
            TextureManager textureCache,
            out TextureImageInfo info,
            uint unit = 0,
            bool useMipMap = true,
            Func<gzTexture, uint, bool, ulong> revision = null)
        {
            result = null;
            info = null;

            if (!state.HasTexture(unit) || state.GetMode(StateMode.TEXTURE) != StateModeActivation.ON)
                return false;

            using (var texture = state.GetTexture(unit))
            {
                try
                {
                    return ReadTexture(
                        texture, out result, textureCache, out info, unit, useMipMap,
                        revision?.Invoke(texture, unit, useMipMap) ?? 0, true);
                }
                finally
                {
                    // prevent dispose from locking object by releasing it manually here
                    texture.ReleaseAlreadyLocked();
                }
            }
        }

        private static bool ReadTextureFromState(
            State state,
            out Texture2D result,
            TextureManager textureCache,
            uint unit = 0,
            bool useMipMap = true,
            Func<gzTexture, uint, bool, ulong> revision = null)
        {
            _profilerMarker.Begin();
            try
            {
                return ReadTextureFromStateInternal(state, out result, textureCache, unit, useMipMap, revision);
            }
            finally
            {
                _profilerMarker.End();
            }
        }

        private static bool ReadTextureFromStateInternal(
            State state,
            out Texture2D result,
            TextureManager textureCache,
            uint unit = 0,
            bool useMipMap = true,
            Func<gzTexture, uint, bool, ulong> revision = null)
        {
            result = null;

            // allow states without texture by using Texture2D.whiteTexture as placeholder
            if (!state.HasTexture(unit) || state.GetMode(StateMode.TEXTURE) != StateModeActivation.ON)
            {
                if (textureCache != null)
                {
                    var ptr = state.GetNativeReference();

                    if (textureCache.TryGet(ptr, 0, unit, useMipMap, out Texture cachedTexture, out _))
                    {
                        result = (Texture2D)cachedTexture;
                        return true;
                    }

                    result = CopyWhiteTexture();
                    if (!result)
                        return false;

                    if (textureCache.TryAdd(ptr, 0, unit, useMipMap, result, null))
                        return true;

                    Texture2DCache.Free(result);
                    result = null;
                    return false;
                }

                result = CopyWhiteTexture();
                return result;
            }

            using (var texture = state.GetTexture(unit))
            {
                try
                {
                    return ReadTexture(
                        texture, out result, textureCache, out _, unit, useMipMap,
                        revision?.Invoke(texture, unit, useMipMap) ?? 0);
                }
                finally
                {
                    // prevent dispose from locking object by releasing it manually here
                    texture.ReleaseAlreadyLocked();
                }
            }
        }

        internal static bool ReadTexture(
            gzTexture texture,
            out Texture2D result,
            TextureManager textureCache,
            out TextureImageInfo info,
            uint unit,
            bool useMipMap,
            ulong revision,
            bool copyImageInfo = false)
        {
            result = null;
            info = null;
            var source = texture.GetNativeReference();
            if (textureCache != null &&
                textureCache.TryGet(source, revision, unit, useMipMap, out Texture cached, out info))
            {
                result = (Texture2D)cached;
                return true;
            }
            if (copyImageInfo)
                info = new TextureImageInfo { CopyOnly = revision != 0 };
            if (!CopyTexture(texture, out result, info, unit, useMipMap))
            {
                info?.Dispose();
                info = null;
                return false;
            }
            if (textureCache == null ||
                textureCache.TryAdd(source, revision, unit, useMipMap, result, info))
                return true;
            Texture2DCache.Free(result);
            info?.Dispose();
            result = null;
            info = null;
            return false;
        }

        private static Texture2D CopyWhiteTexture()
        {
            Texture2D src = Texture2D.whiteTexture;
            var dst = Texture2DCache.GetOrCreateTexture(
                src.width,
                src.height,
                src.graphicsFormat,
                src.mipmapCount);
            if (!dst)
                return null;
            Graphics.CopyTexture(src, dst);
            return dst;
        }

        private static void ReleaseOptionalTexture(
            TextureManager textureManager,
            Texture2D texture,
            TextureImageInfo info)
        {
            if (!texture)
                return;

            if (textureManager != null)
                textureManager.Free(texture);
            else
            {
                info?.Dispose();
                Texture2DCache.Free(texture);
            }
        }

        private static bool CopyTexture(
            gzTexture gzTexture,
            out Texture2D result,
            TextureImageInfo imageInfo,
            uint textureUnit,
            bool mipChain = true)
        {
            result = null;
            var buildStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var selection = mipChain
                ? TextureExport.SourceSelection.CompleteMipChain
                : TextureExport.SourceSelection.BaseOnly;
            var baseLevelFallback = false;
            TextureExport.Prepared preparation = null;
            Texture2D destination = null;
            var copyAttempted = false;
            var published = false;
            long textureDataBytes = 0;

            try
            {
                if (!gzTexture.HasImage())
                    return false;

                var status = gzTexture.PrepareTextureExport(
                    selection,
                    out preparation);
                if (status == TextureExport.Status.IncompleteMipChain &&
                    selection ==
                    TextureExport.SourceSelection.CompleteMipChain)
                {
                    selection = TextureExport.SourceSelection.BaseOnly;
                    baseLevelFallback = true;
                    status = gzTexture.PrepareTextureExport(
                        selection,
                        out preparation);
                }
                if (status != TextureExport.Status.Success ||
                    preparation == null)
                    return false;

                var sourceInfo = preparation.Info;
                status = TextureExportInterop.ValidateInfo(
                    in sourceInfo,
                    selection,
                    textureUnit,
                    out var unityFormat);
                if (status != TextureExport.Status.Success)
                {
                    Debug.LogWarning(
                        $"Texture export rejected source layout {sourceInfo.ImageFormat}/" +
                        $"{sourceInfo.ComponentType} ({sourceInfo.Components} components) " +
                        $"for texture unit {textureUnit}: {status}.");
                    return false;
                }

                var minFilter = gzTexture.MinFilter;
                if (!TextureExportInterop.IsFormatSupportedForSampling(
                        unityFormat.GraphicsFormat,
                        RequiresLinearFiltering(minFilter)))
                {
                    Debug.LogWarning(
                        $"Texture export cannot sample {unityFormat.GraphicsFormat} " +
                        $"for texture unit {textureUnit} on {SystemInfo.graphicsDeviceType}.");
                    return false;
                }

                var generateMipMaps =
                    baseLevelFallback &&
                    !GraphicsFormatUtility.IsCompressedFormat(
                        unityFormat.GraphicsFormat);
                var mipCount = generateMipMaps
                    ? TextureExportInterop.CalculateMipCount(
                        sourceInfo.Width,
                        sourceInfo.Height)
                    : sourceInfo.LevelCount;
                destination = Texture2DCache.GetOrCreateTexture(
                    (int)sourceInfo.Width,
                    (int)sourceInfo.Height,
                    unityFormat.GraphicsFormat,
                    (int)mipCount,
                    out var canBeRecycled);
                if (!destination)
                    return false;

#if DEBUG
                destination.name = "SM - NodeTexture TextureExport";
#endif

                status = TextureExportInterop.CreateDestination(
                    destination,
                    in sourceInfo,
                    in unityFormat,
                    out var exportDestination,
                    out var rawTextureBytes);
                if (status != TextureExport.Status.Success)
                    return false;

                copyAttempted = true;
                status = preparation.Copy(exportDestination);
                if (status != TextureExport.Status.Success)
                    return false;

                preparation.Dispose();
                preparation = null;
                textureDataBytes = (long)rawTextureBytes;
                destination.Apply(
                    updateMipmaps: generateMipMaps,
                    makeNoLongerReadable: !canBeRecycled);
                destination.wrapModeU =
                    GetUnityTextureWrapMode(gzTexture.WrapS);
                destination.wrapModeV =
                    GetUnityTextureWrapMode(gzTexture.WrapT);
                destination.wrapModeW =
                    GetUnityTextureWrapMode(gzTexture.WrapR);
                SetTextureFilterMode(minFilter, destination);

                if (imageInfo != null)
                {
                    using (var image = gzTexture.GetImage())
                    {
                        imageInfo.homography = image.GetAttribute(
                            "UserDataImInfo", "ImI-Wrld-Hom");
                        imageInfo.border_x = image.GetAttribute(
                            "UserDataImInfo", "ImI-Pixel-X-border");
                        imageInfo.border_y = image.GetAttribute(
                            "UserDataImInfo", "ImI-Pixel-Y-border");
                        if (imageInfo.homography != null &&
                            imageInfo.homography.Is("gzImageHomography"))
                        {
                            imageInfo.Homography = (ImageHomography)imageInfo.homography;
                            imageInfo.HasHomography = true;
                        }
                        if (imageInfo.CopyOnly)
                            imageInfo.Dispose();
                    }
                }

                result = destination;
                published = true;
                BuildDiagnostics.RecordTexture(
                    textureDataBytes,
                    System.Diagnostics.Stopwatch.GetTimestamp() - buildStart);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Texture export failed for texture unit {textureUnit}: {exception}");
                return false;
            }
            finally
            {
                preparation?.Dispose();
                if (!published && destination)
                {
                    if (copyAttempted)
                        Texture2DCache.Discard(destination);
                    else
                        Texture2DCache.Free(destination);
                }
            }
        }

        private static void SetTextureFilterMode(
            gzTexture.TextureMinFilter minFilter,
            Texture2D destination)
        {
            switch (minFilter)
            {
                case gzTexture.TextureMinFilter.LINEAR:
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_NEAREST:
                    destination.filterMode = FilterMode.Bilinear;
                    break;
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_LINEAR:
                    destination.filterMode = FilterMode.Trilinear;
                    break;
                default:
                    destination.filterMode = FilterMode.Point;
                    break;
            }
        }

        public static bool RequiresLinearFiltering(
            gzTexture.TextureMinFilter minFilter)
        {
            switch (minFilter)
            {
                case gzTexture.TextureMinFilter.LINEAR:
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_NEAREST:
                case gzTexture.TextureMinFilter.LINEAR_MIPMAP_LINEAR:
                    return true;
                default:
                    return false;
            }
        }

        private static TextureWrapMode GetUnityTextureWrapMode(gzTexture.TextureWrapMode wrapMode)
        {
            switch (wrapMode)
            {
                case gzTexture.TextureWrapMode.CLAMP_TO_EDGE:
                case gzTexture.TextureWrapMode.CLAMP_TO_BORDER:
                case gzTexture.TextureWrapMode.CLAMP:
                    return TextureWrapMode.Clamp;
                case gzTexture.TextureWrapMode.REPEAT:
                    return TextureWrapMode.Repeat;
                case gzTexture.TextureWrapMode.MIRRORED_REPEAT:
                    return TextureWrapMode.Mirror;
                default:
                    throw new NotSupportedException();
            }
        }

    }
}
