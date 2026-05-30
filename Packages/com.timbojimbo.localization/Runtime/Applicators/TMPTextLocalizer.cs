#if TJ_LOCALIZATION_TMP_SUPPORT
using System;
using TMPro;
using UnityEngine;

namespace TimboJimbo.Localization.Applicators
{
    public class TMPTextLocalizer : TextLocalizer, ITextPreprocessor
    {
        public TextMeshProUGUI Target
        {
            get => _target;
            set
            {
                if (_target == value) return;

                Unbind();
                _target = value;
                Bind();
            }
        }

        [SerializeField] private TextMeshProUGUI _target;
        [NonSerialized] private TextMeshProUGUI _boundTarget;
        [NonSerialized] private string _lastAppliedText;

        public string PreprocessText(string text)
        {
            return _lastAppliedText == null ? text : _lastAppliedText;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Bind();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Unbind();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || _target == _boundTarget) return;
                Unbind();
                Bind();
            };
            #endif
        }

        protected override void ApplyTextToTarget(string text)
        {
            _lastAppliedText = text;

            if (_boundTarget != null)
                _boundTarget.SetAllDirty();
        }

        protected override void ClearFromTarget()
        {
            _lastAppliedText = null;

            if (_boundTarget != null)
                _boundTarget.SetAllDirty();
        }

        private void Unbind()
        {
            if (_boundTarget == null) return;

            if (_boundTarget.textPreprocessor == (ITextPreprocessor)this)
                _boundTarget.textPreprocessor = null;
            
            RefreshTarget();
            _boundTarget = null;
        }

        private void Bind()
        {
            if (_target == null) return;

            _boundTarget = _target;

            /// We apply text via ITextPreprocessor to avoid stomping over any existing content in the TMP text field.
            _boundTarget.textPreprocessor = this;
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            if (_boundTarget == null) return;

            _boundTarget.SetAllDirty();
            
            #if UNITY_EDITOR
            // Only way I could figure out how to get TMP to update the text in edit mode
            // SetAllDirty doesn't seem to be enough, since I guess the text hasn't actually
            // 'changed' - only the ITextProcessor has..? Not sure...!
            if(!Application.isPlaying)
            {
                _boundTarget.enabled = false;
                _boundTarget.enabled = true;
            }
            #endif
        }

        private void Reset()
        {
            Target = GetComponent<TextMeshProUGUI>();
        }
    }
}
#endif