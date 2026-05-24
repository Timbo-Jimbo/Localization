using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine.Networking;
using UnityEngine;
using System.Linq;
using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization.Translations
{
    [CreateAssetMenu(fileName = "New AI Translator", menuName = "Localization/Settings and Config/AI Translator (Editor)")]
    public class AiTranslator : ScriptableObject 
    {
        private const string OpenAiApiKeyPrefixEditorPrefsKey = "Localisation.AiTranslator.OpenAI.ApiKey";
        private const string DefaultTranslatorGuidEditorPrefsKey = "Localisation.AiTranslator.DefaultTranslatorGuid";
        private const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";
        private const string DefaultOpenAiModel = "gpt-4.1-mini";
        private const string LogPrefix = "[AiTranslator] ";
        private static bool LoggingEnabled;
        private const int MaxDebugHistoryEntries = 20;

        [Tooltip("Glossaries the LLM should respect when translating. Terms defined here must keep their canonical translations.")]
        [SerializeField]
        private List<TranslationGlossary> _glossaries = new();

        [Tooltip("Additional free-form context blocks (lore, tone of voice, style guide, character bios, etc.) provided to the LLM to guide translation.")]
        [SerializeField]
        private List<TranslationContextBlock> _contextBlocks = new();

        [Tooltip("Optional system-level instructions prepended to every translation request.")]
        [TextArea(3, 10)]
        [SerializeField]
        private string _systemInstructions;

        [Header("OpenAI")]
        [Tooltip("OpenAI model used for translation requests.")]
        [SerializeField]
        private string _model = DefaultOpenAiModel;

        [Tooltip("OpenAI API base URL. Keep the default unless you route OpenAI-compatible requests through a proxy.")]
        [SerializeField]
        private string _apiBaseUrl = DefaultOpenAiBaseUrl;

        [Tooltip("Request timeout in seconds.")]
        [Min(1)]
        [SerializeField]
        private int _timeoutSeconds = 120;

        [Tooltip("Sampling temperature. Lower values are usually better for deterministic translations.")]
        [Range(0f, 2f)]
        [SerializeField]
        private float _temperature = 0.2f;

        [NonSerialized]
        private List<TranslationDebugRecord> _debugHistory;

        public IReadOnlyList<TranslationGlossary> Glossaries => _glossaries;
        public IReadOnlyList<TranslationContextBlock> ContextBlocks => _contextBlocks;
        public string SystemInstructions => _systemInstructions;
        public string Model => string.IsNullOrWhiteSpace(_model) ? DefaultOpenAiModel : _model;
        public string ApiBaseUrl => string.IsNullOrWhiteSpace(_apiBaseUrl) ? DefaultOpenAiBaseUrl : _apiBaseUrl;
        public int TimeoutSeconds => Mathf.Max(1, _timeoutSeconds);
        public float Temperature => Mathf.Clamp(_temperature, 0f, 2f);
        public IReadOnlyList<TranslationDebugRecord> DebugHistory => _debugHistory ??= new List<TranslationDebugRecord>();

        public event Action<TranslationProgress> TranslationProgressed;

        public static bool HasOpenAiApiKey(AiTranslator asset) => !string.IsNullOrWhiteSpace(GetOpenAiApiKey(asset));

        public static string GetOpenAiApiKey(AiTranslator asset)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return EditorPrefs.GetString($"{OpenAiApiKeyPrefixEditorPrefsKey}.{guid}", string.Empty);
        }

        public static void SetOpenAiApiKey(AiTranslator asset, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ClearOpenAiApiKey(asset);
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            EditorPrefs.SetString($"{OpenAiApiKeyPrefixEditorPrefsKey}.{guid}", apiKey.Trim());
        }

        public static void ClearOpenAiApiKey(AiTranslator asset)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            EditorPrefs.DeleteKey($"{OpenAiApiKeyPrefixEditorPrefsKey}.{guid}");
        }

        public static AiTranslator GetDefaultTranslator()
        {
            string guid = EditorPrefs.GetString(DefaultTranslatorGuidEditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AiTranslator>(path);
        }

        public static bool IsDefaultTranslator(AiTranslator translator)
        {
            if (translator == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(translator);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid) && string.Equals(
                guid,
                EditorPrefs.GetString(DefaultTranslatorGuidEditorPrefsKey, string.Empty),
                StringComparison.Ordinal);
        }

        public static void SetDefaultTranslator(AiTranslator translator)
        {
            if (translator == null)
            {
                ClearDefaultTranslator();
                return;
            }

            string path = AssetDatabase.GetAssetPath(translator);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            EditorPrefs.SetString(DefaultTranslatorGuidEditorPrefsKey, guid);
        }

        public static void ClearDefaultTranslator()
        {
            EditorPrefs.DeleteKey(DefaultTranslatorGuidEditorPrefsKey);
        }

        public static string GetDisplayName(AiTranslator translator)
        {
            return translator != null ? GetDisplayName(translator.name) : "Missing Translator";
        }

        public static string GetDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed Translator";
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '_' || current == '-' || current == '/')
                {
                    AppendSpaceIfNeeded(builder);
                    continue;
                }

                if (ShouldInsertWordSeparator(value, i))
                {
                    AppendSpaceIfNeeded(builder);
                }

                builder.Append(current);
            }

            return builder.ToString().Trim();
        }

        private static bool ShouldInsertWordSeparator(string value, int index)
        {
            if (index <= 0)
            {
                return false;
            }

            char previous = value[index - 1];
            char current = value[index];
            if (char.IsWhiteSpace(previous) || char.IsWhiteSpace(current))
            {
                return false;
            }

            if (char.IsLower(previous) && char.IsUpper(current))
            {
                return true;
            }

            if (char.IsLetter(previous) && char.IsDigit(current))
            {
                return true;
            }

            if (char.IsDigit(previous) && char.IsLetter(current))
            {
                return true;
            }

            if (char.IsUpper(previous) && char.IsUpper(current) && index + 1 < value.Length && char.IsLower(value[index + 1]))
            {
                return true;
            }

            return false;
        }

        private static void AppendSpaceIfNeeded(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        public void ClearDebugHistory()
        {
            _debugHistory?.Clear();
        }

        /// <summary>
        /// Translate <paramref name="source"/> from <paramref name="sourceLocale"/> into each of <paramref name="targetLocales"/>.
        /// The source value description, configured glossaries (filtered to source + targets), and context blocks are sent along to guide the LLM.
        /// </summary>
        public async Awaitable<IReadOnlyDictionary<LocalizationLocale, string>> Translate(
            LocalizedString source,
            LocalizationLocale sourceLocale,
            IReadOnlyList<LocalizationLocale> targetLocales,
            Action<TranslationProgress> progress,
            CancellationToken cancellationToken = default
        )
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sourceLocale == null) throw new ArgumentNullException(nameof(sourceLocale));
            if (targetLocales == null) throw new ArgumentNullException(nameof(targetLocales));

            if (!TryGetSourceText(source, sourceLocale, out string sourceText) || string.IsNullOrWhiteSpace(sourceText))
            {
                throw new ArgumentException($"Source LocalizedString '{source.name}' has no text for locale '{GetLocaleCode(sourceLocale)}'.", nameof(source));
            }

            string apiKey = GetOpenAiApiKey(this);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                const string message = "OpenAI API key is not configured for AiTranslator. Set it in the AiTranslator inspector.";
                ReportProgress(TranslationProgressPhase.Failed, null, null, 0, 0, message, progress, true);
                throw new InvalidOperationException(message);
            }

            var uniqueTargets = BuildUniqueTargetLocaleList(targetLocales);
            if (uniqueTargets.Count == 0)
            {
                return new Dictionary<LocalizationLocale, string>();
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(TranslationProgressPhase.PreparingRequest, null, null, 0, uniqueTargets.Count, "Preparing OpenAI translation request.", progress, true);

            string systemPrompt = BuildSystemPrompt(sourceLocale, uniqueTargets);
            string userPrompt = BuildUserPrompt(sourceText, source.Description, sourceLocale, uniqueTargets);
            TranslationDebugRecord debugRecord = AddDebugRecord(systemPrompt, userPrompt, sourceLocale, uniqueTargets);
            var client = new OpenAiClient(ApiBaseUrl, Model, TimeoutSeconds, Temperature, apiKey);

            try
            {
                IReadOnlyDictionary<LocalizationLocale, string> result = await client.Translate(
                    systemPrompt,
                    userPrompt,
                    uniqueTargets,
                    update => ReportProgress(update, progress, true),
                    debugRecord,
                    cancellationToken);

                debugRecord.MarkSucceeded(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                debugRecord.MarkCancelled();
                ReportProgress(TranslationProgressPhase.Cancelled, null, null, 0, uniqueTargets.Count, "OpenAI translation request cancelled.", progress, true);
                throw;
            }
            catch (Exception ex)
            {
                debugRecord.MarkFailed(ex.Message);
                ReportProgress(TranslationProgressPhase.Failed, null, null, 0, uniqueTargets.Count, ex.Message, progress, true);
                throw;
            }
        }

        private static bool TryGetSourceText(LocalizedString source, LocalizationLocale sourceLocale, out string sourceText)
        {
            sourceText = null;
            if (source.Values == null)
            {
                return false;
            }

            for (int i = 0; i < source.Values.Count; i++)
            {
                LocaleValuePair<string> pair = source.Values[i];
                if (pair?.Locale != sourceLocale) continue;

                sourceText = pair.Value;
                return true;
            }

            return false;
        }

        private TranslationDebugRecord AddDebugRecord(string systemPrompt, string userPrompt, LocalizationLocale sourceLocale, IReadOnlyList<LocalizationLocale> targetLocales)
        {
            _debugHistory ??= new List<TranslationDebugRecord>();
            while (_debugHistory.Count >= MaxDebugHistoryEntries)
            {
                _debugHistory.RemoveAt(_debugHistory.Count - 1);
            }

            var record = new TranslationDebugRecord(
                DateTime.Now,
                ApiBaseUrl,
                Model,
                Temperature,
                TimeoutSeconds,
                GetLocaleCode(sourceLocale),
                BuildLocaleListText(targetLocales),
                systemPrompt,
                userPrompt);

            _debugHistory.Insert(0, record);
            return record;
        }

        private static string BuildLocaleListText(IReadOnlyList<LocalizationLocale> locales)
        {
            if (locales == null || locales.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(locales.Count * 8);
            for (int i = 0; i < locales.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(GetLocaleCode(locales[i]));
            }

            return builder.ToString();
        }

        private static List<LocalizationLocale> BuildUniqueTargetLocaleList(IReadOnlyList<LocalizationLocale> targetLocales)
        {
            var uniqueTargets = new List<LocalizationLocale>(targetLocales.Count);
            var seenLocaleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < targetLocales.Count; i++)
            {
                LocalizationLocale locale = targetLocales[i];
                string localeCode = GetLocaleCode(locale);
                if (!string.IsNullOrWhiteSpace(localeCode) && seenLocaleCodes.Add(localeCode))
                {
                    uniqueTargets.Add(locale);
                }
            }

            return uniqueTargets;
        }

        private string BuildSystemPrompt(LocalizationLocale sourceLocale, IReadOnlyList<LocalizationLocale> targetLocales)
        {
            var prompt = new StringBuilder(4096);

            prompt.AppendLine("You are a professional game localization translator.");
            prompt.AppendLine("The user message contains the source locale, source text, optional source text description, and requested target locales.");
            prompt.AppendLine("Translate the source text into every target locale listed in the user message.");
            prompt.AppendLine("Respect glossary entries exactly when a canonical translation is provided for a target locale.");
            prompt.AppendLine("Use the supplied context blocks for additional rules, tone, lore, character voice, and style.");
            prompt.AppendLine("Output strict JSON Lines only: one complete JSON object per target locale, one object per line, no markdown, no code fences, no commentary.");
            prompt.AppendLine("Each JSON object must have exactly these fields: \"locale\" and \"text\".");
            prompt.AppendLine("The locale value must exactly match one requested target locale code.");
            

            if (!string.IsNullOrWhiteSpace(_systemInstructions))
            {
                prompt.AppendLine();
                prompt.AppendLine("Additional project instructions:");
                prompt.AppendLine(_systemInstructions.Trim());
            }

            AppendContextBlocks(prompt);
            AppendGlossaries(prompt, sourceLocale, targetLocales);
            prompt.AppendLine();
            prompt.AppendLine("Return one JSON object line per target locale now.");

            return prompt.ToString();
        }

        private static string BuildUserPrompt(string sourceText, string sourceDescription, LocalizationLocale sourceLocale, IReadOnlyList<LocalizationLocale> targetLocales)
        {
            var prompt = new StringBuilder(1024);

            prompt.Append("Source locale: ").AppendLine(GetLocaleCode(sourceLocale));

            if (targetLocales.Count > 1)
            {
                prompt.AppendLine("Target locales:");
                for (int i = 0; i < targetLocales.Count; i++)
                {
                    prompt.Append("- ").AppendLine(GetLocaleCode(targetLocales[i]));
                }
            }
            else if (targetLocales.Count == 1)
            {
                prompt.Append("Target locale: ").AppendLine(GetLocaleCode(targetLocales[0]));
            }
            else
            {
                prompt.AppendLine("No target locales specified.");
            }

            prompt.AppendLine();
            prompt.AppendLine("Source text:");
            prompt.AppendLine(sourceText ?? string.Empty);
            prompt.AppendLine();
            prompt.AppendLine("Source text description:");
            prompt.AppendLine(string.IsNullOrWhiteSpace(sourceDescription) ? "(none)" : sourceDescription.Trim());

            return prompt.ToString();
        }

        private void AppendContextBlocks(StringBuilder prompt)
        {
            bool wroteHeader = false;
            int untitledBlockCount = 0;

            if (_contextBlocks != null)
            {
                for (int i = 0; i < _contextBlocks.Count; i++)
                {
                    TranslationContextBlock block = _contextBlocks[i];
                    if (block == null)
                    {
                        continue;
                    }

                    AppendContextBlock(prompt, block.Title, block.Body, ref wroteHeader, ref untitledBlockCount);
                }
            }

            var additionalContextBlockAssets = AssetDatabase
                .FindAssets($"t:{nameof(ProjectWideTranslationContextBlocks)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ProjectWideTranslationContextBlocks>)
                .Where(x => x != null)
                .SelectMany(x => x.ContextBlocks)
                .Where(x => x != null);

            foreach (var block in additionalContextBlockAssets)
                AppendContextBlock(prompt, block.Title, block.Body, ref wroteHeader, ref untitledBlockCount);
        }

        private static void AppendContextBlock(StringBuilder prompt, string title, string body, ref bool wroteHeader, ref int untitledBlockCount)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            if (!wroteHeader)
            {
                prompt.AppendLine();
                prompt.AppendLine("Context blocks:");
                wroteHeader = true;
            }

            string displayTitle = string.IsNullOrWhiteSpace(title)
                ? $"Context {++untitledBlockCount}"
                : title.Trim();

            prompt.AppendLine();
            prompt.Append("## ").AppendLine(displayTitle);
            prompt.AppendLine(body.Trim());
            prompt.AppendLine();
        }

        private void AppendGlossaries(StringBuilder prompt, LocalizationLocale sourceLocale, IReadOnlyList<LocalizationLocale> targetLocales)
        {
            if (_glossaries == null || _glossaries.Count == 0)
            {
                return;
            }

            var glossaryLocales = new List<LocalizationLocale>(targetLocales.Count + 1) { sourceLocale };
            for (int i = 0; i < targetLocales.Count; i++)
            {
                glossaryLocales.Add(targetLocales[i]);
            }

            var filteredEntries = new List<TranslationGlossary.FilteredEntry>();
            bool wroteHeader = false;

            for (int i = 0; i < _glossaries.Count; i++)
            {
                TranslationGlossary glossary = _glossaries[i];
                if (glossary == null) continue;

                glossary.GetFiltered(glossaryLocales, filteredEntries);
                if (filteredEntries.Count == 0) continue;

                if (!wroteHeader)
                {
                    prompt.AppendLine();
                    prompt.AppendLine("Glossary entries:");
                    wroteHeader = true;
                }

                for (int entryIndex = 0; entryIndex < filteredEntries.Count; entryIndex++)
                {
                    AppendGlossaryEntry(prompt, filteredEntries[entryIndex]);
                }
            }
        }

        private static void AppendGlossaryEntry(StringBuilder prompt, TranslationGlossary.FilteredEntry entry)
        {
            if (entry.Term == null || entry.Values == null || entry.Values.Count == 0)
            {
                return;
            }

            prompt.Append("- Term ID: ").AppendLine(entry.Term.name);

            if (!string.IsNullOrWhiteSpace(entry.Term.Description))
            {
                prompt.Append("  Description: ").AppendLine(entry.Term.Description.Trim());
            }

            prompt.AppendLine("  Known translations:");
            for (int i = 0; i < entry.Values.Count; i++)
            {
                LocaleValuePair<string> value = entry.Values[i];
                if (value == null || value.Locale == null || string.IsNullOrWhiteSpace(value.Value)) continue;

                prompt.Append("  - ").Append(GetLocaleCode(value.Locale)).Append(": ").AppendLine(value.Value);
            }
        }

        private void ReportProgress(
            TranslationProgressPhase phase,
            LocalizationLocale locale,
            string text,
            int completedCount,
            int totalCount,
            string message,
            Action<TranslationProgress> progress,
            bool log
        )
        {
            ReportProgress(CreateProgress(phase, locale, text, completedCount, totalCount, message), progress, log);
        }

        private void ReportProgress(TranslationProgress update, Action<TranslationProgress> progress, bool log)
        {
            if (log && !string.IsNullOrWhiteSpace(update.Message))
            {
                LogProgress(update.Phase, update.Message);
            }

            TranslationProgressed?.Invoke(update);
            progress?.Invoke(update);
        }

        private void LogProgress(TranslationProgressPhase phase, string message)
        {
            if(!LoggingEnabled) return;
            
            string text = LogPrefix + message;
            if (phase == TranslationProgressPhase.Failed)
            {
                Debug.LogError(text, this);
            }
            else if (phase == TranslationProgressPhase.Warning)
            {
                Debug.LogWarning(text, this);
            }
            else
            {
                Debug.Log(text, this);
            }
        }

        private static TranslationProgress CreateProgress(
            TranslationProgressPhase phase,
            LocalizationLocale locale,
            string text,
            int completedCount,
            int totalCount,
            string message)
        {
            return new TranslationProgress(phase, locale, locale != null ? GetLocaleCode(locale) : null, text, completedCount, totalCount, message);
        }

        private static string GetLocaleCode(LocalizationLocale locale)
        {
            return locale != null ? locale.DisplayCode : string.Empty;
        }


        public sealed class TranslationDebugRecord
        {
            public readonly DateTime StartedAt;
            public readonly string ApiBaseUrl;
            public readonly string Model;
            public readonly float Temperature;
            public readonly int TimeoutSeconds;
            public readonly string SourceLocaleCode;
            public readonly string TargetLocaleCodes;
            public readonly string SystemPrompt;
            public readonly string UserPrompt;

            public DateTime? CompletedAt { get; private set; }
            public long ResponseCode { get; private set; }
            public string RequestJson { get; private set; }
            public string RawResponse { get; private set; }
            public string ParsedResponse { get; private set; }
            public string Error { get; private set; }
            public string Status { get; private set; } = "Running";

            public double DurationSeconds => CompletedAt.HasValue ? (CompletedAt.Value - StartedAt).TotalSeconds : (DateTime.Now - StartedAt).TotalSeconds;

            public TranslationDebugRecord(
                DateTime startedAt,
                string apiBaseUrl,
                string model,
                float temperature,
                int timeoutSeconds,
                string sourceLocaleCode,
                string targetLocaleCodes,
                string systemPrompt,
                string userPrompt)
            {
                StartedAt = startedAt;
                ApiBaseUrl = apiBaseUrl;
                Model = model;
                Temperature = temperature;
                TimeoutSeconds = timeoutSeconds;
                SourceLocaleCode = sourceLocaleCode;
                TargetLocaleCodes = targetLocaleCodes;
                SystemPrompt = systemPrompt;
                UserPrompt = userPrompt;
            }

            public void SetRequestJson(string requestJson)
            {
                RequestJson = requestJson;
            }

            public void SetResponse(long responseCode, string rawResponse)
            {
                ResponseCode = responseCode;
                RawResponse = rawResponse;
            }

            public void MarkSucceeded(IReadOnlyDictionary<LocalizationLocale, string> results)
            {
                Status = "Succeeded";
                Error = null;
                CompletedAt = DateTime.Now;
                ParsedResponse = BuildParsedResponseText(results);
            }

            public void MarkCancelled()
            {
                Status = "Cancelled";
                Error = null;
                CompletedAt = DateTime.Now;
            }

            public void MarkFailed(string error)
            {
                Status = "Failed";
                Error = error;
                CompletedAt = DateTime.Now;
            }

            private static string BuildParsedResponseText(IReadOnlyDictionary<LocalizationLocale, string> results)
            {
                if (results == null || results.Count == 0)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(results.Count * 64);
                foreach (var pair in results)
                {
                    builder.Append(GetLocaleCode(pair.Key)).Append(": ").AppendLine(pair.Value);
                }

                return builder.ToString();
            }
        }

        private sealed class OpenAiClient
        {
            private readonly string _apiBaseUrl;
            private readonly string _model;
            private readonly int _timeoutSeconds;
            private readonly float _temperature;
            private readonly string _apiKey;

            public OpenAiClient(string apiBaseUrl, string model, int timeoutSeconds, float temperature, string apiKey)
            {
                _apiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? DefaultOpenAiBaseUrl : apiBaseUrl.Trim().TrimEnd('/');
                _model = string.IsNullOrWhiteSpace(model) ? DefaultOpenAiModel : model.Trim();
                _timeoutSeconds = Mathf.Max(1, timeoutSeconds);
                _temperature = Mathf.Clamp(temperature, 0f, 2f);
                _apiKey = apiKey;
            }

            public async Awaitable<IReadOnlyDictionary<LocalizationLocale, string>> Translate(
                string systemPrompt,
                string userPrompt,
                IReadOnlyList<LocalizationLocale> targetLocales,
                Action<TranslationProgress> progress,
                TranslationDebugRecord debugRecord,
                CancellationToken cancellationToken
            )
            {
                var results = new Dictionary<LocalizationLocale, string>(targetLocales.Count);
                var streamParser = new StreamingTranslationParser(BuildLocaleMap(targetLocales), results, progress, targetLocales.Count);
                string requestJson = BuildRequestJson(systemPrompt, userPrompt);
                debugRecord?.SetRequestJson(requestJson);

                using var request = CreateRequest(requestJson, streamParser.ReceiveText);
                progress?.Invoke(CreateProgress(TranslationProgressPhase.UploadingRequest, null, null, 0, targetLocales.Count, "Sending OpenAI translation request."));

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                bool streamConnected = false;

                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!streamConnected && request.responseCode > 0)
                    {
                        streamConnected = true;
                        progress?.Invoke(CreateProgress(TranslationProgressPhase.StreamConnected, null, null, results.Count, targetLocales.Count, "Connected to OpenAI response stream."));
                    }

                    await Awaitable.NextFrameAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();

                var downloadHandler = (OpenAiStreamDownloadHandler)request.downloadHandler;
                debugRecord?.SetResponse(request.responseCode, downloadHandler.ResponseText);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(BuildErrorMessage(request, downloadHandler.ResponseText));
                }

                streamParser.FlushRemainingContent();
                progress?.Invoke(CreateProgress(TranslationProgressPhase.Completed, null, null, results.Count, targetLocales.Count, $"OpenAI translation completed. Parsed {results.Count}/{targetLocales.Count} translations."));

                return results;
            }

            private UnityWebRequest CreateRequest(string requestJson, Action<string> receiveText)
            {
                var request = new UnityWebRequest($"{_apiBaseUrl}/chat/completions", UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson)),
                    downloadHandler = new OpenAiStreamDownloadHandler(receiveText),
                    timeout = _timeoutSeconds,
                };

                request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "text/event-stream");
                return request;
            }

            private static Dictionary<string, LocalizationLocale> BuildLocaleMap(IReadOnlyList<LocalizationLocale> targetLocales)
            {
                var localeByCode = new Dictionary<string, LocalizationLocale>(targetLocales.Count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < targetLocales.Count; i++)
                {
                    LocalizationLocale locale = targetLocales[i];
                    localeByCode[GetLocaleCode(locale)] = locale;
                }

                return localeByCode;
            }

            private string BuildRequestJson(string systemPrompt, string userPrompt)
            {
                var request = new ChatCompletionRequest
                {
                    model = _model,
                    stream = true,
                    temperature = _temperature,
                    messages = new List<ChatMessage>(2)
                    {
                        new() { role = "system", content = systemPrompt },
                        new() { role = "user", content = userPrompt },
                    },
                };

                return JsonUtility.ToJson(request);
            }

            private static string BuildErrorMessage(UnityWebRequest request, string responseBody)
            {
                string message = TryReadOpenAiErrorMessage(responseBody);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = string.IsNullOrWhiteSpace(responseBody) ? request.error : responseBody;
                }

                return $"OpenAI request failed ({request.responseCode}): {message}";
            }

            private static string TryReadOpenAiErrorMessage(string responseBody)
            {
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return null;
                }

                try
                {
                    return JsonUtility.FromJson<ErrorResponse>(responseBody)?.error?.message;
                }
                catch
                {
                    return null;
                }
            }

            [Serializable]
            private sealed class ChatCompletionRequest
            {
                public string model;
                public List<ChatMessage> messages;
                public bool stream;
                public float temperature;
            }

            [Serializable]
            private sealed class ChatMessage
            {
                public string role;
                public string content;
            }

            [Serializable]
            private sealed class ErrorResponse
            {
                public OpenAiError error;
            }

            [Serializable]
            private sealed class OpenAiError
            {
                public string message;
            }
        }

        private sealed class OpenAiStreamDownloadHandler : DownloadHandlerScript
        {
            private readonly Action<string> _receiveText;
            private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
            private readonly StringBuilder _responseText = new();
            private char[] _charBuffer = Array.Empty<char>();

            public string ResponseText => _responseText.ToString();

            public OpenAiStreamDownloadHandler(Action<string> receiveText) : base(new byte[8192])
            {
                _receiveText = receiveText;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                {
                    return true;
                }

                int requiredChars = Encoding.UTF8.GetMaxCharCount(dataLength);
                if (_charBuffer.Length < requiredChars)
                {
                    _charBuffer = new char[requiredChars];
                }

                AppendDecodedText(_decoder.GetChars(data, 0, dataLength, _charBuffer, 0, false));
                return true;
            }

            protected override void CompleteContent()
            {
                AppendDecodedText(_decoder.GetChars(Array.Empty<byte>(), 0, 0, _charBuffer, 0, true));
            }

            private void AppendDecodedText(int charCount)
            {
                if (charCount <= 0)
                {
                    return;
                }

                string text = new(_charBuffer, 0, charCount);
                _responseText.Append(text);
                _receiveText?.Invoke(text);
            }
        }

        private sealed class StreamingTranslationParser
        {
            private readonly Dictionary<string, LocalizationLocale> _localeByCode;
            private readonly Dictionary<LocalizationLocale, string> _results;
            private readonly Action<TranslationProgress> _progress;
            private readonly int _totalCount;
            private readonly StringBuilder _sseBuffer = new();
            private readonly StringBuilder _jsonLineBuffer = new();

            public StreamingTranslationParser(
                Dictionary<string, LocalizationLocale> localeByCode,
                Dictionary<LocalizationLocale, string> results,
                Action<TranslationProgress> progress,
                int totalCount
            )
            {
                _localeByCode = localeByCode;
                _results = results;
                _progress = progress;
                _totalCount = totalCount;
            }

            public void ReceiveText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                _sseBuffer.Append(text);
                ProcessCompletedSseFrames();
            }

            public void FlushRemainingContent()
            {
                string line = _jsonLineBuffer.ToString().Trim();
                _jsonLineBuffer.Clear();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    ParseTranslationLine(line);
                }
            }

            private void ProcessCompletedSseFrames()
            {
                while (TryReadSseFrame(out string frame))
                {
                    ProcessSseFrame(frame);
                }
            }

            private bool TryReadSseFrame(out string frame)
            {
                frame = null;

                for (int i = 0; i < _sseBuffer.Length - 1; i++)
                {
                    int delimiterLength = GetSseDelimiterLength(i);
                    if (delimiterLength == 0) continue;

                    frame = _sseBuffer.ToString(0, i);
                    _sseBuffer.Remove(0, i + delimiterLength);
                    return true;
                }

                return false;
            }

            private int GetSseDelimiterLength(int index)
            {
                if (_sseBuffer[index] == '\n' && _sseBuffer[index + 1] == '\n')
                {
                    return 2;
                }

                bool isCrLfCrLf = index < _sseBuffer.Length - 3
                    && _sseBuffer[index] == '\r'
                    && _sseBuffer[index + 1] == '\n'
                    && _sseBuffer[index + 2] == '\r'
                    && _sseBuffer[index + 3] == '\n';

                return isCrLfCrLf ? 4 : 0;
            }

            private void ProcessSseFrame(string frame)
            {
                if (string.IsNullOrWhiteSpace(frame))
                {
                    return;
                }

                string data = ReadSseData(frame);
                if (string.IsNullOrWhiteSpace(data) || string.Equals(data, "[DONE]", StringComparison.Ordinal))
                {
                    return;
                }

                string contentDelta = TryReadContentDelta(data);
                if (string.IsNullOrEmpty(contentDelta))
                {
                    return;
                }

                _progress?.Invoke(CreateProgress(TranslationProgressPhase.StreamDelta, null, contentDelta, _results.Count, _totalCount, $"Received {contentDelta.Length} streamed translation characters."));
                AppendContentDelta(contentDelta);
            }

            private static string ReadSseData(string frame)
            {
                var dataBuilder = new StringBuilder(frame.Length);
                string[] lines = frame.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                    if (dataBuilder.Length > 0)
                    {
                        dataBuilder.Append('\n');
                    }

                    dataBuilder.Append(line.Substring(5).TrimStart());
                }

                return dataBuilder.ToString();
            }

            private void AppendContentDelta(string contentDelta)
            {
                _jsonLineBuffer.Append(contentDelta);

                while (TryReadJsonLine(out string line))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ParseTranslationLine(line.Trim());
                    }
                }

                EmitPartialIfPossible();
            }

            private static readonly Regex PartialLocalePattern = new("\"locale\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
            private static readonly Regex PartialTextStartPattern = new("\"text\"\\s*:\\s*\"", RegexOptions.Compiled);

            private string _lastPartialLocale;
            private string _lastPartialText;

            private void EmitPartialIfPossible()
            {
                if (_progress == null || _jsonLineBuffer.Length == 0)
                {
                    return;
                }

                string buffer = _jsonLineBuffer.ToString();
                Match localeMatch = PartialLocalePattern.Match(buffer);
                if (!localeMatch.Success)
                {
                    return;
                }

                Match textMatch = PartialTextStartPattern.Match(buffer, localeMatch.Index + localeMatch.Length);
                if (!textMatch.Success)
                {
                    return;
                }

                int textStart = textMatch.Index + textMatch.Length;
                int textEnd = buffer.Length;
                for (int i = textStart; i < buffer.Length; i++)
                {
                    if (buffer[i] == '\\' && i + 1 < buffer.Length)
                    {
                        i++;
                        continue;
                    }

                    if (buffer[i] == '"')
                    {
                        textEnd = i;
                        break;
                    }
                }

                string raw = buffer.Substring(textStart, textEnd - textStart);
                string localeCode = localeMatch.Groups[1].Value;
                if (!_localeByCode.TryGetValue(localeCode, out LocalizationLocale locale))
                {
                    return;
                }

                // Skip if locale is already finalized.
                if (_results.ContainsKey(locale))
                {
                    return;
                }

                string text = UnescapePartialJsonString(raw);
                if (localeCode == _lastPartialLocale && text == _lastPartialText)
                {
                    return;
                }

                _lastPartialLocale = localeCode;
                _lastPartialText = text;

                _progress.Invoke(new TranslationProgress(
                    TranslationProgressPhase.PartialLocaleText,
                    locale,
                    localeCode,
                    text,
                    _results.Count,
                    _totalCount,
                    null));
            }

            private static string UnescapePartialJsonString(string raw)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(raw.Length);
                for (int i = 0; i < raw.Length; i++)
                {
                    char c = raw[i];
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    // Incomplete trailing escape — drop it; more bytes will arrive.
                    if (i + 1 >= raw.Length)
                    {
                        break;
                    }

                    char next = raw[++i];
                    switch (next)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (i + 4 < raw.Length
                                && ushort.TryParse(raw.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort code))
                            {
                                builder.Append((char)code);
                                i += 4;
                            }
                            // else: incomplete \u escape — drop it.
                            break;
                        default:
                            builder.Append(next);
                            break;
                    }
                }

                return builder.ToString();
            }

            private bool TryReadJsonLine(out string line)
            {
                line = null;

                for (int i = 0; i < _jsonLineBuffer.Length; i++)
                {
                    if (_jsonLineBuffer[i] != '\n') continue;

                    int lineLength = i;
                    if (lineLength > 0 && _jsonLineBuffer[lineLength - 1] == '\r')
                    {
                        lineLength--;
                    }

                    line = _jsonLineBuffer.ToString(0, lineLength);
                    _jsonLineBuffer.Remove(0, i + 1);
                    return true;
                }

                return false;
            }

            private void ParseTranslationLine(string line)
            {
                TranslationLine translationLine;
                try
                {
                    translationLine = JsonUtility.FromJson<TranslationLine>(line);
                }
                catch (Exception ex)
                {
                    _progress?.Invoke(CreateProgress(TranslationProgressPhase.Warning, null, line, _results.Count, _totalCount, $"Could not parse streamed translation line: {ex.Message}"));
                    return;
                }

                if (translationLine == null || string.IsNullOrWhiteSpace(translationLine.locale))
                {
                    _progress?.Invoke(CreateProgress(TranslationProgressPhase.Warning, null, line, _results.Count, _totalCount, "OpenAI returned a translation line without a locale."));
                    return;
                }

                if (!_localeByCode.TryGetValue(translationLine.locale, out LocalizationLocale locale))
                {
                    _progress?.Invoke(new TranslationProgress(TranslationProgressPhase.Warning, null, translationLine.locale, translationLine.text, _results.Count, _totalCount, $"OpenAI returned an unexpected locale '{translationLine.locale}'."));
                    return;
                }

                _results[locale] = translationLine.text ?? string.Empty;
                _lastPartialLocale = null;
                _lastPartialText = null;
                _progress?.Invoke(CreateProgress(TranslationProgressPhase.LocaleCompleted, locale, translationLine.text, _results.Count, _totalCount, $"Parsed translation for {GetLocaleCode(locale)} ({_results.Count}/{_totalCount})."));
            }

            private static string TryReadContentDelta(string data)
            {
                try
                {
                    StreamChunk chunk = JsonUtility.FromJson<StreamChunk>(data);
                    return chunk?.choices == null || chunk.choices.Count == 0 ? null : chunk.choices[0]?.delta?.content;
                }
                catch
                {
                    return null;
                }
            }

            [Serializable]
            private sealed class StreamChunk
            {
                public List<StreamChoice> choices;
            }

            [Serializable]
            private sealed class StreamChoice
            {
                public StreamDelta delta;
            }

            [Serializable]
            private sealed class StreamDelta
            {
                public string content;
            }

            [Serializable]
            private sealed class TranslationLine
            {
                public string locale;
                public string text;
            }
        }
    }
}