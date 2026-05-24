using System;
using System.Collections.Generic;
using System.Threading;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    public enum TranslationJobScope
    {
        Missing,
        All,
        SingleLocale,
    }

    [InitializeOnLoad]
    public static class TranslationJobs
    {
        public readonly struct LocalePresentationState
        {
            public LocalePresentationState(bool isTranslating, string partialText)
            {
                IsTranslating = isTranslating;
                PartialText = partialText;
            }

            public bool IsTranslating { get; }
            public string PartialText { get; }
        }

        public readonly struct InspectorState
        {
            private readonly Dictionary<LocalizationLocale, LocalePresentationState> _localeStates;

            public InspectorState(
                bool hasRelevantJobs,
                int relevantJobCount,
                float progress,
                string summary,
                Dictionary<LocalizationLocale, LocalePresentationState> localeStates)
            {
                HasRelevantJobs = hasRelevantJobs;
                RelevantJobCount = relevantJobCount;
                Progress = progress;
                Summary = summary;
                _localeStates = localeStates;
            }

            public bool HasRelevantJobs { get; }
            public bool HasBlockingJobs => HasRelevantJobs;
            public int RelevantJobCount { get; }
            public float Progress { get; }
            public string Summary { get; }

            public bool TryGetLocaleState(LocalizationLocale locale, out LocalePresentationState state)
            {
                if (locale != null && _localeStates != null)
                {
                    return _localeStates.TryGetValue(locale, out state);
                }

                state = default;
                return false;
            }
        }

        public static event Action StateChanged;

        private static readonly List<TranslationJob> _activeJobs = new();

        static TranslationJobs()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelAllJobs;
        }

        public static bool CanStartForSelection(UnityEngine.Object[] selectedTargets, out string disabledReason)
        {
            disabledReason = null;

            if (LocalizationSettings.DefaultLocale == null)
            {
                disabledReason = "No default locale is configured.";
                return false;
            }

            if (!HasSelectedLocalizedStrings(selectedTargets))
            {
                disabledReason = "No LocalizedString assets are selected.";
                return false;
            }

            if (SelectionContainsBusyTarget(selectedTargets))
            {
                disabledReason = "A translation job is already running.";
                return false;
            }

            return true;
        }

        public static bool TryStartJob(
            AiTranslator translator,
            UnityEngine.Object[] selectedTargets,
            TranslationJobScope scope,
            LocalizationLocale singleTargetLocale,
            out string failureMessage)
        {
            failureMessage = null;
            try
            {
                if (translator == null)
                {
                    failureMessage = "No AiTranslator asset was selected.";
                    return false;
                }

                if (!CanStartForSelection(selectedTargets, out failureMessage))
                {
                    return false;
                }

                LocalizationLocale defaultLocale = LocalizationSettings.DefaultLocale;
                if (defaultLocale == null)
                {
                    failureMessage = "No default locale is configured.";
                    return false;
                }

                List<TranslationQueueItem> translationQueue = BuildTranslationQueue(selectedTargets, defaultLocale, scope, singleTargetLocale);
                int totalWorkUnits = CountTranslationWorkUnits(translationQueue);
                if (translationQueue.Count == 0 || totalWorkUnits == 0)
                {
                    failureMessage = "No selected assets needed translation.";
                    return false;
                }

                if (QueueContainsBusyTarget(translationQueue))
                {
                    failureMessage = "A translation job is already running for one or more selected assets.";
                    return false;
                }

                var job = new TranslationJob(translator, defaultLocale, translationQueue, totalWorkUnits);
                _activeJobs.Add(job);
                RaiseStateChanged();
                _ = RunJob(job);
                return true;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(failureMessage))
                {
                    EditorUtility.DisplayDialog("Translate", failureMessage, "OK");
                }
            }
        }

        public static InspectorState GetInspectorState(UnityEngine.Object[] selectedTargets)
        {
            if (_activeJobs.Count == 0 || selectedTargets == null || selectedTargets.Length == 0)
            {
                return default;
            }

            var selectedIds = new HashSet<int>();
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is LocalizedString localizedString && localizedString != null)
                {
                    selectedIds.Add(localizedString.GetInstanceID());
                }
            }

            if (selectedIds.Count == 0)
            {
                return default;
            }

            var localeStates = new Dictionary<LocalizationLocale, LocalePresentationState>();
            int relevantJobCount = 0;
            float progressSum = 0f;
            string summary = null;

            for (int jobIndex = 0; jobIndex < _activeJobs.Count; jobIndex++)
            {
                TranslationJob job = _activeJobs[jobIndex];
                if (!job.TouchesAny(selectedIds))
                {
                    continue;
                }

                relevantJobCount++;
                progressSum += job.NormalizedProgress;
                if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrWhiteSpace(job.StatusMessage))
                {
                    summary = job.StatusMessage;
                }

                foreach (KeyValuePair<int, TargetTranslationState> targetEntry in job.TargetStatesById)
                {
                    if (!selectedIds.Contains(targetEntry.Key))
                    {
                        continue;
                    }

                    foreach (KeyValuePair<LocalizationLocale, LocaleTranslationState> localeEntry in targetEntry.Value.LocaleStates)
                    {
                        LocalizationLocale locale = localeEntry.Key;
                        LocaleTranslationState localeState = localeEntry.Value;
                        if (locale == null || (!localeState.IsTranslating && string.IsNullOrEmpty(localeState.PartialText)))
                        {
                            continue;
                        }

                        string partialText = localeState.IsTranslating ? localeState.PartialText : null;
                        if (localeStates.TryGetValue(locale, out LocalePresentationState existingState))
                        {
                            localeStates[locale] = new LocalePresentationState(
                                existingState.IsTranslating || localeState.IsTranslating,
                                string.IsNullOrEmpty(existingState.PartialText) ? partialText : existingState.PartialText);
                        }
                        else
                        {
                            localeStates.Add(locale, new LocalePresentationState(localeState.IsTranslating, partialText));
                        }
                    }
                }
            }

            if (relevantJobCount == 0)
            {
                return default;
            }

            string finalSummary = relevantJobCount == 1
                ? (string.IsNullOrWhiteSpace(summary)
                    ? "A translation job is running for the current selection."
                    : summary)
                : $"{relevantJobCount} translation jobs are running for the current selection.";

            return new InspectorState(
                hasRelevantJobs: true,
                relevantJobCount: relevantJobCount,
                progress: progressSum / relevantJobCount,
                summary: finalSummary,
                localeStates: localeStates);
        }

        public static List<AiTranslator> FindAiTranslators()
        {
            var translators = new List<AiTranslator>();
            string[] guids = AssetDatabase.FindAssets("t:AiTranslator", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AiTranslator translator = AssetDatabase.LoadAssetAtPath<AiTranslator>(path);
                if (translator != null)
                {
                    translators.Add(translator);
                }
            }

            return translators;
        }

        public static AiTranslator CreateAiTranslatorAsset()
        {
            if (!EditorUtility.DisplayDialog(
                "Create AI Translator",
                "No AiTranslator asset was found. Create one now? You can configure model, glossaries, context, and API key on the new asset.",
                "Create",
                "Cancel"))
            {
                return null;
            }

            const string defaultFolder = "Assets/Localization";
            if (!AssetDatabase.IsValidFolder(defaultFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Localization");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{defaultFolder}/AI Translator.asset");
            AiTranslator translator = ScriptableObject.CreateInstance<AiTranslator>();
            AssetDatabase.CreateAsset(translator, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = translator;
            return translator;
        }

        private static async Awaitable RunJob(TranslationJob job)
        {
            try
            {
                await job.RunAsync();
            }
            finally
            {
                _activeJobs.Remove(job);
                RaiseStateChanged();
            }
        }

        private static void CancelAllJobs()
        {
            if (_activeJobs.Count == 0)
            {
                return;
            }

            TranslationJob[] jobs = _activeJobs.ToArray();
            for (int i = 0; i < jobs.Length; i++)
            {
                jobs[i].Cancel();
            }
        }

        private static bool HasSelectedLocalizedStrings(UnityEngine.Object[] selectedTargets)
        {
            if (selectedTargets == null)
            {
                return false;
            }

            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is LocalizedString)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SelectionContainsBusyTarget(UnityEngine.Object[] selectedTargets)
        {
            if (selectedTargets == null || _activeJobs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is not LocalizedString localizedString || localizedString == null)
                {
                    continue;
                }

                int targetId = localizedString.GetInstanceID();
                for (int jobIndex = 0; jobIndex < _activeJobs.Count; jobIndex++)
                {
                    if (_activeJobs[jobIndex].ContainsTarget(targetId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool QueueContainsBusyTarget(List<TranslationQueueItem> translationQueue)
        {
            if (_activeJobs.Count == 0)
            {
                return false;
            }

            for (int queueIndex = 0; queueIndex < translationQueue.Count; queueIndex++)
            {
                int targetId = translationQueue[queueIndex].TargetId;
                for (int jobIndex = 0; jobIndex < _activeJobs.Count; jobIndex++)
                {
                    if (_activeJobs[jobIndex].ContainsTarget(targetId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<TranslationQueueItem> BuildTranslationQueue(
            UnityEngine.Object[] selectedTargets,
            LocalizationLocale defaultLocale,
            TranslationJobScope scope,
            LocalizationLocale singleTargetLocale)
        {
            var translationQueue = new List<TranslationQueueItem>();
            if (selectedTargets == null)
            {
                return translationQueue;
            }

            var seenTargetIds = new HashSet<int>();
            for (int targetIndex = 0; targetIndex < selectedTargets.Length; targetIndex++)
            {
                if (selectedTargets[targetIndex] is not LocalizedString localizedString || localizedString == null)
                {
                    continue;
                }

                int targetId = localizedString.GetInstanceID();
                if (!seenTargetIds.Add(targetId))
                {
                    continue;
                }

                if (!TryGetLocaleValue(localizedString, defaultLocale, out string sourceText) || string.IsNullOrWhiteSpace(sourceText))
                {
                    continue;
                }

                List<LocalizationLocale> targetLocales = BuildTargetLocaleList(localizedString, defaultLocale, scope, singleTargetLocale);
                if (targetLocales.Count == 0)
                {
                    continue;
                }

                translationQueue.Add(new TranslationQueueItem(localizedString, targetLocales));
            }

            return translationQueue;
        }

        private static int CountTranslationWorkUnits(List<TranslationQueueItem> translationQueue)
        {
            int total = 0;
            for (int i = 0; i < translationQueue.Count; i++)
            {
                total += translationQueue[i].TargetLocales.Count;
            }

            return total;
        }

        private static List<LocalizationLocale> BuildTargetLocaleList(
            LocalizedString localizedString,
            LocalizationLocale defaultLocale,
            TranslationJobScope scope,
            LocalizationLocale singleTargetLocale)
        {
            var targetLocales = new List<LocalizationLocale>();

            if (scope == TranslationJobScope.SingleLocale)
            {
                if (singleTargetLocale != null && singleTargetLocale != defaultLocale)
                {
                    targetLocales.Add(singleTargetLocale);
                }

                return targetLocales;
            }

            var seenLocales = new HashSet<LocalizationLocale>();
            IReadOnlyList<LocalizationLocale> projectLocales = LocalizationSettings.Locales;
            for (int i = 0; i < projectLocales.Count; i++)
            {
                LocalizationLocale locale = projectLocales[i];
                if (locale == null || locale == defaultLocale || !seenLocales.Add(locale))
                {
                    continue;
                }

                if (scope == TranslationJobScope.Missing
                    && TryGetLocaleValue(localizedString, locale, out string existingValue)
                    && !string.IsNullOrWhiteSpace(existingValue))
                {
                    continue;
                }

                targetLocales.Add(locale);
            }

            return targetLocales;
        }

        private static bool TryGetLocaleValue(LocalizedString localizedString, LocalizationLocale locale, out string value)
        {
            value = null;
            if (localizedString == null || locale == null || localizedString.Values == null)
            {
                return false;
            }

            for (int i = 0; i < localizedString.Values.Count; i++)
            {
                LocaleValuePair<string> pair = localizedString.Values[i];
                if (pair == null || pair.Locale != locale)
                {
                    continue;
                }

                value = pair.Value;
                return true;
            }

            return false;
        }

        private static void ApplyTranslatedValue(LocalizedString localizedString, LocalizationLocale locale, string value)
        {
            if (localizedString == null || locale == null)
            {
                return;
            }

            Undo.RecordObject(localizedString, $"Translate {localizedString.name}");
            SetLocaleValue(localizedString, locale, value);
            EditorUtility.SetDirty(localizedString);
        }

        private static void SetLocaleValue(LocalizedString localizedString, LocalizationLocale locale, string value)
        {
            if (localizedString == null || locale == null)
            {
                return;
            }

            localizedString.Values ??= new List<LocaleValuePair<string>>();

            try
            {
                for (int i = 0; i < localizedString.Values.Count; i++)
                {
                    LocaleValuePair<string> pair = localizedString.Values[i];
                    if (pair == null || pair.Locale != locale)
                    {
                        continue;
                    }

                    pair.Value = value;
                    return;
                }

                localizedString.Values.Add(new LocaleValuePair<string>
                {
                    Locale = locale,
                    Value = value,
                });
            }
            finally
            {
                LocalizationSettings.RaiseLocalizedValueChanged(localizedString);
            }
        }

        private static void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        private sealed class TranslationJob
        {
            private readonly AiTranslator _translator;
            private readonly LocalizationLocale _defaultLocale;
            private readonly List<TranslationQueueItem> _translationQueue;
            private readonly CancellationTokenSource _cancellation = new();
            private readonly int _totalWorkUnits;
            private int _progressId = -1;

            public TranslationJob(
                AiTranslator translator,
                LocalizationLocale defaultLocale,
                List<TranslationQueueItem> translationQueue,
                int totalWorkUnits)
            {
                _translator = translator;
                _defaultLocale = defaultLocale;
                _translationQueue = translationQueue;
                _totalWorkUnits = Mathf.Max(1, totalWorkUnits);
                TargetStatesById = BuildTargetStateMap(translationQueue);
                StatusMessage = "Preparing translation job…";
            }

            public Dictionary<int, TargetTranslationState> TargetStatesById { get; }
            public float NormalizedProgress { get; private set; }
            public string StatusMessage { get; private set; }

            public bool ContainsTarget(int targetId)
            {
                return TargetStatesById.ContainsKey(targetId);
            }

            public bool TouchesAny(HashSet<int> targetIds)
            {
                foreach (int targetId in targetIds)
                {
                    if (TargetStatesById.ContainsKey(targetId))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Cancel()
            {
                if (_cancellation.IsCancellationRequested)
                {
                    return;
                }

                StatusMessage = "Cancelling translation job…";
                ReportProgress(NormalizedProgress, StatusMessage);
                _cancellation.Cancel();
            }

            public async Awaitable RunAsync()
            {
                BeginBackgroundTask();

                try
                {
                    int translatedAssetCount = 0;
                    int completedWorkUnits = 0;

                    for (int queueIndex = 0; queueIndex < _translationQueue.Count; queueIndex++)
                    {
                        _cancellation.Token.ThrowIfCancellationRequested();

                        TranslationQueueItem queuedItem = _translationQueue[queueIndex];
                        LocalizedString localizedString = queuedItem.LocalizedString;
                        int completedWorkUnitsBeforeAsset = completedWorkUnits;
                        int currentAssetWorkUnits = queuedItem.TargetLocales.Count;

                        SetProgress(
                            $"Translating {localizedString.name}…",
                            completedWorkUnitsBeforeAsset,
                            _totalWorkUnits);

                        BeginAssetTranslation(queuedItem);

                        try
                        {
                            await _translator.Translate(
                                localizedString,
                                _defaultLocale,
                                queuedItem.TargetLocales,
                                update => OnTranslationProgress(queuedItem, update, completedWorkUnitsBeforeAsset, currentAssetWorkUnits),
                                _cancellation.Token);
                        }
                        finally
                        {
                            EndAssetTranslation(queuedItem);
                        }

                        translatedAssetCount++;
                        completedWorkUnits += currentAssetWorkUnits;
                        SetProgress(
                            $"Translated {localizedString.name} ({translatedAssetCount} asset{(translatedAssetCount == 1 ? string.Empty : "s")}).",
                            completedWorkUnits,
                            _totalWorkUnits);
                    }

                    StatusMessage = $"Translation complete. Updated {translatedAssetCount} asset{(translatedAssetCount == 1 ? string.Empty : "s")}.";
                    NormalizedProgress = 1f;
                    ReportProgress(NormalizedProgress, StatusMessage);
                    FinishBackgroundTask(Progress.Status.Succeeded);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = "Translation cancelled.";
                    ReportProgress(NormalizedProgress, StatusMessage);
                    FinishBackgroundTask(Progress.Status.Canceled);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Translation failed: {ex.Message}";
                    ReportProgress(NormalizedProgress, StatusMessage);
                    Debug.LogError(StatusMessage, _translationQueue.Count > 0 ? _translationQueue[0].LocalizedString : _translator);
                    FinishBackgroundTask(Progress.Status.Failed);
                }
                finally
                {
                    _cancellation.Dispose();
                }
            }

            private static Dictionary<int, TargetTranslationState> BuildTargetStateMap(List<TranslationQueueItem> translationQueue)
            {
                var states = new Dictionary<int, TargetTranslationState>(translationQueue.Count);
                for (int i = 0; i < translationQueue.Count; i++)
                {
                    TranslationQueueItem queuedItem = translationQueue[i];
                    states[queuedItem.TargetId] = new TargetTranslationState(queuedItem.LocalizedString);
                }

                return states;
            }

            private void BeginBackgroundTask()
            {
                string title = _translationQueue.Count == 1
                    ? $"Translating {GetSafeObjectName(_translationQueue[0].LocalizedString)}"
                    : $"Translating {_translationQueue.Count} Strings";

                _progressId = Progress.Start(title, StatusMessage, Progress.Options.Managed, -1);
                Progress.RegisterCancelCallback(_progressId, HandleProgressCancelled);
                ReportProgress(0f, StatusMessage);
            }

            private void FinishBackgroundTask(Progress.Status status)
            {
                if (_progressId < 0)
                {
                    return;
                }

                Progress.Finish(_progressId, status);
                _progressId = -1;
            }

            private bool HandleProgressCancelled()
            {
                if (_cancellation.IsCancellationRequested)
                {
                    return false;
                }

                Cancel();
                return true;
            }

            private void BeginAssetTranslation(TranslationQueueItem queuedItem)
            {
                if (queuedItem.LocalizedString == null)
                {
                    return;
                }

                Undo.RecordObject(queuedItem.LocalizedString, $"Translate {queuedItem.LocalizedString.name}");
                TargetTranslationState targetState = TargetStatesById[queuedItem.TargetId];

                for (int i = 0; i < queuedItem.TargetLocales.Count; i++)
                {
                    LocalizationLocale locale = queuedItem.TargetLocales[i];
                    if (locale == null)
                    {
                        continue;
                    }

                    SetLocaleValue(queuedItem.LocalizedString, locale, string.Empty);
                    targetState.BeginLocale(locale);
                }

                EditorUtility.SetDirty(queuedItem.LocalizedString);
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
                GUI.FocusControl(null);
                RaiseStateChanged();
            }

            private void EndAssetTranslation(TranslationQueueItem queuedItem)
            {
                if (!TargetStatesById.TryGetValue(queuedItem.TargetId, out TargetTranslationState targetState))
                {
                    return;
                }

                for (int i = 0; i < queuedItem.TargetLocales.Count; i++)
                {
                    LocalizationLocale locale = queuedItem.TargetLocales[i];
                    if (locale == null)
                    {
                        continue;
                    }

                    targetState.EndLocale(locale);
                }

                RaiseStateChanged();
            }

            private void OnTranslationProgress(
                TranslationQueueItem queuedItem,
                TranslationProgress update,
                int completedWorkUnitsBeforeAsset,
                int currentAssetWorkUnits)
            {
                int totalCount = Mathf.Max(1, update.TotalCount);
                float currentAssetProgress = Mathf.Clamp01(update.CompletedCount / (float)totalCount);
                float completedWorkUnits = completedWorkUnitsBeforeAsset + currentAssetProgress * currentAssetWorkUnits;
                NormalizedProgress = Mathf.Clamp01(completedWorkUnits / _totalWorkUnits);

                if (!string.IsNullOrWhiteSpace(update.Message) && queuedItem.LocalizedString != null)
                {
                    StatusMessage = $"{queuedItem.LocalizedString.name}: {update.Message}";
                }

                if (TargetStatesById.TryGetValue(queuedItem.TargetId, out TargetTranslationState targetState) && update.Locale != null)
                {
                    if (update.Phase == TranslationProgressPhase.LocaleCompleted)
                    {
                        targetState.CompleteLocale(update.Locale);
                        ApplyTranslatedValue(queuedItem.LocalizedString, update.Locale, update.Text ?? string.Empty);
                    }
                    else if (update.Phase == TranslationProgressPhase.PartialLocaleText)
                    {
                        targetState.SetPartialText(update.Locale, update.Text ?? string.Empty);
                    }
                }

                ReportProgress(NormalizedProgress, StatusMessage);
            }

            private void SetProgress(string message, int completedCount, int totalCount)
            {
                StatusMessage = message;
                NormalizedProgress = totalCount <= 0 ? 0f : Mathf.Clamp01(completedCount / (float)totalCount);
                ReportProgress(NormalizedProgress, StatusMessage);
            }

            private void ReportProgress(float progress, string description)
            {
                if (_progressId >= 0)
                {
                    Progress.Report(_progressId, progress, description);
                }

                RaiseStateChanged();
            }

            private static string GetSafeObjectName(UnityEngine.Object target)
            {
                return target != null && !string.IsNullOrWhiteSpace(target.name)
                    ? target.name
                    : "Localized String";
            }
        }

        private sealed class TargetTranslationState
        {
            public TargetTranslationState(LocalizedString localizedString)
            {
                LocalizedString = localizedString;
            }

            public LocalizedString LocalizedString { get; }
            public Dictionary<LocalizationLocale, LocaleTranslationState> LocaleStates { get; } = new();

            public void BeginLocale(LocalizationLocale locale)
            {
                if (locale == null)
                {
                    return;
                }

                LocaleTranslationState state = GetOrCreate(locale);
                state.IsTranslating = true;
                state.PartialText = null;
            }

            public void SetPartialText(LocalizationLocale locale, string partialText)
            {
                if (locale == null)
                {
                    return;
                }

                LocaleTranslationState state = GetOrCreate(locale);
                state.IsTranslating = true;
                state.PartialText = partialText;
            }

            public void CompleteLocale(LocalizationLocale locale)
            {
                if (locale == null)
                {
                    return;
                }

                if (LocaleStates.TryGetValue(locale, out LocaleTranslationState state))
                {
                    state.IsTranslating = false;
                    state.PartialText = null;
                }
            }

            public void EndLocale(LocalizationLocale locale)
            {
                if (locale == null)
                {
                    return;
                }

                LocaleStates.Remove(locale);
            }

            private LocaleTranslationState GetOrCreate(LocalizationLocale locale)
            {
                if (!LocaleStates.TryGetValue(locale, out LocaleTranslationState state))
                {
                    state = new LocaleTranslationState();
                    LocaleStates.Add(locale, state);
                }

                return state;
            }
        }

        private sealed class LocaleTranslationState
        {
            public bool IsTranslating;
            public string PartialText;
        }

        private readonly struct TranslationQueueItem
        {
            public TranslationQueueItem(LocalizedString localizedString, List<LocalizationLocale> targetLocales)
            {
                LocalizedString = localizedString;
                TargetLocales = targetLocales;
                TargetId = localizedString != null ? localizedString.GetInstanceID() : 0;
            }

            public LocalizedString LocalizedString { get; }
            public List<LocalizationLocale> TargetLocales { get; }
            public int TargetId { get; }
        }
    }
}