using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization
{
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "Localization/Settings and Config/Localization Settings")]
    public sealed class LocalizationSettings : ScriptableObject
    {
        private static LocalizationSettings ActiveInstance;

        [SerializeField] private List<LocalizationLocale> _locales = new();
        [SerializeField] private LocalizationLocale _defaultLocale;

        public static event Action<LocalizationLocale> OnActiveLocaleChanged;
        public static event Action<LocalizedValue> OnLocalizedValueChanged;
        private static List<ILocalizationChangeListener> _listeners = new();
        public static bool IsInitialized => ActiveInstance != null;
        public static LocalizationSettings ActiveAsset => ActiveInstance;
        public static IReadOnlyList<LocalizationLocale> Locales => ActiveInstance != null ? ActiveInstance._locales : Array.Empty<LocalizationLocale>();
        public static LocalizationLocale DefaultLocale => ActiveInstance != null ? ActiveInstance._defaultLocale : null;
        public static LocalizationLocale ActiveLocale => _active != null ? _active : DefaultLocale;
        private static LocalizationLocale _active;
        private static string ActiveLocalePrefKey => $"{nameof(LocalizationSettings)}.{nameof(ActiveLocale)}";

        public static void SetActiveLocale(LocalizationLocale locale)
        {
            if (_active == locale) return;
            _active = locale;

            PlayerPrefs.SetString(ActiveLocalePrefKey, _active?.DisplayCode);
            PlayerPrefs.Save();

            OnActiveLocaleChanged?.Invoke(ActiveLocale);

            foreach (var listener in _listeners)
            {
                // Handle destroyed Unity objects that still exist in the list
                if (listener is UnityEngine.Object unityObj && unityObj == null)
                    continue;

                listener.OnLocaleChanged(ActiveLocale);
            }
        }

        public static void ClearActiveLocale()
        {
            SetActiveLocale(null);
        }

        public static LocalizationLocale GetBestMatchForDeviceLocale(IReadOnlyList<LocalizationLocale> locales = null)
        {
            if (TryGetBestMatchForDeviceLocale(out var locale, locales))
                return locale;

            return DefaultLocale ?? (locales != null && locales.Count > 0 ? locales[0] : null) ?? (Locales.Count > 0 ? Locales[0] : null);
        }

        public static bool TryGetBestMatchForDeviceLocale(out LocalizationLocale locale, IReadOnlyList<LocalizationLocale> locales = null)
        {
            locale = null;
            locales ??= Locales;

            if (locales == null || locales.Count == 0)
                return false;

            var deviceCulture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
            var deviceLanguageCode = deviceCulture?.TwoLetterISOLanguageName?.ToLowerInvariant().Trim();
            var deviceCountryCode = GetCountryCode(deviceCulture);

            if (string.IsNullOrEmpty(deviceLanguageCode) && Application.systemLanguage != SystemLanguage.Unknown)
            {
                deviceLanguageCode = GetLanguageCodeFromSystemLanguage(Application.systemLanguage);
            }

            int bestScore = -1;
            LocalizationLocale bestMatch = null;

            foreach (var candidate in locales)
            {
                if (candidate == null)
                    continue;

                int score = GetDeviceLocaleMatchScore(candidate, deviceLanguageCode, deviceCountryCode);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestMatch = candidate;

                if (score == 4)
                    break;
            }

            if (bestMatch != null)
            {
                locale = bestMatch;
                return true;
            }

            if (DefaultLocale != null)
            {
                locale = DefaultLocale;
                return true;
            }

            locale = locales.Count > 0 ? locales[0] : null;
            return locale != null;
        }

        private static int GetDeviceLocaleMatchScore(LocalizationLocale candidate, string deviceLanguageCode, string deviceCountryCode)
        {
            const int perfectMatch = 4;
            const int languageOnlyMatch = 2;
            const int languageWithDefaultCountryMatch = 3;
            const int defaultFallback = 1;

            if (candidate == null)
                return 0;

            if (string.IsNullOrEmpty(deviceLanguageCode))
                return candidate == DefaultLocale ? defaultFallback : 0;

            if (!string.Equals(candidate.LanguageCode, deviceLanguageCode, StringComparison.OrdinalIgnoreCase))
                return candidate == DefaultLocale ? defaultFallback : 0;

            if (!string.IsNullOrEmpty(deviceCountryCode) && string.Equals(candidate.CountryCode, deviceCountryCode, StringComparison.OrdinalIgnoreCase))
                return perfectMatch;

            if (string.IsNullOrEmpty(candidate.CountryCode))
                return languageWithDefaultCountryMatch;

            return languageOnlyMatch;
        }

        private static string GetCountryCode(CultureInfo culture)
        {
            if (culture == null)
                return string.Empty;

            var name = culture.Name;
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var separatorIndex = name.IndexOfAny(new[] { '-', '_' });
            if (separatorIndex < 0 || separatorIndex >= name.Length - 1)
                return string.Empty;

            return name.Substring(separatorIndex + 1).ToUpperInvariant();
        }

        private static string GetLanguageCodeFromSystemLanguage(SystemLanguage systemLanguage)
        {
            return systemLanguage switch
            {
                SystemLanguage.Afrikaans => "af",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Basque => "eu",
                SystemLanguage.Belarusian => "be",
                SystemLanguage.Bulgarian => "bg",
                SystemLanguage.Catalan => "ca",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Danish => "da",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.English => "en",
                SystemLanguage.Estonian => "et",
                SystemLanguage.Faroese => "fo",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hebrew => "he",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Icelandic => "is",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Italian => "it",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Latvian => "lv",
                SystemLanguage.Lithuanian => "lt",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Russian => "ru",
                SystemLanguage.SerboCroatian => "sr",
                SystemLanguage.Slovak => "sk",
                SystemLanguage.Slovenian => "sl",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Thai => "th",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Vietnamese => "vi",
                SystemLanguage.ChineseSimplified => "zh",
                SystemLanguage.ChineseTraditional => "zh",
                SystemLanguage.Unknown => string.Empty,
                _ => string.Empty,
            };
        }

        public static void RaiseLocalizedValueChanged(LocalizedValue localizedString)
        {
            try
            {
                OnLocalizedValueChanged?.Invoke(localizedString);

                foreach (var listener in _listeners)
                {
                    // Handle destroyed Unity objects that still exist in the list
                    if (listener is UnityEngine.Object unityObj && unityObj == null)
                        continue;

                    listener.OnLocalizedValueChanged(localizedString);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public static void AddListener(ILocalizationChangeListener listener)
        {
            _listeners.Add(listener);
        }

        public static void RemoveListener(ILocalizationChangeListener listener)
        {
            _listeners.Remove(listener);
        }

        public IReadOnlyList<LocalizationLocale> GetLocales() => _locales;

        private void OnDisable()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            var savedLocaleCode = PlayerPrefs.GetString(ActiveLocalePrefKey, null);
            var localeLoaded = false;
            
            if (!string.IsNullOrEmpty(savedLocaleCode))
            {
                var savedLocale = _locales.Find(l => string.Equals(l.DisplayCode, savedLocaleCode, StringComparison.OrdinalIgnoreCase));
                if (savedLocale != null)
                {
                    SetActiveLocale(savedLocale);
                    localeLoaded = true;
                }
            }

            if (!localeLoaded)
            {
                var bestMatch = GetBestMatchForDeviceLocale(_locales);
                if (bestMatch != null)
                {
                    SetActiveLocale(bestMatch);
                    localeLoaded = true;
                }
            }
        }
    }
}