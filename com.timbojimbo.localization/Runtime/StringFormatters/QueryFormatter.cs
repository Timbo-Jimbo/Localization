using System;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.Localization.StringFormatters
{

    /// <summary>
    /// Formats values by matching query conditions.
    /// Example: <c>{0:query|<=10=few|>10=many|unknown}</c>
    /// </summary>
    public static class QueryFormatter
    {
        public static ReadOnlySpan<char> Prefix => "query";
        public static ReadOnlySpan<char> ShortPrefix => "q";


        #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        #else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #endif
        public static void InitOnLoad()
        {
            ZStringUtility.AppendTryFormat((string value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new StringValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((bool value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new BoolValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((int value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new IntValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((long value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new LongValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((float value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new FloatValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((double value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new DoubleValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new DecimalValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((TimeSpan value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new TimeSpanValue(value), destination, out charsWritten, format));

            ZStringUtility.AppendTryFormat((DateTime value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                TryFormatQuery(new DateTimeValue(value), destination, out charsWritten, format));
        }

        private interface IQueryValue : FormatHelper.IValueFormatter
        {
            bool MatchesCondition(ReadOnlySpan<char> key);
        }

        private static TryFormatResult TryFormatQuery<TValue>(
            TValue value,
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format) where TValue : struct, IQueryValue
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
                if (FormatHelper.TrySplitEntry(entry, out var key, out var entryValue))
                {
                    if (value.MatchesCondition(key))
                        return FormatHelper.RenderTemplate(entryValue, value, destination, out charsWritten);
                }
                else if (!hasFallback)
                {
                    fallback = entry;
                    hasFallback = true;
                }
            }

            if (hasFallback)
                return FormatHelper.RenderTemplate(fallback, value, destination, out charsWritten);

            return FormatHelper.EmitNothing(out charsWritten);
        }

        private enum Op { Eq, Lt, Le, Gt, Ge }

        private static void StripOp(ref ReadOnlySpan<char> condition, out Op op)
        {
            if (condition.Length >= 2)
            {
                if (condition[0] == '<' && condition[1] == '=') { op = Op.Le; condition = condition.Slice(2); return; }
                if (condition[0] == '>' && condition[1] == '=') { op = Op.Ge; condition = condition.Slice(2); return; }
                if (condition[0] == '=' && condition[1] == '=') { op = Op.Eq; condition = condition.Slice(2); return; }
            }

            if (condition.Length >= 1)
            {
                if (condition[0] == '<') { op = Op.Lt; condition = condition.Slice(1); return; }
                if (condition[0] == '>') { op = Op.Gt; condition = condition.Slice(1); return; }
                if (condition[0] == '=') { op = Op.Eq; condition = condition.Slice(1); return; }
            }

            op = Op.Eq;
        }

        private static bool CompareNumber(decimal value, ReadOnlySpan<char> key)
        {
            var remaining = key;
            StripOp(ref remaining, out var op);

            if (!decimal.TryParse(remaining, NumberStyles.Float, CultureInfo.InvariantCulture, out var target))
                return false;

            return op switch
            {
                Op.Eq => value == target,
                Op.Lt => value < target,
                Op.Le => value <= target,
                Op.Gt => value > target,
                Op.Ge => value >= target,
                _ => false,
            };
        }

        private static bool CompareTimeSpan(TimeSpan value, ReadOnlySpan<char> key)
        {
            var remaining = key;
            StripOp(ref remaining, out var op);

            if (!TryParseDuration(remaining, out var target))
                return false;

            long a = value.Ticks;
            long b = target.Ticks;
            return op switch
            {
                Op.Eq => a == b,
                Op.Lt => a < b,
                Op.Le => a <= b,
                Op.Gt => a > b,
                Op.Ge => a >= b,
                _ => false,
            };
        }

        private static bool TryParseDuration(ReadOnlySpan<char> text, out TimeSpan result)
        {
            result = default;
            if (text.IsEmpty) return false;

            int suffixStart = text.Length;
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsLetter(text[i]))
                {
                    suffixStart = i + 1;
                    break;
                }
                suffixStart = i;
            }

            var numberSpan = text.Slice(0, suffixStart);
            var suffixSpan = text.Slice(suffixStart);

            if (numberSpan.IsEmpty || suffixSpan.IsEmpty) return false;

            if (!double.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
                return false;

            if (suffixSpan.Equals("ms".AsSpan(), StringComparison.InvariantCultureIgnoreCase))
            {
                result = TimeSpan.FromMilliseconds(amount);
                return true;
            }

            if (suffixSpan.Length == 1)
            {
                switch (char.ToLowerInvariant(suffixSpan[0]))
                {
                    case 's': result = TimeSpan.FromSeconds(amount); return true;
                    case 'm': result = TimeSpan.FromMinutes(amount); return true;
                    case 'h': result = TimeSpan.FromHours(amount); return true;
                    case 'd': result = TimeSpan.FromDays(amount); return true;
                }
            }

            return false;
        }

        private readonly struct StringValue : IQueryValue
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

            public bool MatchesCondition(ReadOnlySpan<char> key) =>
                key.Equals((_value ?? string.Empty).AsSpan(), StringComparison.InvariantCultureIgnoreCase);
        }

        private readonly struct BoolValue : IQueryValue
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

            public bool MatchesCondition(ReadOnlySpan<char> key) =>
                (key.Equals("true".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && _value) ||
                (key.Equals("false".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && !_value);
        }

        private readonly struct IntValue : IQueryValue
        {
            private readonly int _value;
            public IntValue(int value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareNumber(_value, key);
        }

        private readonly struct LongValue : IQueryValue
        {
            private readonly long _value;
            public LongValue(long value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareNumber(_value, key);
        }

        private readonly struct FloatValue : IQueryValue
        {
            private readonly float _value;
            public FloatValue(float value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareNumber((decimal)_value, key);
        }

        private readonly struct DoubleValue : IQueryValue
        {
            private readonly double _value;
            public DoubleValue(double value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareNumber((decimal)_value, key);
        }

        private readonly struct DecimalValue : IQueryValue
        {
            private readonly decimal _value;
            public DecimalValue(decimal value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareNumber(_value, key);
        }

        private readonly struct TimeSpanValue : IQueryValue
        {
            private readonly TimeSpan _value;
            public TimeSpanValue(TimeSpan value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareTimeSpan(_value, key);
        }

        private readonly struct DateTimeValue : IQueryValue
        {
            private readonly DateTime _value;
            public DateTimeValue(DateTime value) { _value = value; }

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) =>
                _value.TryFormat(destination, out charsWritten, format, CultureInfo.CurrentCulture);

            public bool MatchesCondition(ReadOnlySpan<char> key) => CompareTimeSpan(_value - DateTime.Now, key);
        }
    }
}