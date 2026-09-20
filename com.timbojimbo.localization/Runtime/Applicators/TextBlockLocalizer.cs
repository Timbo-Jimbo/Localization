#if TJ_LOCALIZATION_UI_TEXT_SUPPORT

using TimboJimbo.UI.Text;
using UnityEngine;

namespace TimboJimbo.Localization.Applicators
{
    [RequireComponent(typeof(TextBlock))]
    public class TextBlockLocalizer : TextLocalizer
    {
        public TextBlock Target;

        protected override void ApplyTextToTarget(string text)
        {
            if (Target != null)
                Target.Text = text;
        }

        protected override void ClearFromTarget()
        {
            if (Target != null)
                Target.Text = string.Empty;
        }

        private void Reset()
        {
            Target = GetComponent<TextBlock>();
        }
    }
}

#endif
