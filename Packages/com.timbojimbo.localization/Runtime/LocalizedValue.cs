using System;
using System.Collections.Generic;
using Cysharp.Text;
using TimboJimbo.Localization.Debugging;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public abstract class LocalizedValue : ScriptableObject
    {
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

        public T Resolve(LocalizationLocale locale)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
                if (Application.isPlaying)
                    LogWarning($"No localization found for locale \"{(locale != null ? locale.DisplayCode : null)}\". Returning default value.");

                return NonNullFallbackValue.ForType<T>();
            }

            return bestMatch.Value;
        }
    }
}