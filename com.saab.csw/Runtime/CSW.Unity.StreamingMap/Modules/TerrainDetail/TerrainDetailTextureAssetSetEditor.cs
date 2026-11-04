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
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

namespace CSW.Unity.StreamingMap
{
    [CustomEditor(typeof(TerrainDetailTextureAssetSet))]
    public class TerrainDetailTextureAssetSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TerrainDetailTextureAssetSet asset = (TerrainDetailTextureAssetSet)target;
            if (asset == null)
                return;

            //Calculate layout information
            var availableSpace = EditorGUIUtility.currentViewWidth;
            var numColumns = (int)Math.Floor((availableSpace - 50) / 100);
            numColumns = Math.Max(1, numColumns);

            BuildUI(asset, numColumns);
        }

        private static void BuildUI(TerrainDetailTextureAssetSet asset, int numColumns)
        {
            EditorGUILayout.BeginHorizontal();

            if (asset.Textures == null)
                asset.Textures = new List<MappedDetailTextureAsset>();

            //Asset list
            for (int i = 0; i < asset.Textures.Count; i++)
            {
                HandleRowChange(numColumns, i);

                CreateTextureAssetField(asset, i);
            }

            EditorGUILayout.EndHorizontal();

            CreateAddButton(asset.Textures);

            CreateErrorBox(asset);

            EditorUtility.SetDirty(asset);
        }

        private static void HandleRowChange(int numColumns, int fieldCounter)
        {
            if (fieldCounter != 0 && (fieldCounter % numColumns) == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }

        private static void CreateTextureAssetField(TerrainDetailTextureAssetSet asset, int index)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;

            GUILayout.BeginVertical(style);

            GUILayout.Label("Detail asset " + index.ToString());

            var result = (TerrainDetailTextureAsset)EditorGUILayout.ObjectField(asset.Textures[index].Asset, typeof(TerrainDetailTextureAsset), false, GUILayout.Width(100), GUILayout.Height(EditorGUIUtility.singleLineHeight));

            CreateFeaturePicker(asset, index, out MapFeature featureResult);

            if (result == asset.Textures[index].Asset)
                asset.Textures[index] = new MappedDetailTextureAsset() { Asset = result, Mapping = featureResult };
            else if (result == null)
                asset.Textures[index] = new MappedDetailTextureAsset() { Asset = null, Mapping = MapFeature.None };
            else
                asset.Textures[index] = new MappedDetailTextureAsset() { Asset = result, Mapping = result.MatchingFeatures };

            if (result != null && result.Albedo != null)
                GUILayout.Box(result.Albedo, GUILayout.Width(100), GUILayout.Height(100));

            CreateDeleteButton(asset.Textures, index);

            GUILayout.EndVertical();
        }

        private static void CreateFeaturePicker(TerrainDetailTextureAssetSet asset, int index, out MapFeature result)
        {
            var currentFlags = asset.Textures[index].Mapping;
            GUILayout.Label("Assigned to");
            result = (MapFeature)EditorGUILayout.EnumFlagsField(currentFlags);
        }

        private static void CreateAddButton(List<MappedDetailTextureAsset> textures)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;
            GUILayout.BeginVertical(style, GUILayout.Width(100));

            if (GUILayout.Button(new GUIContent("Add"), GUILayout.Height(100)))
            {
                textures.Add(new MappedDetailTextureAsset() { Asset = null, Mapping = MapFeature.None });
            }

            GUILayout.EndVertical();
        }

        private static void CreateDeleteButton(List<MappedDetailTextureAsset> textures, int index)
        {
            if (GUILayout.Button(new GUIContent("Delete"), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                textures.RemoveAt(index);
            }
        }

        private static void CreateErrorBox(TerrainDetailTextureAssetSet asset)
        {
            Dictionary<MapFeature, int> foundFeatures = CountFeatures(asset);

            var errorString = "";

            CheckForAssignmentsErrors(foundFeatures, ref errorString);
            CheckForAssetsWithMissingTextures(asset, ref errorString);
            CheckForEmptyOrUnmappedAssets(asset, ref errorString);

            if (!string.IsNullOrEmpty(errorString))
                EditorGUILayout.HelpBox(errorString, MessageType.Warning);
        }

        private static void CheckForAssignmentsErrors(Dictionary<MapFeature, int> foundFeatures, ref string errorString)
        {
            foreach (var kvp in foundFeatures)
            {
                if (kvp.Value == 0)
                {
                    errorString += $"Feature \"{kvp.Key}\" has no detail texture asset assigned.\n";
                }
                else if (kvp.Value > 1)
                {
                    errorString += $"Feature \"{kvp.Key}\" has multiple detail texture assets assigned.\n";
                }
            }
        }

        private static void CheckForAssetsWithMissingTextures(TerrainDetailTextureAssetSet asset, ref string errorString)
        {
            for (int i = 0; i < asset.Textures.Count; i++)
            {
                var textureAsset = asset.Textures[i];

                if (textureAsset.Asset == null)
                    continue;

                if (textureAsset.Asset.Albedo == null ||
                    textureAsset.Asset.Displacement == null ||
                    textureAsset.Asset.Normal == null ||
                    textureAsset.Asset.Roughness == null)
                {
                    errorString += $"Detail asset {i} is missing one or more of its textures.";
                }
            }
        }

        private static void CheckForEmptyOrUnmappedAssets(TerrainDetailTextureAssetSet asset, ref string errorString)
        {
            if (asset.Textures.Any(t => t.Asset == null))
            {
                errorString += "There are empty assets in the set.\n";
            }

            if (asset.Textures.Any(t => t.Mapping == 0))
            {
                errorString += "There are unmapped assets in the set.\n";
            }
        }

        private static Dictionary<MapFeature, int> CountFeatures(TerrainDetailTextureAssetSet asset)
        {
            var features = (MapFeature[])Enum.GetValues(typeof(MapFeature));

            //Remove the "None" entry as this is not a valid feature.
            features = features.Where(f => f != MapFeature.None).ToArray();

            //Dictionary with feature as key and 0 as initial value.
            var foundFeatures = features.ToDictionary(f => f, f => 0);

            //Start counting feature occurences.
            foreach (var textureAsset in asset.Textures)
            {
                var mappedFlags = TerrainMapping.ExtractFlags(textureAsset.Mapping).Where(f => f != MapFeature.None);

                foreach (var flag in mappedFlags)
                {
                    foundFeatures[flag]++;
                }
            }

            return foundFeatures;
        }
    }
}
#endif