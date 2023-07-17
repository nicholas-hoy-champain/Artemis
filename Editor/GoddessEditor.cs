using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Perell.Artemis.Generated;

namespace Perell.Artemis.Editor
{
    [CustomEditor(typeof(Goddess))]
    public class GoddessEditor : UnityEditor.Editor
    {
        SerializedProperty flagsIdsToKeep;
        SerializedProperty globallyLoadedFlagBundles;
        Vector2 scrollPos;

        protected virtual void OnEnable()
        {
            flagsIdsToKeep = serializedObject.FindProperty("flagsIdsToKeep");
            globallyLoadedFlagBundles = serializedObject.FindProperty("globallyLoadedFlagBundles");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            Goddess goddess = (Goddess)target;

            //Centered icon for the goddess
            GUILayout.BeginHorizontal();
            int iconSize = 70;
            GUILayout.Space((EditorGUIUtility.currentViewWidth / 2) - (iconSize/2)); //Space to the left of the icon with the correct sizing to center the icon in the window

            GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.perell.artemis/Editor/Icons/Goddess.png"), GUILayout.Height(iconSize), GUILayout.Width(iconSize));
            GUILayout.EndHorizontal();

            serializedObject.Update();

            //Interactable Properties
            EditorGUILayout.PropertyField(flagsIdsToKeep);
            EditorGUILayout.PropertyField(globallyLoadedFlagBundles);

            //Preview list of all Flag IDs and their value types (SYMBOL, FLOAT, BOOL)
            EditorGUILayout.Space();
            scrollPos = GUILayout.BeginScrollView(scrollPos, false, true); //Lets the list be scrollable if the list of flags wouldn't normally fit in the window given.
            FlagID[] flagIds = goddess.GetFlagIDs();
            foreach (FlagID id in flagIds)
            {
                EditorGUILayout.LabelField(id.ToString(), goddess.GetFlagValueType(id).ToString());
            }
            GUILayout.EndScrollView();

            //Reset button
            EditorGUILayout.Space();
            if (GUILayout.Button("RESET \u26A0"))
            {
                goddess.Reset();
            }

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                goddess.Modify();
                EditorUtility.SetDirty(goddess);
                AssetDatabase.SaveAssets();
                Repaint();
            }
        }
    }
}