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
using UnityEngine;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CustomEditor(typeof(FoliageSet))]
    public class FoliageSetEditor : Editor
    {
        private static MapFeature _feature;

        public override void OnInspectorGUI()
        {
            FoliageSet set = (FoliageSet)target;
            if (set == null) return;

            //Calculate layout information
            var availableSpace = EditorGUIUtility.currentViewWidth;
            var numColumns = (int)Math.Floor((availableSpace - 50) / 100);
            numColumns = Math.Max(1, numColumns);

            BuildUI(set, numColumns);
        }

        private static void BuildUI(FoliageSet set, int numColumns)
        {
            EditorGUILayout.BeginHorizontal();

            if (set.Assets == null)
                set.Assets = new List<MappedFoliageAsset>();

            //Asset list
            for (int i = 0; i < set.Assets.Count; i++)
            {
                HandleRowChange(numColumns, i);
                CreateTextureAssetField(set, i);
            }

            EditorGUILayout.EndHorizontal();

            _feature = (MapFeature)EditorGUILayout.EnumPopup("Feature", _feature);

            EditorGUILayout.BeginHorizontal();
            CreateAddButton(set.Assets);
            CreateAutoFillButton(set.Assets, _feature);
            CreateClearButton(set.Assets);
            EditorGUILayout.EndHorizontal();

            CreateErrorBox(set.Assets);

            EditorUtility.SetDirty(set);
        }

        private static void HandleRowChange(int numColumns, int fieldCounter)
        {
            if (fieldCounter != 0 && (fieldCounter % numColumns) == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }

        private static void CreateTextureAssetField(FoliageSet set, int index)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;

            GUILayout.BeginVertical(style);

            GUILayout.Label("Detail asset " + index.ToString());

            var result = (Foliage)EditorGUILayout.ObjectField(set.Assets[index].Foliage, typeof(Foliage), false, GUILayout.Width(100), GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (result == set.Assets[index].Foliage)
                set.Assets[index] = new MappedFoliageAsset() { Foliage = result, Weight = 0 };
            else
                set.Assets[index] = new MappedFoliageAsset() { Foliage = result, Weight = result.Weight };

            if (result != null && result.MainTexture != null)
                GUILayout.Box(result.MainTexture, GUILayout.Width(100), GUILayout.Height(100));

            CreateDeleteButton(set.Assets, index);
            GUILayout.EndVertical();
        }

        private static void CreateDeleteButton(List<MappedFoliageAsset> foliage, int index)
        {
            if (GUILayout.Button(new GUIContent("Delete"), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                foliage.RemoveAt(index);
            }
        }

        private static void CreateAddButton(List<MappedFoliageAsset> foliage)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;
            GUILayout.BeginVertical(style, GUILayout.Width(100));

            if (GUILayout.Button(new GUIContent("Add"), GUILayout.Height(50)))
            {
                foliage.Add(new MappedFoliageAsset() { Foliage = null, Weight = 0 });
            }

            GUILayout.EndVertical();
        }

        private static void CreateClearButton(List<MappedFoliageAsset> foliage)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;
            GUILayout.BeginVertical(style, GUILayout.Width(100));

            if (GUILayout.Button(new GUIContent("Clear"), GUILayout.Height(50)))
            {
                foliage.Clear();
            }

            GUILayout.EndVertical();
        }

        private static void CreateAutoFillButton(List<MappedFoliageAsset> foliage, MapFeature mapFeature)
        {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;
            GUILayout.BeginVertical(style, GUILayout.Width(100));

            if (GUILayout.Button(new GUIContent("Auto Fill"), GUILayout.Height(50)))
            {
                FindAssets(mapFeature, foliage);
            }

            GUILayout.EndVertical();
        }

        private static void CreateErrorBox(List<MappedFoliageAsset> foliage)
        {
            var errorString = "";
            var showError = false;

            // check for duplicates
            if (foliage.GroupBy(asset => asset.Foliage).Any(match => match.Count() > 1))
            {
                showError = true;
                var duplicateGroups = foliage.GroupBy(asset => asset.Foliage).Where(match => match.Count() > 1);
                errorString = "There is duplicates of foliage in set:\n";

                foreach (var group in duplicateGroups)
                {
                    foreach (var asset in group)
                    {
                        if (asset.Foliage != null)
                            errorString += $"{asset.Foliage.name}\n";
                        else
                            errorString += "multiple undefiend in set\n";
                        break;
                    }
                }
            }
            // check for undefiend
            if (foliage.Any(asset => asset.Foliage == null))
            {
                showError = true;
                errorString += "There is undefiend foliage in set";
            }
            // Draw error box
            if(showError)
                EditorGUILayout.HelpBox(errorString, MessageType.Warning);
        }

        private static void FindAssets(MapFeature features, List<MappedFoliageAsset> assets)
        {
            var result = AssetDatabase.FindAssets($"t: {typeof(Foliage).Name}").ToList()
                      .Select(AssetDatabase.GUIDToAssetPath)
                      .Select(AssetDatabase.LoadAssetAtPath<Foliage>)
                      .ToList();

            result = result.Where(f => f.Feature.HasFlag(features)).ToList();

            foreach (var foliage in result)
            {
                if (!assets.Any(asset => asset.Foliage == foliage))
                {
                    assets.Add(new MappedFoliageAsset() { Foliage = foliage, Weight = foliage.Weight });
                }
            }
        }
    }
}
#endif