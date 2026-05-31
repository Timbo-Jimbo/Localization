using System;

namespace TimboJimbo.Localization
{
    [Serializable]
    public class LocaleValuePair<T>
    {
        public LocalizationLocale Locale;
        public T Value;
    }
}