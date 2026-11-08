namespace TimboJimbo.Localization
{
    /// <summary>Resolves a value for a locale code. The <see cref="LocalizationLocale"/> overloads in <see cref="LocalizedValueResolverExtensions"/> forward the locale's code.</summary>
    public interface ILocalizedValueResolver<T>
    {
        T Resolve(LocaleCode code);
    }
}
