using System;
using System.Collections.Generic;
using TimboJimbo.Localization;
using UnityEngine;
using UnityEngine.Pool;

namespace TimboJimboEditor.Localization.Translations
{
    [CreateAssetMenu(fileName = "New Translation Glossary", menuName = "Localization/Settings and Config/Translation Glossary")]
    public class TranslationGlossary : ScriptableObject
    {
        [SerializeField]
        private List<LocalizedString> _terms = new();

        public IReadOnlyList<LocalizedString> Terms => _terms;

        /// <summary>
        /// Builds a filtered view of the glossary containing only the requested locales.
        /// The default locale (typically English) is always included so LLMs always have
        /// a canonical source term to translate from.
        /// </summary>
        /// <param name="locales">Locales to include. Null or empty returns only the default locale.</param>
        /// <param name="results">List to populate. Cleared before use.</param>
        public void GetFiltered(IEnumerable<LocalizationLocale> locales, List<FilteredEntry> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            var defaultLocale = LocalizationSettings.DefaultLocale;

            var includedLocales = HashSetPool<LocalizationLocale>.Get();
            try
            {
                if (defaultLocale != null)
                    includedLocales.Add(defaultLocale);

                if (locales != null)
                {
                    foreach (var locale in locales)
                    {
                        if (locale != null)
                            includedLocales.Add(locale);
                    }
                }

                for (int i = 0; i < _terms.Count; i++)
                {
                    var entry = _terms[i];
                    if (entry == null) continue;

                    var localizedValues = new List<LocaleValuePair<string>>();
                    var termValues = entry.Values;
                    for (int j = 0; j < termValues.Count; j++)
                    {
                        var pair = termValues[j];
                        if (pair == null || pair.Locale == null) continue;
                        if (includedLocales.Contains(pair.Locale))
                            localizedValues.Add(pair);
                    }

                    results.Add(new FilteredEntry(entry, localizedValues));
                }
            }
            finally
            {
                HashSetPool<LocalizationLocale>.Release(includedLocales);
            }
        }

        public readonly struct FilteredEntry
        {
            public readonly LocalizedString Term;
            public readonly IReadOnlyList<LocaleValuePair<string>> Values;

            public FilteredEntry(LocalizedString term, IReadOnlyList<LocaleValuePair<string>> values)
            {
                Term = term;
                Values = values;
            }
        }
    }
}