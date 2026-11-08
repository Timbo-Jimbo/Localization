using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TimboJimboEditor.Core;
using UnityEditor;
using UnityEngine;
using TimboJimbo.Localization;
using TimboJimboEditor.Localization.Utility;
using System;

namespace TimboJimboEditor.Localization
{
    /// <summary>
    /// Base inspector for <see cref="LocalizedValue"/> types. Rows are the asset's entries, keyed by locale code; each
    /// row shows the locale the settings register for its code, or a warning when the settings don't list the code.
    /// </summary>
    public abstract class LocalizedValueEditor : Editor
    {
        protected const string ValuesPropertyName = LocalizedValueEditorUtility.ValuesPropertyName;
        protected const string DescriptionPropertyName = "Description";
        protected const string CodePropertyName = LocalizedValueEditorUtility.CodePropertyName;
        protected const string ValuePropertyName = LocalizedValueEditorUtility.ValuePropertyName;

        private const float TagWidth = 70f;

        private const string FoldoutStateKeyPrefix = "LocalizedValueEditor.Foldout.";

        protected SerializedProperty ValuesProperty { get; private set; }
        protected SerializedProperty DescriptionProperty { get; private set; }
        protected bool IsEditingMultipleObjects => serializedObject.targetObjects.Length > 1;

        protected virtual void OnEnable()
        {
            // Opening an asset is one of the editor's moments to bring its entries in step with the settings' locales.
            var synced = false;
            foreach (var target in targets)
            {
                if (target is LocalizedValue localizedValue)
                    synced |= LocalizedValueEditorUtility.SyncWithProjectLocales(localizedValue);
            }

            if (synced) serializedObject.Update();

            ValuesProperty = serializedObject.FindProperty(ValuesPropertyName);
            DescriptionProperty = serializedObject.FindProperty(DescriptionPropertyName);
        }

        protected virtual void OnDisable()
        {
        }

        protected Stats GetStats()
        {
            Stats stats = default;
            if (target is not LocalizedValue localizedValue) return stats;

            stats.TotalRows = LocalizationSettings.Locales.Count;
            stats.LocalisedCount = LocalizationSettings.Locales.Count(locale => locale != null && localizedValue.HasMeaningfulValue(locale.Code));
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
            var defaultLocale = LocalizationSettings.DefaultLocale;
            int defaultIndex = defaultLocale != null ? LocalizedValueEditorUtility.IndexOfCode(ValuesProperty, defaultLocale.Code) : -1;
            if (defaultIndex < 0)
            {
                EditorGUILayout.HelpBox(
                    "No default locale is configured. Set one on the LocalizationSettings asset.",
                    MessageType.Warning);
                return;
            }

            SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(defaultIndex);
            SerializedProperty valueProperty = elementProperty.FindPropertyRelative(ValuePropertyName);

            DrawRowHeaderTitleArea(defaultLocale, valueProperty);
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
            var defaultCode = LocalizationSettings.DefaultCode;

            for (int i = 0; i < ValuesProperty.arraySize; i++)
            {
                var code = ReadCode(i);
                if (!defaultCode.IsNone && code == defaultCode) continue;

                DrawRow(i);
            }
        }

        private void DrawRow(int index)
        {
            SerializedProperty elementProperty = ValuesProperty.GetArrayElementAtIndex(index);
            SerializedProperty valueProperty = elementProperty.FindPropertyRelative(ValuePropertyName);
            LocaleCode code = ReadCode(index);
            LocalizationLocale locale = LocalizationSettings.Find(code);

            bool expanded = GetLocaleRowExpanded(code);
            FoldoutGUI.Draw(
                ref expanded,
                () => DrawRowHeaderContent(code, locale, valueProperty),
                onToggle: toggle => SetLocaleRowExpanded(code, toggle),
                onGroupToggle: toggle =>
                {
                    foreach (var l in LocalizationSettings.Locales)
                        SetLocaleRowExpanded(LocaleCode.Of(l), toggle);
                }
            );

            if (!expanded) return;

            EditorGUILayout.Space(4f);
            OnLocaleValueGUI(locale, valueProperty);

            if (IsDuplicateCode(code))
                EditorGUILayout.HelpBox($"Duplicate Locale: There are two or more entries that define a value for '{code}'.", MessageType.Warning);

            EditorGUILayout.Space(2f);
        }

        private void DrawRowHeaderContent(LocaleCode code, LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (locale == null)
            {
                DrawUnknownCodeRowHeader(code);
                return;
            }

            DrawRowHeaderTitleArea(locale, valueProperty);
            GUILayout.FlexibleSpace();
            DrawRowHeaderMenuArea(locale, valueProperty);
        }

