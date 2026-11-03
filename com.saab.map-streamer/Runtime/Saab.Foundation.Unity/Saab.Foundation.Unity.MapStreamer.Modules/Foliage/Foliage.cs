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

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [System.Flags]
    public enum FoliageBiome
    {
        None = 0,
        PolarDesert = 1 << 0,
        Tundra = 1 << 1,
        Taiga = 1 << 2,
        TemperateForest = 1 << 3,
        TemperateSteppe = 1 << 4,
        SubtropicalForest = 1 << 5,
        MediterraneanVegetation = 1 << 6,
        MonsoonForest = 1 << 7,
        AridDesert = 1 << 8,
        XericShrubland = 1 << 9,
        DrySteppe = 1 << 10,
        SemiaridDesert = 1 << 11,
        GrassSavanna = 1 << 12,
        TreeSavanna = 1 << 13,
        TropicalForest = 1 << 14,
        TropicalRainForest = 1 << 15,
        AlpineTundra = 1 << 16,
        MontaneForest = 1 << 17,
    }

    public enum TextureMode
    {
        Single,
        Atlas
    }

    [CreateAssetMenu(fileName = "Foliage", menuName = "Terrain/Foliage")]
    public class Foliage : ScriptableObject
    {
        public Texture2D MainTexture;
        public Texture2D AutumnTexture;
        public Texture2D WinterTexture;
        public Texture2D Normal;
        public Mesh Mesh;
        public Material MeshMaterial;
        public Vector2 MaxMin;
        public Vector2 Offset;

        [Range(0.1f, 1f)]
        public float CullAreaWidth = 0.25f;
        [Range(0f,1f)]
        public float Weight = 1;
        public MapFeature Feature;
        public FoliageBiome Biome;
        public TextureMode TextureMode;
    }
}