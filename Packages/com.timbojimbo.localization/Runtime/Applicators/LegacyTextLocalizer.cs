namespace TimboJimbo.Localization.Appicators
{
    public class LegacyTextLocalizer : TextLocalizer
    {
        public UnityEngine.UI.Text Target;

        protected override void ApplyTextToTarget(string text)
        {
            if (Target != null)
                Target.text = text;
        }
    }
}