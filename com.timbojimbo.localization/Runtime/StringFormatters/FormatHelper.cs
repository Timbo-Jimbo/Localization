using System;
using System.Collections.Generic;

namespace TimboJimbo.Localization.StringFormatters
{
    /// <summary>
    /// Helpers for parsing format strings and conditional formatter entries.
    /// </summary>
    public static class FormatHelper
    {
        public const char EntrySeparator = '|';
        public const char KeyValueSeparator = '=';
        public const char PlaceholderStart = '[';
        public const char PlaceholderEnd = ']';
        public const char PlaceholderFormatSeparator = ':';

        private static readonly IReadOnlyList<FormatToken> EmptyTokens = Array.Empty<FormatToken>();
        private static readonly IReadOnlyList<FormatSpan> EmptySpans = Array.Empty<FormatSpan>();

        /// <summary>
        /// Formats a placeholder value into a character buffer.
        /// </summary>
        public interface IValueFormatter
        {
            bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format);
        }

        /// <summary>
        /// Parses format tokens from a string.
        /// </summary>
        public static IReadOnlyList<FormatToken> ExtractTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return EmptyTokens;
            }

            List<FormatToken> tokens = null;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (current == '{')
                {
                    if (IsEscapedBrace(text, index, '{'))
                    {
                        index++;
                        continue;
                    }

                    if (TryParseToken(text, index, out FormatToken token, out int nextIndex))
                    {
                        tokens ??= new List<FormatToken>();
                        tokens.Add(token);
                        index = nextIndex - 1;
                    }

                    continue;
                }

                if (current == '}' && IsEscapedBrace(text, index, '}'))
                {
                    index++;
                }
            }

            return tokens ?? EmptyTokens;
        }

        private static bool TryParseToken(string text, int startIndex, out FormatToken token, out int nextIndex)
        {
            token = default;
            nextIndex = startIndex + 1;

            int index = startIndex + 1;
            SkipWhitespace(text, ref index);

            if (!TryReadUnsignedInteger(text, ref index, out int parameterIndex))
            {
                return false;
            }

            SkipWhitespace(text, ref index);

            bool hasAlignment = false;
            int alignment = 0;
            if (index < text.Length && text[index] == ',')
            {
                hasAlignment = true;
                index++;
                SkipWhitespace(text, ref index);

                if (!TryReadSignedInteger(text, ref index, out alignment))
                {
                    return false;
                }

                SkipWhitespace(text, ref index);
            }

            string format = null;
            string formatterName = null;
            string formatterArguments = null;
            int formatStartIndex = -1;
            if (index < text.Length && text[index] == ':')
            {
                formatStartIndex = ++index;
                while (index < text.Length)
                {
                    char current = text[index];
                    if (current == '{')
                    {
                        if (!IsEscapedBrace(text, index, '{'))
                        {
                            return false;
                        }

                        index += 2;
                        continue;
                    }

                    if (current == '}')
                    {
                        if (IsEscapedBrace(text, index, '}'))
                        {
                            index += 2;
                            continue;
                        }

                        break;
                    }

                    index++;
                }

                if (index >= text.Length)
                {
                    return false;
                }

                format = text.Substring(formatStartIndex, index - formatStartIndex);
                TryExtractFormatter(format, out formatterName, out formatterArguments);
            }

            if (index >= text.Length || text[index] != '}')
            {
                return false;
            }

            int length = index - startIndex + 1;
            IReadOnlyList<FormatSpan> sourceTextRanges = BuildSourceTextRanges(text, index, formatStartIndex, format, formatterName, formatterArguments);
            IReadOnlyList<FormatSpan> syntaxRanges = BuildSyntaxRanges(startIndex, index + 1, sourceTextRanges);

            token = new FormatToken(
                startIndex,
                length,
                parameterIndex,
                hasAlignment,
                alignment,
                format,
                formatterName,
                formatterArguments,
                text.Substring(startIndex, length),
                syntaxRanges,
                sourceTextRanges);
            nextIndex = index + 1;
            return true;
        }

        private static IReadOnlyList<FormatSpan> BuildSourceTextRanges(
            string text,
            int tokenEndIndexInclusive,
            int formatStartIndex,
            string format,
            string formatterName,
            string formatterArguments)
        {
            // Only formatter tokens of the form "name|..." expose conditional source-text ranges.
            if (formatStartIndex < 0 || string.IsNullOrEmpty(formatterName) || formatterArguments == null)
            {
                return EmptySpans;
            }

            int formatterSeparatorIndex = format.IndexOf(EntrySeparator);
            if (formatterSeparatorIndex < 0)
            {
                return EmptySpans;
            }

            int argumentsStartIndex = formatStartIndex + formatterSeparatorIndex + 1;
            int argumentsEndIndexExclusive = tokenEndIndexInclusive;
            if (argumentsStartIndex >= argumentsEndIndexExclusive)
            {
                return EmptySpans;
            }

            var sourceTextRanges = new List<FormatSpan>();
            CollectEntrySourceTextRanges(text, argumentsStartIndex, argumentsEndIndexExclusive, sourceTextRanges);
            return sourceTextRanges.Count == 0 ? EmptySpans : sourceTextRanges;
        }

        private static IReadOnlyList<FormatSpan> BuildSyntaxRanges(
            int tokenStartIndex,
            int tokenEndIndexExclusive,
            IReadOnlyList<FormatSpan> sourceTextRanges)
        {
            if (sourceTextRanges == null || sourceTextRanges.Count == 0)
            {
                return new[] { new FormatSpan(tokenStartIndex, tokenEndIndexExclusive - tokenStartIndex) };
            }

            var syntaxRanges = new List<FormatSpan>();
            int currentIndex = tokenStartIndex;

            for (int i = 0; i < sourceTextRanges.Count; i++)
            {
                FormatSpan sourceRange = sourceTextRanges[i];
                if (sourceRange.StartIndex > currentIndex)
                {
                    syntaxRanges.Add(new FormatSpan(currentIndex, sourceRange.StartIndex - currentIndex));
                }

                currentIndex = Math.Max(currentIndex, sourceRange.EndIndexExclusive);
            }

            if (currentIndex < tokenEndIndexExclusive)
            {
                syntaxRanges.Add(new FormatSpan(currentIndex, tokenEndIndexExclusive - currentIndex));
            }

            return syntaxRanges.Count == 0 ? EmptySpans : syntaxRanges;
        }

        private static void TryExtractFormatter(string format, out string formatterName, out string formatterArguments)
        {
            formatterName = null;
            formatterArguments = null;

            if (string.IsNullOrWhiteSpace(format))
            {
                return;
            }

            int separatorIndex = format.IndexOf(EntrySeparator);
            if (separatorIndex <= 0)
            {
                return;
            }

            string candidateName = format.Substring(0, separatorIndex).Trim();
            if (!IsFormatterName(candidateName))
            {
                return;
            }

            formatterName = candidateName;
            formatterArguments = separatorIndex + 1 < format.Length
                ? format.Substring(separatorIndex + 1)
                : string.Empty;
        }

        private static bool IsFormatterName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetterOrDigit(current) || current == '_')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool IsEscapedBrace(string text, int index, char brace)
        {
            return index + 1 < text.Length && text[index] == brace && text[index + 1] == brace;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        private static bool TryReadUnsignedInteger(string text, ref int index, out int value)
        {
            value = 0;
            int startIndex = index;

            while (index < text.Length && char.IsDigit(text[index]))
            {
                value = (value * 10) + (text[index] - '0');
                index++;
            }

            return index > startIndex;
        }

        private static bool TryReadSignedInteger(string text, ref int index, out int value)
        {
            value = 0;
            if (index >= text.Length)
            {
                return false;
            }

            int sign = 1;
            if (text[index] == '+')
            {
                index++;
            }
            else if (text[index] == '-')
            {
                sign = -1;
                index++;
            }

            if (!TryReadUnsignedInteger(text, ref index, out int unsignedValue))
            {
                return false;
            }

            value = unsignedValue * sign;
            return true;
        }

        /// <summary>
        /// Strips a formatter prefix like <c>name|</c> from a format span.
        /// </summary>
        public static bool TryStripPrefix(ReadOnlySpan<char> format, ReadOnlySpan<char> prefix, out ReadOnlySpan<char> remainder)
        {
            if (format.Length > prefix.Length &&
                format.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) &&
                format[prefix.Length] == EntrySeparator)
            {
                remainder = format.Slice(prefix.Length);
                return true;
            }

            remainder = default;
            return false;
        }

        /// <summary>
        /// Reads the next <c>|</c>-separated entry.
        /// </summary>
        public static bool TryGetNextEntry(ref ReadOnlySpan<char> entriesSection, out ReadOnlySpan<char> entry)
        {
            if (entriesSection.IsEmpty)
            {
                entry = default;
                return false;
            }

            // Skip the leading separator (always present between prefix/entries and between entries)
            if (entriesSection[0] == EntrySeparator)
                entriesSection = entriesSection.Slice(1);

            var nextSeparator = entriesSection.IndexOf(EntrySeparator);
            if (nextSeparator == -1)
            {
                entry = entriesSection;
                entriesSection = default;
            }
            else
            {
                entry = entriesSection.Slice(0, nextSeparator);
                entriesSection = entriesSection.Slice(nextSeparator);
            }

            return true;
        }

        /// <summary>
        /// Splits an entry into key and value at the first non-operator <c>=</c>.
        /// </summary>
        public static bool TrySplitEntry(ReadOnlySpan<char> entry, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
        {
            for (int i = 0; i < entry.Length; i++)
            {
                if (entry[i] != KeyValueSeparator) continue;
                if (IsKeyValueSeparatorPartOfOperator(entry, i)) continue;

                key = entry.Slice(0, i);
                value = entry.Slice(i + 1);
                return true;
            }

            key = default;
            value = entry;
            return false;
        }

        /// <summary>
        /// Writes a template to the destination buffer and replaces placeholders as needed.
        /// </summary>
        public static TryFormatResult RenderTemplate<TFormatter>(
            ReadOnlySpan<char> template,
            TFormatter valueFormatter,
            Span<char> destination,
            out int charsWritten) where TFormatter : struct, IValueFormatter
        {
            int destIndex = 0;
            int templateIndex = 0;
            charsWritten = 0;

            while (templateIndex < template.Length)
            {
                var remaining = template.Slice(templateIndex);
                var placeholderStartIndex = remaining.IndexOf(PlaceholderStart);

                if (placeholderStartIndex == -1)
                {
                    if (!TryWrite(remaining, destination, ref destIndex, ref charsWritten))
                        return TryFormatResult.OutOfSpace;
                    break;
                }

                var beforePlaceholder = remaining.Slice(0, placeholderStartIndex);
                if (!TryWrite(beforePlaceholder, destination, ref destIndex, ref charsWritten))
                    return TryFormatResult.OutOfSpace;

                var placeholderContentEndIndex = remaining.IndexOf(PlaceholderEnd);

                // Unclosed placeholder: emit the rest as literal text and stop.
                if (placeholderContentEndIndex == -1 || placeholderContentEndIndex < placeholderStartIndex)
                {
                    if (!TryWrite(remaining.Slice(placeholderStartIndex), destination, ref destIndex, ref charsWritten))
                        return TryFormatResult.OutOfSpace;
                    break;
                }

                var placeholderContentStartIndex = placeholderStartIndex + 1;
                var placeholderContent = remaining.Slice(placeholderContentStartIndex, placeholderContentEndIndex - placeholderContentStartIndex);
                var colonIndex = placeholderContent.IndexOf(PlaceholderFormatSeparator);

                ReadOnlySpan<char> placeholderFormat = colonIndex == -1
                    ? ReadOnlySpan<char>.Empty
                    : placeholderContent.Slice(colonIndex + 1);

                if (!valueFormatter.TryFormat(destination.Slice(destIndex), out int placeholderCharsWritten, placeholderFormat))
                    return TryFormatResult.OutOfSpace;

                destIndex += placeholderCharsWritten;
                charsWritten += placeholderCharsWritten;

                templateIndex += placeholderContentEndIndex + 1;
            }

            return TryFormatResult.Success;
        }

        /// <summary>
        /// Returns success without writing anything.
        /// </summary>
        public static TryFormatResult EmitNothing(out int charsWritten)
        {
            charsWritten = 0;
            return TryFormatResult.Success;
        }

        /// <summary>
        /// Collects literal text ranges from formatter entry values.
        /// </summary>
        public static void CollectEntrySourceTextRanges(
            string text,
            int entriesStartIndex,
            int entriesEndIndexExclusive,
            List<FormatSpan> output)
        {
            if (output == null || text == null || entriesStartIndex >= entriesEndIndexExclusive)
                return;

            int entryStartIndex = entriesStartIndex;
            while (entryStartIndex < entriesEndIndexExclusive)
            {
                int entrySeparatorIndex = IndexOfInRange(text, EntrySeparator, entryStartIndex, entriesEndIndexExclusive);
                int entryEndIndexExclusive = entrySeparatorIndex == -1
                    ? entriesEndIndexExclusive
                    : entrySeparatorIndex;

                int valueStartIndex = FindEntryValueStartIndex(text, entryStartIndex, entryEndIndexExclusive);
                CollectTemplateLiteralRanges(text, valueStartIndex, entryEndIndexExclusive, output);

                if (entrySeparatorIndex == -1)
                    break;

                entryStartIndex = entrySeparatorIndex + 1;
            }
        }

        private static int FindEntryValueStartIndex(string text, int entryStartIndex, int entryEndIndexExclusive)
        {
            int entryLength = entryEndIndexExclusive - entryStartIndex;
            var entry = text.AsSpan(entryStartIndex, entryLength);

            for (int i = 0; i < entryLength; i++)
            {
                if (entry[i] != KeyValueSeparator) continue;
                if (IsKeyValueSeparatorPartOfOperator(entry, i)) continue;

                return entryStartIndex + i + 1;
            }

            // No key — the entire entry is the value (unconditional default).
            return entryStartIndex;
        }

        private static bool IsKeyValueSeparatorPartOfOperator(ReadOnlySpan<char> entry, int separatorIndex)
        {
            if (separatorIndex <= 0) return false;
            char prev = entry[separatorIndex - 1];
            return prev == '<' || prev == '>' || prev == '=' || prev == '!';
        }

        private static void CollectTemplateLiteralRanges(
            string text,
            int startIndex,
            int endIndexExclusive,
            List<FormatSpan> output)
        {
            if (startIndex >= endIndexExclusive)
                return;

            int segmentStartIndex = startIndex;
            int index = startIndex;

            while (index < endIndexExclusive)
            {
                if (text[index] != PlaceholderStart)
                {
                    index++;
                    continue;
                }

                int placeholderEndIndex = IndexOfInRange(text, PlaceholderEnd, index + 1, endIndexExclusive);
                if (placeholderEndIndex == -1)
                    break;

                AddSpan(output, segmentStartIndex, index - segmentStartIndex);
                index = placeholderEndIndex + 1;
                segmentStartIndex = index;
            }

            AddSpan(output, segmentStartIndex, endIndexExclusive - segmentStartIndex);
        }

        private static void AddSpan(List<FormatSpan> spans, int startIndex, int length)
        {
            if (length <= 0) return;
            spans.Add(new FormatSpan(startIndex, length));
        }

        private static int IndexOfInRange(string text, char value, int startIndex, int endIndexExclusive)
        {
            for (int i = startIndex; i < endIndexExclusive; i++)
            {
                if (text[i] == value) return i;
            }

            return -1;
        }

        private static bool TryWrite(ReadOnlySpan<char> content, Span<char> destination, ref int destIndex, ref int charsWritten)
        {
            if (destIndex + content.Length > destination.Length)
                return false;

            content.CopyTo(destination.Slice(destIndex));
            destIndex += content.Length;
            charsWritten += content.Length;
            return true;
        }
    }

    public readonly struct FormatToken
    {
        public FormatToken(
            int startIndex,
            int length,
            int parameterIndex,
            bool hasAlignment,
            int alignment,
            string format,
            string formatterName,
            string formatterArguments,
            string placeholderText,
            IReadOnlyList<FormatSpan> syntaxRanges,
            IReadOnlyList<FormatSpan> sourceTextRanges)
        {
            StartIndex = startIndex;
            Length = length;
            ParameterIndex = parameterIndex;
            HasAlignment = hasAlignment;
            Alignment = alignment;
            Format = format;
            FormatterName = formatterName;
            FormatterArguments = formatterArguments;
            PlaceholderText = placeholderText;
            SyntaxRanges = syntaxRanges ?? Array.Empty<FormatSpan>();
            SourceTextRanges = sourceTextRanges ?? Array.Empty<FormatSpan>();
        }

        public int StartIndex { get; }
        public int Length { get; }
        public int ParameterIndex { get; }
        public bool HasAlignment { get; }
        public int Alignment { get; }
        public string Format { get; }
        public string FormatterName { get; }
        public string FormatterArguments { get; }
        public string PlaceholderText { get; }
        public IReadOnlyList<FormatSpan> SyntaxRanges { get; }
        public IReadOnlyList<FormatSpan> SourceTextRanges { get; }
        public bool HasFormat => !string.IsNullOrEmpty(Format);
        public bool HasCustomFormatter => !string.IsNullOrEmpty(FormatterName);
    }

    public readonly struct FormatSpan
    {
        public FormatSpan(int startIndex, int length)
        {
            StartIndex = startIndex;
            Length = length;
        }

        public int StartIndex { get; }
        public int Length { get; }
        public int EndIndexExclusive => StartIndex + Length;
    }
}