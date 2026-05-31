using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization
{
    /// <summary>
    /// Base inspector for <see cref="LocalizedValue"/> types.
    /// </summary>
    public abstract class LocalizedValueEditor : Editor
    {
        protected const string ValuesPropertyName = "Values";
        protected const string DescriptionPropertyName = "Description";
        protected const string LocalePropertyName = "Locale";
        protected const string ValuePropertyName = "Value";

        private const float TagWidth = 70f;

        private const string FoldoutStateKeyPrefix = "LocalizedValueEditor.Foldout.";

        protected SerializedProperty ValuesProperty { get; private set; }
        protected SerializedProperty DescriptionProperty { get; private set; }
        protected bool IsEditingMultipleObjects => serializedObject.targetObjects.Length > 1;

        protected virtual void OnEnable()
        {
            ValuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            DescriptionProperty = serializedObject.FindProperty(DescriptionPropertyName);
        }

        protected virtual void OnDisable()
        {
        }

        protected virtual Stats GetStats()
        {
            Stats stats = default;
            if (ValuesProperty == null) return stats;

            stats.TotalRows = ValuesProperty.arraySize;
            stats.LocalisedCount = CountLocalised();
            stats.MissingCount = Mathf.Max(0, stats.TotalRows - stats.LocalisedCount);
            return stats;
        }

        /// <summary>
        /// Draws the localized value inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (ValuesProperty == null)
            {
                EditorGUILayout.HelpBox($"Could not find serialized property '{ValuesPropertyName}'.", MessageType.Error);
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (!LocalizationSettings.IsInitialized || LocalizationSettings.Locales.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No locales configured. Add them on the LocalizationSettings asset.",
                    MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawTopSection();
            GUILayout.Space(8f);
            DrawNonDefaultLocaleRows();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTopSection()
        {
            var targetName = targets.Length == 1 ? ObjectNames.NicifyVariableName(targets[0].name) : $"{targets.Length} {targets[0].GetType().Name}s";
            var stats = GetStats();
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(targetName, LocalizationEditorGUI.HeaderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{stats.LocalisedCount} / {stats.TotalRows} Localized" , LocalizationEditorGUI.StatLabelStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                if (stats.MissingCount > 0)
                {
                    GUILayout.Label("•", LocalizationEditorGUI.StatLabelStyle, GUILayout.ExpandHeight(true));
                    GUILayout.Label($"{stats.MissingCount} Missing", LocalizationEditorGUI.MissingTagStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                }
            }
            
            EditorGUILayout.Space(4f);
            
            DrawDescription();
            EditorGUILayout.Space(4f);
            DrawDefaultLocaleSection();
            EditorGUILayout.Space(4f);  
            DrawAdditionalControls();
        }

        // ---------------------------------------------------------------------
        // Default locale section
        // ---------------------------------------------------------------------

        private void DrawDefaultLocaleSection()
        {
            int defaultIndex = FindDefaultLocaleRowIndex();
            if (defaultIndex < 0)
            {
                EditorGUILayout.HelpBox(
                    "No default locale is configured. Set one on the LocalizationSettings asset.",
                    MessageType.Warning);
                return;
            }

            SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(defaultIndex);
            SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
            SerializedProperty valueProperty = elementProperty.FindPropertyRelative(ValuePropertyName);
            LocalizationLocale defaultLocale = localeProperty != null ? localeProperty.objectReferenceValue as LocalizationLocale : null;

            DrawInlineTitleSubtitle(GetRowTitle(defaultLocale), GetRowSubtitle(defaultLocale));
            EditorGUILayout.Space(2f);
            OnLocaleValueGUI(defaultLocale, valueProperty);
        }

        // ---------------------------------------------------------------------
        // Description section
        // ---------------------------------------------------------------------

        private void DrawDescription()
        {
            if (DescriptionProperty == null) return;

            GUILayout.Label("Description", LocalizationEditorGUI.RowTitleStyle);
            GUILayout.Label("Notes for translators or designers. Not shown in-game.", LocalizationEditorGUI.RowSubtitleStyle);
            EditorGUILayout.Space(2f);
            DrawDescriptionField(DescriptionProperty);
        }

        /// <summary>
        /// Draws the Description text area. Override to provide a different control.
        /// </summary>
        protected virtual void DrawDescriptionField(SerializedProperty descriptionProperty)
        {
            bool previousShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = descriptionProperty.hasMultipleDifferentValues;

            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUILayout.TextArea(descriptionProperty.stringValue ?? string.Empty, GUILayout.MinHeight(48f));
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUI.showMixedValue = previousShowMixedValue;

            if (changed)
            {
                descriptionProperty.stringValue = newValue;
                descriptionProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        // ---------------------------------------------------------------------
        // Additional controls (extension point for type-specific tools)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Drawn between the description and the per-locale rows. Default is empty. Override
        /// to add type-specific tooling (e.g. AI translate buttons for strings, a progress
        /// bar, a "regenerate variants" action for sprite sets, etc.).
        /// </summary>
        protected virtual void DrawAdditionalControls() { }

        // ---------------------------------------------------------------------
        // Non-default locale rows
        // ---------------------------------------------------------------------

        private void DrawNonDefaultLocaleRows()
        {
            for (int i = 0; i < ValuesProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
                LocalizationLocale locale = localeProperty != null ? localeProperty.objectReferenceValue as LocalizationLocale : null;

                if (locale != null && IsDefaultLocale(locale)) continue;

                DrawRow(i);
            }
        }

        private void DrawRow(int index)
        {
            SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(index);
            SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
            SerializedProperty valueProperty = elementProperty.FindPropertyRelative(ValuePropertyName);
            LocalizationLocale locale = localeProperty != null ? localeProperty.objectReferenceValue as LocalizationLocale : null;

            bool expanded = GetLocaleRowExpanded(locale);
            LocalizationEditorGUI.DrawFoldout(
                ref expanded, 
                () => DrawRowHeaderContent(locale, valueProperty),
                onToggle: toggle => SetLocaleRowExpanded(locale, toggle),
                onGroupToggle: toggle =>
                {
                    foreach (LocalizationLocale l in LocalizationSettings.Locales)
                        SetLocaleRowExpanded(l, toggle);
                }
            );

            if (!expanded) return;

            EditorGUILayout.Space(4f);
            OnLocaleValueGUI(locale, valueProperty);

            if (locale == null)
                EditorGUILayout.HelpBox("This entry has no Locale assigned. It will be ignored at runtime.", MessageType.Warning);
            else if (IsDuplicateLocale(locale))
                EditorGUILayout.HelpBox("Duplicate Locale: another entry uses the same Locale asset.", MessageType.Warning);

            EditorGUILayout.Space(2f);
        }

        private void DrawRowHeaderContent(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            DrawInlineTitleSubtitle(GetRowTitle(locale), GetRowSubtitle(locale));
            
            GUILayout.FlexibleSpace();

            if (!HasMeaningfulValue(valueProperty) && !valueProperty.hasMultipleDifferentValues)
                GUILayout.Label("Missing", LocalizationEditorGUI.MissingTagStyle, GUILayout.Width(TagWidth));
            else
                GUILayout.Space(TagWidth);

            DrawRowMenuButton(locale, valueProperty);
        }

        private void DrawRowMenuButton(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if(!ShouldShowLocaleMenuButton(locale)) return;

        
            if (LocalizationEditorGUI.DrawKebabMenu())
            {
                GenericMenu menu = new();
                BuildLocaleMenu(menu, locale, valueProperty);
                menu.ShowAsContext();
            }
        }

        private void DrawInlineTitleSubtitle(string title, string subtitle)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, LocalizationEditorGUI.RowTitleStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));

                if (!string.IsNullOrEmpty(subtitle))
                    GUILayout.Label(subtitle, LocalizationEditorGUI.RowSubtitleStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));

                GUILayout.FlexibleSpace();
            }
        }

        protected virtual bool ShouldShowLocaleMenuButton(LocalizationLocale locale) => false;

        /// <summary>
        /// Populate the per-locale context menu. Base implementation adds a "Clear" item.
        /// Override and call <c>base.BuildLocaleMenu</c> to insert items around the default.
        /// Returning without adding any items will hide the kebab button entirely.
        /// </summary>
        protected virtual void BuildLocaleMenu(GenericMenu menu, LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (locale == null) return;
            string clearLabel = IsEditingMultipleObjects ? "Clear For Selection" : "Clear";
            menu.AddItem(new GUIContent(clearLabel), false, () => ClearLocaleForSelectedTargets(locale));
        }

        /// <summary>
        /// Draws the value field for one locale row.
        /// </summary>
        public virtual void OnLocaleValueGUI(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (valueProperty == null)
            {
                EditorGUILayout.HelpBox("Could not find the row's Value property.", MessageType.Error);
                return;
            }

            var change = EditorGUILayout.PropertyField(valueProperty, GUIContent.none, true);
            if (change)
            {
                valueProperty.serializedObject.ApplyModifiedProperties();
                LocalizationSettings.RaiseLocalizedValueChanged(valueProperty.serializedObject.targetObject as LocalizedValue);
            }
        }

        public virtual bool TrySeedDefaultValueFromTarget(GameObject target, LocalizationLocale locale, SerializedProperty value)
        {
            return false;
        }

        /// <summary>
        /// Returns true if the property holds a meaningful (non-default) value. Override for
        /// custom equality on non-trivial value types.
        /// </summary>
        protected virtual bool HasMeaningfulValue(SerializedProperty valueProperty)
        {
            if (valueProperty == null) return false;

            switch (valueProperty.propertyType)
            {
                case SerializedPropertyType.String:
                    return !string.IsNullOrWhiteSpace(valueProperty.stringValue);
                case SerializedPropertyType.ObjectReference:
                    return valueProperty.objectReferenceValue != null;
                default:
                    return true;
            }
        }

        // ---------------------------------------------------------------------
        // Helpers exposed to derived editors
        // ---------------------------------------------------------------------

        /// <summary>
        /// Clears the value for the given locale across all selected targets. No-op for the
        /// default locale. Exposed so derived editors can offer the same behavior from custom
        /// controls.
        /// </summary>
        protected void ClearLocaleForSelectedTargets(LocalizationLocale locale)
        {
            if (locale == null || IsDefaultLocale(locale)) return;

            UnityEngine.Object[] selectedTargets = serializedObject.targetObjects;
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is not LocalizedValue localizedValue) continue;

                Undo.RecordObject(localizedValue, $"Clear {GetRowTitle(locale)}");

                using SerializedObject targetObject = new(localizedValue);
                SerializedProperty valuesProperty = targetObject.FindProperty(ValuesPropertyName);
                if (valuesProperty == null) continue;

                for (int valueIndex = 0; valueIndex < valuesProperty.arraySize; valueIndex++)
                {
                    SerializedProperty pair = valuesProperty.GetArrayElementAtIndex(valueIndex);
                    SerializedProperty localeProperty = pair.FindPropertyRelative(LocalePropertyName);
                    if (localeProperty == null || localeProperty.objectReferenceValue != locale) continue;

                    ClearValue(pair.FindPropertyRelative(ValuePropertyName));
                    break;
                }

                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(localizedValue);
                LocalizationSettings.RaiseLocalizedValueChanged(localizedValue);
            }

            serializedObject.Update();
            Repaint();
        }

        /// <summary>
        /// Clears every non-default-locale value across all selected targets. Not used by
        /// the base UI directly; exposed so derived editors / custom tools can offer it.
        /// </summary>
        protected void ClearNonDefaultTranslationsForSelectedTargets()
        {
            LocalizationLocale defaultLocale = LocalizationSettings.DefaultLocale;
            if (defaultLocale == null) return;

            UnityEngine.Object[] selectedTargets = serializedObject.targetObjects;
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is not LocalizedValue localizedValue) continue;

                Undo.RecordObject(localizedValue, "Clear Non-Default Values");

                using SerializedObject targetObject = new(localizedValue);
                SerializedProperty valuesProperty = targetObject.FindProperty(ValuesPropertyName);
                if (valuesProperty == null) continue;

                for (int valueIndex = 0; valueIndex < valuesProperty.arraySize; valueIndex++)
                {
                    SerializedProperty pair = valuesProperty.GetArrayElementAtIndex(valueIndex);
                    SerializedProperty localeProperty = pair.FindPropertyRelative(LocalePropertyName);
                    if (localeProperty == null || localeProperty.objectReferenceValue == null || localeProperty.objectReferenceValue == defaultLocale)
                        continue;

                    ClearValue(pair.FindPropertyRelative(ValuePropertyName));
                }

                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(localizedValue);
                LocalizationSettings.RaiseLocalizedValueChanged(localizedValue);
            }

            serializedObject.Update();
            Repaint();
        }

        // ---------------------------------------------------------------------
        // Internal scaffolding (foldouts, sync, ordering, value clearing)
        // ---------------------------------------------------------------------

        protected static bool GetLocaleRowExpanded(LocalizationLocale locale)
        {
            return SessionState.GetBool(GetFoldoutKey(locale), true);
        }

        /// <summary>
        /// Forces the foldout row for the given locale to be expanded or collapsed. Safe to
        /// call from derived editors (e.g. to reveal a row that is about to receive a
        /// translation).
        /// </summary>
        protected static void SetLocaleRowExpanded(LocalizationLocale locale, bool expanded)
        {
            SessionState.SetBool(GetFoldoutKey(locale), expanded);
        }

        private static string GetFoldoutKey(LocalizationLocale locale)
        {
            string suffix = locale != null ? locale.DisplayCode : "<unassigned>";
            return FoldoutStateKeyPrefix + "locale." + suffix;
        }

        protected static bool IsDefaultLocale(LocalizationLocale locale)
        {
            if (locale == null) return false;
            return LocalizationSettings.DefaultLocale == locale;
        }

        private int FindDefaultLocaleRowIndex()
        {
            LocalizationLocale defaultLocale = LocalizationSettings.DefaultLocale;
            if (defaultLocale == null || ValuesProperty == null) return -1;

            for (int i = 0; i < ValuesProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
                if (localeProperty != null && localeProperty.objectReferenceValue == defaultLocale) return i;
            }

            return -1;
        }

        private int CountLocalised()
        {
            int count = 0;
            for (int i = 0; i < ValuesProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
                SerializedProperty valueProperty = elementProperty.FindPropertyRelative(ValuePropertyName);

                if (localeProperty == null || localeProperty.objectReferenceValue == null) continue;
                if (HasMeaningfulValue(valueProperty)) count++;
            }

            return count;
        }

        private bool IsDuplicateLocale(UnityEngine.Object locale)
        {
            if (locale == null) return false;

            using (HashSetPool<LocalizationLocale>.Get(out HashSet<LocalizationLocale> seenLocales))
            {
                for (int i = 0; i < ValuesProperty.arraySize; i++)
                {
                    SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty localeProperty = elementProperty.FindPropertyRelative(LocalePropertyName);
                    if (localeProperty != null && localeProperty.objectReferenceValue == locale)
                    {
                        if (seenLocales.Contains(locale as LocalizationLocale))
                            return true;

                        seenLocales.Add(locale as LocalizationLocale);
                    }
                }
            }

            return false;
        }

        protected static string GetRowTitle(LocalizationLocale locale)
        {
            if (locale == null) return "Unassigned Locale";

            CultureInfo culture = locale.CultureInfo;
            if (culture != null && !Equals(culture, CultureInfo.InvariantCulture) && !string.IsNullOrEmpty(culture.EnglishName))
                return culture.EnglishName;

            if (!string.IsNullOrEmpty(locale.NativeName)) return locale.NativeName;

            return string.IsNullOrEmpty(locale.DisplayCode) ? locale.name : locale.DisplayCode;
        }

        protected static string GetRowSubtitle(LocalizationLocale locale)
        {
            if (locale == null) return string.Empty;

            string code = string.IsNullOrEmpty(locale.DisplayCode) ? locale.name : locale.DisplayCode;
            string nativeName = locale.NativeName;

            if (!string.IsNullOrEmpty(nativeName) && nativeName != GetRowTitle(locale))
                return $"{code}  •  {nativeName}";

            return code;
        }

        private static void ClearValue(SerializedProperty valueProperty)
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

        protected struct Stats
        {
            public int TotalRows;
            public int LocalisedCount;
            public int MissingCount;
        }
    }
}