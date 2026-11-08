namespace TimboJimbo.Localization
{
    /// <summary>Resolves by a locale asset: each overload forwards the locale's <see cref="LocalizationLocale.Code"/> (a null locale is <see cref="LocaleCode.None"/>).</summary>
    public static class LocalizedValueResolverExtensions
    {
        public static T Resolve<T>(this ILocalizedValueResolver<T> resolver, LocalizationLocale locale)
            => resolver.Resolve(LocaleCode.Of(locale));

        public static string Resolve<T1>(this ILocalizedStringResolver resolver, LocalizationLocale locale, T1 param1)
            => resolver.Resolve(LocaleCode.Of(locale), param1);

        public static string Resolve<T1, T2>(this ILocalizedStringResolver resolver, LocalizationLocale locale, T1 param1, T2 param2)
            => resolver.Resolve(LocaleCode.Of(locale), param1, param2);

        public static string Resolve<T1, T2, T3>(this ILocalizedStringResolver resolver, LocalizationLocale locale, T1 param1, T2 param2, T3 param3)
            => resolver.Resolve(LocaleCode.Of(locale), param1, param2, param3);

        public static string Resolve<T1, T2, T3, T4>(this ILocalizedStringResolver resolver, LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4)
            => resolver.Resolve(LocaleCode.Of(locale), param1, param2, param3, param4);

        public static string Resolve<T1, T2, T3, T4, T5>(this ILocalizedStringResolver resolver, LocalizationLocale locale, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
            => resolver.Resolve(LocaleCode.Of(locale), param1, param2, param3, param4, param5);

        public static bool HasMeaningfulValue(this LocalizedValue value, LocalizationLocale locale)
            => value.HasMeaningfulValue(LocaleCode.Of(locale));
    }
}
