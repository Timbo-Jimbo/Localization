using System.Collections.Generic;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    [CustomEditor(typeof(TranslationGlossary))]
    public sealed class TranslationGlossaryEditor : UnityEditor.Editor
    {
        private const string TermsPropertyName = "_terms";
        private const string FoldoutStateKeyPrefix = "TranslationGlossaryEditor.Foldout.";


        private SerializedProperty _termsProperty;

        // Cached sub-editors per term asset so the embedded translations panel keeps its state.
        private readonly Dictionary<Object, LocalizedValueEditor> _termEditors = new();

        private void OnEnable()
        {
            _termsProperty = serializedObject.FindProperty(TermsPropertyName);
        }

        private void OnDisable()
        {
            foreach (var editor in _termEditors.Values)
            {
                if (editor != null)
                {
                    DestroyImmediate(editor);
                }
            }
            _termEditors.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_termsProperty == null)
            {
                EditorGUILayout.HelpBox($"Could not find serialized property '{TermsPropertyName}'.", MessageType.Error);
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawGlossaryHeader();
            DrawEntries();
            DrawAddEntryButton();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGlossaryHeader()
        {
            int totalEntries = _termsProperty.arraySize;
            int missingCount = CountMissingTerms();
            int validCount = Mathf.Max(0, totalEntries - missingCount);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Glossary Entries", LocalizationEditorGUI.HeaderStyle);
                GUILayout.FlexibleSpace();

                string statText = missingCount == 0
                    ? $"{totalEntries} {(totalEntries == 1 ? "entry" : "entries")}"
                    : $"{validCount} / {totalEntries} assigned  •  {missingCount} missing";
                GUILayout.Label(statText, LocalizationEditorGUI.StatLabelStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));

            }

            if (totalEntries == 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "This glossary is empty. Click \"Add Entry\" below to add a term.",
                    MessageType.Info);
            }
            
            EditorGUILayout.Space(4f);
        }

        private void DrawEntries()
        {
            for (int i = 0; i < _termsProperty.arraySize; i++)
            {
                DrawEntry(i);
            }
        }

        private void DrawEntry(int index)
        {
            SerializedProperty termEntryProperty = _termsProperty.GetArrayElementAtIndex(index);

            LocalizedString termAsset = termEntryProperty != null ? termEntryProperty.objectReferenceValue as LocalizedString : null;

            bool expanded = GetEntryExpanded(index);
            bool removeClicked = false;
            LocalizationEditorGUI.DrawFoldout(
                ref expanded,
                () => DrawEntryHeaderContent(termEntryProperty, ref removeClicked),
                onToggle: toggle => SetEntryExpanded(index, toggle),
                onGroupToggle: toggle =>
                {
                    for (int i = 0; i < _termsProperty.arraySize; i++)
                        SetEntryExpanded(i, toggle);
                }
            );

            if (removeClicked)
            {
                _termsProperty.DeleteArrayElementAtIndex(index);
                return;
            }

            if (!expanded)
                return;

            EditorGUILayout.Space(4f);

            if (termAsset != null)
                DrawDefaultValueField(termAsset);

            EditorGUILayout.Space(2f);
        }

        private void DrawDefaultValueField(LocalizedString termAsset)
        {
            string description = termAsset != null ? termAsset.Description : null;
            bool hasDescription = !string.IsNullOrEmpty(description);

            var defaultLocale = LocalizationSettings.DefaultLocale;
            SerializedProperty defaultValueProperty = null;
            LocalizedValueEditor termEditor = null;

            if (defaultLocale != null && termAsset != null)
            {
                termEditor = GetOrCreateTermEditor(termAsset);
                if (termEditor != null)
                {
                    termEditor.serializedObject.Update();
                    defaultValueProperty = FindDefaultLocaleValueProperty(termEditor.serializedObject, defaultLocale);
                }
            }

            bool hasDefault = defaultValueProperty != null;
            if (!hasDefault && !hasDescription)
            {
                return;
            }

            if (hasDefault)
            {
                GUILayout.Label($"{defaultLocale.NativeName} Value", EditorStyles.label);
                EditorGUILayout.Space(2f);
                using (new EditorGUI.DisabledScope(true))
                {
                    termEditor.OnLocaleValueGUI(defaultLocale, defaultValueProperty);
                }
            }

            if (hasDescription)
            {
                GUILayout.Label("Description", EditorStyles.label);
                EditorGUILayout.Space(2f);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextArea(description, GUILayout.MinHeight(36f));
                }
            }

            EditorGUILayout.Space(4f);
        }

        private LocalizedValueEditor GetOrCreateTermEditor(LocalizedString termAsset)
        {
            if (termAsset == null) return null;

            if (_termEditors.TryGetValue(termAsset, out LocalizedValueEditor cached) && cached != null)
                return cached;

            var created = CreateEditor(termAsset) as LocalizedValueEditor;
            _termEditors[termAsset] = created;
            return created;
        }

        private static SerializedProperty FindDefaultLocaleValueProperty(SerializedObject termObject, LocalizationLocale defaultLocale)
        {
            if (termObject == null || defaultLocale == null) return null;

            SerializedProperty valuesProperty = termObject.FindProperty("Values");
            if (valuesProperty == null) return null;

            for (int i = 0; i < valuesProperty.arraySize; i++)
            {
                SerializedProperty element = valuesProperty.GetArrayElementAtIndex(i);
                SerializedProperty localeProperty = element.FindPropertyRelative("Locale");
                if (localeProperty != null && localeProperty.objectReferenceValue == defaultLocale)
                {
                    return element.FindPropertyRelative("Value");
                }
            }

            return null;
        }

        private static void DrawEntryHeaderContent(SerializedProperty termEntryProperty, ref bool removeClicked)
        {
            EditorGUILayout.PropertyField(termEntryProperty, new GUIContent(GetEntryTitle(termEntryProperty.objectReferenceValue as LocalizedString)));
            if (LocalizationEditorGUI.DrawRemoveButton())
                removeClicked = true;
        }

        private void DrawAddEntryButton()
        {
            if (GUILayout.Button("+ Add Entry", GUILayout.Height(24f)))
            {
                int newIndex = _termsProperty.arraySize;
                _termsProperty.InsertArrayElementAtIndex(newIndex);
                SetEntryExpanded(newIndex, true);
            }
        }

        private int CountMissingTerms()
        {
            int count = 0;
            for (int i = 0; i < _termsProperty.arraySize; i++)
            {
                SerializedProperty termProperty = _termsProperty.GetArrayElementAtIndex(i);
                if (termProperty == null || termProperty.objectReferenceValue == null)
                {
                    count++;
                }
            }
            return count;
        }

        private static string GetEntryTitle(LocalizedString termAsset)
        {
            if (termAsset == null)
                return "Unassigned Term";
            
            return ObjectNames.NicifyVariableName(termAsset.name);
        }

        private bool GetEntryExpanded(int index)
        {
            return SessionState.GetBool(GetFoldoutKey(index), false);
        }

        private void SetEntryExpanded(int index, bool expanded)
        {
            SessionState.SetBool(GetFoldoutKey(index), expanded);
        }

        private string GetFoldoutKey(int index)
        {
            Object firstTarget = serializedObject.targetObject;
            string assetPath = firstTarget != null ? AssetDatabase.GetAssetPath(firstTarget) : null;
            string guid = string.IsNullOrEmpty(assetPath) ? (firstTarget != null ? firstTarget.GetInstanceID().ToString() : "none") : AssetDatabase.AssetPathToGUID(assetPath);
            return FoldoutStateKeyPrefix + guid + "." + index;
        }
    }
}