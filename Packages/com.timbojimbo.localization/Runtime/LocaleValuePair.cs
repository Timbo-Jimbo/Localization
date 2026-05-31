using System;
using System.Collections.Generic;

namespace TimboJimbo.Localization
{
    [Serializable]
    public class LocaleValuePair<T>
    {
        public LocalizationLocale Locale;
        public T Value;
    }

    internal static class LocaleValuePair
    {
        public static bool TryFind<T>(List<LocaleValuePair<T>> list, LocalizationLocale targetLocale, out LocaleValuePair<T> result)
        {
            result = default;

            if (list == null || list.Count == 0) return false;

            for (int i = 0; i < list.Count; i++)
            {
                LocaleValuePair<T> pair = list[i];

                if (pair == null || pair.Locale == null) continue;

                if (pair.Locale.Equals(targetLocale))
                {
                    result = pair;
                    return true;
                }
            }

            return false;
        }
        
        public static bool TryFindBestMatch<T>(List<LocaleValuePair<T>> list, LocalizationLocale targetLocale, out LocaleValuePair<T> result)
        {
            result = default;

            if (list == null || list.Count == 0) return false;

            var bestScore = -1;

            for (int i = 0; i < list.Count; i++)
            {
                LocaleValuePair<T> pair = list[i];

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
    }
}