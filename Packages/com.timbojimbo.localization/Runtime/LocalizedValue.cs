using System;
using System.Collections.Generic;
using TimboJimbo.Localization.Utility;
using UnityEngine;
using UnityEngine.Pool;

namespace TimboJimbo.Localization
{
    public abstract class LocalizedValue : ScriptableObject
    {
        internal abstract void SyncWithProjectLocales();

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
        internal override void SyncWithProjectLocales()
        {
            if(LocalizationSettings.ActiveAsset == null) return;

            using(ListPool<LocalizationLocale>.Get(out var projectLocales))
            {
                projectLocales.AddRange(LocalizationSettings.Locales);
                    
                var changed = false;

                foreach(var locale in LocalizationSettings.Locales)
                {
                    if(locale == null) continue;

                    if (!LocaleValuePair.TryFind(Values, locale, out _))
                    {
                        Values.Add(new LocaleValuePair<T>()
                        {
                            Locale = locale,
                            Value = NonNullFallbackValue.ForType<T>()
                        });
                        changed = true;
                    }
                }

                // ensure same order as LocalizationSettings.Locales
                for(int i = 0; i < projectLocales.Count; i++)
                {
                    var locale = projectLocales[i];
                    if(locale == null) continue;

                    if (LocaleValuePair.TryFind(Values, locale, out var pair))
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

                if(changed)
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this);
                    #endif
                }
            }
        }

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