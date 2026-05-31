using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization
{

    /// <summary>
    /// Generic property drawer base for any <see cref="LocalizableValue{TContainer,TValue}"/>.
    /// Plugin users inherit this and override <see cref="LocalizedAssetType"/> to register a
    /// drawer for their localized container type.
    /// </summary>
    public abstract class LocalizableValueDrawer : PropertyDrawer
    {
        private const string LocalizedPropertyName = "Localized";
        private const string UnlocalizedPropertyName = "Unlocalized";
        private const float ModeButtonWidth = 54f;
        private const float Spacing = 4f;

        private static readonly GUIContent InlineModeContent = new("Inline", "Currently using the inline fallback value. Click to pick a localized asset.");
        private static readonly GUIContent AssetModeContent = new("Asset", "Currently using a localized asset. Click to return to the inline fallback value.");
        private static readonly GUIContent MissingPropertiesContent = new("Invalid Localizable Value", "Expected Localized and Unlocalized serialized fields.");

        protected abstract Type LocalizedAssetType { get; }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty localizedProperty = property.FindPropertyRelative(LocalizedPropertyName);
            SerializedProperty unlocalizedProperty = property.FindPropertyRelative(UnlocalizedPropertyName);

            if (localizedProperty == null || unlocalizedProperty == null)
                return EditorGUIUtility.singleLineHeight;

            if (IsAssetMode(localizedProperty))
                return EditorGUI.GetPropertyHeight(localizedProperty, GUIContent.none, true);

            return GetUnlocalizedPropertyHeight(unlocalizedProperty);
        }

        protected virtual float GetUnlocalizedPropertyHeight(SerializedProperty unlocalizedProperty)
        {
            return EditorGUI.GetPropertyHeight(unlocalizedProperty, GUIContent.none, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty localizedProperty = property.FindPropertyRelative(LocalizedPropertyName);
            SerializedProperty unlocalizedProperty = property.FindPropertyRelative(UnlocalizedPropertyName);
            bool assetMode = IsAssetMode(localizedProperty);
            SerializedProperty activeProperty = assetMode ? localizedProperty : unlocalizedProperty;

            EditorGUI.BeginProperty(position, label, activeProperty);

            Rect modePosition = position;
            modePosition.xMin = modePosition.xMax - ModeButtonWidth;
            modePosition.width = ModeButtonWidth;
            modePosition.height = EditorGUIUtility.singleLineHeight;

            Rect valuePosition = position;
            valuePosition.xMax = modePosition.xMin - Spacing;

            EditorGUI.PropertyField(valuePosition, activeProperty, label, true);

            GUIContent modeContent = assetMode ? InlineModeContent : AssetModeContent;
            if (GUI.Button(modePosition, modeContent, EditorStyles.miniButton))
            {
                if (assetMode)
                {
                    localizedProperty.objectReferenceValue = null;
                }
                else
                {
                    LocalizedValueDrawer.ShowLocalizedAssetPicker(modePosition, LocalizedAssetType, localizedProperty);
                }
            }

            EditorGUI.EndProperty();
        }

        private static bool IsAssetMode(SerializedProperty localizedProperty)
        {
            return localizedProperty.objectReferenceValue != null || localizedProperty.hasMultipleDifferentValues;
        }
    }
}