#if TJ_LOCALIZATION_UNITEXT_SUPPORT

using LightSide;

namespace TimboJimbo.Localization.Appicators
{
    public class UniTextLocalizer : TextLocalizer
    {
        public UniText Target;

        protected override void ApplyTextToTarget(string text)
        {
            if (Target != null)
                Target.SetText(text);
        }
    }
}

#endif