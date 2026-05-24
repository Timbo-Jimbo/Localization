using System;
using System.Collections.Generic;

namespace LightSide
{
    /// <summary>
    /// Resolves named entries by case-insensitive name. Used by every "named catalog" feature in
    /// UniText (gradients, inline sprites, inline-object prefabs) so the same set of providers —
    /// inline list, shared asset, custom code — composes across all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Concrete implementations decide where the catalog lives — an explicit asset reference,
    /// an inline list edited per-modifier, or a custom runtime resolver — and raise
    /// <see cref="Changed"/> whenever their resolution result changes. The modifier that
    /// consults the provider subscribes during its active lifecycle and rebuilds when the
    /// event fires.
    /// </para>
    /// <para>
    /// Per-domain marker interfaces (<see cref="IGradientProvider"/>, <see cref="ISpriteProvider"/>,
    /// <see cref="IObjProvider"/>) extend this with no additional members — they exist purely to
    /// scope <c>[SerializeReference, TypeSelector]</c> dropdowns and to give domain-specific
    /// reading.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEntry">The entry type stored in the catalog (e.g. <c>InlineSprite</c>, <c>InlineObject</c>, <c>UniTextGradients.NamedGradient</c>).</typeparam>
    public interface INamedCatalog<TEntry>
    {
        /// <summary>
        /// Tries to resolve a name to a catalog entry. Name matching is case-insensitive.
        /// </summary>
        /// <param name="name">The lookup name (typically the tag parameter, e.g. <c>jump</c> in <c>&lt;sprite=jump&gt;</c>).</param>
        /// <param name="entry">The resolved entry when this call returns <see langword="true"/>; otherwise <see langword="default"/>.</param>
        /// <returns><see langword="true"/> when an entry with the given name was found.</returns>
        bool TryGet(string name, out TEntry entry);

        /// <summary>
        /// Enumerates every entry exposed by this provider in catalog order. Used by editor
        /// tooling to populate the tag-parameter dropdown. Lazy or runtime-resolved providers
        /// may legitimately return an empty sequence.
        /// </summary>
        IEnumerable<TEntry> Enumerate();

        /// <summary>
        /// Raised whenever the catalog's resolution result may have changed: inline entries
        /// added/removed/edited, the underlying asset replaced or its entries mutated, or a
        /// custom provider explicitly invalidating its cache. Subscribers should treat this as
        /// "invalidate cached resolution and rebuild" — there is no per-source filtering.
        /// </summary>
        event Action Changed;
    }
}
