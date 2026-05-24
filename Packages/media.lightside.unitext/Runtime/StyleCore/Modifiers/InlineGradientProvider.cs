using System;

namespace LightSide
{
    /// <summary>
    /// <see cref="IGradientProvider"/> with an inline list of named gradients edited directly
    /// on the modifier. Use this for one-off catalogs that don't deserve a dedicated asset
    /// and aren't shared across components.
    /// </summary>
    [Serializable]
    [TypeDescription("Inline list of named gradients edited directly on the modifier.")]
    public sealed class InlineGradientProvider : InlineNamedCatalog<UniTextGradients.NamedGradient>, IGradientProvider
    {
        protected override string GetEntryName(UniTextGradients.NamedGradient entry) => entry.name;
    }
}
