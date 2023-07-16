using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Perell.Artemis.Editor
{
    [CustomEditor(typeof(PreDictionaryFletcher),true)]
    public class PreDictionaryFletcherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PreDictionaryFletcher preDictionaryFletcher = (PreDictionaryFletcher)target;

            EditorGUIUtility.SetIconForObject(preDictionaryFletcher, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Artemis/Editor/Resources/Fletcher.png"));

            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (GUILayout.Button("Parse CSV into database"))
            {
                preDictionaryFletcher.GeneratorArrowDatabase();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(preDictionaryFletcher);
                AssetDatabase.SaveAssets();
                Repaint();
            }
        }
    }

}
