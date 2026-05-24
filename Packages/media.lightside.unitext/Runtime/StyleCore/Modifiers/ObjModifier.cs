using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Defines an inline object (UI prefab) that can be embedded within text flow.
    /// </summary>
    /// <remarks>
    /// Pure data: the host <see cref="ObjModifier"/> owns lifecycle state (instance count,
    /// instantiated prefab clones), so the same <see cref="InlineObject"/> instance can safely
    /// be referenced by multiple modifiers.
    /// </remarks>
    [Serializable]
    public class InlineObject : InlineMedia
    {
        /// <summary>UI prefab to instantiate as the inline child.</summary>
        public RectTransform prefab;
    }

    /// <summary>
    /// Embeds UI prefabs (images, icons, custom elements) inline with text. The set of named
    /// inline objects available to <c>&lt;obj=name&gt;</c> tags is supplied by an
    /// <see cref="IObjProvider"/>.
    /// </summary>
    /// <remarks>
    /// Parameter: the name of the object to embed — resolved through <see cref="Provider"/>.
    /// Built-in providers: <see cref="InlineObjProvider"/> (default — inline list on the
    /// modifier), <see cref="AssetObjProvider"/> (shared <see cref="UniTextObjects"/> asset).
    /// For dynamic catalogs, implement <see cref="IObjProvider"/> directly and raise its
    /// <see cref="INamedCatalog{TEntry}.Changed"/> event when the resolution result changes.
    /// </remarks>
    /// <seealso cref="SpriteModifier"/>
    /// <seealso cref="IObjProvider"/>
    /// <seealso cref="InlineObject"/>
    [Serializable]
    [TypeGroup("Inline", 5)]
    [TypeDescription("Embeds an inline object (image, icon) within text.")]
    [ParameterField(0, "Key", "string")]
    public sealed class ObjModifier : InlineMediaModifier<InlineObject, PrefabMediaWrapper>
    {
        [SerializeReference, TypeSelector]
        [Tooltip("Source of named inline objects for <obj=name> tags handled by this modifier.")]
        private IObjProvider provider = new InlineObjProvider();

        /// <summary>
        /// Source of named inline objects used by this modifier when resolving <c>&lt;obj=name&gt;</c>
        /// tags. <see langword="null"/> disables resolution.
        /// </summary>
        public IObjProvider Provider
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

        protected override INamedCatalog<InlineObject> Catalog => provider;

        protected override bool HasRenderable(InlineObject entry) => entry.prefab is not null;

        protected override void ConfigureWrapper(PrefabMediaWrapper wrapper, InlineObject entry, int cluster)
        {
            wrapper.prefab = entry.prefab;
        }
    }
}
