using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    [CustomEditor(typeof(AiTranslator))]
    public sealed class AiTranslatorEditor : Editor
    {
        private string _apiKeyInput;
        private bool _showDebugHistory = true;
        private int _expandedDebugRecordIndex = -1;

        private AiTranslator Translator => (AiTranslator)target;

        private void OnEnable()
        {
            _apiKeyInput = AiTranslator.GetOpenAiApiKey(Translator);
            Translator.TranslationProgressed += OnTranslationProgressed;
        }

        private void OnDisable()
        {
            Translator.TranslationProgressed -= OnTranslationProgressed;
        }

        private void OnTranslationProgressed(TranslationProgress _)
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script");

            EditorGUILayout.Space(10f);
            LocalizationEditorGUI.DrawSeparator();
            DrawDefaultTranslatorSection();

            EditorGUILayout.Space(10f);
            LocalizationEditorGUI.DrawSeparator();
            DrawApiKeySection();

            EditorGUILayout.Space(10f);
            LocalizationEditorGUI.DrawSeparator();
            DrawDebugHistorySection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultTranslatorSection()
        {
            AiTranslator translator = Translator;
            AiTranslator defaultTranslator = AiTranslator.GetDefaultTranslator();

            GUILayout.Label("Default Translator", LocalizationEditorGUI.HeaderStyle);
            EditorGUILayout.LabelField(
                "Used by the plain Translate action when multiple translators exist.",
                LocalizationEditorGUI.RowSubtitleStyle);

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(AiTranslator.IsDefaultTranslator(translator)))
                {
                    if (GUILayout.Button("Use This Translator As Default"))
                    {
                        AiTranslator.SetDefaultTranslator(translator);
                        Repaint();
                    }
                }

                using (new EditorGUI.DisabledScope(defaultTranslator == null))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(64f)))
                    {
                        AiTranslator.ClearDefaultTranslator();
                        Repaint();
                    }
                }
            }
        }

        private void DrawApiKeySection()
        {
            GUILayout.Label("OpenAI API Key", LocalizationEditorGUI.HeaderStyle);
            EditorGUILayout.LabelField(
                "Stored in EditorPrefs only. It is not saved into this translator asset.",
                LocalizationEditorGUI.RowSubtitleStyle);

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                _apiKeyInput = EditorGUILayout.PasswordField("API Key", _apiKeyInput ?? string.Empty);
                if (GUILayout.Button("Save", GUILayout.Width(56f)))
                {
                    SaveApiKey();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!AiTranslator.HasOpenAiApiKey(Translator) && string.IsNullOrEmpty(_apiKeyInput)))
                {
                    if (GUILayout.Button("Clear API Key", GUILayout.Width(120f)))
                    {
                        ClearApiKey();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("Translation progress is exposed via TranslationProgressed, and LocalizedString translation requests are surfaced through the editor job system and Unity background tasks.", MessageType.Info);
        }

        private void SaveApiKey()
        {
            AiTranslator.SetOpenAiApiKey(Translator, _apiKeyInput);
            _apiKeyInput = AiTranslator.GetOpenAiApiKey(Translator);
            GUI.FocusControl(null);
        }

        private void ClearApiKey()
        {
            AiTranslator.ClearOpenAiApiKey(Translator);
            _apiKeyInput = string.Empty;
            GUI.FocusControl(null);
        }

        private void DrawDebugHistorySection()
        {
            AiTranslator translator = Translator;

            using (new EditorGUILayout.HorizontalScope())
            {
                _showDebugHistory = EditorGUILayout.Foldout(_showDebugHistory, "Request / Response Debug History", true);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(translator.DebugHistory.Count == 0))
                {
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(56f)))
                    {
                        translator.ClearDebugHistory();
                        _expandedDebugRecordIndex = -1;
                        Repaint();
                    }
                }
            }

            if (!_showDebugHistory)
            {
                return;
            }

            EditorGUILayout.LabelField(
                "In-memory only. This debug history is not serialized into the AiTranslator asset.",
                LocalizationEditorGUI.RowSubtitleStyle);

            if (translator.DebugHistory.Count == 0)
            {
                EditorGUILayout.HelpBox("No translation requests have been recorded this editor session.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            for (int i = 0; i < translator.DebugHistory.Count; i++)
            {
                DrawDebugRecord(i, translator.DebugHistory[i]);
            }
        }

        private void DrawDebugRecord(int index, AiTranslator.TranslationDebugRecord record)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string title = $"{record.StartedAt:HH:mm:ss}  •  {record.Status}  •  {record.Model}  •  {record.SourceLocaleCode} → {record.TargetLocaleCodes}";
                bool expanded = _expandedDebugRecordIndex == index;
                bool nextExpanded = EditorGUILayout.Foldout(expanded, title, true);

                if (!nextExpanded)
                {
                    if (expanded)
                    {
                        _expandedDebugRecordIndex = -1;
                    }

                    return;
                }

                _expandedDebugRecordIndex = index;
                DrawDebugRecordDetails(record);
            }
        }

        private static void DrawDebugRecordDetails(AiTranslator.TranslationDebugRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.Error))
            {
                EditorGUILayout.HelpBox(record.Error, MessageType.Error);
            }

            DrawTextBlock("System Prompt", record.SystemPrompt);
            DrawTextBlock("User Prompt", record.UserPrompt);
            DrawTextBlock("Request JSON", record.RequestJson);
            DrawTextBlock("Raw Response Stream", record.RawResponse);
            DrawTextBlock("Parsed Response", record.ParsedResponse);
        }

        private static void DrawTextBlock(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EditorGUILayout.Space(4f);
            GUILayout.Label(label, LocalizationEditorGUI.RowTitleStyle);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(value, GUILayout.MinHeight(72f));
            }
        }
    }
}
