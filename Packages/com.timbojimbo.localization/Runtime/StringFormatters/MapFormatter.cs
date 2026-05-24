using System;
using System.Globalization;
using UnityEditor;

namespace TimboJimbo.Localization.StringFormatters
{

    /// <summary>
    /// Maps an input value to a string using key/value entries in the format.
    /// Example: <c>{0:map|true=Enabled|false=Disabled|other=Unknown}</c>
    /// </summary>
    public static class MapFormatter
    {
        public  static ReadOnlySpan<char> Prefix => "map";
        public static ReadOnlySpan<char> ShortPrefix => "m";
        
        private static ReadOnlySpan<char> OtherKey => "other";

        [InitializeOnLoadMethod]
        public static void InitOnLoad()
        {
            ZStringUtility.AppendTryFormat((string value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new StringValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((bool value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new BoolValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((int value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new IntValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((long value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new LongValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((float value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new FloatValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((double value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new DoubleValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatMap(new DecimalValue(value), destination, out charsWritten, format));
        }

        private interface IMapValue : FormatHelper.IValueFormatter
        {
            bool MatchesKey(ReadOnlySpan<char> key);
        }

        private static TryFormatResult TryFormatMap<TValue>(
            TValue value,
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format) where TValue : struct, IMapValue
        {
            if (!FormatHelper.TryStripPrefix(format, Prefix, out var entriesSection) &&
                !FormatHelper.TryStripPrefix(format, ShortPrefix, out entriesSection))
            {
                charsWritten = 0;
                return TryFormatResult.Skipped;
            }

            ReadOnlySpan<char> fallback = default;
            bool hasFallback = false;

            while (FormatHelper.TryGetNextEntry(ref entriesSection, out var entry))
            {
                if (!FormatHelper.TrySplitEntry(entry, out var key, out var entryValue))
                    continue; // Map requires explicit keys; ignore unconditional entries.

                if (value.MatchesKey(key))
                    return FormatHelper.RenderTemplate(entryValue, value, destination, out charsWritten);

                if (!hasFallback && key.Equals(OtherKey, StringComparison.InvariantCultureIgnoreCase))
                {
                    fallback = entryValue;
                    hasFallback = true;
                }
            }

            if (hasFallback)
                return FormatHelper.RenderTemplate(fallback, value, destination, out charsWritten);

            return FormatHelper.EmitNothing(out charsWritten);
        }
        
        private readonly struct StringValue : IMapValue
        {
            private readonly string _value;
            public StringValue(string value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)
            {
                var span = (_value ?? string.Empty).AsSpan();
                if (span.Length > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }
                span.CopyTo(destination);
                charsWritten = span.Length;
                return true;
            }

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                key.Equals((_value ?? string.Empty).AsSpan(), StringComparison.InvariantCultureIgnoreCase);
        }

        private readonly struct BoolValue : IMapValue
        {
            private readonly bool _value;
            public BoolValue(bool value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)
            {
                var text = (_value ? "true" : "false").AsSpan();
                if (text.Length > destination.Length)
                {
                    charsWritten = 0;
                    return false;
                }
                text.CopyTo(destination);
                charsWritten = text.Length;
                return true;
            }

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                (key.Equals("true".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && _value) ||
                (key.Equals("false".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && !_value);
        }

        private readonly struct IntValue : IMapValue
        {
            private readonly int _value;
            public IntValue(int value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed == _value;
        }

        private readonly struct LongValue : IMapValue
        {
            private readonly long _value;
            public LongValue(long value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed == _value;
        }

        private readonly struct FloatValue : IMapValue
        {
            private readonly float _value;
            public FloatValue(float value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                decimal.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == (decimal)_value;
        }

        private readonly struct DoubleValue : IMapValue
        {
            private readonly double _value;
            public DoubleValue(double value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                decimal.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == (decimal)_value;
        }

        private readonly struct DecimalValue : IMapValue
        {
            private readonly decimal _value;
            public DecimalValue(decimal value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesKey(ReadOnlySpan<char> key) =>
                decimal.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == _value;
        }
    }
}