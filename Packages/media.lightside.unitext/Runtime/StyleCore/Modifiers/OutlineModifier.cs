using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Applies an outline effect by appending duplicate glyph geometry behind the face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All parameters come from the tag/rule parameter string.
    /// Format: <c>&lt;outline=dilate&gt;</c>, <c>&lt;outline=#color&gt;</c>,
    /// or <c>&lt;outline=dilate,#color&gt;</c>.
    /// Defaults: dilate = 0.2, color = black (#000000FF).
    /// </para>
    /// </remarks>
    [Serializable]
    [TypeGroup("Appearance", 3)]
    [TypeDescription("Adds an outline effect around the text.")]
    [ParameterField(0, "Dilate", "float", "0.2")]
    [ParameterField(1, "Color", "color", "#000000FF")]
    public class OutlineModifier : EffectModifier
    {
        private const float defaultDilate = 0.2f;
        private static readonly Color32 defaultColor = new(0, 0, 0, 255);

        [SerializeField] private bool fixedPixelSize;

        /// <summary>
        /// When <see langword="true"/>, <c>dilate</c> is interpreted in fixed pixels and
        /// compensated by the glyph's gradientScale so the outline has a constant on-screen
        /// thickness independent of font size. Default <see langword="false"/> — <c>dilate</c>
        /// scales with the glyph SDF padding.
        /// </summary>
        /// <remarks>
        /// Read on the mesh-generation hot path (<c>onGlyph</c>); changing it requeues a mesh
        /// rebuild via <see cref="UniTextDirtyFlags.Color"/> — the parsed attribute buffer and
        /// glyph positions are reused.
        /// </remarks>
        public bool FixedPixelSize
        {
            get => fixedPixelSize;
            set
            {
                if (fixedPixelSize == value) return;
                fixedPixelSize = value;
                uniText?.SetDirty(UniTextDirtyFlags.Color);
            }
        }

        private struct EffectRange
        {
            public int start;
            public int end;
            public float dilate;
            public Vector2 packedColor;
        }

        private PooledBuffer<EffectRange> ranges;

        protected override void OnEnable()
        {
            ranges.FakeClear();
            base.OnEnable();
        }

        protected override void OnDestroy()
        {
            ranges.Return();
            base.OnDestroy();
        }

        protected override void OnApply(int start, int end, string parameter)
        {
            var dilate = defaultDilate;
            var color = defaultColor;

            if (!string.IsNullOrEmpty(parameter))
                ParseParameter(parameter, ref dilate, ref color);

            ranges.Add(new EffectRange
            {
                start = start,
                end = end,
                dilate = dilate,
                packedColor = EffectPacking.PackColor(color)
            });
        }

        protected override void OnGlyphEffect()
        {
            var gen = uniText.MeshGenerator;
            if (gen.font.IsColor) return;

            var cluster = gen.currentCluster;
            var count = ranges.count;
            var data = ranges.data;

            for (var i = 0; i < count; i++)
            {
                ref var range = ref data[i];
                if (cluster < range.start || cluster >= range.end) continue;

                var baseIdx = gen.faceBaseIdx;
                var glyphH = gen.Uvs0[baseIdx].w;
                if (glyphH < 1e-6f) return;

                var faceDilate = gen.Uvs1[baseIdx].y;
                var padGlyph = GlyphAtlas.Pad / glyphH;

                var dilate = fixedPixelSize
                    ? range.dilate / (GlyphAtlas.Pad * gen.fontMetricFactor)
                    : range.dilate;

                var extent = (faceDilate + dilate) * padGlyph;
                var effectiveExtent = extent < padGlyph ? extent : padGlyph;
                if (effectiveExtent > gen.currentMaxGlyphExtent)
                    gen.currentMaxGlyphExtent = effectiveExtent;

                var currentPad = UniTextMeshGenerator.DefaultSdfPadding;
                var facePad = faceDilate * padGlyph;
                if (facePad > currentPad) currentPad = facePad;
                var delta = effectiveExtent - currentPad;

                EnqueueEffectQuad(
                    baseIdx,
                    new Vector4(dilate, range.packedColor.x, range.packedColor.y, 0f),
                    expandDelta: delta > 0f ? delta : 0f);
                return;
            }
        }

        private static void ParseParameter(ReadOnlySpan<char> param, ref float dilate, ref Color32 color)
        {
            var reader = new ParameterReader(param);
            while (reader.Next(out var token))
            {
                if (token.IsEmpty) continue;
                if (ColorParsing.TryParse(token, out var c))
                    color = c;
                else
                    ParameterReader.ParseFloat(token, out dilate);
            }
        }
    }
}
