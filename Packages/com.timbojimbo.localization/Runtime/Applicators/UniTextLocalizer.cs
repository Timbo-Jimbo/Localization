#if TJ_LOCALIZATION_UNITEXT_SUPPORT

using LightSide;

namespace TimboJimbo.Localization.Applicators
{
    [RequireComponent(typeof(UniText))]
    public class UniTextLocalizer : TextLocalizer
    {
        public UniText Target;

        protected override void ApplyTextToTarget(string text)
        {
            if (Target != null)
                Target.SetText(text);
        }

        protected override void ClearFromTarget()
        {
            if (Target != null)
                Target.SetText(string.Empty);
        }

        private void Reset()
        {
            Target = GetComponent<UniText>();
        }
    }
}

#endif