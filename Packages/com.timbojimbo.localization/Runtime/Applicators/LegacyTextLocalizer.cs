using UnityEngine;

namespace TimboJimbo.Localization.Applicators
{
    [RequireComponent(typeof(UnityEngine.UI.Text))]
    public class LegacyTextLocalizer : TextLocalizer
    {
        public UnityEngine.UI.Text Target;

        protected override void ApplyTextToTarget(string text)
        {
            if (Target != null)
                Target.text = text;
        }

        protected override void ClearFromTarget() { }

        private void Reset()
        {
            Target = GetComponent<UnityEngine.UI.Text>();
        }
    }
}