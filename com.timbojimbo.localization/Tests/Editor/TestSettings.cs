using System;
using System.Collections.Generic;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboTests.Localization
{
    /// <summary>
    /// An in-memory <see cref="LocalizationSettings"/> registered for the length of a test; disposing restores the
    /// project's own settings as the registry.
    /// </summary>
    internal sealed class TestSettings : IDisposable
    {
        private readonly LocalizationSettings _previous;
        private readonly List<UnityEngine.Object> _created = new();

        public LocalizationSettings Settings { get; }
        public LocalizationLocale Default { get; }
        public IReadOnlyList<LocalizationLocale> Locales { get; }

        /// <summary>Settings with one locale per code; the first is the default.</summary>
        public TestSettings(params string[] codes)
        {
            _previous = LocalizationSettings.ActiveAsset;

            Settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            Settings.name = "TestLocalizationSettings";
            _created.Add(Settings);

            var locales = new List<LocalizationLocale>();
            foreach (var code in codes)
                locales.Add(CreateLocale(LocaleCode.Parse(code)));

            Locales = locales;
            Default = locales.Count > 0 ? locales[0] : null;

            using (var serialized = new SerializedObject(Settings))
            {
                var localesProperty = serialized.FindProperty("_locales");
                localesProperty.arraySize = locales.Count;
                for (int i = 0; i < locales.Count; i++)
                    localesProperty.GetArrayElementAtIndex(i).objectReferenceValue = locales[i];

                serialized.FindProperty("_defaultLocale").objectReferenceValue = Default;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            LocalizationSettings.Register(Settings);
        }

        /// <summary>A locale asset for a code, outside the settings: a stand-in for a content build's copy.</summary>
        public LocalizationLocale CreateLocale(LocaleCode code, string nativeName = null)
        {
            var locale = ScriptableObject.CreateInstance<LocalizationLocale>();
            locale.name = nativeName ?? code.ToString();
            _created.Add(locale);

            using var serialized = new SerializedObject(locale);
            serialized.FindProperty("_languageCode").stringValue = code.Language;
            serialized.FindProperty("_countryCode").stringValue = code.Country;
            serialized.FindProperty("_nativeName").stringValue = nativeName ?? code.ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return locale;
        }

        public void Track(UnityEngine.Object created) => _created.Add(created);

        public void Dispose()
        {
            if (_previous != null)
                LocalizationSettings.Register(_previous);

            foreach (var created in _created)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            if (_previous == null && LocalizationSettings.ActiveAsset == Settings)
                LocalizationSettings.ClearActiveLocale();
        }
    }

    internal static class Strings
    {
        /// <summary>A string asset with the given entries, in order; a null value is an empty entry.</summary>
        public static LocalizedString Create(TestSettings settings, params (string code, string value)[] entries)
        {
            var asset = ScriptableObject.CreateInstance<LocalizedString>();
            asset.name = "TestString";
            settings.Track(asset);

            asset.Values.Clear();
            foreach (var (code, value) in entries)
                asset.Values.Add(new LocaleValuePair<string>(LocaleCode.Parse(code), value));

            return asset;
        }
    }
}
