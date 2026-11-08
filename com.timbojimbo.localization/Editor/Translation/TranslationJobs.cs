using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{

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
            private readonly Dictionary<LocaleCode, LocalePresentationState> _localeStates;

            public InspectorState(
                bool hasRelevantJobs,
                int relevantJobCount,
                float progress,
                string summary,
                Dictionary<LocaleCode, LocalePresentationState> localeStates)
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

            public bool TryGetLocaleState(LocaleCode code, out LocalePresentationState state)
            {
                if (!code.IsNone && _localeStates != null)
                {
                    return _localeStates.TryGetValue(code, out state);
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

                LocaleCode defaultCode = LocalizationSettings.DefaultCode;
                if (defaultCode.IsNone)
                {
                    failureMessage = "No default locale is configured.";
                    return false;
                }

                List<TranslationQueueItem> translationQueue = BuildTranslationQueue(selectedTargets, defaultCode, scope);
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

                var job = new TranslationJob(translator, defaultCode, translationQueue, totalWorkUnits);
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

            var selectedIds = new HashSet<EntityId>();
            for (int i = 0; i < selectedTargets.Length; i++)
            {
                if (selectedTargets[i] is LocalizedString localizedString && localizedString != null)
                {
                    selectedIds.Add(localizedString.GetEntityId());
                }
            }

            if (selectedIds.Count == 0)
            {
                return default;
            }

            var localeStates = new Dictionary<LocaleCode, LocalePresentationState>();
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

                foreach (KeyValuePair<EntityId, TargetTranslationState> targetEntry in job.TargetStatesById)
                {
                    if (!selectedIds.Contains(targetEntry.Key))
                    {
                        continue;
                    }

                    foreach (KeyValuePair<LocaleCode, LocaleTranslationState> localeEntry in targetEntry.Value.LocaleStates)
                    {
                        LocaleCode code = localeEntry.Key;
                        LocaleTranslationState localeState = localeEntry.Value;
                        if (code.IsNone || (!localeState.IsTranslating && string.IsNullOrEmpty(localeState.PartialText)))
                        {
                            continue;
                        }

                        string partialText = localeState.IsTranslating ? localeState.PartialText : null;
                        if (localeStates.TryGetValue(code, out LocalePresentationState existingState))
                        {
                            localeStates[code] = new LocalePresentationState(
                                existingState.IsTranslating || localeState.IsTranslating,
                                string.IsNullOrEmpty(existingState.PartialText) ? partialText : existingState.PartialText);
                        }
                        else
                        {
                            localeStates.Add(code, new LocalePresentationState(localeState.IsTranslating, partialText));
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

        public static bool IsTranslating(UnityEngine.Object asset)
        {
            if (asset == null || _activeJobs.Count == 0) return false;
            EntityId targetId = asset.GetEntityId();
            for (int i = 0; i < _activeJobs.Count; i++)
            {
                if (_activeJobs[i].ContainsTarget(targetId)) return true;
            }
            return false;
        }

        private static bool SelectionContainsBusyTarget(UnityEngine.Object[] selectedTargets)
        {
            return selectedTargets.Any(IsTranslating);
        }

        private static bool QueueContainsBusyTarget(List<TranslationQueueItem> translationQueue)
        {
            if (_activeJobs.Count == 0)
            {
                return false;
            }

            for (int queueIndex = 0; queueIndex < translationQueue.Count; queueIndex++)
            {
                EntityId targetId = translationQueue[queueIndex].TargetId;
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
            LocaleCode defaultCode,
            TranslationJobScope scope)
        {
            var translationQueue = new List<TranslationQueueItem>();
            if (selectedTargets == null)
            {
                return translationQueue;
            }

            var seenTargetIds = new HashSet<EntityId>();
            for (int targetIndex = 0; targetIndex < selectedTargets.Length; targetIndex++)
            {
                if (selectedTargets[targetIndex] is not LocalizedString localizedString || localizedString == null)
                {
                    continue;
                }

                EntityId targetId = localizedString.GetEntityId();

                if (!seenTargetIds.Add(targetId))
                    continue;

                if (!localizedString.HasMeaningfulValue(defaultCode))
                    continue;

                List<LocaleCode> targetCodes = BuildTargetCodeList(localizedString, defaultCode, scope);
                if (targetCodes.Count == 0)
                {
                    continue;
                }

                translationQueue.Add(new TranslationQueueItem(localizedString, targetCodes));
            }

            return translationQueue;
        }

        private static int CountTranslationWorkUnits(List<TranslationQueueItem> translationQueue)
        {
            int total = 0;
            for (int i = 0; i < translationQueue.Count; i++)
            {
                total += translationQueue[i].TargetCodes.Count;
            }

            return total;
        }

        private static List<LocaleCode> BuildTargetCodeList(
            LocalizedString localizedString,
            LocaleCode defaultCode,
            TranslationJobScope scope
        )
        {
            var targetCodes = new List<LocaleCode>();

            if (scope is SingleLocale singleLocaleScope)
            {
                if (!singleLocaleScope.TargetCode.IsNone)
                    targetCodes.Add(singleLocaleScope.TargetCode);
                return targetCodes;
            }

            IReadOnlyList<LocalizationLocale> projectLocales = LocalizationSettings.Locales;
            for (int i = 0; i < projectLocales.Count; i++)
            {
                LocalizationLocale locale = projectLocales[i];
                if (locale == null) continue;

                LocaleCode code = locale.Code;
                if (code.IsNone || code == defaultCode || targetCodes.Contains(code))
                    continue;

                if (scope is MissingLocales && localizedString.HasMeaningfulValue(code))
                    continue;

                targetCodes.Add(code);
            }

            return targetCodes;
        }

        private static void ApplyTranslatedValue(LocalizedString localizedString, LocaleCode code, string value)
        {
            if (localizedString == null || code.IsNone)
            {
                return;
            }

            Undo.RecordObject(localizedString, $"Translate {localizedString.name}");
            SetLocaleValue(localizedString, code, value);
            EditorUtility.SetDirty(localizedString);
        }

        private static void SetLocaleValue(LocalizedString localizedString, LocaleCode code, string value)
        {
            if (localizedString == null || code.IsNone)
            {
                return;
            }

            localizedString.Values ??= new List<LocaleValuePair<string>>();

            try
            {
                if (localizedString.TryFindValue(code, out LocaleValuePair<string> pair))
                    pair.Value = value;
                else
                    localizedString.Values.Add(new LocaleValuePair<string>(code, value));
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
            private readonly LocaleCode _defaultCode;
            private readonly List<TranslationQueueItem> _translationQueue;
            private readonly CancellationTokenSource _cancellation = new();
            private readonly int _totalWorkUnits;
            private int _progressId = -1;

            public TranslationJob(
                AiTranslator translator,
                LocaleCode defaultCode,
                List<TranslationQueueItem> translationQueue,
                int totalWorkUnits)
            {
                _translator = translator;
                _defaultCode = defaultCode;
                _translationQueue = translationQueue;
                _totalWorkUnits = Mathf.Max(1, totalWorkUnits);
                TargetStatesById = BuildTargetStateMap(translationQueue);
                StatusMessage = "Preparing translation job…";
            }

            public Dictionary<EntityId, TargetTranslationState> TargetStatesById { get; }
            public float NormalizedProgress { get; private set; }
            public string StatusMessage { get; private set; }

            public bool ContainsTarget(EntityId targetId)
            {
                return TargetStatesById.ContainsKey(targetId);
            }

            public bool TouchesAny(HashSet<EntityId> targetIds)
            {
                foreach (EntityId targetId in targetIds)
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
                        int currentAssetWorkUnits = queuedItem.TargetCodes.Count;

                        SetProgress(
                            $"Translating {localizedString.name}…",
                            completedWorkUnitsBeforeAsset,
                            _totalWorkUnits);

                        BeginAssetTranslation(queuedItem);

                        try
                        {
                            await _translator.Translate(
                                localizedString,
                                _defaultCode,
                                queuedItem.TargetCodes,
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

            private static Dictionary<EntityId, TargetTranslationState> BuildTargetStateMap(List<TranslationQueueItem> translationQueue)
            {
                var states = new Dictionary<EntityId, TargetTranslationState>(translationQueue.Count);
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

                for (int i = 0; i < queuedItem.TargetCodes.Count; i++)
                {
                    LocaleCode code = queuedItem.TargetCodes[i];
                    if (code.IsNone)
                    {
                        continue;
                    }

                    SetLocaleValue(queuedItem.LocalizedString, code, string.Empty);
                    targetState.BeginLocale(code);
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

                for (int i = 0; i < queuedItem.TargetCodes.Count; i++)
                {
                    LocaleCode code = queuedItem.TargetCodes[i];
                    if (code.IsNone)
                    {
                        continue;
                    }

                    targetState.EndLocale(code);
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

                if (TargetStatesById.TryGetValue(queuedItem.TargetId, out TargetTranslationState targetState) && !update.Code.IsNone)
                {
                    if (update.Phase == TranslationProgressPhase.LocaleCompleted)
                    {
                        targetState.CompleteLocale(update.Code);
                        ApplyTranslatedValue(queuedItem.LocalizedString, update.Code, update.Text ?? string.Empty);
                    }
                    else if (update.Phase == TranslationProgressPhase.PartialLocaleText)
                    {
                        targetState.SetPartialText(update.Code, update.Text ?? string.Empty);
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
            public Dictionary<LocaleCode, LocaleTranslationState> LocaleStates { get; } = new();

            public void BeginLocale(LocaleCode code)
            {
                if (code.IsNone)
                {
                    return;
                }

                LocaleTranslationState state = GetOrCreate(code);
                state.IsTranslating = true;
                state.PartialText = null;
            }

            public void SetPartialText(LocaleCode code, string partialText)
            {
                if (code.IsNone)
                {
                    return;
                }

                LocaleTranslationState state = GetOrCreate(code);
                state.IsTranslating = true;
                state.PartialText = partialText;
            }

            public void CompleteLocale(LocaleCode code)
            {
                if (code.IsNone)
                {
                    return;
                }

                if (LocaleStates.TryGetValue(code, out LocaleTranslationState state))
                {
                    state.IsTranslating = false;
                    state.PartialText = null;
                }
            }

            public void EndLocale(LocaleCode code)
            {
                if (code.IsNone)
                {
                    return;
                }

                LocaleStates.Remove(code);
            }

            private LocaleTranslationState GetOrCreate(LocaleCode code)
            {
                if (!LocaleStates.TryGetValue(code, out LocaleTranslationState state))
                {
                    state = new LocaleTranslationState();
                    LocaleStates.Add(code, state);
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
            public TranslationQueueItem(LocalizedString localizedString, List<LocaleCode> targetCodes)
            {
                LocalizedString = localizedString;
                TargetCodes = targetCodes;
                TargetId = localizedString != null ? localizedString.GetEntityId() : EntityId.None;
            }

            public LocalizedString LocalizedString { get; }
            public List<LocaleCode> TargetCodes { get; }
            public EntityId TargetId { get; }
        }
    }

    public abstract class TranslationJobScope
    {
        public static TranslationJobScope Missing => new MissingLocales();
        public static TranslationJobScope All => new AllLocales();
        public static TranslationJobScope SingleLocale(LocaleCode targetCode) => new SingleLocale(targetCode);
    }

    public sealed class SingleLocale : TranslationJobScope
    {
        public LocaleCode TargetCode { get; }

        public SingleLocale(LocaleCode targetCode)
        {
            TargetCode = targetCode;
        }
    }

    public sealed class AllLocales : TranslationJobScope
    {
    }

    public sealed class MissingLocales : TranslationJobScope
    {
    }

}