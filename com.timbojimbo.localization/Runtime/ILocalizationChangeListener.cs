namespace TimboJimbo.Localization
{
    public interface ILocalizationChangeListener
    {
        void OnLocaleChanged(LocalizationLocale newLocale);
        void OnLocalizedValueChanged(LocalizedValue localizedValue);
    }
}