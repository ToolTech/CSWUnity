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

namespace CSW.Unity.StreamingMap
{
    [CreateAssetMenu(fileName = "TerrainDetailTextureAsset", menuName = "Terrain/DetailTextureAsset")]
    [Serializable]
    public class TerrainDetailTextureAsset : ScriptableObject
    {
        public MapFeature MatchingFeatures;
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D Displacement;
        public Texture2D Roughness;
    }
}