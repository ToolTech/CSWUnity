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
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    /// <summary>
    /// Records the compute work that copies camera depth into mip zero and reduces it into a
    /// farthest-depth pyramid.
    /// </summary>
    public sealed class HiZGenerator
    {
        private const string CopyKernelName = "CopyDepth";
        private const string ReduceKernelName = "ReduceDepth";
        private const string ProfilingSampleName = "MapStreamer Hi-Z";

        private static readonly int SourceDepthId = Shader.PropertyToID("_SourceDepth");
        private static readonly int SourceMipId = Shader.PropertyToID("_SourceMip");
        private static readonly int DestinationMipId = Shader.PropertyToID("_DestinationMip");
        private static readonly int SourceWidthId = Shader.PropertyToID("_SourceWidth");
        private static readonly int SourceHeightId = Shader.PropertyToID("_SourceHeight");
        private static readonly int DestinationWidthId = Shader.PropertyToID("_DestinationWidth");
        private static readonly int DestinationHeightId = Shader.PropertyToID("_DestinationHeight");

        private readonly ComputeShader _computeShader;
        private readonly int _copyKernel;
        private readonly int _reduceKernel;
        private readonly uint _copyThreadsX;
        private readonly uint _copyThreadsY;
        private readonly uint _reduceThreadsX;
        private readonly uint _reduceThreadsY;

        /// <summary>
        /// Creates a generator and resolves the required compute kernels and thread-group sizes.
        /// </summary>
        public HiZGenerator(ComputeShader computeShader)
        {
            if (computeShader == null)
                throw new ArgumentNullException(nameof(computeShader));

            if (!computeShader.HasKernel(CopyKernelName))
                throw new ArgumentException($"Compute shader is missing kernel '{CopyKernelName}'.", nameof(computeShader));

            if (!computeShader.HasKernel(ReduceKernelName))
                throw new ArgumentException($"Compute shader is missing kernel '{ReduceKernelName}'.", nameof(computeShader));

            _computeShader = computeShader;
            _copyKernel = computeShader.FindKernel(CopyKernelName);
            _reduceKernel = computeShader.FindKernel(ReduceKernelName);

            computeShader.GetKernelThreadGroupSizes(_copyKernel, out _copyThreadsX, out _copyThreadsY, out _);
            computeShader.GetKernelThreadGroupSizes(_reduceKernel, out _reduceThreadsX, out _reduceThreadsY, out _);

            if (_copyThreadsX == 0 || _copyThreadsY == 0 || _reduceThreadsX == 0 || _reduceThreadsY == 0)
                throw new ArgumentException("Hi-Z compute kernels must use non-zero X and Y thread-group sizes.", nameof(computeShader));
        }

        /// <summary>
        /// Records generation of every mip in <paramref name="destination"/> from
        /// <paramref name="sourceDepth"/>.
        /// </summary>
        /// <remarks>
        /// The destination must already be allocated. Commands are recorded only; execution occurs
        /// when Unity executes the supplied command buffer at the camera event.
        /// </remarks>
        public void Record(
            CommandBuffer commandBuffer,
            RenderTargetIdentifier sourceDepth,
            HiZBuffer destination)
        {
            if (commandBuffer == null)
                throw new ArgumentNullException(nameof(commandBuffer));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            RenderTexture texture = destination.Texture;
            if (texture == null || !texture.IsCreated())
                throw new InvalidOperationException("The destination Hi-Z buffer must be created before recording generation.");

            var destinationIdentifier = new RenderTargetIdentifier(texture);
            int width = destination.Size.x;
            int height = destination.Size.y;

            commandBuffer.BeginSample(ProfilingSampleName);

            // Mip zero preserves raw device depth. Keeping depth in its native representation avoids
            // precision loss before the later occlusion test converts the final values to eye depth.
            commandBuffer.SetComputeTextureParam(_computeShader, _copyKernel, SourceDepthId, sourceDepth);
            commandBuffer.SetComputeTextureParam(
                _computeShader,
                _copyKernel,
                DestinationMipId,
                destinationIdentifier,
                0);
            SetDimensions(commandBuffer, width, height, width, height);
            commandBuffer.DispatchCompute(
                _computeShader,
                _copyKernel,
                DivideRoundUp(width, _copyThreadsX),
                DivideRoundUp(height, _copyThreadsY),
                1);

            int sourceWidth = width;
            int sourceHeight = height;

            // Every mip is reduced from the previous mip so each texel represents the farthest depth
            // over its complete source region, including non-power-of-two texture edges.
            for (int mipLevel = 1; mipLevel < destination.MipCount; mipLevel++)
            {
                int destinationWidth = Mathf.Max(1, width >> mipLevel);
                int destinationHeight = Mathf.Max(1, height >> mipLevel);

                commandBuffer.SetComputeTextureParam(
                    _computeShader,
                    _reduceKernel,
                    SourceMipId,
                    destinationIdentifier,
                    mipLevel - 1);
                commandBuffer.SetComputeTextureParam(
                    _computeShader,
                    _reduceKernel,
                    DestinationMipId,
                    destinationIdentifier,
                    mipLevel);
                SetDimensions(
                    commandBuffer,
                    sourceWidth,
                    sourceHeight,
                    destinationWidth,
                    destinationHeight);
                commandBuffer.DispatchCompute(
                    _computeShader,
                    _reduceKernel,
                    DivideRoundUp(destinationWidth, _reduceThreadsX),
                    DivideRoundUp(destinationHeight, _reduceThreadsY),
                    1);

                sourceWidth = destinationWidth;
                sourceHeight = destinationHeight;
            }

            commandBuffer.EndSample(ProfilingSampleName);
        }

        private void SetDimensions(
            CommandBuffer commandBuffer,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight)
        {
            commandBuffer.SetComputeIntParam(_computeShader, SourceWidthId, sourceWidth);
            commandBuffer.SetComputeIntParam(_computeShader, SourceHeightId, sourceHeight);
            commandBuffer.SetComputeIntParam(_computeShader, DestinationWidthId, destinationWidth);
            commandBuffer.SetComputeIntParam(_computeShader, DestinationHeightId, destinationHeight);
        }

        private static int DivideRoundUp(int value, uint divisor)
        {
            return (int)((value + divisor - 1) / divisor);
        }
    }
}
