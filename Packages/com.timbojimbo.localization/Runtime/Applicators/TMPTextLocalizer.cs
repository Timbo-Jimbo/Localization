using System;
using TMPro;

namespace TimboJimbo.Localization.Appicators
{
    public class TMPTextLocalizer : TextLocalizer, ITextPreprocessor
    {
        public TextMeshProUGUI Target;
        [NonSerialized] private string _lastAppliedText;

        public string PreprocessText(string text)
        {
            return _lastAppliedText == null ? text : _lastAppliedText;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Target != null) Target.textPreprocessor = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (Target != null && Target.textPreprocessor == (ITextPreprocessor)this)
                Target.textPreprocessor = null;

        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (Target == null && enabled) Target.textPreprocessor = this;
        }

        protected override void ApplyTextToTarget(string text)
        {
            _lastAppliedText = text;

            if (Target != null)
                Target.SetAllDirty();
        }
    }
}