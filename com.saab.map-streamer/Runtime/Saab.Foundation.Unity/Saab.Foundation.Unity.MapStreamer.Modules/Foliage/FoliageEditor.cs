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

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [CustomEditor(typeof(Foliage))]
    public class FoliageEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            Foliage foliage = (Foliage)target;
            if (foliage == null) return;

            EditorGUILayout.BeginHorizontal();
            foliage.MainTexture = TextureField("Main Texture", foliage.MainTexture);
            TextureField("Autumn Texture", foliage.AutumnTexture);
            TextureField("Winter Texture", foliage.WinterTexture);
            foliage.Normal = TextureField("Normal", foliage.Normal);
            EditorGUILayout.EndHorizontal();

            foliage.Feature = (MapFeature)EditorGUILayout.EnumFlagsField("Feature", foliage.Feature);
            foliage.Biome = (FoliageBiome)EditorGUILayout.EnumFlagsField("Biome", foliage.Biome);
            foliage.Mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", foliage.Mesh, typeof(Mesh), allowSceneObjects: false);
            foliage.MeshMaterial = (Material)EditorGUILayout.ObjectField("Mesh Material", foliage.MeshMaterial, typeof(Material), allowSceneObjects: false);

            EditorGUILayout.BeginHorizontal();
            foliage.MaxMin.x = EditorGUILayout.FloatField("Min height", foliage.MaxMin.x);
            foliage.MaxMin.y = EditorGUILayout.FloatField("Max height", foliage.MaxMin.y);
            foliage.MaxMin.y = Mathf.Max(foliage.MaxMin.x, foliage.MaxMin.y);
            foliage.MaxMin.x = Mathf.Min(foliage.MaxMin.x, foliage.MaxMin.y);
            EditorGUILayout.EndHorizontal();

            foliage.Offset.y = EditorGUILayout.FloatField("GroundOffset", foliage.Offset.y);
            foliage.Offset.y = Mathf.Abs(foliage.Offset.y > 1 ? 1 : foliage.Offset.y);

            foliage.Offset.x = 0;

            foliage.TextureMode = (TextureMode)EditorGUILayout.EnumPopup("Mode", foliage.TextureMode);
            EditorGUILayout.BeginHorizontal();
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperLeft;
            style.fixedWidth = 100;
            GUILayout.Label("Weight", style);
            foliage.Weight = EditorGUILayout.Slider(foliage.Weight, 0.1f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Cull", style);
            foliage.CullAreaWidth = EditorGUILayout.Slider(foliage.CullAreaWidth, 0.01f, 1f);
            EditorGUILayout.EndHorizontal();

            if (foliage.MainTexture != null)
            {
                EditorGUILayout.BeginVertical();
                int PreviewSize = 400;
                int HeightPos = 340;
                EditorGUILayout.Space(450, true);
                EditorGUI.DrawRect(new Rect(3, HeightPos, PreviewSize, PreviewSize), Color.white);

                if (foliage.TextureMode == TextureMode.Atlas)
                {
                    GUI.DrawTextureWithTexCoords(new Rect(3, HeightPos, PreviewSize, PreviewSize), foliage.MainTexture, new Rect(0, 0, 0.5f, 0.5f));
                    EditorGUI.DrawRect(new Rect(3, HeightPos - 5 + PreviewSize * (1 - foliage.Offset.y) + 4, PreviewSize, 2), new Color(1, 0, 0, 0.5f));

                    var width = PreviewSize * foliage.CullAreaWidth;

                    var offset = PreviewSize * (foliage.Offset.y);
                    var rect = new Rect(3 + PreviewSize / 2f - width / 2f, HeightPos + offset, width, PreviewSize);
                    rect.y -= offset;
                    rect.height -= offset;
                    DrawRect(rect, 2, Color.green);

                }
                else if (foliage.TextureMode == TextureMode.Single)
                {
                    GUI.DrawTextureWithTexCoords(new Rect(3, HeightPos, PreviewSize, PreviewSize), foliage.MainTexture, new Rect(0, 0, 1, 1));
                }

                EditorGUILayout.EndVertical();
            }
            EditorUtility.SetDirty(foliage);
        }

        private static void DrawRect(Rect rect, float strokeWidth, Color color)
        {
            // Draw the top border.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, strokeWidth), color);

            // Draw the bottom border.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - strokeWidth, rect.width, strokeWidth), color);

            // Draw the left border.
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, strokeWidth, rect.height), color);

            // Draw the right border.
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - strokeWidth, rect.y, strokeWidth, rect.height), color);
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