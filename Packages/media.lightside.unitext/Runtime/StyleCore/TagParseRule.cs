using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LightSide
{
    /// <summary>
    /// Base class for parsing XML-style markup tags (e.g., &lt;b&gt;, &lt;color=#FF0000&gt;).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived classes override <see cref="TagName"/> to specify the tag name to match.
    /// Parameters are always optional: &lt;tag&gt;, &lt;tag=value&gt;, &lt;tag/&gt;, &lt;tag=value/&gt;.
    /// Self-closing is purely syntax-driven via /&gt;.
    /// </para>
    /// </remarks>
    /// <seealso cref="IParseRule"/>
    [Serializable]
    public abstract class TagParseRule : IParseRule
    {
        private readonly Stack<OpenTag> openTags = new(8);

        [ThreadStatic] private static FastIntDictionary<string> parameterCache;

        private struct OpenTag
        {
            public int tagStart;
            public int tagEnd;
            public string parameter;
        }

        protected abstract string TagName { get; }

        /// <summary>
        /// When <see langword="true"/>, the tag has HTML void-element semantics: every
        /// <c>&lt;tag&gt;</c> or <c>&lt;tag=value&gt;</c> form is treated as self-closing
        /// (produces a single <c>￼</c> placeholder), and a stray <c>&lt;/tag&gt;</c>
        /// is silently consumed. Use for inline-media tags (<c>obj</c>, <c>sprite</c>)
        /// where range semantics don't apply.
        /// </summary>
        protected virtual bool IsVoid => false;

        public void Reset()
        {
            openTags.Clear();
        }

        public virtual void Finalize(ReadOnlySpan<char> text,PooledList<ParsedRange> results)
        {
            while (openTags.Count > 0)
            {
                var open = openTags.Pop();
                results.Add(new ParsedRange(
                    open.tagStart,
                    open.tagEnd,
                    text.Length, text.Length, open.parameter
                ));
            }
        }

        public virtual int TryMatch(ReadOnlySpan<char> text,int index, PooledList<ParsedRange> results)
        {
            if (text[index] != '<')
                return index;

            var openResult = TryMatchOpenTag(text, index, results);
            if (openResult > index)
                return openResult;

            var closeResult = TryMatchCloseTag(text, index, results);
            if (closeResult > index)
                return closeResult;

            return index;
        }

        private int TryMatchOpenTag(ReadOnlySpan<char> text,int index, PooledList<ParsedRange> results)
        {
            var tagNameLen = TagName.Length;
            if (index + tagNameLen + 2 > text.Length)
                return index;

            if (!AsciiCaseInsensitive.StartsWith(text, index + 1, TagName))
                return index;

            var afterName = index + 1 + tagNameLen;
            if (afterName >= text.Length)
                return index;

            string parameter = null;
            int tagEnd;

            var c = text[afterName];
            if (c == '=')
            {
                var paramStart = afterName + 1;
                var closePos = FindTagClose(text, paramStart);
                if (closePos < 0)
                    return index;

                var selfClose = closePos > paramStart && text[closePos - 1] == '/';
                var paramEnd = selfClose ? closePos - 1 : closePos;
                parameter = ExtractParameter(text, paramStart, paramEnd);
                tagEnd = closePos + 1;

                if (selfClose || IsVoid)
                {
                    results.Add(ParsedRange.SelfClosing(index, tagEnd, "\uFFFC", parameter));
                    return tagEnd;
                }
            }
            else if (c == '/')
            {
                if (afterName + 1 >= text.Length || text[afterName + 1] != '>')
                    return index;
                tagEnd = afterName + 2;
                results.Add(ParsedRange.SelfClosing(index, tagEnd, "\uFFFC", null));
                return tagEnd;
            }
            else if (c == '>')
            {
                tagEnd = afterName + 1;
                if (IsVoid)
                {
                    results.Add(ParsedRange.SelfClosing(index, tagEnd, "\uFFFC", null));
                    return tagEnd;
                }
            }
            else
            {
                return index;
            }

            openTags.Push(new OpenTag { tagStart = index, tagEnd = tagEnd, parameter = parameter });
            return tagEnd;
        }

        /// <summary>
        /// Scans for the tag-closing <c>&gt;</c> starting at <paramref name="start"/>, treating
        /// the contents of a balanced <c>"…"</c> or <c>'…'</c> pair as opaque. This lets
        /// attribute values themselves contain <c>&lt;</c> and <c>&gt;</c> — e.g. Unity Input
        /// System binding paths like <c>&lt;sprite="&lt;Keyboard&gt;/a"&gt;</c> — without
        /// requiring HTML-style entity escaping.
        /// </summary>
        /// <remarks>
        /// Mirrors HTML5 attribute-value tokenisation: a quote character entered outside an
        /// existing quote opens a quoted region; the same character closes it. Unmatched quote
        /// pairs fall back to ordinary scanning (return -1 if no <c>&gt;</c> follows).
        /// </remarks>
        private static int FindTagClose(ReadOnlySpan<char> text, int start)
        {
            char quote = '\0';
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (quote != '\0')
                {
                    if (c == quote) quote = '\0';
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }
                if (c == '>')
                    return i;
            }
            return -1;
        }

        private int TryMatchCloseTag(ReadOnlySpan<char> text,int index, PooledList<ParsedRange> results)
        {
            var tagNameLen = TagName.Length;

            var closeLen = 3 + tagNameLen;
            if (index + closeLen > text.Length)
                return index;

            if (text[index + 1] != '/')
                return index;

            if (!AsciiCaseInsensitive.StartsWith(text, index + 2, TagName))
                return index;

            if (text[index + 2 + tagNameLen] != '>')
                return index;

            var closeTagEnd = index + closeLen;

            if (IsVoid)
            {
                results.Add(ParsedRange.SelfClosing(index, closeTagEnd, string.Empty, null));
                return closeTagEnd;
            }

            if (openTags.Count == 0)
                return index;

            var open = openTags.Pop();

            results.Add(new ParsedRange(
                open.tagStart,
                open.tagEnd,
                index,
                closeTagEnd,
                open.parameter
            ));

            return closeTagEnd;
        }

        private static string ExtractParameter(ReadOnlySpan<char> text,int start, int end)
        {
            var span = text.Slice(start, end - start);

            span = span.Trim();

            if (span.Length >= 2)
            {
                var first = span[0];
                var last = span[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\'')) span = span.Slice(1, span.Length - 2);
            }

            return GetOrCreateCachedString(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetOrCreateCachedString(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty) return string.Empty;

            var cache = parameterCache ??= new FastIntDictionary<string>(128);
            var hash = ComputeSpanHash(span);

            if (cache.TryGetValue(hash, out var cached))
                if (cached.Length == span.Length && span.SequenceEqual(cached.AsSpan()))
                    return cached;

            var result = span.ToString();
            cache[hash] = result;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeSpanHash(ReadOnlySpan<char> span)
        {
            unchecked
            {
                var hash = -2128831035;
                for (var i = 0; i < span.Length; i++)
                {
                    hash ^= span[i];
                    hash *= 16777619;
                }

                return hash;
            }
        }

    }
}
