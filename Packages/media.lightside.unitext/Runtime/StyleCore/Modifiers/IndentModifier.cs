using System;

namespace LightSide
{
    /// <summary>
    /// Adds left indentation that begins where the tag opens and persists across wrapped lines
    /// until the tag closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two complementary mechanisms produce a continuous visual indent:
    /// <list type="bullet">
    /// <item>Every line whose first codepoint falls inside the tagged range is pushed right by
    /// the resolved value (line-start margin).</item>
    /// <item>If the tag opens in the middle of a line (i.e. the codepoint preceding the tag is
    /// not a mandatory line break), the advance of that previous codepoint is widened by the
    /// indent so that content following the open boundary visibly shifts right within the
    /// current line.</item>
    /// </list>
    /// As soon as the tag closes, subsequent codepoints carry no extra indent, so the next line
    /// wraps back to the container edge.
    /// </para>
    /// <para>
    /// Indents from overlapping or nested ranges accumulate, so writing
    /// <c>&lt;indent=1em&gt;outer &lt;indent=1em&gt;inner&lt;/indent&gt;&lt;/indent&gt;</c>
    /// indents the inner content by two ems. Negative values pull the line back, which can be
    /// used to outdent a nested run or to balance an outer indent.
    /// </para>
    /// <para>
    /// Parameter — single value with optional unit:
    /// <list type="bullet">
    /// <item><c>20</c> or <c>20px</c> — layout units; like every other UniText geometric modifier
    /// these scale together with auto-sized text.</item>
    /// <item><c>1.5em</c> — multiplied by the current shaping font size.</item>
    /// <item><c>10%</c> — fraction of the host RectTransform's width, resolved into render
    /// space using the active glyph scale so the on-screen indent stays at the requested
    /// percentage with or without auto-size. Ignored when the rect has no finite width.</item>
    /// <item><c>+5</c> / <c>-5</c> — explicit delta. Functionally identical to the unsigned form
    /// because indents always compose additively.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [Serializable]
    [TypeGroup("Layout", 5)]
    [TypeDescription("Indents content from the tag opening through every wrapped line within the range.")]
    [ParameterField(0, "Indent", "unit:px|em|%", "1em")]
    public class IndentModifier : BaseModifier
    {
        private const string MidLineBumpKey = "indent.midLineBump";
        private PooledArrayAttribute<float> midLineBumps;

        protected override void OnEnable()
        {
            buffers.PrepareAttribute(ref midLineBumps, MidLineBumpKey);
            uniText.TextProcessor.Shaped += OnShaped;
        }

        protected override void OnDisable()
        {
            uniText.TextProcessor.Shaped -= OnShaped;
        }

        protected override void OnDestroy()
        {
            buffers?.ReleaseAttributeData(MidLineBumpKey);
            midLineBumps = null;
        }

        protected override void OnApply(int start, int end, string parameter)
        {
            if (string.IsNullOrEmpty(parameter))
                return;

            var reader = new ParameterReader(parameter);
            if (!reader.NextUnitFloat(out var rawValue, out var unit))
                return;

            var indent = ResolveToShapingUnits(rawValue, unit);
            if (indent == 0f)
                return;

            buffers.PrepareStartMargins();

            var margins = buffers.startMargins.data;
            if (margins == null)
                return;

            var cpCount = buffers.codepoints.count;
            var safeEnd = Math.Min(end, cpCount);
            for (var i = start; i < safeEnd; i++)
                margins[i] += indent;

            if (start > 0 && start < cpCount && !PrecededByMandatoryBreak(start))
                midLineBumps.buffer[start - 1] += indent;
        }

        /// <summary>
        /// True when the codepoint at <c>index - 1</c> ends a line per UAX#14 (BK / CR / LF / NL).
        /// In that case the tag opens at a hard line start and the line-start margin alone
        /// produces the visible indent — no glyph-advance bump is needed.
        /// </summary>
        private bool PrecededByMandatoryBreak(int index)
        {
            var cls = UnicodeData.Provider.GetLineBreakClass(buffers.codepoints.data[index - 1]);
            return cls == LineBreakClass.BK
                || cls == LineBreakClass.CR
                || cls == LineBreakClass.LF
                || cls == LineBreakClass.NL;
        }

        private void OnShaped()
        {
            var bufLen = midLineBumps.buffer.Capacity;
            if (bufLen == 0) return;

            var bumps = midLineBumps.buffer.data;
            if (bumps == null) return;

            var glyphs = buffers.shapedGlyphs.data;
            var runs = buffers.shapedRuns.data;
            var runCount = buffers.shapedRuns.count;

            for (var r = 0; r < runCount; r++)
            {
                ref var run = ref runs[r];
                var glyphEnd = run.glyphStart + run.glyphCount;
                float widthDelta = 0f;

                for (var g = run.glyphStart; g < glyphEnd; g++)
                {
                    var cluster = glyphs[g].cluster;
                    if ((uint)cluster >= (uint)bufLen) continue;
                    var bump = bumps[cluster];
                    if (bump == 0f) continue;

                    glyphs[g].advanceX += bump;
                    widthDelta += bump;
                    bumps[cluster] = 0f;
                }

                run.width += widthDelta;
            }
        }

        private float ResolveToShapingUnits(float value, ParameterReader.UnitKind unit)
        {
            switch (unit)
            {
                case ParameterReader.UnitKind.Em:
                    var emBase = buffers.shapingFontSize > 0 ? buffers.shapingFontSize : uniText.FontSize;
                    return value * emBase;

                case ParameterReader.UnitKind.Percent:
                    var maxWidth = uniText.cachedTransformData.rect.width;
                    if (float.IsNaN(maxWidth) || float.IsInfinity(maxWidth) || maxWidth <= 0f)
                        return 0f;
                    var glyphScale = buffers.GetGlyphScale(uniText.CurrentFontSize);
                    if (glyphScale <= 0f) glyphScale = 1f;
                    return value * 0.01f * maxWidth / glyphScale;

                default:
                    return value;
            }
        }
    }
}
