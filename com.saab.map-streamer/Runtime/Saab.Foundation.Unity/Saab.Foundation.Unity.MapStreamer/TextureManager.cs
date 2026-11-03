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
// File			: TextureManager.cs
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
// ZJP	240902	Created file                                        (2.12.179)
//
//******************************************************************************

// Framework
using System;
using System.Collections.Generic;


// Unity
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Saab.Foundation.Unity.MapStreamer.Interop;

// GizmoSDK

namespace Saab.Foundation.Unity.MapStreamer
{
    public readonly struct TextureManagerDiagnostics
    {
        public readonly int ActiveTextures;
        public readonly long ActiveReferences;
        public readonly int CachedTextures;
        public readonly ulong CachedBytes;
        public readonly ulong PeakCachedBytes;
        public readonly ulong MaximumCachedBytes;
        public readonly long Created;
        public readonly long Reused;
        public readonly long Destroyed;
        public readonly long Discarded;

        internal TextureManagerDiagnostics(
            int activeTextures,
            long activeReferences,
            int cachedTextures,
            ulong cachedBytes,
            ulong peakCachedBytes,
            ulong maximumCachedBytes,
            long created,
            long reused,
            long destroyed,
            long discarded)
        {
            ActiveTextures = activeTextures;
            ActiveReferences = activeReferences;
            CachedTextures = cachedTextures;
            CachedBytes = cachedBytes;
            PeakCachedBytes = peakCachedBytes;
            MaximumCachedBytes = maximumCachedBytes;
            Created = created;
            Reused = reused;
            Destroyed = destroyed;
            Discarded = discarded;
        }
    }

    public class TextureManager
    {
        private struct TextureCacheItem
        {
            public Texture2D Texture;
            public TextureImageInfo Info;
            public int RefCount;
        }

        private readonly Dictionary<IntPtr, TextureCacheItem> _textureCache = new Dictionary<IntPtr, TextureCacheItem>();
        private readonly Dictionary<Texture2D, IntPtr> _lookup = new Dictionary<Texture2D, IntPtr>();
        private long _activeReferences;

        public TextureManagerDiagnostics Diagnostics =>
            Texture2DCache.CreateDiagnostics(
                _textureCache.Count,
                _activeReferences);

        public bool TryAdd(IntPtr key, Texture2D value, TextureImageInfo info)
        {
            if (_textureCache.TryAdd(key, new TextureCacheItem() { Texture = value, Info = info, RefCount = 1 }))
            {
                // add a reverse lookup to support the free operation
                _lookup.Add(value, key);
                _activeReferences++;
                return true;
            }

            // resource already existed, no operation performed
            return false;
        }

        public bool TryGet(IntPtr key, out Texture value, out TextureImageInfo info)
        {
            if (_textureCache.TryGetValue(key, out TextureCacheItem item))
            {
                // item existed in the cache, increase the ref count and return the resource
                item.RefCount++;
                _textureCache[key] = item;
                _activeReferences++;
                
                value = item.Texture;
                info = item.Info;
                return true;
            }

            // failed to find the given resource
            value = null;
            info = null;
            return false;
        }

        public bool Free(Texture2D texture)
        {
            if (_lookup.TryGetValue(texture, out IntPtr key))
            {
                TextureCacheItem item = _textureCache[key];
                item.RefCount--;
                _activeReferences--;

                // check if this was the last reference
                if (item.RefCount > 0)
                {
                    // simply update the ref counter
                    _textureCache[key] = item;
                    return true;
                }

                // this was the last reference for the texture, we should release it
                _lookup.Remove(texture);
                _textureCache.Remove(key);

                item.Info?.Dispose();
                Texture2DCache.Free(texture);

                return true;
            }

            // failed to find the given resource, no operation performed
            return false;
        }

        public void Clear()
        {
            foreach (var item in _textureCache.Values)
            {
                item.Info?.Dispose();
                Texture2DCache.Free(item.Texture);
            }
            
            _lookup.Clear();
            _textureCache.Clear();
            _activeReferences = 0;
        }

        public void ResetDiagnostics()
        {
            Texture2DCache.ResetDiagnostics();
        }

    }

    public static class Texture2DCache
    {
        private sealed class GraphicsTexturePool
        {
            public readonly Stack<Texture2D> Textures =
                new Stack<Texture2D>();
            public readonly ulong TextureSize;

            public GraphicsTexturePool(ulong textureSize)
            {
                TextureSize = textureSize;
            }
        }

        private static readonly Dictionary<
            (int Width, int Height, GraphicsFormat Format, int MipCount),
            GraphicsTexturePool> _graphicsTextures =
                new Dictionary<
                    (int, int, GraphicsFormat, int),
                    GraphicsTexturePool>();

        private static readonly HashSet<int> _cacheableTextureIds =
            new HashSet<int>();

        private const ulong MaximumCachedBytes = 256UL * 1024 * 1024;
        private const int MaximumTextureSize = 2048;

        private static ulong _cachedBytes;
        private static ulong _peakCachedBytes;
        private static int _cachedTextureCount;

        private static long _texturesCreated;
        private static long _texturesReused;
        private static long _texturesDestroyed;
        private static long _texturesDiscarded;

