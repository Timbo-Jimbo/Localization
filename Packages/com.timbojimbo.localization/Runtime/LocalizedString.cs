using System;
using Cysharp.Text;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public interface ILocalizedStringResolver : ILocalizedValueResolver<string>
    {
        string Resolve<T1>(LocalizationLocale locale, T1 param1);
        string Resolve<T1, T2>(LocalizationLocale locale, T1 param1, T2 param2);
        string Resolve<T1, T2, T3>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3);
        string Resolve<T1, T2, T3, T4>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4);
        string Resolve<T1, T2, T3, T4, T5>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5);
    }

    [CreateAssetMenu(fileName = "New Localized String", menuName = "Localization/Localized String")]
    public class LocalizedString : LocalizedValue<string>, ILocalizedStringResolver
    {
        public string Resolve<T1>(LocalizationLocale locale, T1 param1)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }
            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1);
                }
                catch (Exception ex)
                {
                    LogFormatError(1, locale, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2>(LocalizationLocale locale, T1 param1, T2 param2)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2);
                }
                catch (Exception ex)
                {
                    LogFormatError(2, locale, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3);
                }
                catch (Exception ex)
                {
                    LogFormatError(3, locale, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3, param4);
                }
                catch (Exception ex)
                {
                    LogFormatError(4, locale, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4, T5>(LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            if (!LocaleValuePair.TryFindBestMatch(Values, locale, out var bestMatch))
            {
                LogLocalizationMissingWarn(locale);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(locale.CultureInfo))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3, param4, param5);
                }
                catch (Exception ex)
                {
                    LogFormatError(5, locale, ex);
                    return bestMatch.Value;
                }
            }
        }

        private void LogLocalizationMissingWarn(LocalizationLocale locale)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;

            LogWarning($"No localization found for locale \"{(locale != null ? locale.DisplayCode : null)}\". Returning empty string.");
        }

        private void LogFormatError(int paramCount, LocalizationLocale locale, Exception ex)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;

            LogException(ex, $"Format error when resolving locale \"{(locale != null ? locale.DisplayCode : null)}\" with {paramCount} parameters");
        }
    }
}