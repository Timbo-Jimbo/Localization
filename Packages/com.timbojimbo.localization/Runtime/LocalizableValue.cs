using TimboJimbo.Localization.Debugging;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public abstract class LocalizableValue<TContainer, TValue> : ILocalizedValueResolver<TValue>
        where TContainer : ILocalizedValueResolver<TValue>
    {
        public TContainer Localized;
        public TValue Unlocalized;
        public bool IsLocalized => Localized != null;

        [SerializeField]
        protected UsageContext UsageContext;

        public TValue Resolve(LocalizationLocale locale)
        {
            if (IsLocalized)
                return Localized.Resolve(locale);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<TValue>();
            }

            return Unlocalized;
        }

        protected void LogLocalizationMissingWarn(LocalizationLocale locale)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;

            UsageContext.LogWarning($"No localization found for locale {locale.DisplayCode}. Returning unlocalized value.");
        }
    }
}