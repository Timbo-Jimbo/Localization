using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    [CustomEditor(typeof(AiTranslator))]
    public sealed class AiTranslatorEditor : Editor
    {
        private string _apiKeyInput;
        private bool _showGeneralSection = true;
        private bool _showApiKeySection = true;
        private bool _showDebugHistory = true;
        private int _expandedDebugRecordIndex = -1;

        private SerializedProperty _glossariesProperty;
        private SerializedProperty _contextBlocksProperty;
        private SerializedProperty _systemInstructionsProperty;
        private SerializedProperty _apiBaseUrlProperty;
        private SerializedProperty _temperatureProperty;
        private SerializedProperty _modelProperty;
        private SerializedProperty _timeoutSecondsProperty;
        private AiTranslator Translator => (AiTranslator)target;

        private void OnEnable()
        {
            _apiKeyInput = AiTranslator.GetOpenAiApiKey(Translator);
            Translator.TranslationProgressed += OnTranslationProgressed;

            _glossariesProperty = serializedObject.FindProperty("_glossaries");
            _contextBlocksProperty = serializedObject.FindProperty("_contextBlocks");
            _systemInstructionsProperty = serializedObject.FindProperty("_systemInstructions");
            _apiBaseUrlProperty = serializedObject.FindProperty("_apiBaseUrl");
            _temperatureProperty = serializedObject.FindProperty("_temperature");
            _modelProperty = serializedObject.FindProperty("_model");
            _timeoutSecondsProperty = serializedObject.FindProperty("_timeoutSeconds");
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

            DrawGeneralSection();
            DrawApiConfigSection();
            DrawDebugHistorySection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGeneralSection()
        {
            LocalizationEditorGUI.DrawFoldout(
                ref _showGeneralSection,
                () => {
                    GUILayout.Label("General", LocalizationEditorGUI.RowTitleStyle);
                    GUILayout.FlexibleSpace();
                    var isDefaultTranslator = target is AiTranslator translator && translator == AiTranslator.GetDefaultTranslator();

                    if (isDefaultTranslator)
                        GUILayout.Label("Default Translator", LocalizationEditorGUI.DefaultTagStyle);
                    
                    if(LocalizationEditorGUI.KebabMenuButton())
                    {
                        var menu = new GenericMenu();
                        if(isDefaultTranslator)
                        {
                            menu.AddDisabledItem(new GUIContent("Set as Default Translator"));
                        }
                        else
                        {
                            menu.AddItem(new GUIContent("Set as Default Translator"), false, () => AiTranslator.SetDefaultTranslator(Translator));
                        }
                        menu.ShowAsContext();
                    }
                },
                onToggle: toggle => _showGeneralSection = toggle);

            if (!_showGeneralSection)
                return;

            EditorGUILayout.PropertyField(_systemInstructionsProperty);
            EditorGUILayout.PropertyField(_glossariesProperty, true);
            EditorGUILayout.PropertyField(_contextBlocksProperty, true);

            GUILayout.Space(4f);
        }

        private void DrawApiConfigSection()
        {
            LocalizationEditorGUI.DrawFoldout(
                ref _showApiKeySection,
                () => {
                    var isMissingApiKey = string.IsNullOrEmpty(_apiKeyInput);
                    var warningIcon = EditorGUIUtility.IconContent("warning").image;
                    var titleContent = new GUIContent("API Configuration", "Configure the API key and other settings for this translator. An API key is required to use this translator.");
                    var subtitleContent = new GUIContent(isMissingApiKey ? "API Key missing" : "API Key configured");
                    
                    if (isMissingApiKey)
                        titleContent.image = warningIcon;

                    GUILayout.Label(titleContent, LocalizationEditorGUI.RowTitleStyle, GUILayout.ExpandWidth(false));
                    GUILayout.Space(6f);
                    GUILayout.Label(subtitleContent, LocalizationEditorGUI.RowSubtitleStyle, GUILayout.ExpandWidth(false));

                    GUILayout.FlexibleSpace();
                },
                onToggle: toggle => _showApiKeySection = toggle);

            if (!_showApiKeySection)
                return;

            //api key:

            EditorGUILayout.Space(4f);

            EditorGUILayout.PropertyField(_apiBaseUrlProperty);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Api Key", EditorStyles.label);
                
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.SetNextControlName("ApiKeyInputField");
                        var result = EditorGUILayout.TextField(_apiKeyInput);
                        var textFieldIsFocused = GUI.GetNameOfFocusedControl() == "ApiKeyInputField";
                        var lastRect = GUILayoutUtility.GetLastRect();
                        if (Event.current.type == EventType.Repaint && !textFieldIsFocused)
                        {
                            EditorStyles.textField.Draw(lastRect, new GUIContent(new string('*', Mathf.Min(_apiKeyInput.Length, 60))), false, false, false, false);
                        }

                        if(result != _apiKeyInput && textFieldIsFocused)
                        {
                            _apiKeyInput = result;
                            AiTranslator.SetOpenAiApiKey(Translator, _apiKeyInput);
                        }

                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_apiKeyInput)))
                        {
                            if (GUILayout.Button("Clear"))
                            {
                                _apiKeyInput = string.Empty;
                                AiTranslator.SetOpenAiApiKey(Translator, _apiKeyInput);
                            }
                        }
                    }

                    EditorGUILayout.HelpBox("Your API key is stored in EditorPrefs on a per-machine basis and is not saved in this asset.", MessageType.Info);
                }
            }

            EditorGUILayout.PropertyField(_modelProperty);
            EditorGUILayout.PropertyField(_temperatureProperty);
            EditorGUILayout.PropertyField(_timeoutSecondsProperty);

            GUILayout.Space(4f);
        }

        private void DrawDebugHistorySection()
        {
            AiTranslator translator = Translator;

            LocalizationEditorGUI.DrawFoldout(
                ref _showDebugHistory,
                () => {
                    GUILayout.Label("Request / Response History", LocalizationEditorGUI.HeaderStyle);
                    GUILayout.FlexibleSpace();

                    if (LocalizationEditorGUI.KebabMenuButton())
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Clear History"), false, () =>
                        {
                            translator.ClearDebugHistory();
                            _expandedDebugRecordIndex = -1;
                            Repaint();
                        });
                        menu.ShowAsContext();
                    }
                },
                onToggle: toggle => _showDebugHistory = toggle);

            if (!_showDebugHistory)
                return;

            EditorGUILayout.LabelField(
                "This history is for debugging purposes and is not saved in this asset.",
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
