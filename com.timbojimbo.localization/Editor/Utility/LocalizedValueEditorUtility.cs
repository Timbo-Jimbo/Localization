using System.Collections.Generic;
using System.Linq;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Utility
{
    /// <summary>
    /// The editor's writes to localized assets: keeping their entries in step with the settings' locales, and
    /// migrating 1.x assets to locale codes. A player never does any of this; it only reads.
    /// </summary>
    public static class LocalizedValueEditorUtility
    {
        public const string ValuesPropertyName = "Values";
        public const string CodePropertyName = "Code";
        public const string ValuePropertyName = "Value";
        public const string LegacyLocalePropertyName = "LegacyLocale";

        public static List<LocalizedValue> FindAllLocalizedValuesInProject()
        {
            return AssetDatabase.FindAssets($"t:{nameof(LocalizedValue)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<LocalizedValue>()
                .Where(lv => lv != null)
                .ToList();
        }

        /// <summary>
        /// Gives <paramref name="localizedValue"/> one entry per locale in the settings, in the settings' order, keeping
        /// the values it has. Entries for codes the settings don't list stay (after the listed ones) unless
        /// <paramref name="removeUnknownCodes"/>. Returns whether anything changed; a change marks the asset dirty.
        /// </summary>
        public static bool SyncWithProjectLocales(LocalizedValue localizedValue, bool removeUnknownCodes = false)
        {
            if (localizedValue == null || !LocalizationSettings.IsInitialized) return false;

            using var serializedObject = new SerializedObject(localizedValue);
            var changed = SyncWithProjectLocales(serializedObject, removeUnknownCodes);
            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(localizedValue);
            }

            return changed;
        }

        /// <summary>The <see cref="SerializedObject"/> form of <see cref="SyncWithProjectLocales(LocalizedValue,bool)"/>; the caller applies.</summary>
        public static bool SyncWithProjectLocales(SerializedObject serializedObject, bool removeUnknownCodes = false)
        {
            var valuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            if (valuesProperty == null || !valuesProperty.isArray) return false;

            var changed = false;
            var projectCodes = new List<LocaleCode>();
            foreach (var locale in LocalizationSettings.Locales)
            {
                if (locale != null && !projectCodes.Contains(locale.Code))
                    projectCodes.Add(locale.Code);
            }

            // One entry per project locale, in the settings' order.
            for (int targetIndex = 0; targetIndex < projectCodes.Count; targetIndex++)
            {
                var code = projectCodes[targetIndex];
                int currentIndex = IndexOfCode(valuesProperty, code);

                if (currentIndex < 0)
                {
                    valuesProperty.InsertArrayElementAtIndex(targetIndex);
                    var element = valuesProperty.GetArrayElementAtIndex(targetIndex);
                    WriteCode(element.FindPropertyRelative(CodePropertyName), code);
                    ClearValue(element.FindPropertyRelative(ValuePropertyName));
                    element.FindPropertyRelative(LegacyLocalePropertyName).objectReferenceValue = null;
                    changed = true;
                }
                else if (currentIndex != targetIndex)
                {
                    valuesProperty.MoveArrayElement(currentIndex, targetIndex);
                    changed = true;
                }
            }

            if (removeUnknownCodes)
            {
                for (int i = valuesProperty.arraySize - 1; i >= projectCodes.Count; i--)
                {
                    valuesProperty.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Removes every entry keyed by <paramref name="code"/>. Returns whether any was.</summary>
        public static bool RemoveEntries(LocalizedValue localizedValue, LocaleCode code)
        {
            if (localizedValue == null) return false;

            using var serializedObject = new SerializedObject(localizedValue);
            var valuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            if (valuesProperty == null) return false;

            var changed = false;
            for (int i = valuesProperty.arraySize - 1; i >= 0; i--)
            {
                if (ReadCode(valuesProperty.GetArrayElementAtIndex(i).FindPropertyRelative(CodePropertyName)) != code) continue;
                valuesProperty.DeleteArrayElementAtIndex(i);
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(localizedValue);
            }

            return changed;
        }

        /// <summary>
        /// Moves a 1.x asset onto locale codes: every entry still keyed by a locale reference gets that locale's code and
        /// drops the reference. Returns whether anything changed; a change marks the asset dirty.
        /// </summary>
        public static bool MigrateToLocaleCodes(LocalizedValue localizedValue)
        {
            if (localizedValue == null) return false;

            // Codes the asset's OnEnable already filled in memory are visible through the SerializedObject.
            using var serializedObject = new SerializedObject(localizedValue);
            var valuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            if (valuesProperty == null) return false;

            var changed = false;
            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                var element = valuesProperty.GetArrayElementAtIndex(i);
                var legacyProperty = element.FindPropertyRelative(LegacyLocalePropertyName);
                if (legacyProperty.objectReferenceValue is not LocalizationLocale legacyLocale) continue;

                var codeProperty = element.FindPropertyRelative(CodePropertyName);
                if (ReadCode(codeProperty).IsNone)
                    WriteCode(codeProperty, legacyLocale.Code);

                legacyProperty.objectReferenceValue = null;
                changed = true;
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(localizedValue);
            }

            return changed;
        }

        /// <summary>Whether any entry is still keyed by a 1.x locale reference.</summary>
        public static bool NeedsMigration(LocalizedValue localizedValue)
        {
            if (localizedValue == null) return false;

            using var serializedObject = new SerializedObject(localizedValue);
            var valuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            if (valuesProperty == null) return false;

            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                var legacyProperty = valuesProperty.GetArrayElementAtIndex(i).FindPropertyRelative(LegacyLocalePropertyName);
                if (legacyProperty.objectReferenceValue != null) return true;
            }

            return false;
        }

        public static int IndexOfCode(SerializedProperty valuesProperty, LocaleCode code)
        {
            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                if (ReadCode(valuesProperty.GetArrayElementAtIndex(i).FindPropertyRelative(CodePropertyName)) == code)
                    return i;
            }

            return -1;
        }

        /// <summary>The <c>Value</c> property of the entry keyed by <paramref name="code"/>, or null.</summary>
        public static SerializedProperty FindValueProperty(SerializedProperty valuesProperty, LocaleCode code)
        {
            int index = IndexOfCode(valuesProperty, code);
            return index < 0 ? null : valuesProperty.GetArrayElementAtIndex(index).FindPropertyRelative(ValuePropertyName);
        }

        public static LocaleCode ReadCode(SerializedProperty codeProperty)
        {
            if (codeProperty == null) return LocaleCode.None;
            return new LocaleCode(
                codeProperty.FindPropertyRelative("_language").stringValue,
                codeProperty.FindPropertyRelative("_country").stringValue);
        }

        public static void WriteCode(SerializedProperty codeProperty, LocaleCode code)
        {
            codeProperty.FindPropertyRelative("_language").stringValue = code.Language;
            codeProperty.FindPropertyRelative("_country").stringValue = code.Country;
        }

        public static void ClearValue(SerializedProperty valueProperty)
        {
            if (valueProperty == null) return;

            switch (valueProperty.propertyType)
            {
                case SerializedPropertyType.String: valueProperty.stringValue = string.Empty; break;
                case SerializedPropertyType.ObjectReference: valueProperty.objectReferenceValue = null; break;
                case SerializedPropertyType.Integer: valueProperty.intValue = 0; break;
                case SerializedPropertyType.Boolean: valueProperty.boolValue = false; break;
                case SerializedPropertyType.Float: valueProperty.floatValue = 0f; break;
                case SerializedPropertyType.Enum: valueProperty.enumValueIndex = 0; break;
                case SerializedPropertyType.Color: valueProperty.colorValue = Color.white; break;
                case SerializedPropertyType.Vector2: valueProperty.vector2Value = Vector2.zero; break;
                case SerializedPropertyType.Vector3: valueProperty.vector3Value = Vector3.zero; break;
                case SerializedPropertyType.Vector4: valueProperty.vector4Value = Vector4.zero; break;
                case SerializedPropertyType.Rect: valueProperty.rectValue = Rect.zero; break;
                case SerializedPropertyType.Bounds: valueProperty.boundsValue = new Bounds(Vector3.zero, Vector3.zero); break;
            }
        }
    }
}
