using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TimboJimbo.Localization
{
    /// <summary>One locale's value in a <see cref="LocalizedValue{T}"/>, keyed by the locale's code.</summary>
    [Serializable]
    public class LocaleValuePair<T>
    {
        public LocaleCode Code;
        public T Value;

        // The 1.x key: a reference to the locale asset. Still read so 1.x assets load with their codes filled from
        // it (see LocalizedValue.FillCodesFromLegacyLocales); the editor's migration command clears it and re-saves.
        [SerializeField, FormerlySerializedAs("Locale"), HideInInspector]
        internal LocalizationLocale LegacyLocale;

        public LocaleValuePair() { }

        public LocaleValuePair(LocaleCode code, T value)
        {
            Code = code;
            Value = value;
        }
    }
}
