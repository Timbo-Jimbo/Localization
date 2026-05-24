namespace LightSide
{
    /// <summary>
    /// Resolves named <see cref="UniTextGradients.NamedGradient"/> entries for
    /// <see cref="GradientModifier"/>.
    /// </summary>
    /// <remarks>
    /// Marker interface over <see cref="INamedCatalog{TEntry}"/> — exists to scope the
    /// <c>[SerializeReference, TypeSelector]</c> dropdown and to give a domain-specific
    /// name. Built-in implementations: <see cref="InlineGradientProvider"/>,
    /// <see cref="AssetGradientProvider"/>, <see cref="GlobalSettingsGradientProvider"/>.
    /// Custom providers should raise their <see cref="INamedCatalog{TEntry}.Changed"/> event
    /// whenever their resolution result changes.
    /// </remarks>
    /// <seealso cref="GradientModifier"/>
    public interface IGradientProvider : INamedCatalog<UniTextGradients.NamedGradient>
    {
    }
}
