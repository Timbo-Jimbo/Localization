using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Defines an inline sprite that can be embedded within text flow.
    /// </summary>
    /// <remarks>
    /// Pure data: the host <see cref="SpriteModifier"/> owns lifecycle state (instance count,
    /// generated GameObjects), so the same <see cref="InlineSprite"/> instance can safely be
    /// referenced by multiple modifiers.
    /// </remarks>
    [Serializable]
    public class InlineSprite : InlineMedia
    {
        /// <summary>The sprite to draw.</summary>
        public Sprite sprite;
        /// <summary>Tint color multiplied with the sprite.</summary>
        public Color color = Color.white;
        /// <summary>If true, the sprite is letterboxed inside the <c>width x height</c> box to keep its native aspect ratio.</summary>
        public bool preserveAspect = true;
    }

    /// <summary>
    /// Embeds <see cref="Sprite"/> assets inline with text. The set of named sprites available
    /// to <c>&lt;sprite=name&gt;</c> tags is supplied by an <see cref="ISpriteProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tag syntax: <c>&lt;sprite=name[,colorArg]&gt;</c>. The optional second positional argument
    /// controls the rendered color of this occurrence:
    /// </para>
    /// <list type="bullet">
    /// <item>omitted — use the entry's own <see cref="InlineSprite.color"/> (the default; keeps
    /// atlas/asset colors, matching how raster images are embedded in HTML, iOS NSTextAttachment,
    /// Android ImageSpan).</item>
    /// <item><c>i</c> — inherit the host UniText component's <see cref="UnityEngine.UI.Graphic.color"/>.
    /// Equivalent to CSS <c>currentColor</c>.</item>
    /// <item><c>#RGB</c>/<c>#RRGGBB</c>/<c>#RRGGBBAA</c> or a named color (red, blue, …) — explicit
    /// per-occurrence override.</item>
    /// </list>
    /// <para>
    /// Examples: <c>&lt;sprite=heart&gt;</c>, <c>&lt;sprite=heart,i&gt;</c>, <c>&lt;sprite=heart,#FF0000&gt;</c>.
    /// </para>
    /// <para>
    /// The sprite name is resolved through <see cref="Provider"/>. Built-in providers:
    /// <see cref="InlineSpriteProvider"/> (default — inline list on the modifier),
    /// <see cref="AssetSpriteProvider"/> (shared <see cref="UniTextSprites"/> asset). For dynamic
    /// catalogs — input-prompt icon services, localisation, item icons — implement
    /// <see cref="ISpriteProvider"/> directly and raise its
    /// <see cref="INamedCatalog{TEntry}.Changed"/> event when the resolution result changes.
    /// </para>
    /// </remarks>
    /// <seealso cref="ObjModifier"/>
    /// <seealso cref="ISpriteProvider"/>
    /// <seealso cref="InlineSprite"/>
    [Serializable]
    [TypeGroup("Inline", 5)]
    [TypeDescription("Embeds an inline sprite (no prefab required) within text.")]
    [ParameterField(0, "Key", "string")]
    [ParameterField(1, "Color", "variant:Original|Inherit=i|Override=color:#FFFFFFFF")]
    public sealed class SpriteModifier : InlineMediaModifier<InlineSprite, SpriteImageWrapper>
    {
        private enum SpriteColorMode : byte
        {
            Inherit,
            Override,
        }

        private struct SpriteColorBinding
        {
            public SpriteColorMode mode;
            public Color32 color;
        }

        [SerializeReference, TypeSelector]
        [Tooltip("Source of named sprites for <sprite=name> tags handled by this modifier.")]
        private ISpriteProvider provider = new InlineSpriteProvider();

        private FastIntDictionary<SpriteColorBinding> clusterColors;

        /// <summary>
        /// Source of named sprites used by this modifier when resolving <c>&lt;sprite=name&gt;</c>
        /// tags. <see langword="null"/> disables resolution.
        /// </summary>
        public ISpriteProvider Provider
        {
            get => provider;
            set
            {
                if (ReferenceEquals(provider, value)) return;
                var old = provider;
                provider = value;
                RebindCatalog(old, value);
                uniText?.SetDirty(UniTextDirtyFlags.Text);
            }
        }

        protected override INamedCatalog<InlineSprite> Catalog => provider;

        protected override bool HasRenderable(InlineSprite entry) => entry.sprite is not null;

        protected override void OnEnable()
        {
            base.OnEnable();
            clusterColors?.Clear();
        }

        protected override void ConfigureWrapper(SpriteImageWrapper wrapper, InlineSprite entry, int cluster)
        {
            wrapper.sprite = entry.sprite;
            wrapper.preserveAspect = entry.preserveAspect;

            if (clusterColors != null && clusterColors.TryGetValue(cluster, out var binding))
            {
                wrapper.color = binding.mode == SpriteColorMode.Inherit
                    ? uniText.color
                    : binding.color;
            }
            else
            {
                wrapper.color = entry.color;
            }
        }

        protected override void OnExtraTokens(int cluster, ReadOnlySpan<char> extras)
        {
            if (extras.IsEmpty)
            {
                clusterColors?.Remove(cluster);
                return;
            }

            var reader = new ParameterReader(extras);
            if (!reader.Next(out var token) || token.IsEmpty)
            {
                clusterColors?.Remove(cluster);
                return;
            }

            if (token.Length == 1 && (token[0] == 'i' || token[0] == 'I'))
            {
                clusterColors ??= new FastIntDictionary<SpriteColorBinding>(8);
                clusterColors[cluster] = new SpriteColorBinding { mode = SpriteColorMode.Inherit };
                return;
            }

            if (ColorParsing.TryParse(token, out var color))
            {
                clusterColors ??= new FastIntDictionary<SpriteColorBinding>(8);
                clusterColors[cluster] = new SpriteColorBinding { mode = SpriteColorMode.Override, color = color };
                return;
            }

            clusterColors?.Remove(cluster);
        }
    }
}
