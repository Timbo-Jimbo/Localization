using System;
using Cysharp.Text;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public interface ILocalizedStringResolver : ILocalizedValueResolver<string>
    {
        string Resolve<T1>(LocaleCode code, T1 param1);
        string Resolve<T1, T2>(LocaleCode code, T1 param1, T2 param2);
        string Resolve<T1, T2, T3>(LocaleCode code, T1 param1, T2 param2, T3 param3);
        string Resolve<T1, T2, T3, T4>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4);
        string Resolve<T1, T2, T3, T4, T5>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5);
    }

    [CreateAssetMenu(fileName = "New Localized String", menuName = "Localization/Localized String")]
    public class LocalizedString : LocalizedValue<string>, ILocalizedStringResolver
    {
        protected override bool IsMeaningfulValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        public string Resolve<T1>(LocaleCode code, T1 param1)
        {
            if (!TryFindBestMatchValue(code, out var bestMatch))
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1);
                }
                catch (Exception ex)
                {
                    LogFormatError(1, code, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2>(LocaleCode code, T1 param1, T2 param2)
        {
            if (!TryFindBestMatchValue(code, out var bestMatch))
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2);
                }
                catch (Exception ex)
                {
                    LogFormatError(2, code, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3>(LocaleCode code, T1 param1, T2 param2, T3 param3)
        {
            if (!TryFindBestMatchValue(code, out var bestMatch))
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3);
                }
                catch (Exception ex)
                {
                    LogFormatError(3, code, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (!TryFindBestMatchValue(code, out var bestMatch))
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3, param4);
                }
                catch (Exception ex)
                {
                    LogFormatError(4, code, ex);
                    return bestMatch.Value;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4, T5>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            if (!TryFindBestMatchValue(code, out var bestMatch))
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(bestMatch.Value, param1, param2, param3, param4, param5);
                }
                catch (Exception ex)
                {
                    LogFormatError(5, code, ex);
                    return bestMatch.Value;
                }
            }
        }

        private void LogFormatError(int paramCount, LocaleCode code, Exception ex)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;

            LogException(ex, $"Format error when resolving locale \"{code}\" with {paramCount} parameters");
        }
    }
}
