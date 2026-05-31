using System;
using System.Collections.Generic;
using TimboJimbo.Localization.Utility;
using UnityEngine;
using UnityEngine.Pool;

namespace TimboJimbo.Localization
{
    public abstract class LocalizedValue : ScriptableObject
    {
        internal abstract void SyncWithProjectLocales(bool removeEntriesWithMissingLocales = false);
        public abstract bool HasMeaningfulValueForLocale(LocalizationLocale locale);

        protected virtual void OnEnable()
        {
            SyncWithProjectLocales();
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

    public abstract class LocalizedValue<T> : LocalizedValue, ILocalizedValueResolver<T>
    {
        [Tooltip("Optional notes for translators or designers describing the purpose, tone, or context of this localized value.")]
        [TextArea(2, 6)]
        public string Description;

        public List<LocaleValuePair<T>> Values = new();

        internal override void SyncWithProjectLocales(bool removeEntriesWithMissingLocales = false)
        {
            if(LocalizationSettings.ActiveAsset == null) return;

            #if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Sync Localized Value with Project Locales");
            #endif

            using(ListPool<LocalizationLocale>.Get(out var projectLocales))
            {
                projectLocales.AddRange(LocalizationSettings.Locales);
                    
                var changed = false;

                foreach(var locale in LocalizationSettings.Locales)
                {
                    if(locale == null) continue;

                    if (!TryFindValue(locale, out _))
                    {
                        Values.Add(new LocaleValuePair<T>()
                        {
                            Locale = locale,
                            Value = default
                        });
                        changed = true;
                    }
                }

                // ensure same order as LocalizationSettings.Locales
                for(int i = 0; i < projectLocales.Count; i++)
                {
                    var locale = projectLocales[i];
                    if(locale == null) continue;

                    if (TryFindValue(locale, out var pair))
                    {
                        int currentIndex = Values.IndexOf(pair);
                        if(currentIndex != i)
                        {
                            Values.RemoveAt(currentIndex);
                            Values.Insert(i, pair);
                            changed = true;
                        }
                    }
                }

                if (removeEntriesWithMissingLocales)
                {
                    for (int i = Values.Count - 1; i >= 0; i--)
                    {
                        var pair = Values[i];
                        if (pair == null || pair.Locale == null)
                        {
                            Values.RemoveAt(i);
                            changed = true;
                        }
                    }
                }

                if(changed)
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this);
                    #endif
                }
            }
        }

        public bool TryFindValue(LocalizationLocale targetLocale, out LocaleValuePair<T> result)
        {
            result = default;

            if (Values == null || Values.Count == 0) return false;

            for (int i = 0; i < Values.Count; i++)
            {
                LocaleValuePair<T> pair = Values[i];

                if (pair == null || pair.Locale == null) continue;

                if (pair.Locale.Equals(targetLocale))
                {
                    result = pair;
                    return true;
                }
            }

            return false;
        }
        
        public bool TryFindBestMatchValue(LocalizationLocale targetLocale, out LocaleValuePair<T> result)
        {
            result = default;

            if (Values == null || Values.Count == 0) return false;

            var bestScore = -1;

            for (int i = 0; i < Values.Count; i++)
            {
                LocaleValuePair<T> pair = Values[i];

                if (pair == null || pair.Locale == null) continue;
                const int perfectScore = 4;

                int score;
                if (pair.Locale.LanguageCode == targetLocale.LanguageCode && pair.Locale.CountryCode == targetLocale.CountryCode)
                {
                    score = perfectScore;
                }
                else if (pair.Locale.LanguageCode == targetLocale.LanguageCode && string.IsNullOrEmpty(pair.Locale.CountryCode))
                {
                    score = 3;
                }
                else if (pair.Locale.LanguageCode == targetLocale.LanguageCode)
                {
                    score = 2;
                }
                else if (pair.Locale == LocalizationSettings.DefaultLocale)
                {
                    score = 1;
                }
                else
                {
                    score = 0;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    result = pair;

                    if (score == perfectScore) break; // Perfect match, no need to continue searching
                }
            }

            if (result == null)
                return false;

            var value = result.Value;

            if (
                value == null ||
                value.Equals(default(T)) ||
                value is string str && string.IsNullOrEmpty(str)
            )
            {
                return false;
            }

            return true;
        }

        public T Resolve(LocalizationLocale locale)
        {
            if (!TryFindBestMatchValue(locale, out var bestMatch))
            {
                //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
                if (Application.isPlaying)
                    LogWarning($"No localization found for locale \"{(locale != null ? locale.DisplayCode : null)}\". Returning default value.");

                return NonNullFallbackValue.ForType<T>();
            }

            return bestMatch.Value;
        }

        public override bool HasMeaningfulValueForLocale(LocalizationLocale locale)
        {
            if (!TryFindValue(locale, out var bestMatch))
                return false;

            return IsMeaningfulValue(bestMatch.Value);
        }

        protected virtual bool IsMeaningfulValue(T value)
        {
            if (value == null) return false;
            if (value.Equals(default(T))) return false;
            if (value is string str && string.IsNullOrEmpty(str)) return false;

            return true;
        }
    }
}