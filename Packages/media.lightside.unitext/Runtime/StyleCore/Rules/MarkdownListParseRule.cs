using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LightSide
{
    /// <summary>Parses Markdown-style lists (bulleted and numbered).</summary>
    /// <remarks>
    /// Supports unordered lists with <c>-</c>, <c>*</c>, <c>+</c> markers and ordered lists with
    /// <c>1.</c>, <c>2)</c>, etc. Nested lists are detected by indentation level. An item ends at
    /// the first line break unless the following line is indented deeper than the item's own
    /// indent (which extends the item to cover an indented continuation or a nested list).
    /// CommonMark-style lazy continuation across non-indented lines is not supported — a
    /// non-indented line after an item is treated as a separate paragraph.
    /// </remarks>
    /// <seealso cref="ListModifier"/>
    [Serializable]
    [TypeGroup("Markdown", 1)]
    [TypeDescription("Parses Markdown-style bullet and numbered lists.")]
    public sealed class MarkdownListParseRule : IParseRule
    {
        /// <summary>Number of spaces per indentation level for nested lists.</summary>
        [UnityEngine.Tooltip("Number of spaces per indentation level for nested lists.")]
        public int spacesPerLevel = 2;

        private static readonly char[] bulletChars = { '-', '*', '+' };

        private static readonly string[] levelStrings = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        [ThreadStatic] private static Dictionary<(int level, int number), string> orderedParamCache;

        public int TryMatch(ReadOnlySpan<char> text,int index, PooledList<ParsedRange> results)
        {
            if (index > 0 && text[index - 1] != '\n')
                return index;

            var pos = index;
            var textLen = text.Length;

            var indent = 0;
            while (pos < textLen && text[pos] == ' ')
            {
                indent++;
                pos++;
            }

            if (pos >= textLen)
                return index;

            var nestingLevel = spacesPerLevel > 0 ? indent / spacesPerLevel : 0;

            if (pos < textLen - 1 &&
                Array.IndexOf(bulletChars, text[pos]) >= 0 &&
                text[pos + 1] == ' ')
            {
                var contentStart = pos + 2;
                var contentEnd = FindEndOfListItem(text, contentStart, indent);

                results.Add(new ParsedRange
                {
                    start = contentStart,
                    end = contentEnd,
                    parameter = GetLevelString(nestingLevel),
                    tagStart = index,
                    tagEnd = contentStart,
                    closeTagStart = contentEnd,
                    closeTagEnd = contentEnd
                });

                return contentStart;
            }

            if (TryParseOrderedMarker(text, pos, out var markerEnd, out var number))
            {
                var contentStart = markerEnd;
                var contentEnd = FindEndOfListItem(text, contentStart, indent);

                results.Add(new ParsedRange
                {
                    start = contentStart,
                    end = contentEnd,
                    parameter = GetOrderedParam(nestingLevel, number),
                    tagStart = index,
                    tagEnd = contentStart,
                    closeTagStart = contentEnd,
                    closeTagEnd = contentEnd
                });

                return contentStart;
            }

            return index;
        }

        private static bool TryParseOrderedMarker(ReadOnlySpan<char> text,int pos, out int end, out int number)
        {
            end = pos;
            number = 0;

            var textLen = text.Length;

            var numStart = pos;
            while (end < textLen && end - numStart < 9 && char.IsDigit(text[end]))
                end++;

            if (end == numStart)
                return false;

            if (end >= textLen - 1)
                return false;

            var terminator = text[end];
            if (terminator != '.' && terminator != ')')
                return false;

            if (text[end + 1] != ' ')
                return false;

            if (!int.TryParse(text.Slice(numStart, end - numStart), out number))
                return false;

            end += 2;
            return true;
        }

        private int FindEndOfListItem(ReadOnlySpan<char> text,int start, int currentIndent)
        {
            var textLen = text.Length;
            var pos = start;

            while (pos < textLen)
            {
                var relIdx = text.Slice(pos).IndexOf('\n');
                var lineEnd = relIdx < 0 ? -1 : pos + relIdx;
                if (lineEnd < 0)
                    return textLen;

                pos = lineEnd + 1;
                if (pos >= textLen)
                    return textLen;

                var nextIndent = 0;
                var checkPos = pos;
                while (checkPos < textLen && text[checkPos] == ' ')
                {
                    nextIndent++;
                    checkPos++;
                }

                if (checkPos < textLen && text[checkPos] == '\n')
                    return lineEnd + 1;

                if (nextIndent <= currentIndent)
                    return lineEnd + 1;
            }

            return textLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetLevelString(int level)
        {
            return (uint)level < (uint)levelStrings.Length ? levelStrings[level] : level.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetOrderedParam(int level, int number)
        {
            var cache = orderedParamCache ??= new Dictionary<(int level, int number), string>(64);
            var key = (level, number);
            if (cache.TryGetValue(key, out var cached))
                return cached;

            var result = $"{level},{number}";
            cache[key] = result;
            return result;
        }
    }
}
