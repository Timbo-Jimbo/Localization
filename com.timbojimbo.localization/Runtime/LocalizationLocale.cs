using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization
{
    /// <summary>
    /// A locale's metadata: its <see cref="Code"/>, native name and culture. Lives in <see cref="LocalizationSettings"/>,
    /// the one registry. Localized values never reference it; they key by <see cref="Code"/>. Compare two locales with
    /// <c>a.Code == b.Code</c>, never by object: a content build may carry its own copy of a locale.
    /// </summary>
    public class LocalizationLocale : ScriptableObject
    {
        [SerializeField] private string _languageCode;
        [SerializeField] private string _countryCode;
        [SerializeField] private string _nativeName;

        private LocaleCode? _cachedCode;
        private CultureInfo _cachedCultureInfo;

        public LocaleCode Code => _cachedCode ??= new LocaleCode(_languageCode, _countryCode);
        public string LanguageCode => Code.Language;
        public string CountryCode => Code.Country;
        public string NativeName => _nativeName;

        /// <summary>The code as text: <c>en</c> or <c>en-AU</c>.</summary>
        public string DisplayCode => Code.ToString();

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
                    Debug.LogWarning($"Culture not found for locale {DisplayCode}, falling back to invariant culture.", this);
                    _cachedCultureInfo = CultureInfo.InvariantCulture;
                }

                return _cachedCultureInfo;
            }
        }

        public bool IsDefaultLocale => LocalizationSettings.DefaultLocale != null && LocalizationSettings.DefaultLocale.Code == Code;

        private void OnValidate()
        {
            _cachedCode = null;
            _cachedCultureInfo = null;

            var code = new LocaleCode(_languageCode, _countryCode);
            _languageCode = code.Language;
            _countryCode = code.Country;
        }
    }
}
