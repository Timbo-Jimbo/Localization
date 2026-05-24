using System;
using System.Collections.Generic;
using TimboJimboEditor.Localization.Translations;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization
{

    [CustomEditor(typeof(LocalizedString))]
    [CanEditMultipleObjects]
    public sealed class LocalizedStringEditor : LocalizedValueEditor
    {
        private const float TranslatingPulseFrequency = 1.5f;
        private const float TranslatingPulseMinAlpha = 0.35f;

        private TranslationJobs.InspectorState _jobState;

        protected override void OnEnable()
        {
            base.OnEnable();
            TranslationJobs.StateChanged += OnTranslationJobsChanged;
            EditorApplication.update += OnEditorUpdate;
            RefreshJobState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            TranslationJobs.StateChanged -= OnTranslationJobsChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_jobState.HasRelevantJobs)
                Repaint();
        }

        private void OnTranslationJobsChanged()
        {
            RefreshJobState();
            serializedObject?.Update();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                RefreshJobState();
                ExpandRowsForActiveLocales();
            }
            
            base.OnInspectorGUI();
        }

        protected override void DrawAdditionalControls()
        {
            if (_jobState.HasRelevantJobs)
            {
                DrawJobProgressBar();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var stats = GetStats();

                string missingLabel = IsEditingMultipleObjects ? "Translate Missing (Selection)" : "Translate Missing";
                string allLabel = IsEditingMultipleObjects ? "Translate All (Selection)" : "Translate All";

                var buttons = new List<LocalizationEditorGUI.ButtonGroupEntry>();
                if (IsEditingMultipleObjects || stats.MissingCount > 0)
                {
                    buttons.Add(new LocalizationEditorGUI.ButtonGroupEntry(
                        label: missingLabel,
                        onClick: () => OnTranslateButtonClicked(TranslationJobScope.Missing, null)
                    ));
                }

                buttons.Add(new LocalizationEditorGUI.ButtonGroupEntry(
                    label: allLabel,
                    onClick: () => OnTranslateButtonClicked(TranslationJobScope.All, null)
                ));

                LocalizationEditorGUI.DrawButtonGroup(
                    buttons: buttons,
                    populateContextMenu: (menu) => PopulateMenuTranslatorPicker(menu, TranslationJobScope.All, null)
                );
            }
        }

        private void OnTranslateButtonClicked(TranslationJobScope scope, LocalizationLocale singleTargetLocale)
        {
            List<AiTranslator> translators = TranslationJobs.FindAiTranslators();
            if (translators.Count <= 1)
            {
                // Single (or zero) translator — start directly.
                StartTranslationForSelectedTargets(scope, singleTargetLocale);
                return;
            }

            AiTranslator defaultTranslator = AiTranslator.GetDefaultTranslator();
            if (defaultTranslator != null)
            {
                StartTranslationForSelectedTargets(defaultTranslator, scope, singleTargetLocale);
                return;
            }
            
            var menu = new GenericMenu();
            PopulateMenuTranslatorPicker(menu, scope, singleTargetLocale);
            menu.ShowAsContext();
        }

        private void PopulateMenuTranslatorPicker(GenericMenu menu, TranslationJobScope scope, LocalizationLocale singleTargetLocale)
        {
            List<AiTranslator> translators = TranslationJobs.FindAiTranslators();

            if (translators.Count == 0)
            {
                menu.AddItem(new GUIContent("Create AI Translator…"), false, () =>
                {
                    AiTranslator translator = TranslationJobs.CreateAiTranslatorAsset();
                    if (translator != null)
                        StartTranslationForSelectedTargets(translator, scope, singleTargetLocale);
                });
            }
            else if(TranslationJobs.CanStartForSelection(targets, out var disabledReason))
            {
                for (int i = 0; i < translators.Count; i++)
                {
                    AiTranslator translator = translators[i];
                    string label = "Translate With/" + GetTranslatorMenuLabel(translators, i);
                    menu.AddItem(new GUIContent(label), false, () => StartTranslationForSelectedTargets(translator, scope, singleTargetLocale));
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(disabledReason));
            }
        }

        protected override bool ShouldShowLocaleMenuButton(LocalizationLocale locale)
        {
            return locale != LocalizationSettings.DefaultLocale;
        }

        protected override void BuildLocaleMenu(GenericMenu menu, LocalizationLocale locale, SerializedProperty valueProperty)
        {
            string translateLabel = IsEditingMultipleObjects ? "Translate For Selection" : "Translate";
            AddTranslationMenuItem(menu, translateLabel, TranslationJobScope.SingleLocale, locale);
            menu.AddSeparator(string.Empty);
            base.BuildLocaleMenu(menu, locale, valueProperty);
        }

        public override void OnLocaleValueGUI(LocalizationLocale locale, SerializedProperty valueProperty)
        {
            bool hasLocaleState = _jobState.TryGetLocaleState(locale, out TranslationJobs.LocalePresentationState localeState);
            bool isTranslating = hasLocaleState && localeState.IsTranslating;
            Color previousGuiColor = GUI.color;
            if (isTranslating)
            {
                float pulse = TranslatingPulseMinAlpha
                    + (1f - TranslatingPulseMinAlpha)
                    * (0.5f + 0.5f * Mathf.Sin((float)(EditorApplication.timeSinceStartup * Mathf.PI * 2f * TranslatingPulseFrequency)));
                GUI.color = new Color(previousGuiColor.r, previousGuiColor.g, previousGuiColor.b, previousGuiColor.a * pulse);
            }

            float height = EditorGUIUtility.singleLineHeight * (locale == LocalizationSettings.DefaultLocale ? 8f : 4f) + 4f;

            using (new EditorGUI.DisabledScope(isTranslating))
            {
                if (isTranslating && !string.IsNullOrEmpty(localeState.PartialText))
                {
                    LocalizationEditorGUI.DrawFormatTextArea(GetTextAreaControlKey(valueProperty), localeState.PartialText, height);
                }
                else
                {
                    DrawStringTextArea(valueProperty, height);
                }
            }

            GUI.color = previousGuiColor;
        }


        private static void DrawStringTextArea(SerializedProperty property, float minHeight)
        {

            using (new EditorGUI.MixedValueScope(property.hasMultipleDifferentValues))
            {
                EditorGUI.BeginChangeCheck();
                string newValue = LocalizationEditorGUI.DrawFormatTextArea(GetTextAreaControlKey(property), property.stringValue ?? string.Empty, minHeight);
                bool changed = EditorGUI.EndChangeCheck();

                if (changed)
                {
                    property.stringValue = newValue;
                    property.serializedObject.ApplyModifiedProperties();
                    LocalizationSettings.RaiseLocalizedValueChanged(property.serializedObject.targetObject as LocalizedValue);
                }
            }
        }

        private static string GetTextAreaControlKey(SerializedProperty property)
        {
            int targetId = property.serializedObject.targetObject != null
                ? property.serializedObject.targetObject.GetInstanceID()
                : 0;
            return $"LocalizedValueEditor.{targetId}.{property.propertyPath}";
        }

        // ---------------------------------------------------------------------
        // Translation menu helpers
        // ---------------------------------------------------------------------

        private void AddTranslationMenuItem(GenericMenu menu, string label, TranslationJobScope scope, LocalizationLocale singleTargetLocale)
        {
            List<AiTranslator> translators = TranslationJobs.FindAiTranslators();
            if (translators.Count == 0)
            {
                menu.AddItem(new GUIContent(label), false, () => StartTranslationForSelectedTargets(scope, singleTargetLocale));
                return;
            }

            int defaultTranslatorIndex = IndexOfTranslator(translators, AiTranslator.GetDefaultTranslator());
            if (defaultTranslatorIndex >= 0)
            {
                AiTranslator defaultTranslator = translators[defaultTranslatorIndex];
                menu.AddItem(new GUIContent(label), false, () => StartTranslationForSelectedTargets(defaultTranslator, scope, singleTargetLocale));
            }

            AddTranslatorSelectionSubmenu(menu, GetTranslateWithLabel(label), translators, scope, singleTargetLocale);
        }

        private void AddTranslatorSelectionSubmenu(GenericMenu menu, string label, List<AiTranslator> translators, TranslationJobScope scope, LocalizationLocale singleTargetLocale)
        {
            for (int i = 0; i < translators.Count; i++)
            {
                AiTranslator translator = translators[i];
                string translatorLabel = GetTranslatorMenuLabel(translators, i);
                menu.AddItem(new GUIContent($"{label}/{translatorLabel}"), false, () => StartTranslationForSelectedTargets(translator, scope, singleTargetLocale));
            }
        }

        private static string GetTranslateWithLabel(string label)
        {
            const string suffix = " With…";
            return label.EndsWith(" For Selection", StringComparison.Ordinal)
                ? label.Substring(0, label.Length - " For Selection".Length) + suffix + " For Selection"
                : label + suffix;
        }

        private static int IndexOfTranslator(List<AiTranslator> translators, AiTranslator translator)
        {
            if (translator == null) return -1;
            for (int i = 0; i < translators.Count; i++)
                if (translators[i] == translator) return i;
            return -1;
        }


        private void StartTranslationForSelectedTargets(TranslationJobScope scope, LocalizationLocale singleTargetLocale = null)
        {
            List<AiTranslator> translators = TranslationJobs.FindAiTranslators();
            if (translators.Count == 0)
            {
                AiTranslator translator = TranslationJobs.CreateAiTranslatorAsset();
                if (translator != null)
                    StartTranslationForSelectedTargets(translator, scope, singleTargetLocale);
                return;
            }

            if (translators.Count == 1)
            {
                StartTranslationForSelectedTargets(translators[0], scope, singleTargetLocale);
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < translators.Count; i++)
            {
                AiTranslator translator = translators[i];
                string path = AssetDatabase.GetAssetPath(translator);
                string label = string.IsNullOrEmpty(path) ? translator.name : path;
                menu.AddItem(new GUIContent(label), false, () => StartTranslationForSelectedTargets(translator, scope, singleTargetLocale));
            }

            menu.ShowAsContext();
        }

        private void StartTranslationForSelectedTargets(AiTranslator translator, TranslationJobScope scope, LocalizationLocale singleTargetLocale = null)
        {
            if (!TranslationJobs.TryStartJob(translator, serializedObject.targetObjects, scope, singleTargetLocale, out string failureMessage))
            {
                EditorUtility.DisplayDialog("Translate", failureMessage, "OK");
                return;
            }

            RefreshJobState();
            Repaint();
        }

        private static string GetTranslatorMenuLabel(List<AiTranslator> translators, int index)
        {
            AiTranslator translator = translators[index];
            if (translator == null) return "Missing Translator";

            string translatorName = string.IsNullOrWhiteSpace(translator.name) ? "Unnamed Translator" : translator.name;
            string translatorDisplayName = AiTranslator.GetDisplayName(translator);
            if (!HasDuplicateTranslatorName(translators, translatorName, index))
                return EscapeMenuPathSegment(translatorDisplayName);

            string path = AssetDatabase.GetAssetPath(translator);
            if (string.IsNullOrEmpty(path))
                return EscapeMenuPathSegment($"{translatorDisplayName} ({translator.GetInstanceID()})");

            return EscapeMenuPathSegment($"{translatorDisplayName} ({path})");
        }

        private static bool HasDuplicateTranslatorName(List<AiTranslator> translators, string translatorName, int currentIndex)
        {
            for (int i = 0; i < translators.Count; i++)
            {
                if (i == currentIndex || translators[i] == null) continue;
                if (string.Equals(translators[i].name, translatorName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string EscapeMenuPathSegment(string value) => value.Replace('/', '∕');

        private void DrawJobProgressBar()
        {
            float height = EditorGUIUtility.singleLineHeight + 2f;
            Rect rect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            float progress = Mathf.Clamp01(_jobState.Progress);
            string label = $"Translating… {Mathf.RoundToInt(progress * 100f)}%";
            EditorGUI.ProgressBar(rect, progress, label);
        }

        private void RefreshJobState()
        {
            _jobState = TranslationJobs.GetInspectorState(serializedObject != null ? serializedObject.targetObjects : null);
        }

        private void ExpandRowsForActiveLocales()
        {
            if (!_jobState.HasRelevantJobs)
            {
                return;
            }

            for (int i = 0; i < ProjectLocales.Length; i++)
            {
                LocalizationLocale locale = ProjectLocales[i];
                if (locale != null && _jobState.TryGetLocaleState(locale, out TranslationJobs.LocalePresentationState localeState) && localeState.IsTranslating)
                    SetLocaleRowExpanded(locale, true);
            }
        }
    }
}