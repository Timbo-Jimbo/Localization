namespace TimboJimbo.Localization
{
    public interface ILocalizedValueResolver<T>
    {
        T Resolve(LocalizationLocale locale);
    }
}