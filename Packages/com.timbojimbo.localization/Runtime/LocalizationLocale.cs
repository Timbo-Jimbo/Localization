using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public class LocalizationLocale : ScriptableObject
    {
        public string LanguageCode => _languageCode;
        public string CountryCode => _countryCode;
        public string NativeName => _nativeName;
        public string DisplayCode
        {
            get
            {
                if (_cachedDisplayCode != null) return _cachedDisplayCode;

                if (string.IsNullOrEmpty(CountryCode))
                    _cachedDisplayCode = LanguageCode;
                else
                    _cachedDisplayCode = $"{LanguageCode}-{CountryCode}";

                return _cachedDisplayCode;
            }
        }

        public CultureInfo CultureInfo
        {
            get
            {
                if (_cachedCultureInfo != null) return _cachedCultureInfo;

                try
                {
                    _cachedCultureInfo = CultureInfo.GetCultureInfo(DisplayCode);
                }
                catch (CultureNotFoundException)
                {
                    Debug.LogWarning($"Culture not found for locale {DisplayCode}, falling back to invariant culture.");
                    _cachedCultureInfo = CultureInfo.InvariantCulture;
                }

                return _cachedCultureInfo;
            }
        }

        public bool IsDefaultLocale => LocalizationSettings.DefaultLocale == this;

        [SerializeField] private string _languageCode;
        [SerializeField] private string _countryCode;
        [SerializeField] private string _nativeName;

        private string _cachedDisplayCode;
        private CultureInfo _cachedCultureInfo;

        void OnValidate()
        {
            // Invalidate cached display code when language or country code changes
            _cachedDisplayCode = null;
            _cachedCultureInfo = null;
            _languageCode = _languageCode?.ToLowerInvariant().Trim();
            _countryCode = _countryCode?.ToUpperInvariant().Trim();
        }
    }
}