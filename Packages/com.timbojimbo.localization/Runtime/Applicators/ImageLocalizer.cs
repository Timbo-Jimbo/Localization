using System;
using UnityEngine;
using UnityEngine.UI;

namespace TimboJimbo.Localization.Applicators
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ImageLocalizer : MonoBehaviour, ILocalizationChangeListener
    {
        public LocalizedSprite LocalizedSprite;
        public Image TargetImage;
        
        [NonSerialized] private bool _hasApplied;
        [NonSerialized] private Sprite _lastAppliedSprite;

        private void OnEnable()
        {
            LocalizationSettings.AddListener(this);

            // Guards against cases where the user may have manually 
            // Apply'ed prior to enabling this components 
            if (!_hasApplied) Apply();
        }

        private void OnDisable()
        {
            LocalizationSettings.RemoveListener(this);
            _hasApplied = false;
            _lastAppliedSprite = null;
        }

        private void Reset()
        {
            TargetImage = GetComponent<Image>();
        }

        private void OnValidate()
        {
            if(gameObject.activeInHierarchy && enabled)
                Apply();
        }

        public void OnLocalizedValueChanged(LocalizedValue value)
        {
            if (LocalizedSprite != null && LocalizedSprite == value)
                Apply();
        }

        public void OnLocaleChanged(LocalizationLocale locale)
        {
            Apply();
        }

        public void Apply()
        {
            var activeLocale = LocalizationSettings.ActiveLocale;

            if (LocalizedSprite == null || activeLocale == null)
            {
                if (TargetImage != null)
                    TargetImage.sprite = null;

                _hasApplied = true;
                _lastAppliedSprite = null;
                return;
            }

            var resolvedSprite = LocalizedSprite.Resolve(activeLocale);
            _hasApplied = true;

            if (resolvedSprite == _lastAppliedSprite)
                return;

            _lastAppliedSprite = resolvedSprite;
            
            if (TargetImage != null)
                TargetImage.sprite = resolvedSprite;
        }
    }
}