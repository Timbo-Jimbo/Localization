using System;
using System.Collections.Generic;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public abstract class LocalizedValue : ScriptableObject
    {
        /// <summary>Whether this asset holds a usable value for exactly this code (no language-only or default fallback).</summary>
        public abstract bool HasMeaningfulValue(LocaleCode code);

        /// <summary>The codes this asset holds entries for, in entry order. Empty entries count.</summary>
        public abstract IEnumerable<LocaleCode> Codes { get; }

        /// <summary>The number of entries. Editor tooling reads it without knowing the value type.</summary>
        public abstract int EntryCount { get; }

        /// <summary>Fills each entry's code from the 1.x locale reference it still carries, in memory. The entries themselves are untouched.</summary>
        internal abstract void FillCodesFromLegacyLocales();

        protected virtual void OnEnable()
        {
            FillCodesFromLegacyLocales();
        }

        protected void Log(string message)
        {
            Debug.Log($"{name} ({GetType().Name}): {message}", this);
        }

        protected void LogWarning(string message)
        {
            Debug.LogWarning($"{name} ({GetType().Name}): {message}", this);
        }

        protected void LogError(string message)
        {
            Debug.LogError($"{name} ({GetType().Name}): {message}", this);
        }

        protected void LogException(Exception exception, string message = null)
        {
            Debug.LogException(new Exception($"{name} ({GetType().Name}): {message ?? exception.Message}", exception), this);
        }
    }

    /// <summary>
    /// A value with one entry per locale, keyed by <see cref="LocaleCode"/>. A player only reads: entries are added,
    /// ordered and removed by the editor (on import, in the inspector, when the settings' locales change).
    /// </summary>
    public abstract class LocalizedValue<T> : LocalizedValue, ILocalizedValueResolver<T>
    {
        [Tooltip("Optional notes for translators or designers describing the purpose, tone, or context of this localized value.")]
        [TextArea(2, 6)]
        public string Description;

        public List<LocaleValuePair<T>> Values = new();

        [NonSerialized] private HashSet<LocaleCode> _warnedCodes;

        public override IEnumerable<LocaleCode> Codes
        {
            get
            {
                for (int i = 0; i < Values.Count; i++)
                {
                    if (Values[i] != null) yield return Values[i].Code;
                }
            }
        }

        public override int EntryCount => Values.Count;

        /// <summary>The entry for exactly this code, empty or not.</summary>
        public bool TryFindValue(LocaleCode code, out LocaleValuePair<T> result)
        {
            result = null;
            if (Values == null || code.IsNone) return false;

            for (int i = 0; i < Values.Count; i++)
            {
                var pair = Values[i];
                if (pair != null && pair.Code == code)
                {
                    result = pair;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The best entry with a usable value for this code: exact code, then the language alone (<c>en</c> for
        /// <c>en-AU</c>), then the language with any country, then the default locale's code. Entries with no usable
        /// value are skipped, never matched.
        /// </summary>
        public bool TryFindBestMatchValue(LocaleCode code, out LocaleValuePair<T> result)
        {
            result = null;
            if (Values == null) return false;

            const int exactMatch = 4;
            const int languageOnlyMatch = 3;
            const int languageMatch = 2;
            const int defaultMatch = 1;

            var defaultCode = LocalizationSettings.DefaultCode;
            var bestScore = 0;

            for (int i = 0; i < Values.Count; i++)
            {
                var pair = Values[i];
                if (pair == null || pair.Code.IsNone || !IsMeaningfulValue(pair.Value)) continue;

                int score;
                if (pair.Code == code)
                    score = exactMatch;
                else if (!pair.Code.HasCountry && pair.Code == code.LanguageOnly)
                    score = languageOnlyMatch;
                else if (pair.Code.LanguageOnly == code.LanguageOnly)
                    score = languageMatch;
                else if (!defaultCode.IsNone && pair.Code == defaultCode)
                    score = defaultMatch;
                else
                    continue;

                if (score <= bestScore) continue;

                bestScore = score;
                result = pair;

                if (score == exactMatch) break;
            }

            return result != null;
        }

        public T Resolve(LocaleCode code)
        {
            if (TryFindBestMatchValue(code, out var bestMatch))
                return bestMatch.Value;

            WarnMissing(code);
            return NonNullFallbackValue.ForType<T>();
        }

        public override bool HasMeaningfulValue(LocaleCode code)
        {
            return TryFindValue(code, out var pair) && IsMeaningfulValue(pair.Value);
        }

        protected virtual bool IsMeaningfulValue(T value)
        {
            if (value == null) return false;
            if (value.Equals(default(T))) return false;
            if (value is string str && string.IsNullOrEmpty(str)) return false;

            return true;
        }

        /// <summary>Warns that no value resolved for <paramref name="code"/>: once per code for this asset, and only in a playing game.</summary>
        protected void WarnMissing(LocaleCode code)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;
            if (!MarkWarned(code)) return;

            LogWarning($"No value for locale \"{code}\" and nothing to fall back on. Returning the default value. (Warned once per locale.)");
        }

        /// <summary>True the first time it is called for <paramref name="code"/> on this asset.</summary>
        internal bool MarkWarned(LocaleCode code)
        {
            _warnedCodes ??= new HashSet<LocaleCode>();
            return _warnedCodes.Add(code);
        }

        internal override void FillCodesFromLegacyLocales()
        {
            if (Values == null) return;

            for (int i = 0; i < Values.Count; i++)
            {
                var pair = Values[i];
                if (pair == null || !pair.Code.IsNone || pair.LegacyLocale == null) continue;

                pair.Code = pair.LegacyLocale.Code;
            }
        }
    }
}
