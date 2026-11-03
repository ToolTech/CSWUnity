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

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [Flags]
    public enum MapFeature
    {
        None = 0,
        // ****** ****** Ground types  ****** ******
        // both a Ground type and a foliage
        Grass = 1 << 0,
        // for mountainous terrain or other types of rocky terrain
        Rock = 1 << 1,
        // needed for year around snow covered terrain (mountain peaks)
        Snow = 1 << 2,
        Sand = 1 << 3,
        Gravel = 1 << 4,
        Dirt = 1 << 5,
        Barren = 1 << 6,
        Water = 1 << 7,
        Concrete = 1 << 8,
        // ****** Transport ******
        PavedRoad = 1 << 9,
        DirtRoad = 1 << 10,
        // ****** Foliage ******
        // unknown foliage (bushes + trees)
        Vegetation = 1 << 11,
        GrassLawn = 1 << 12,
        GrassWildFlowers = 1 << 13,
        GrassTall = 1 << 14,
        CropField = 1 << 15,
        Bushes = 1 << 16,
        // unknown tree (Evergreen + Deciduous)
        Trees = 1 << 17,
        Evergreen = 1 << 18,
        Deciduous = 1 << 19,
        Swamp = 1 << 20,
        Asphalt = 1 << 21,
    }

    public static class TerrainMapping
    {
        private static Dictionary<int, MapFeature> _gzFeature = new Dictionary<int, MapFeature>
        {
            {0, MapFeature.None },              // Nodata
            {1, MapFeature.None },              // Undefined
            {6, MapFeature.Concrete},           // Building
            {8, MapFeature.Concrete },          // Manmade object
            {21, MapFeature.Grass},             // Ground grass
            {22, MapFeature.Barren},            // Ground barren
            {23, MapFeature.Sand},              // Ground sand
            {24, MapFeature.Rock},              // Ground hydro soil
            {25, MapFeature.Dirt},              // Ground rock
            {26, MapFeature.Gravel},            // Ground Gravel
            {27, MapFeature.CropField},         // Ground Crops
            {28, MapFeature.Swamp},             // Ground Swamp
            {29, MapFeature.GrassWildFlowers},  // Ground heath

            {40, MapFeature.Vegetation},        // Vegetation
            {47, MapFeature.DirtRoad},          // Vegetation over road
            {48, MapFeature.None },             // Vegetation over building
            {49, MapFeature.None },             // Vegetation over bridge

            {60, MapFeature.Water},             // Water unspecified
            {65, MapFeature.Water},             // Water swimmingpool

            {80, MapFeature.Concrete},          // Man-made surface
            {81, MapFeature.PavedRoad},         // Man-made surface paved road
            {83, MapFeature.DirtRoad},          // Man-made surface dirt road
            {86, MapFeature.None},              // Man-made surface bridge
            {89, MapFeature.None},              // Man-made surface railbridge
            {90, MapFeature.Concrete},          // Man-made surface rail
            {92, MapFeature.Asphalt},           // Man-made surface runway
        };


        static private MapFeature MapgzData(int label)
        {
            if (_gzFeature.TryGetValue(label, out var feature))
            {
                return feature;
            }
            else
            {
                return MapFeature.None;
            }
        }

        public static int BinaryToIndex(int binaryValue)
        {
            if (binaryValue == 0)
                return 0;

            int index = 1;
            while ((binaryValue & 1) != 1)
            {
                binaryValue >>= 1;
                index++;
            }
            return index;
        }

        public static int[] MapFeatureData()
        {
            return MapFeatureData(MapgzData);
        }

        public static int[] MapFeatureData(Func<int, MapFeature> Mapping)
        {
            int[] labels = new int[256];

            for (int i = 0; i < 256; i++)
            {
                labels[i] = BinaryToIndex((int)Mapping(i));
            }
            return labels;
        }

        public static int[] FeatureTruthTable(int[] labels, MapFeature features)
        {
            var truthTable = new int[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                MapFeature feature = (MapFeature)(1 << (labels[i] - 1));
                truthTable[i] = features.HasFlag(feature) ? 1 : 0;
            }

            return truthTable;
        }

        public static MapFeature[] ExtractFlags(MapFeature flags, bool includeZero = false)
        {
            var values = Enum.GetValues(typeof(MapFeature));
            List<MapFeature> foundFlags = new List<MapFeature>();

            foreach (var value in values)
            {
                var flag = (MapFeature)value;

                if (flags.HasFlag(flag) && (includeZero || flag != 0))
                    foundFlags.Add(flag);
            }

            return foundFlags.ToArray();
        }
    }
}