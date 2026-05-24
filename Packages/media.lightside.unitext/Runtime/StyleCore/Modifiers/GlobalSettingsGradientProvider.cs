using System;
using System.Collections.Generic;
using System.Linq;

namespace LightSide
{
    /// <summary>
    /// <see cref="IGradientProvider"/> backed by the project-wide
    /// <see cref="UniTextSettings.Gradients"/> asset. Use this when the same gradient
    /// catalog is shared across all UniText components in the project.
    /// </summary>
    /// <remarks>
    /// Listens to both <see cref="UniTextSettings.Changed"/> (in case the project-wide asset
    /// pointer is reassigned) and the bound asset's own <see cref="INamedCatalog{TEntry}.Changed"/>
    /// event (in case its entries are edited). Both subscriptions are lazy and tied to this
    /// provider's own <see cref="Changed"/> subscriber count, so an unused provider doesn't keep
    /// itself alive through the settings/asset delegate lists.
    /// </remarks>
    [Serializable]
    [TypeDescription("Resolves names through the project-wide UniTextSettings.Gradients asset.")]
    public sealed class GlobalSettingsGradientProvider : IGradientProvider
    {
        [NonSerialized] private UniTextGradients boundAsset;
        [NonSerialized] private Action changed;
        [NonSerialized] private bool boundToSettings;

        /// <inheritdoc/>
        public event Action Changed
        {
            add
            {
                var wasEmpty = changed == null;
                changed += value;
                if (wasEmpty) Bind();
            }
            remove
            {
                changed -= value;
                if (changed == null) Unbind();
            }
        }

        /// <inheritdoc/>
        public bool TryGet(string name, out UniTextGradients.NamedGradient entry)
        {
            var asset = UniTextSettings.Gradients;
            if (asset == null)
            {
                entry = default;
                return false;
            }
            return asset.TryGet(name, out entry);
        }

        /// <inheritdoc/>
        public IEnumerable<UniTextGradients.NamedGradient> Enumerate()
        {
            var asset = UniTextSettings.Gradients;
            return asset == null ? Enumerable.Empty<UniTextGradients.NamedGradient>() : asset.Enumerate();
        }

        private void Bind()
        {
            if (!boundToSettings)
            {
                UniTextSettings.Changed += OnSettingsChanged;
                boundToSettings = true;
            }
            BindAsset(UniTextSettings.Gradients);
        }

        private void Unbind()
        {
            if (boundToSettings)
            {
                UniTextSettings.Changed -= OnSettingsChanged;
                boundToSettings = false;
            }
            BindAsset(null);
        }

        private void BindAsset(UniTextGradients asset)
        {
            if (ReferenceEquals(boundAsset, asset)) return;
            if (boundAsset != null) boundAsset.Changed -= OnAssetChanged;
            boundAsset = asset;
            if (boundAsset != null) boundAsset.Changed += OnAssetChanged;
        }

        private void OnSettingsChanged(string property)
        {
            if (!UniTextSettings.Affects(property, nameof(UniTextSettings.Gradients))) return;
            BindAsset(UniTextSettings.Gradients);
            changed?.Invoke();
        }

        private void OnAssetChanged() => changed?.Invoke();
    }
}
