using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization
{
    /// <summary>
    /// The one locale registry: the project's locale assets (metadata) and the active one. Ships with the player as a
    /// preloaded asset and registers itself on load; a game that loads it another way calls <see cref="Register"/>.
    /// Localized values key their entries by <see cref="LocaleCode"/> and never reference this asset or its locales.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "Localization/Settings and Config/Localization Settings")]
    public sealed class LocalizationSettings : ScriptableObject
    {
        private static LocalizationSettings ActiveInstance;

        [SerializeField] private List<LocalizationLocale> _locales = new();
        [SerializeField] private LocalizationLocale _defaultLocale;

        public static event Action<LocalizationLocale> OnActiveLocaleChanged;
        public static event Action<LocalizedValue> OnLocalizedValueChanged;
        private static readonly List<ILocalizationChangeListener> _listeners = new();
        private static readonly Dictionary<LocaleCode, CultureInfo> _culturesByCode = new();

        public static bool IsInitialized => ActiveInstance != null;
        public static LocalizationSettings ActiveAsset => ActiveInstance;
        public static IReadOnlyList<LocalizationLocale> Locales => ActiveInstance != null ? ActiveInstance._locales : Array.Empty<LocalizationLocale>();
        public static LocalizationLocale DefaultLocale => ActiveInstance != null ? ActiveInstance._defaultLocale : null;
        public static LocalizationLocale ActiveLocale => _active != null ? _active : DefaultLocale;
        public static LocaleCode ActiveCode => LocaleCode.Of(ActiveLocale);
        public static LocaleCode DefaultCode => LocaleCode.Of(DefaultLocale);
        private static LocalizationLocale _active;

        // The device's UI culture, as it was before the active locale's culture took its place: what the best match for
        // the device's locale is worked out from, and what comes back with no locale at all.
        private static CultureInfo _deviceUICulture;
        private static string ActiveLocalePrefKey => $"{nameof(LocalizationSettings)}.{nameof(ActiveLocale)}";

        /// <summary>
        /// Makes <paramref name="settings"/> the registry, replacing any other, and picks its active locale (the saved
        /// one, else the best match for the device). A preloaded settings asset registers itself; call this for one loaded
        /// another way (Addressables, a bundle, Resources).
        /// </summary>
        public static void Register(LocalizationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            ActiveInstance = settings;
            _active = null;
            settings.SelectInitialLocale();
        }

        /// <summary>The registered locale with this code, or null when the settings don't list it.</summary>
        public static LocalizationLocale Find(LocaleCode code)
        {
            if (ActiveInstance == null || code.IsNone) return null;

            var locales = ActiveInstance._locales;
            for (int i = 0; i < locales.Count; i++)
            {
                var locale = locales[i];
                if (locale != null && locale.Code == code)
                    return locale;
            }

            return null;
        }

        public static bool TryFind(LocaleCode code, out LocalizationLocale locale)
        {
            locale = Find(code);
            return locale != null;
        }

        /// <summary>
        /// The culture for a code: the registered locale's, else the culture of that name, else the invariant culture.
        /// </summary>
        public static CultureInfo GetCulture(LocaleCode code)
        {
            if (code.IsNone) return CultureInfo.InvariantCulture;

            var locale = Find(code);
            if (locale != null) return locale.CultureInfo;

            if (_culturesByCode.TryGetValue(code, out var culture)) return culture;

            try
            {
                culture = CultureInfo.GetCultureInfo(code.ToString());
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.InvariantCulture;
            }

            _culturesByCode[code] = culture;
            return culture;
        }

        public static void SetActiveLocale(LocalizationLocale locale)
        {
            if (_active == locale) return;
            _active = locale;
            ApplyUICulture();

            PlayerPrefs.SetString(ActiveLocalePrefKey, _active != null ? _active.DisplayCode : null);
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

        /// <summary>Activates the registered locale with this code; false, and nothing changes, when the settings don't list it.</summary>
        public static bool SetActiveLocale(LocaleCode code)
        {
            if (!TryFind(code, out var locale)) return false;
            SetActiveLocale(locale);
            return true;
        }

        public static void ClearActiveLocale()
        {
            SetActiveLocale(null);
        }

        // The active locale's culture becomes the current UI culture, so whatever follows the UI language (which way the
        // UI reads, resource lookups) follows the active locale; with no locale at all, the device's comes back.
        private static void ApplyUICulture()
        {
            _deviceUICulture ??= CultureInfo.CurrentUICulture;
            var active = ActiveLocale;
            var culture = active != null ? active.CultureInfo : _deviceUICulture;
            if (culture != null && CultureInfo.CurrentUICulture.Name != culture.Name)
                CultureInfo.CurrentUICulture = culture;
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

            var deviceCode = GetDeviceCode();
            var defaultCode = DefaultCode;

            int bestScore = -1;
            LocalizationLocale bestMatch = null;

            foreach (var candidate in locales)
            {
                if (candidate == null)
                    continue;

                int score = GetDeviceLocaleMatchScore(candidate.Code, deviceCode, defaultCode);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestMatch = candidate;

                if (score == PerfectMatch)
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

        /// <summary>The device's locale code: from its UI culture (as it was before any locale was activated), else the system language.</summary>
        public static LocaleCode GetDeviceCode()
        {
            // The device's, not the active locale's, which is the current UI culture once a locale is active.
            var deviceCulture = _deviceUICulture ?? CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
            var code = LocaleCode.Of(deviceCulture);

            if (code.IsNone && Application.systemLanguage != SystemLanguage.Unknown)
                code = new LocaleCode(GetLanguageCodeFromSystemLanguage(Application.systemLanguage));

            return code;
        }

        private const int PerfectMatch = 4;
        private const int LanguageWithDefaultCountryMatch = 3;
        private const int LanguageOnlyMatch = 2;
        private const int DefaultFallback = 1;

        private static int GetDeviceLocaleMatchScore(LocaleCode candidate, LocaleCode device, LocaleCode defaultCode)
        {
            bool isDefault = !defaultCode.IsNone && candidate == defaultCode;

            if (device.IsNone)
                return isDefault ? DefaultFallback : 0;

            if (!string.Equals(candidate.Language, device.Language, StringComparison.OrdinalIgnoreCase))
                return isDefault ? DefaultFallback : 0;

            if (device.HasCountry && string.Equals(candidate.Country, device.Country, StringComparison.OrdinalIgnoreCase))
                return PerfectMatch;

            if (!candidate.HasCountry)
                return LanguageWithDefaultCountryMatch;

            return LanguageOnlyMatch;
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

        public static void RaiseLocalizedValueChanged(LocalizedValue localizedValue)
        {
            try
            {
                OnLocalizedValueChanged?.Invoke(localizedValue);

                foreach (var listener in _listeners)
                {
                    // Handle destroyed Unity objects that still exist in the list
                    if (listener is UnityEngine.Object unityObj && unityObj == null)
                        continue;

                    listener.OnLocalizedValueChanged(localizedValue);
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
            // The first settings asset to load is the registry. A later one (a copy in a content build, a second asset
            // in the project) doesn't take over; a game that means to switch calls Register.
            if (ActiveInstance != null && ActiveInstance != this) return;

            Register(this);
        }

        private void SelectInitialLocale()
        {
            var savedCode = LocaleCode.Parse(PlayerPrefs.GetString(ActiveLocalePrefKey, null));
            if (!savedCode.IsNone && TryFind(savedCode, out var savedLocale))
            {
                SetActiveLocale(savedLocale);
                return;
            }

            var bestMatch = GetBestMatchForDeviceLocale(_locales);
            if (bestMatch != null)
                SetActiveLocale(bestMatch);
        }
    }
}
