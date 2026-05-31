using System;
using Cysharp.Text;
using TimboJimbo.Localization.Debugging;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    [Serializable]
    public class LocalizableString : LocalizableValue<LocalizedString, string>, ILocalizedStringResolver
    {
        public string Resolve<T1>(LocalizationLocale locale, T1 param1)
        {
            if (IsLocalized)
                return Localized.Resolve(locale, param1);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 1, locale, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2>(LocalizationLocale locale, T1 param1, T2 param2)
        {
            if (IsLocalized)
                return Localized.Resolve(locale, param1, param2);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 2, locale, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3)
        {
            if (IsLocalized)
                return Localized.Resolve(locale, param1, param2, param3);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 3, locale, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (IsLocalized)
                return Localized.Resolve(locale, param1, param2, param3, param4);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3, param4);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 4, locale, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4, T5>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            if (IsLocalized)
                return Localized.Resolve(locale, param1, param2, param3, param4, param5);

            if (Unlocalized == null)
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3, param4, param5);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 5, locale, ex);
                    return Unlocalized;
                }
            }
        }

        private static void LogFormatError(UsageContext usageContext, int paramCount, LocalizationLocale locale, Exception ex)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;
            usageContext.LogException(ex, $"Failed to format unlocalized value with {paramCount} params and locale {locale.DisplayCode}. Returning unlocalized value.");
        }
    }
}