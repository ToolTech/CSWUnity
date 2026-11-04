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
#if UNITY_EDITOR
using UnityEditor;

namespace CSW.Unity.StreamingMap
{
    [CustomEditor(typeof(TerrainDetailTextureAsset))]
    public class TerrainDetailTextureAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TerrainDetailTextureAsset asset = (TerrainDetailTextureAsset)target;
            if (asset == null)
                return;

            CreateFeaturePicker(asset, out MapFeature result);
            asset.MatchingFeatures = result;

            EditorGUILayout.BeginHorizontal();

            asset.Albedo = TextureField("Albedo", asset.Albedo);
            asset.Normal = TextureField("Normal", asset.Normal);
            asset.Displacement = TextureField("Displacement", asset.Displacement);
            asset.Roughness = TextureField("Roughness", asset.Roughness);

            EditorGUILayout.EndHorizontal();

            EditorUtility.SetDirty(asset);
        }

        private static void CreateFeaturePicker(TerrainDetailTextureAsset asset, out MapFeature result)
        {
            var currentFlags = asset.MatchingFeatures;
            GUILayout.Label("Matching features");
            result = (MapFeature)EditorGUILayout.EnumFlagsField(currentFlags);
        }

        private static Texture2D TextureField(string name, Texture2D texture)
        {
            GUILayout.BeginVertical();
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.fixedWidth = 100;
            GUILayout.Label(name, style);
            var result = (Texture2D)EditorGUILayout.ObjectField(texture, typeof(Texture2D), false, GUILayout.Width(100), GUILayout.Height(100));
            GUILayout.EndVertical();
            return result;
        }
    }
}
#endif