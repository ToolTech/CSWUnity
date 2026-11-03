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
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    /// <summary>
    /// Owns the random-write, point-sampled R32 texture used as the Hi-Z depth pyramid.
    /// </summary>
    public sealed class HiZBuffer : IDisposable
    {
        private const string TextureName = "MapStreamer Hi-Z Buffer";

        public RenderTexture Texture { get; private set; }
        public Vector2Int Size { get; private set; }
        public int MipCount { get; private set; }
        public int MaxMipLevel => MipCount - 1;

        /// <summary>
        /// Ensures the pyramid matches the requested base dimensions.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when the texture was created or recreated; otherwise
        /// <see langword="false"/>.
        /// </returns>
        public bool EnsureSize(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Hi-Z width must be greater than zero.");

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Hi-Z height must be greater than zero.");

            if (Texture != null &&
                Texture.IsCreated() &&
                Size.x == width &&
                Size.y == height)
            {
                return false;
            }

            ValidateSupport();
            ReleaseTexture();

            int mipCount = CalculateMipCount(width, height);
            // R32 preserves the camera's raw depth values and supports both sampling and UAV writes.
            var descriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R32_SFloat,
                depthBufferBits = 0,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                mipCount = mipCount,
                useMipMap = true,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };

            var texture = new RenderTexture(descriptor)
            {
                name = TextureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };

            if (!texture.Create())
            {
                DestroyTexture(texture);
                throw new InvalidOperationException(
                    $"Failed to create a {width}x{height} {GraphicsFormat.R32_SFloat} Hi-Z texture.");
            }

            Texture = texture;
            Size = new Vector2Int(width, height);
            MipCount = mipCount;
            return true;
        }

        public void Dispose()
        {
            ReleaseTexture();
        }

        internal static int CalculateMipCount(int width, int height)
        {
            int largestDimension = Mathf.Max(width, height);
            int mipCount = 1;

            while (largestDimension > 1)
            {
                largestDimension >>= 1;
                mipCount++;
            }

            return mipCount;
        }

        private static void ValidateSupport()
        {
            if (!SystemInfo.supportsComputeShaders)
                throw new NotSupportedException("Hi-Z generation requires compute shader support.");

            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.Sample) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, GraphicsFormatUsage.LoadStore))
            {
                throw new NotSupportedException(
                    $"{GraphicsFormat.R32_SFloat} sampling and load/store support are required for Hi-Z generation.");
            }
        }

        private void ReleaseTexture()
        {
            if (Texture != null)
                DestroyTexture(Texture);

            Texture = null;
            Size = Vector2Int.zero;
            MipCount = 0;
        }

        private static void DestroyTexture(RenderTexture texture)
        {
            texture.Release();

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
