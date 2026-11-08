using System;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization
{
    /// <summary>
    /// A locale's identity: a language (<c>en</c>) with an optional country (<c>en-AU</c>). Localized values key their
    /// entries by code, so an asset never references a locale object and two copies of a locale (the player's and a
    /// content build's) are the same locale. Compare locales with <c>a.Code == b.Code</c>, never by object.
    /// </summary>
    [Serializable]
    public struct LocaleCode : IEquatable<LocaleCode>
    {
        public static readonly LocaleCode None = default;

        [SerializeField] private string _language;
        [SerializeField] private string _country;

        public LocaleCode(string language, string country = null)
        {
            _language = NormalizeLanguage(language);
            _country = NormalizeCountry(country);
        }

        /// <summary>The lower-case language part (<c>en</c>); empty for <see cref="None"/>.</summary>
        public string Language => _language ?? string.Empty;

        /// <summary>The upper-case country part (<c>AU</c>); empty when the code has none.</summary>
        public string Country => _country ?? string.Empty;

        public bool IsNone => string.IsNullOrEmpty(_language);
        public bool HasCountry => !string.IsNullOrEmpty(_country);

        /// <summary>This code without its country: <c>en-AU</c> becomes <c>en</c>.</summary>
        public LocaleCode LanguageOnly => new(_language);

        /// <summary>The code of a locale asset, or <see cref="None"/> for a null locale.</summary>
        public static LocaleCode Of(LocalizationLocale locale) => locale != null ? locale.Code : None;

        /// <summary>The code of a culture: <c>en-AU</c> for the culture named <c>en-AU</c>; <see cref="None"/> for the invariant culture.</summary>
        public static LocaleCode Of(CultureInfo culture)
        {
            if (culture == null || string.IsNullOrEmpty(culture.Name)) return None;
            return Parse(culture.Name);
        }

        /// <summary>Parses <c>en</c>, <c>en-AU</c> or <c>en_AU</c>, in any case. Anything unparseable is <see cref="None"/>.</summary>
        public static LocaleCode Parse(string text)
        {
            TryParse(text, out var code);
            return code;
        }

        public static bool TryParse(string text, out LocaleCode code)
        {
            code = None;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var trimmed = text.Trim();
            var separator = trimmed.IndexOfAny(Separators);
            code = separator < 0
                ? new LocaleCode(trimmed)
                : new LocaleCode(trimmed.Substring(0, separator), trimmed.Substring(separator + 1));

            return !code.IsNone;
        }

        public bool Equals(LocaleCode other)
        {
            return string.Equals(Language, other.Language, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Country, other.Country, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => obj is LocaleCode other && Equals(other);

        public override int GetHashCode()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            unchecked
            {
                return comparer.GetHashCode(Language) * 397 ^ comparer.GetHashCode(Country);
            }
        }

        public static bool operator ==(LocaleCode left, LocaleCode right) => left.Equals(right);
        public static bool operator !=(LocaleCode left, LocaleCode right) => !left.Equals(right);

        /// <summary><c>en</c> or <c>en-AU</c>; empty for <see cref="None"/>.</summary>
        public override string ToString() => HasCountry ? $"{Language}-{Country}" : Language;

        private static readonly char[] Separators = { '-', '_' };

        private static string NormalizeLanguage(string language)
        {
            return string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();
        }

        private static string NormalizeCountry(string country)
        {
            return string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        }
    }
}
