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

// GizmoSDK

namespace Saab.Foundation.Unity.MapStreamer
{
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

        public bool TryAdd(IntPtr key, Texture2D value, TextureImageInfo info)
        {
            if (_textureCache.TryAdd(key, new TextureCacheItem() { Texture = value, Info = info, RefCount = 1 }))
            {
                // add a reverse lookup to support the free operation
                _lookup.Add(value, key);
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

                Texture2DCache.Free(texture);

                return true;
            }

            // failed to find the given resource, no operation performed
            return false;
        }

        public void Clear()
        {
            foreach (var kvp in _lookup)
                Texture2DCache.Free(kvp.Key);
            
            _lookup.Clear();
            _textureCache.Clear();
        }
    }

    public static class Texture2DCache
    {
        private static readonly Dictionary<ulong, Stack<Texture2D>> _textures = new Dictionary<ulong, Stack<Texture2D>>();

        private static readonly Dictionary<ulong, ulong> _singleTextureSize = new Dictionary<ulong, ulong>();

        // estimate of current texture cache size
        private static ulong _estimatedCacheSizeInBytes;

        // maximum allowed texture cache size (256 MB)
        private static ulong _maxCacheSizeInBytes = 256 * 1024 * 1024;

        // maximum allowed texture size, to prevent caching large textures (2048x2048)
        private static int _maxSize = 2048;

        private static int _texturesCreated;
        private static int _texturesDestroyed;

        public struct TexturePoolInfo
        {
            public int Width;
            public int Height;
            public TextureFormat Format;
            public bool MipChain;
            public int TextureCount;
            public ulong EstimatedSizeInBytes;
        }

        public struct CacheInfo
        {
            public ulong EstimatedSizeInBytes;
            public ulong MaxMemory;
            public int MaxSize;
            public int TexturesInCache;
            public int TexturesCreated;
            public int TexturesDestroyed;
        }

        public static List<TexturePoolInfo> GetDetailInfo(out CacheInfo cacheInfo)
        {
            int inCache = 0;

            var result = new List<TexturePoolInfo>();

            foreach (var kvp in _textures)
            {
                Stack<Texture2D> textures = kvp.Value;
                if (textures.Count == 0)
                    continue;

                Texture2D texture = textures.Peek();

                inCache += textures.Count;

                result.Add(new TexturePoolInfo()
                {
                    EstimatedSizeInBytes = (ulong)textures.Count * _singleTextureSize[kvp.Key],
                    Width = texture.width,
                    Height = texture.height,
                    Format = texture.format,
                    MipChain = texture.mipmapCount > 1,
                    TextureCount = textures.Count,
                });
            }

            cacheInfo = new CacheInfo()
            {
                EstimatedSizeInBytes = _estimatedCacheSizeInBytes,
                MaxMemory = _maxCacheSizeInBytes,
                MaxSize = _maxSize,
                TexturesCreated = _texturesCreated,
                TexturesDestroyed = _texturesDestroyed,
                TexturesInCache = inCache,
            };

            return result;
        }

        public static void SetMaximumTextureMemory(int sizeInMegabytes)
        {
            _maxCacheSizeInBytes = (ulong)sizeInMegabytes * (1024 * 1024);
        }

        public static void SetMaximumTextureSize(int size)
        {
            _maxSize = size;
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public static void Clear()
        {
            foreach (var kvp in _textures)
            {
                Stack<Texture2D> textures = kvp.Value;
                while (textures.Count > 0)
                {
                    GameObject.Destroy(textures.Pop());
                    _texturesDestroyed++;
                }
            }

            _estimatedCacheSizeInBytes = 0;
        }

        
        /// <summary>
        /// Allocates a new texture or reuses a texture from the cache
        /// </summary>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <param name="format">Texture format</param>
        /// <param name="mipChain">True to use mip maps</param>
        /// <param name="canBeCached">True if the texture setting was valid for caching</param>
        /// <returns>New or recycled texture</returns>
        public static Texture2D GetOrCreateTexture(int width, int height, TextureFormat format, 
            bool mipChain, out bool canBeCached)
        {
            canBeCached = UseCache(width, height, format);

            if (canBeCached)
            {
                ulong key = CreateTextureKey(width, height, format, mipChain);

                // check if cache contains a reusable texture
                if (_textures.TryGetValue(key, out Stack<Texture2D> textures) && textures.Count > 0)
                {
                    _estimatedCacheSizeInBytes -= _singleTextureSize[key];
                    return textures.Pop();
                }
            }

            _texturesCreated++;

            // Unity 6 sometimes maps TextureFormat.R8 to R8_UINT. 
            // We must force it to UNorm, or compute sampling breaks.
            if (format == TextureFormat.R8)
            {
                return new Texture2D(width, height, GraphicsFormat.R8_UNorm, 
                    mipChain ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
            }

            return new Texture2D(width, height, format, mipChain);
        }

        /// <summary>
        /// Frees a texture by adding it to the cache for later recycling, or destroys the texture
        /// if cache was full or the texture was not valid for recycling, such as wrong format or size.
        /// </summary>
        /// <param name="texture">Texture to free</param>
        public static void Free(Texture2D texture)
        {
            var width = texture.width;
            var height = texture.height;
            var format = texture.format;
            var mipChain = texture.mipmapCount > 1;

            if (!UseCache(width, height, format))
            {
                // this type of texture should never be recycled
                GameObject.Destroy(texture);
                _texturesDestroyed++;
                return;
            }
            
            ulong key = CreateTextureKey(width, height, format, mipChain);

            if (!_textures.TryGetValue(key, out Stack<Texture2D> textures))
            {
                // create new pool for this texture setting
                textures = new Stack<Texture2D>();
                _textures.Add(key, textures);

                // create new estimated size entry for this texture setting
                _singleTextureSize.Add(key, EstimateTextureSizeInBytes(width, height, format, mipChain));
            }

            ulong newEstimatedCacheSizeInBytes = _estimatedCacheSizeInBytes + _singleTextureSize[key];
            if (newEstimatedCacheSizeInBytes > _maxCacheSizeInBytes)
            {
                // texture did not fit in cache, destroy it
                GameObject.Destroy(texture);
                _texturesDestroyed++;
            }
            else
            {
                // add texture to our cache
                _estimatedCacheSizeInBytes = newEstimatedCacheSizeInBytes;
                textures.Push(texture);
            }
        }

        private static ulong CreateTextureKey(int width, int height, TextureFormat format, bool mipChain)
        {
            ulong key = 0;
            key |= (ulong)(ushort)width;                         // bits 0–15
            key |= (ulong)(ushort)height << 16;                  // bits 16–31
            key |= (ulong)(ushort)format << 32;                  // bits 32–47
            key |= mipChain ? (1UL << 48) : 0;                   // bit 48
            return key;
        }

        private static ulong EstimateTextureSizeInBytes(int width, int height, TextureFormat format,
            bool mipChain)
        {
            // Approximate bytes per pixel for common formats
            int bytesPerPixel = format switch
            {
                TextureFormat.Alpha8 => 1,
                TextureFormat.RGB24 => 3,
                TextureFormat.RGBA32 => 4,
                TextureFormat.ARGB32 => 4,
                TextureFormat.RGFloat => 8,
                TextureFormat.RGHalf => 4,
                TextureFormat.R8 => 1,
                TextureFormat.DXT1 => 0, // compressed formats, see below
                TextureFormat.DXT5 => 0,
                _ => 4 // fallback assumption
            };

            // Handle compressed formats separately
            if (TextureFormatIsCompressed(format))
            {
                // 4x4 block compression: DXT1 = 8 bytes/block, DXT5 = 16 bytes/block
                int blockSize = (format == TextureFormat.DXT1 || format == TextureFormat.ETC2_RGBA1) ? 8 : 16;
                int blocksWide = (width + 3) / 4;
                int blocksHigh = (height + 3) / 4;
                int baseLevelSize = blocksWide * blocksHigh * blockSize;

                ulong totalSize = (ulong)baseLevelSize;
                if (mipChain)
                {
                    int mipCount = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2)) + 1;
                    int w = width, h = height;
                    for (int mip = 1; mip < mipCount; mip++)
                    {
                        w = Mathf.Max(1, w / 2);
                        h = Mathf.Max(1, h / 2);
                        blocksWide = (w + 3) / 4;
                        blocksHigh = (h + 3) / 4;
                        totalSize += (ulong)(blocksWide * blocksHigh * blockSize);
                    }
                }

                return totalSize;
            }
            else
            {
                ulong baseLevel = (ulong)width * (ulong)height * (ulong)bytesPerPixel;

                if (!mipChain)
                    return baseLevel;

                // Approximate mipchain overhead: 1.33x the base level size
                return (baseLevel * 4) / 3;
            }
        }

        private static bool TextureFormatIsCompressed(TextureFormat format)
        {
            return format == TextureFormat.DXT1 ||
                   format == TextureFormat.DXT5 ||
                   format == TextureFormat.BC7 ||
                   format == TextureFormat.ETC_RGB4 ||
                   format == TextureFormat.ETC2_RGBA8 ||
                   format == TextureFormat.ASTC_4x4 ||
                   format == TextureFormat.ASTC_6x6;
        }

        private static bool UseCache(int width, int height, TextureFormat format)
        {
            // Ignore large textures
            if (width > _maxSize || height > _maxSize)
                return false;

            // Include only common and reusable formats
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.Alpha8:
                case TextureFormat.RGHalf:
                case TextureFormat.RGFloat:
                case TextureFormat.R8:
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                    return true;
                default:
                    return false;
            }
        }
    }
}