        private void DrawUnknownCodeRowHeader(LocaleCode code)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var warningIcon = EditorGUIUtility.IconContent("warning").image;
                string title = code.IsNone ? "Entry Without A Locale" : $"Unknown Locale '{code}'";
                GUILayout.Label(new GUIContent(title, warningIcon, "The LocalizationSettings asset doesn't list this locale code."), LocalizationEditorGUI.RowTitleStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                GUILayout.FlexibleSpace();
                if (LocalizationEditorGUI.KebabMenuButton())
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Remove Entries For Unknown Locales"), false, () =>
                    {
                        var response = EditorUtility.DisplayDialogComplex(
                            "Remove Entries For Unknown Locales",
                            "Remove the entries whose locale code the LocalizationSettings asset doesn't list from all other localized assets in the project as well?",
                            ok: "Yes, remove from all assets",
                            cancel: "Cancel",
                            alt: "Just this asset"
                        );

                        const int Ok = 0;
                        const int Cancel = 1;
                        const int Alt = 2;

                        if (response == Cancel) return;

                        var removeTargets = response switch
                        {
                            Ok => LocalizedValueEditorUtility.FindAllLocalizedValuesInProject(),
                            Alt => targets.OfType<LocalizedValue>().ToList(),
                            _ => throw new ArgumentOutOfRangeException(nameof(response))
                        };

                        foreach (var localizedValue in removeTargets)
                        {
                            Undo.RecordObject(localizedValue, "Remove Entries For Unknown Locales");
                            LocalizedValueEditorUtility.SyncWithProjectLocales(localizedValue, removeUnknownCodes: true);
                        }

                        serializedObject.Update();
                    });
                    menu.ShowAsContext();
                }
            }
        }

        protected virtual void DrawRowHeaderTitleArea(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            var title = GetRowTitle(locale);
            var subtitle = GetRowSubtitle(locale);

            using (new EditorGUILayout.HorizontalScope())
            {
                FoldoutGUI.Title(title, subtitle);
                GUILayout.FlexibleSpace();
            }
        }

        protected virtual void DrawRowHeaderMenuArea(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (!valueProperty.hasMultipleDifferentValues && target is LocalizedValue localizedValue && !localizedValue.HasMeaningfulValue(locale.Code))
                GUILayout.Label("Missing", LocalizationEditorGUI.MissingTagStyle, GUILayout.Width(TagWidth));
            else
                GUILayout.Space(TagWidth);

            DrawRowHeaderMenuButton(locale, valueProperty);
        }

        protected void DrawRowHeaderMenuButton(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (LocalizationEditorGUI.KebabMenuButton())
            {
                GenericMenu menu = new();
                BuildRowHeaderMenu(menu, locale, valueProperty);
                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Populate the per-locale context menu. Base implementation adds a "Clear" item.
        /// Override and call <c>base.BuildRowHeaderMenu</c> to insert items around the default.
        /// Returning without adding any items will hide the kebab button entirely.
        /// </summary>
        protected virtual void BuildRowHeaderMenu(GenericMenu menu, LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (locale == null) return;
            string clearLabel = IsEditingMultipleObjects ? "Clear For Selection" : "Clear";
            menu.AddItem(new GUIContent(clearLabel), false, () => ClearLocaleForSelectedTargets(locale.Code));
        }

        /// <summary>
        /// Draws the value field for one locale row. <paramref name="locale"/> is null for an entry whose code the
        /// settings don't list.
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

        public virtual bool TryFindAndSeedDefaultValue(GameObject context, LocalizationLocale locale, SerializedProperty targetToSeed)
        {
            return false;
        }

        // ---------------------------------------------------------------------
        // Helpers exposed to derived editors
        // ---------------------------------------------------------------------

        /// <summary>
        /// Clears the value for the given locale code across all selected targets. No-op for the
        /// default locale. Exposed so derived editors can offer the same behavior from custom
        /// controls.
        /// </summary>
        protected void ClearLocaleForSelectedTargets(LocaleCode code)
        {
            if (code.IsNone || IsDefaultCode(code)) return;

            UnityEngine.Object[] selectedTargets = serializedObject.targetObjects;
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is not LocalizedValue localizedValue) continue;

                Undo.RecordObject(localizedValue, $"Clear {code}");

                using SerializedObject targetObject = new(localizedValue);
                SerializedProperty valuesProperty = targetObject.FindProperty(ValuesPropertyName);
                if (valuesProperty == null) continue;

                LocalizedValueEditorUtility.ClearValue(LocalizedValueEditorUtility.FindValueProperty(valuesProperty, code));

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
            var defaultCode = LocalizationSettings.DefaultCode;
            if (defaultCode.IsNone) return;

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
                    var code = LocalizedValueEditorUtility.ReadCode(pair.FindPropertyRelative(CodePropertyName));
                    if (code.IsNone || code == defaultCode)
                        continue;

                    LocalizedValueEditorUtility.ClearValue(pair.FindPropertyRelative(ValuePropertyName));
                }

                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(localizedValue);
                LocalizationSettings.RaiseLocalizedValueChanged(localizedValue);
            }

            serializedObject.Update();
            Repaint();
        }

        // ---------------------------------------------------------------------
        // Internal scaffolding (foldouts, ordering)
        // ---------------------------------------------------------------------

        protected LocaleCode ReadCode(int index)
        {
            return LocalizedValueEditorUtility.ReadCode(ValuesProperty.GetArrayElementAtIndex(index).FindPropertyRelative(CodePropertyName));
        }

        protected static bool GetLocaleRowExpanded(LocaleCode code)
        {
            return SessionState.GetBool(GetFoldoutKey(code), true);
        }

        /// <summary>
        /// Forces the foldout row for the given locale code to be expanded or collapsed. Safe to
        /// call from derived editors (e.g. to reveal a row that is about to receive a
        /// translation).
        /// </summary>
        protected static void SetLocaleRowExpanded(LocaleCode code, bool expanded)
        {
            SessionState.SetBool(GetFoldoutKey(code), expanded);
        }

        private static string GetFoldoutKey(LocaleCode code)
        {
            string suffix = code.IsNone ? "<unassigned>" : code.ToString();
            return FoldoutStateKeyPrefix + "locale." + suffix;
        }

        protected static bool IsDefaultCode(LocaleCode code)
        {
            var defaultCode = LocalizationSettings.DefaultCode;
            return !defaultCode.IsNone && code == defaultCode;
        }

        private bool IsDuplicateCode(LocaleCode code)
        {
            if (code.IsNone) return false;

            int count = 0;
            for (int i = 0; i < ValuesProperty.arraySize; i++)
            {
                if (ReadCode(i) == code && ++count > 1)
                    return true;
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

        protected struct Stats
        {
            public int TotalRows;
            public int LocalisedCount;
            public int MissingCount;
        }
    }
}
