using System.Runtime.CompilerServices;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Utility methods for packing effect layer parameters into vertex UV channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by effect modifiers to encode per-glyph layer parameters (color, offsets)
    /// into float UV values that are unpacked in the shader.
    /// </para>
    /// </remarks>
    public static class EffectPacking
    {
        /// <summary>
        /// Packs a Color32 into two floats safe for hardware interpolation: <c>x = R*256 + G</c>,
        /// <c>y = B*256 + A</c>. The two floats are stored in <c>UV2.y</c> and <c>UV2.z</c>
        /// of every effect-quad vertex and decoded in the shader by <c>UnpackEffectColor</c>
        /// (or <c>UniTextUnpackEffectColor</c> in custom shaders that include
        /// <c>UniText_Custom.cginc</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Color space contract: the input <c>Color32</c> is treated as sRGB (gamma) and copied
        /// into the UV channel verbatim. UV channels are not converted by the Canvas vertex
        /// pipeline, so the unpack helper applies gamma→linear unconditionally in linear color
        /// space. Authors of custom shaders that read this packing must use
        /// <c>UniTextUnpackEffectColor</c> rather than rolling their own decode.
        /// </para>
        /// <para>
        /// Both packed values lie in <c>[0, 65535]</c>, well inside the 24-bit integer range
        /// that single-precision float represents exactly. The previous bit-reinterpret packing
        /// produced NaN/Inf bit patterns for many color values; some GPUs canonicalize quiet-NaN
        /// at the vertex–fragment interpolator boundary, which forced bit 22 of the mantissa
        /// (the high bit of the green channel) on, randomly tinting effect layers as their
        /// channels crossed bit-6 thresholds.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PackColor(Color32 c)
        {
            return new Vector2(c.r * 256 + c.g, c.b * 256 + c.a);
        }
    }
}
