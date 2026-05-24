using System;

namespace LightSide
{
    /// <summary>
    /// <see cref="IGradientProvider"/> backed by an explicit <see cref="UniTextGradients"/>
    /// asset referenced on the modifier. Use this when a single component should resolve
    /// gradients from a different catalog than the project-wide one.
    /// </summary>
    [Serializable]
    [TypeDescription("Resolves names through an explicit UniTextGradients asset reference.")]
    public sealed class AssetGradientProvider : AssetNamedCatalog<UniTextGradients.NamedGradient, UniTextGradients>, IGradientProvider
    {
    }
}
