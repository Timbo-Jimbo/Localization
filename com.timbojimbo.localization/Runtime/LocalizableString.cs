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
        public string Resolve<T1>(LocaleCode code, T1 param1)
        {
            if (IsLocalized)
                return Localized.Resolve(code, param1);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 1, code, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2>(LocaleCode code, T1 param1, T2 param2)
        {
            if (IsLocalized)
                return Localized.Resolve(code, param1, param2);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 2, code, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3>(LocaleCode code, T1 param1, T2 param2, T3 param3)
        {
            if (IsLocalized)
                return Localized.Resolve(code, param1, param2, param3);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 3, code, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4)
        {
            if (IsLocalized)
                return Localized.Resolve(code, param1, param2, param3, param4);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3, param4);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 4, code, ex);
                    return Unlocalized;
                }
            }
        }

        public string Resolve<T1, T2, T3, T4, T5>(LocaleCode code, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            if (IsLocalized)
                return Localized.Resolve(code, param1, param2, param3, param4, param5);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<string>();
            }

            using (CultureOverrideBlock.Auto(LocalizationSettings.GetCulture(code)))
            {
                try
                {
                    return ZString.Format(Unlocalized, param1, param2, param3, param4, param5);
                }
                catch (Exception ex)
                {
                    LogFormatError(UsageContext, 5, code, ex);
                    return Unlocalized;
                }
            }
        }

        private static void LogFormatError(UsageContext usageContext, int paramCount, LocaleCode code, Exception ex)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;
            usageContext.LogException(ex, $"Failed to format unlocalized value with {paramCount} params and locale \"{code}\". Returning unlocalized value.");
        }
    }
}