        public static void ResetDiagnostics()
        {
            _texturesCreated = 0;
            _texturesReused = 0;
            _texturesDestroyed = 0;
            _texturesDiscarded = 0;
            _peakCachedBytes = _cachedBytes;
        }

        internal static TextureManagerDiagnostics CreateDiagnostics(
            int activeTextures,
            long activeReferences)
        {
            return new TextureManagerDiagnostics(
                activeTextures,
                activeReferences,
                _cachedTextureCount,
                _cachedBytes,
                _peakCachedBytes,
                MaximumCachedBytes,
                _texturesCreated,
                _texturesReused,
                _texturesDestroyed,
                _texturesDiscarded);
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public static void Clear()
        {
            foreach (var pool in _graphicsTextures.Values)
            {
                while (pool.Textures.Count > 0)
                {
                    var texture = pool.Textures.Pop();
                    _cachedTextureCount--;
                    _cacheableTextureIds.Remove(
                        texture.GetInstanceID());
                    DestroyTexture(texture);
                }
            }

            _graphicsTextures.Clear();
            _cacheableTextureIds.Clear();
            _cachedBytes = 0;
            _cachedTextureCount = 0;
        }

        
        public static Texture2D GetOrCreateTexture(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount)
        {
            return GetOrCreateTexture(
                width,
                height,
                format,
                mipCount,
                out _);
        }

        public static Texture2D GetOrCreateTexture(
            int width,
            int height,
            GraphicsFormat format,
            int mipCount,
            out bool canBeCached)
        {
            canBeCached = false;
            if (!TextureExportInterop.IsFormatSupportedForSampling(format, false))
            {
                Debug.LogWarning($"No safe Unity texture upload is supported for {format}.");
                return null;
            }
            var key = (width, height, format, mipCount);
            var textureSize =
                GraphicsFormatUtility.ComputeMipChainSize(
                    width,
                    height,
                    format,
                    mipCount);
            canBeCached =
                width <= MaximumTextureSize &&
                height <= MaximumTextureSize &&
                textureSize <= MaximumCachedBytes;

            if (canBeCached &&
                _graphicsTextures.TryGetValue(
                    key,
                    out var pool) &&
                pool.Textures.Count > 0)
            {
                _cachedBytes -= pool.TextureSize;
                var texture = pool.Textures.Pop();
                _cachedTextureCount--;
                _texturesReused++;
                return texture;
            }
            var created = TextureExportInterop.CreateTexture(width, height, format, mipCount);
            if (!created)
            {
                Debug.LogError($"Unity failed to create a texture with storage format {format}.");
                return null;
            }
            _texturesCreated++;
            if (created.width != width ||
                created.height != height ||
                created.graphicsFormat != format ||
                created.mipmapCount != mipCount ||
                !created.isReadable ||
                (ulong)created.GetRawTextureData<byte>().Length != textureSize)
            {
                Debug.LogError(
                    $"Unity changed the requested texture storage layout for {format}; " +
                    "the source bytes cannot be uploaded without conversion.");
                Discard(created);
                return null;
            }

            if (canBeCached)
                _cacheableTextureIds.Add(created.GetInstanceID());
            return created;
        }

        /// <summary>
        /// Frees a texture by adding it to the cache for later recycling, or destroys the texture
        /// if cache was full or the texture was not valid for recycling, such as wrong format or size.
        /// </summary>
        /// <param name="texture">Texture to free</param>
        public static void Free(Texture2D texture)
        {
            if (!texture)
                return;

            var instanceId = texture.GetInstanceID();
            if (_cacheableTextureIds.Contains(instanceId) &&
                texture.isReadable)
            {
                var key = (
                    texture.width,
                    texture.height,
                    texture.graphicsFormat,
                    texture.mipmapCount);
                FreeGraphicsTexture(texture, key);
                return;
            }

            _cacheableTextureIds.Remove(instanceId);
            DestroyTexture(texture);
        }

        public static void Discard(Texture2D texture)
        {
            if (!texture)
                return;

            _texturesDiscarded++;
            _cacheableTextureIds.Remove(texture.GetInstanceID());
            DestroyTexture(texture);
        }

        private static void FreeGraphicsTexture(
            Texture2D texture,
            (int Width, int Height, GraphicsFormat Format, int MipCount) key)
        {
            var hasPool = _graphicsTextures.TryGetValue(
                key,
                out var pool);
            var textureSize = hasPool
                ? pool.TextureSize
                : GraphicsFormatUtility.ComputeMipChainSize(
                    key.Width,
                    key.Height,
                    key.Format,
                    key.MipCount);
            var newCachedBytes =
                checked(_cachedBytes + textureSize);
            if (newCachedBytes > MaximumCachedBytes)
            {
                _cacheableTextureIds.Remove(texture.GetInstanceID());
                DestroyTexture(texture);
                return;
            }

            if (!hasPool)
            {
                pool = new GraphicsTexturePool(textureSize);
                _graphicsTextures.Add(key, pool);
            }

            _cachedBytes = newCachedBytes;
            if (_cachedBytes > _peakCachedBytes)
                _peakCachedBytes = _cachedBytes;
            _cachedTextureCount++;
            pool.Textures.Push(texture);
        }

        private static void DestroyTexture(Texture2D texture)
        {
            _texturesDestroyed++;
            GameObject.Destroy(texture);
        }
    }
}
