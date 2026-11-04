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
    /// Builds and owns a hierarchical depth texture for a camera in Unity's built-in render pipeline.
    /// Each mip stores the farthest scene depth covered by that texel so consumers can perform
    /// conservative occlusion tests at a resolution appropriate for their screen-space bounds.
    /// </summary>
    [AddComponentMenu("Saab/Map Streamer/Built-In Hi-Z Depth Pyramid")]
    [DisallowMultipleComponent]
    public sealed class BuiltInHiZDepthPyramid : MonoBehaviour
    {
        private static readonly int GlobalTextureId = Shader.PropertyToID("_HiZTexture");
        private static readonly int GlobalTextureSizeId = Shader.PropertyToID("_HiZTextureSize");
        private static readonly int GlobalMaxMipLevelId = Shader.PropertyToID("_HiZMaxMipLevel");

        [SerializeField]
        private ComputeShader _generationShader;

        [SerializeField]
        private bool _exposeGlobally;

        [SerializeField]
        private Camera _targetCamera;

        private ComputeShader _activeGenerationShader;
        private HiZBuffer _buffer;
        private HiZGenerator _generator;
        private CommandBuffer _commandBuffer;
        private Camera _attachedCamera;
        private CameraEvent _attachedEvent;
        private bool _commandBufferAttached;
        private bool _configurationDirty = true;
        private string _lastError;

        [SerializeField]
        private RenderTexture _debugRenderTexture;

        public RenderTexture HiZTexture => _buffer?.Texture;
        public Vector2Int TextureSize => _buffer?.Size ?? Vector2Int.zero;
        public int MipCount => _buffer?.MipCount ?? 0;
        public int MaxMipLevel => _buffer?.MaxMipLevel ?? -1;
        public bool UsesReversedZBuffer => SystemInfo.usesReversedZBuffer;

        /// <summary>
        /// Returns whether this instance has a valid Hi-Z texture matching the supplied camera and
        /// its current render-target dimensions.
        /// </summary>
        /// <remarks>
        /// Consumers should fall back to non-occlusion culling when this returns false. A texture
        /// from another camera or an old render-target size cannot be used safely.
        /// </remarks>
        public bool IsReadyFor(Camera camera)
        {
            RenderTexture texture = HiZTexture;
            RenderTexture targetTexture = camera != null ? camera.targetTexture : null;
            int targetWidth = targetTexture != null ? targetTexture.width : camera?.pixelWidth ?? 0;
            int targetHeight = targetTexture != null ? targetTexture.height : camera?.pixelHeight ?? 0;

            return isActiveAndEnabled &&
                   camera != null &&
                   camera == _targetCamera &&
                   !UsesUnsupportedConfiguration(camera) &&
                   _commandBufferAttached &&
                   _attachedCamera == camera &&
                   texture != null &&
                   texture.IsCreated() &&
                   TextureSize.x == targetWidth &&
                   TextureSize.y == targetHeight &&
                   MaxMipLevel >= 0;
        }

        public Camera TargetCamera
        {
            get => _targetCamera;
            set
            {
                if (_targetCamera == value)
                    return;

                DetachCommandBuffer();
                ClearOwnedGlobal(_buffer?.Texture);

                _targetCamera = value;
                _configurationDirty = true;
                _lastError = null;

                if (isActiveAndEnabled && _targetCamera != null)
                    _targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        /// <summary>
        /// When true, the generated Hi-Z texture (and properties) is exposed globally to all shaders using SetGlobalTexture. 
        /// When false, the texture (and properties) is only available to shaders in the same command buffer as the generation.
        /// </summary>
        public bool ExposeGlobally
        {
            get => _exposeGlobally;
            set
            {
                if (_exposeGlobally == value)
                    return;

                _exposeGlobally = value;
                if (!value)
                    ClearOwnedGlobal(_buffer?.Texture);

                _configurationDirty = true;
            }
        }

        public CameraEvent GenerationEvent
        {
            get
            {
                if (_commandBufferAttached)
                    return _attachedEvent;

                if (_targetCamera == null)
                    throw new InvalidOperationException("A target camera must be assigned.");

                return SelectGenerationEvent(_targetCamera.actualRenderingPath);
            }
        }

        private void OnEnable()
        {
            if (_targetCamera == null)
                _targetCamera = GetComponent<Camera>();

            UnityEngine.Camera.onPreCull -= HandleCameraPreCull;
            UnityEngine.Camera.onPreCull += HandleCameraPreCull;
            _configurationDirty = true;

            if (_targetCamera != null)
                _targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnValidate()
        {
            _configurationDirty = true;

            if (!isActiveAndEnabled)
                return;

            DetachCommandBuffer();
            ClearOwnedGlobal(_buffer?.Texture);

            if (_targetCamera != null)
                _targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void HandleCameraPreCull(Camera camera)
        {
            if (camera != _targetCamera)
                return;

            camera.depthTextureMode |= DepthTextureMode.Depth;

            if (UsesUnsupportedConfiguration(camera, out string configurationError))
            {
                SuspendGeneration();
                ReportError(configurationError);
                return;
            }

            if (!EnsureCoreResources())
                return;

            RenderTexture targetTexture = camera.targetTexture;
            if (targetTexture != null && targetTexture.dimension != TextureDimension.Tex2D)
            {
                SuspendGeneration();
                ReportError("Hi-Z generation supports only 2D camera target textures.");
                return;
            }

            int width = targetTexture != null ? targetTexture.width : camera.pixelWidth;
            int height = targetTexture != null ? targetTexture.height : camera.pixelHeight;

            if (width <= 0 || height <= 0)
            {
                SuspendGeneration();
                ReportError($"Hi-Z generation requires positive camera dimensions, received {width}x{height}.");
                return;
            }

            CameraEvent generationEvent = SelectGenerationEvent(camera.actualRenderingPath);
            RenderTexture previousTexture = _buffer?.Texture;
            bool bufferChanged;

            try
            {
                if (_buffer == null)
                    _buffer = new HiZBuffer();

                bool mustClearGlobal = previousTexture != null &&
                    (!previousTexture.IsCreated() ||
                     previousTexture.width != width ||
                     previousTexture.height != height);

                if (mustClearGlobal)
                    ClearOwnedGlobal(previousTexture);

                bufferChanged = _buffer.EnsureSize(width, height);
            }
            catch (ArgumentException exception)
            {
                SuspendGeneration();
                ReportError(exception.Message);
                return;
            }
            catch (InvalidOperationException exception)
            {
                SuspendGeneration();
                ReportError(exception.Message);
                return;
            }
            catch (NotSupportedException exception)
            {
                SuspendGeneration();
                ReportError(exception.Message);
                return;
            }

            if (bufferChanged || _configurationDirty || !_commandBufferAttached || generationEvent != _attachedEvent)
                RebuildCommandBuffer(camera, generationEvent);

            _configurationDirty = false;
            _lastError = null;

            _debugRenderTexture = HiZTexture;
        }

        private void OnDisable()
        {
            UnityEngine.Camera.onPreCull -= HandleCameraPreCull;
            ReleaseResources();
        }

        private void OnDestroy()
        {
            UnityEngine.Camera.onPreCull -= HandleCameraPreCull;
            ReleaseResources();
        }

        private bool EnsureCoreResources()
        {
            if (_generationShader == null)
            {
                SuspendGeneration();
                ReportError("A Hi-Z generation compute shader must be assigned.");
                return false;
            }

            if (_commandBuffer == null)
            {
                _commandBuffer = new CommandBuffer
                {
                    name = "MapStreamer Hi-Z Generation"
                };
            }

            if (_generator != null && _activeGenerationShader == _generationShader)
                return true;

            DetachCommandBuffer();
            _commandBuffer.Clear();

            try
            {
                _generator = new HiZGenerator(_generationShader);
                _activeGenerationShader = _generationShader;
                _configurationDirty = true;
                return true;
            }
            catch (ArgumentException exception)
            {
                _generator = null;
                _activeGenerationShader = null;
                SuspendGeneration();
                ReportError(exception.Message);
                return false;
            }
        }

        private void RebuildCommandBuffer(Camera camera, CameraEvent generationEvent)
        {
            DetachCommandBuffer();
            ClearOwnedGlobal(_buffer.Texture);

            _commandBuffer.Clear();
            // Deferred rendering exposes resolved depth before reflections, while forward rendering
            // exposes the camera depth texture after Unity has generated it.
            BuiltinRenderTextureType depthSource =
                generationEvent == CameraEvent.BeforeReflections
                    ? BuiltinRenderTextureType.ResolvedDepth
                    : BuiltinRenderTextureType.Depth;
            _generator.Record(
                _commandBuffer,
                new RenderTargetIdentifier(depthSource),
                _buffer);

            if (_exposeGlobally)
            {
                Vector2Int size = _buffer.Size;
                _commandBuffer.SetGlobalTexture(GlobalTextureId, _buffer.Texture);
                _commandBuffer.SetGlobalVector(
                    GlobalTextureSizeId,
                    new Vector4(size.x, size.y, 1.0f / size.x, 1.0f / size.y));
                _commandBuffer.SetGlobalFloat(GlobalMaxMipLevelId, _buffer.MaxMipLevel);
            }

            camera.AddCommandBuffer(generationEvent, _commandBuffer);
            _attachedCamera = camera;
            _attachedEvent = generationEvent;
            _commandBufferAttached = true;
        }

        private void SuspendGeneration()
        {
            DetachCommandBuffer();

            if (_commandBuffer != null)
                _commandBuffer.Clear();

            if (_buffer != null)
            {
                ClearOwnedGlobal(_buffer.Texture);
                _buffer.Dispose();
                _buffer = null;
            }
        }

        private void ReleaseResources()
        {
            DetachCommandBuffer();

            if (_commandBuffer != null)
            {
                _commandBuffer.Release();
                _commandBuffer = null;
            }

            if (_buffer != null)
            {
                ClearOwnedGlobal(_buffer.Texture);
                _buffer.Dispose();
                _buffer = null;
            }

            _generator = null;
            _activeGenerationShader = null;
            _configurationDirty = true;
        }

        private void DetachCommandBuffer()
        {
            if (!_commandBufferAttached || _commandBuffer == null)
                return;

            if (_attachedCamera != null)
                _attachedCamera.RemoveCommandBuffer(_attachedEvent, _commandBuffer);

            _attachedCamera = null;
            _commandBufferAttached = false;
        }

        private void ClearOwnedGlobal(Texture texture)
        {
            if (texture == null || Shader.GetGlobalTexture(GlobalTextureId) != texture)
                return;

            Shader.SetGlobalTexture(GlobalTextureId, null);
            Shader.SetGlobalVector(GlobalTextureSizeId, Vector4.zero);
            Shader.SetGlobalFloat(GlobalMaxMipLevelId, 0.0f);
        }

        private void ReportError(string message)
        {
            if (_lastError == message)
                return;

            _lastError = message;
            Debug.LogError($"{nameof(BuiltInHiZDepthPyramid)} on '{name}': {message}", this);
        }

        private static bool UsesUnsupportedConfiguration(Camera camera)
        {
            return UsesUnsupportedConfiguration(camera, out _);
        }

        private static bool UsesUnsupportedConfiguration(Camera camera, out string error)
        {
            if (camera.allowDynamicResolution)
            {
                error = "Hi-Z generation does not support cameras using dynamic resolution.";
                return true;
            }

            RenderTexture targetTexture = camera.targetTexture;
            int msaaSamples = GetDepthMsaaSamples(camera, targetTexture);
            if (msaaSamples > 1)
            {
                error = "Hi-Z generation does not support multisampled camera depth buffers.";
                return true;
            }

            error = null;
            return false;
        }

        private static int GetDepthMsaaSamples(Camera camera, RenderTexture targetTexture)
        {
            if (targetTexture != null)
                return targetTexture.antiAliasing;

            RenderingPath renderingPath = camera.actualRenderingPath;
            bool usesDeferredRendering = renderingPath == RenderingPath.DeferredShading;

            return usesDeferredRendering ? 1 : QualitySettings.antiAliasing;
        }

        private static CameraEvent SelectGenerationEvent(RenderingPath renderingPath)
        {
            return renderingPath == RenderingPath.DeferredShading
                ? CameraEvent.BeforeReflections
                : CameraEvent.AfterDepthTexture;
        }
    }
}
