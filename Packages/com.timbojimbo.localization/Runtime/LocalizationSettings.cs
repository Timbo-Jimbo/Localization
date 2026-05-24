using System;
using System.Collections.Generic;
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

        public static void SetActiveLocale(LocalizationLocale locale)
        {
            if (_active == locale) return;
            _active = locale;

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
        }
    }
}