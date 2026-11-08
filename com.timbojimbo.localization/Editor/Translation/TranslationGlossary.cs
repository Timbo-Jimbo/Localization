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
        /// <param name="codes">Locale codes to include. Null or empty returns only the default locale.</param>
        /// <param name="results">List to populate. Cleared before use.</param>
        public void GetFiltered(IEnumerable<LocaleCode> codes, List<FilteredEntry> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            var includedCodes = HashSetPool<LocaleCode>.Get();
            try
            {
                var defaultCode = LocalizationSettings.DefaultCode;
                if (!defaultCode.IsNone)
                    includedCodes.Add(defaultCode);

                if (codes != null)
                {
                    foreach (var code in codes)
                    {
                        if (!code.IsNone)
                            includedCodes.Add(code);
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
                        if (pair == null || pair.Code.IsNone) continue;
                        if (includedCodes.Contains(pair.Code))
                            localizedValues.Add(pair);
                    }

                    results.Add(new FilteredEntry(entry, localizedValues));
                }
            }
            finally
            {
                HashSetPool<LocaleCode>.Release(includedCodes);
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