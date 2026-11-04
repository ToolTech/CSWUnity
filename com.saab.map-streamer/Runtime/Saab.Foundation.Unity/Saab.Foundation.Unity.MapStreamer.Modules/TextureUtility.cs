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


using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Utils
{
    public static class TextureUtility
    {
        private static uint NextPowerOfTwo(uint v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return v + 1;
        }

        public static Texture2DArray Create2DArray(List<Texture2D> Textures, TextureFormat targetFormat)
        {
            var textureCount = Textures.Count;
            var textureResolution = Mathf.Max(Textures.Max(item => item.width), Textures.Max(item => item.height));
            textureResolution = (int)NextPowerOfTwo((uint)textureResolution);

            Texture2DArray textureArray;

            textureArray = new Texture2DArray(textureResolution, textureResolution, textureCount, targetFormat, true)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            //RenderTexture temporaryRenderTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            //{
            //    useMipMap = true,
            //    antiAliasing = 1
            //};

            RenderTexture temporaryRenderTexture = RenderTexture.GetTemporary(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);


            for (int i = 0; i < textureCount; i++)
            {
                //Debug.LogWarning($"graphic format: {foliages[i].MainTexture.graphicsFormat} format: {foliages[i].MainTexture.format}");
                Graphics.Blit(Textures[i], temporaryRenderTexture);
                RenderTexture.active = temporaryRenderTexture;

                Texture2D temporaryTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.ARGB32, true);
                temporaryTexture.ReadPixels(new Rect(0, 0, temporaryTexture.width, temporaryTexture.height), 0, 0);
                RenderTexture.active = null;
                temporaryTexture.Apply(updateMipmaps: true);
                temporaryTexture.Compress(true);
                Graphics.CopyTexture(temporaryTexture, 0, textureArray, i);

                RenderTexture.ReleaseTemporary(temporaryRenderTexture);
            }
            textureArray.Apply(false, true);

            RenderTexture.ReleaseTemporary(temporaryRenderTexture);

            return textureArray;
        }
    }
}
