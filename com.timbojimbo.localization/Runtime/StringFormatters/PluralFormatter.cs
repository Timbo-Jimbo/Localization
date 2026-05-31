using System;
using Cysharp.Text;
using UnityEditor;
using UnityEngine.Assertions;

namespace TimboJimbo.Localization.StringFormatters
{

    /// <summary>
    /// Formats localized plural text from a numeric value.
    /// Example: <c>{count:plural:zero=No items|one=# item|other=# items}</c>
    /// </summary>
    public static class PluralFormatter
    {
        public static ReadOnlySpan<char> Prefix => "plural";
        public static ReadOnlySpan<char> ShortPrefix => "p";

        /// <summary>
        /// Registers plural formatting for supported numeric types.
        /// </summary>
        [InitializeOnLoadMethod]
        public static void InitOnLoad()
        {
            ZStringUtility.AppendTryFormat((decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
            {
                return TryFormatPluralization(value, destination, out charsWritten, format);
            });

            ZStringUtility.AppendTryFormat((double value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
            {
                return TryFormatPluralization((decimal)value, destination, out charsWritten, format);
            });

            ZStringUtility.AppendTryFormat((float value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
            {
                return TryFormatPluralization((decimal)value, destination, out charsWritten, format);
            });

            ZStringUtility.AppendTryFormat((int value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
            {
                return TryFormatPluralization(value, destination, out charsWritten, format);
            });
        }

        private static TryFormatResult TryFormatPluralization(decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)
        {
            if (
                !FormatHelper.TryStripPrefix(format, Prefix, out var entriesSection) &&
                !FormatHelper.TryStripPrefix(format, ShortPrefix, out entriesSection)
            )
            {
                charsWritten = 0;
                return TryFormatResult.Skipped;
            }

            var desiredPluralForm = GetPluralForm(value, LocalizationSettings.ActiveLocale);
            var formatter = new DecimalValue(value);

            while (FormatHelper.TryGetNextEntry(ref entriesSection, out var entry))
            {
                PluralForm form;
                ReadOnlySpan<char> userString;

                if (FormatHelper.TrySplitEntry(entry, out var key, out userString))
                {
                    if (!TryGetPluralForm(key, out form))
                        form = PluralForm.Other;
                }
                else
                {
                    form = PluralForm.Other;
                    userString = entry;
                }

                if (form != desiredPluralForm && form != PluralForm.Other)
                    continue;

                return FormatHelper.RenderTemplate(userString, formatter, destination, out charsWritten);
            }

            charsWritten = 0;
            return TryFormatResult.Skipped;
        }

        private readonly struct DecimalValue : FormatHelper.IValueFormatter
        {
            private readonly decimal _value;
            public DecimalValue(decimal value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, System.Globalization.CultureInfo.CurrentCulture);
        }


        private static bool TryGetPluralForm(ReadOnlySpan<char> formString, out PluralForm form)
        {
            if (
                formString.Equals("zero", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("none", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("0", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                form = PluralForm.Zero;
                return true;
            }

            if (
                formString.Equals("one", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("single", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("1", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                form = PluralForm.One;
                return true;
            }

            if (
                formString.Equals("two", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("double", StringComparison.InvariantCultureIgnoreCase) ||
                formString.Equals("2", StringComparison.InvariantCultureIgnoreCase)
            )
            {
                form = PluralForm.Two;
                return true;
            }

            if (formString.Equals("few", StringComparison.InvariantCultureIgnoreCase))
            {
                form = PluralForm.Few;
                return true;
            }

            if (formString.Equals("many", StringComparison.InvariantCultureIgnoreCase))
            {
                form = PluralForm.Many;
                return true;
            }

            if (formString.Equals("other", StringComparison.InvariantCultureIgnoreCase))
            {
                form = PluralForm.Other;
                return true;
            }

            form = default;
            return false;
        }

        private enum PluralForm
        {
            Zero,
            One,
            Two,
            Few,
            Many,
            Other
        }

        private static PluralForm GetPluralForm(int number, LocalizationLocale locale) => GetPluralForm((decimal)number, locale);
        private static PluralForm GetPluralForm(float number, LocalizationLocale locale) => GetPluralForm((decimal)number, locale);
        private static PluralForm GetPluralForm(double number, LocalizationLocale locale) => GetPluralForm((decimal)number, locale);
        private static PluralForm GetPluralForm(decimal number, LocalizationLocale locale)
        {
            decimal n = Math.Abs(number);
            bool isIntegerIsh = Math.Abs(n - Math.Round(n)) < 0.000001m;

            switch (locale.LanguageCode)
            {
                case "zh":
                case "ja":
                case "ko":
                case "vi":
                case "id":
                case "ms":
                case "th":
                case "km":
                case "lo":
                case "my":
                    return PluralForm.Other;

                case "fr":
                case "hi":
                case "bn":
                case "gu":
                case "mr":
                case "pa":
                case "fa":
                    return (n < 2) ? PluralForm.One : PluralForm.Other;

                case "ru":
                case "uk":
                case "be":
                case "sr":
                case "hr":
                case "bs":
                    if (!isIntegerIsh) return PluralForm.Other;
                    int slavicM10 = (int)n % 10;
                    int slavicM100 = (int)n % 100;
                    if (slavicM10 == 1 && slavicM100 != 11) return PluralForm.One;
                    if (slavicM10 >= 2 && slavicM10 <= 4 && (slavicM100 < 12 || slavicM100 > 14)) return PluralForm.Few;
                    if (slavicM10 == 0 || (slavicM10 >= 5 && slavicM10 <= 9) || (slavicM100 >= 11 && slavicM100 <= 14)) return PluralForm.Many;
                    return PluralForm.Other;

                case "pl":
                    if (n == 1) return PluralForm.One;
                    if (!isIntegerIsh) return PluralForm.Other;
                    int plM10 = (int)n % 10;
                    int plM100 = (int)n % 100;
                    if (plM10 >= 2 && plM10 <= 4 && (plM100 < 12 || plM100 > 14)) return PluralForm.Few;
                    if (plM10 == 0 || (plM10 >= 5 && plM10 <= 9) || (plM100 >= 12 && plM100 <= 14)) return PluralForm.Many;
                    return PluralForm.Other;

                case "ar":
                    if (n == 0) return PluralForm.Zero;
                    if (n == 1) return PluralForm.One;
                    if (n == 2) return PluralForm.Two;
                    if (!isIntegerIsh) return PluralForm.Other;
                    int arM100 = (int)n % 100;
                    if (arM100 >= 3 && arM100 <= 10) return PluralForm.Few;
                    if (arM100 >= 11 && arM100 <= 99) return PluralForm.Many;
                    return PluralForm.Other;

                case "cs":
                case "sk":
                    if (n == 1) return PluralForm.One;
                    if (isIntegerIsh && n >= 2 && n <= 4) return PluralForm.Few;
                    return PluralForm.Other;

                case "ro":
                    if (n == 1) return PluralForm.One;
                    if (n == 0 || (isIntegerIsh && (n % 100 >= 1 && n % 100 <= 19))) return PluralForm.Few;
                    return PluralForm.Other;

                case "lt":
                    if (n % 10 == 1 && n % 100 != 11) return PluralForm.One;
                    if (isIntegerIsh && n % 10 >= 2 && n % 10 <= 9 && (n % 100 < 11 || n % 100 > 19)) return PluralForm.Few;
                    return PluralForm.Other;
                case "lv":
                    if (isIntegerIsh && n % 10 == 1 && n % 100 != 11) return PluralForm.One;
                    if (n == 0) return PluralForm.Zero;
                    return PluralForm.Other;

                default:
                    return n switch
                    {
                        0 => PluralForm.Zero,
                        1 => PluralForm.One,
                        _ => PluralForm.Other
                    };
            }
        }
    }

    /// <summary>
    /// Registers custom formatters with ZString.
    /// </summary>
    public static class ZStringUtility
    {
        public delegate TryFormatResult AppendedTryFormat<T>(T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format);

        /// <summary>
        /// Adds a formatter and falls back to the existing formatter when skipped.
        /// </summary>
        public static void AppendTryFormat<T>(
            AppendedTryFormat<T> tryFormatFn
        )
        {
            if (tryFormatFn == null) return;

            var existingFormatter = Utf16ValueStringBuilder.FormatterCache<T>.TryFormatDelegate;
            Assert.IsNotNull(tryFormatFn);

            Utf16ValueStringBuilder.RegisterTryFormat((T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
            {
                var result = tryFormatFn(value, destination, out charsWritten, format);

                switch (result)
                {
                    case TryFormatResult.Success:
                        return true;
                    case TryFormatResult.OutOfSpace:
                        return false;
                    case TryFormatResult.Skipped:
                        return existingFormatter(value, destination, out charsWritten, format);
                    default:
                        throw new InvalidOperationException("Unknown TryFormatResult: " + result);
                }
            });
        }
    }

    /// <summary>
    /// Result for a custom formatting attempt.
    /// </summary>
    public enum TryFormatResult
    {
        Skipped,
        OutOfSpace,
        Success
    }
}