using System;

namespace LightSide
{
    /// <summary>
    /// Replaces every occurrence of the void <c>&lt;br&gt;</c> tag with a hard line break.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matches <c>&lt;br&gt;</c>, <c>&lt;br/&gt;</c>, and <c>&lt;br /&gt;</c> case-insensitively,
    /// mirroring the HTML void-element contract: there is no closing tag, no nested content, and
    /// the element renders nothing of its own. The tag is stripped and a single line-feed
    /// (U+000A) is written into the parsed text in its place.
    /// </para>
    /// <para>
    /// Once inserted, the line-feed is consumed by the regular UAX #14 mandatory-break path,
    /// so the new line keeps the surrounding paragraph's direction, alignment, and styling —
    /// the tag forces a line wrap, not a paragraph reset. Because the rule does not pair with a
    /// modifier, it is registered as a standalone rule.
    /// </para>
    /// </remarks>
    /// <seealso cref="IParseRule"/>
    [Serializable]
    [TypeGroup("Tags", 1)]
    [TypeDescription("Replaces the void <br> / <br/> tag with a hard line break.")]
    public sealed class LineBreakParseRule : IParseRule
    {
        private const string TagName = "br";

        public int Priority => 1;
        public bool IsStandalone => true;

        public int TryMatch(ReadOnlySpan<char> text, int index, PooledList<ParsedRange> results)
        {
            if (text[index] != '<')
                return index;

            const int minTagLen = 4;
            if (index + minTagLen > text.Length)
                return index;

            if (!AsciiCaseInsensitive.StartsWith(text, index + 1, TagName))
                return index;

            var i = index + 1 + TagName.Length;

            while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
                i++;

            if (i >= text.Length)
                return index;

            if (text[i] == '/')
            {
                i++;
                if (i >= text.Length || text[i] != '>')
                    return index;
            }
            else if (text[i] != '>')
            {
                return index;
            }

            var tagEnd = i + 1;
            results.Add(ParsedRange.SelfClosing(index, tagEnd, "\n"));
            return tagEnd;
        }
    }
}
