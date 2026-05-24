using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Serializable base for <see cref="INamedCatalog{TEntry}"/> implementations that delegate
    /// resolution to an explicit ScriptableObject asset reference — useful when multiple
    /// components should share the same catalog managed as a project asset.
    /// </summary>
    /// <remarks>
    /// Subscription to the underlying asset's <see cref="INamedCatalog{TEntry}.Changed"/> event
    /// is lazy: this provider attaches only while it has at least one subscriber and detaches
    /// when the last unsubscribes, so an unused provider doesn't keep itself alive through the
    /// asset's delegate list. The provider also re-binds on inspector edits that change the
    /// asset reference (<see cref="ISerializationCallbackReceiver.OnAfterDeserialize"/>).
    /// </remarks>
    /// <typeparam name="TEntry">The entry type stored in the catalog.</typeparam>
    /// <typeparam name="TAsset">The asset type that implements <see cref="INamedCatalog{TEntry}"/> for this domain.</typeparam>
    [Serializable]
    public abstract class AssetNamedCatalog<TEntry, TAsset> : INamedCatalog<TEntry>, ISerializationCallbackReceiver
        where TAsset : ScriptableObject, INamedCatalog<TEntry>
    {
        private static Action onBeforeProcess;
        
        static AssetNamedCatalog() => UniTextBase.BeforeProcess += () => onBeforeProcess?.Invoke();
        
        [SerializeField]
        [Tooltip("Asset to resolve tag names from.")]
        protected TAsset asset;

        [NonSerialized] private TAsset boundAsset;
        [NonSerialized] private Action changed;

        /// <summary>The asset used for name resolution. Setting to <see langword="null"/> makes <see cref="TryGet"/> return <see langword="false"/> for every name.</summary>
        public TAsset Asset
        {
            get => asset;
            set
            {
                if (ReferenceEquals(asset, value)) return;
                UnbindAsset();
                asset = value;
                BindAssetIfNeeded();
                changed?.Invoke();
            }
        }

        /// <inheritdoc/>
        public event Action Changed
        {
            add
            {
                var wasEmpty = changed == null;
                changed += value;
                if (wasEmpty) BindAssetIfNeeded();
            }
            remove
            {
                changed -= value;
                if (changed == null) UnbindAsset();
            }
        }

        /// <inheritdoc/>
        public bool TryGet(string name, out TEntry entry)
        {
            if (asset == null)
            {
                entry = default;
                return false;
            }
            return asset.TryGet(name, out entry);
        }

        /// <inheritdoc/>
        public IEnumerable<TEntry> Enumerate()
        {
            return asset == null ? Enumerable.Empty<TEntry>() : asset.Enumerate();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            onBeforeProcess -= OnAssetChanged;
            onBeforeProcess += OnAssetChanged;
            
            if (!ReferenceEquals(boundAsset, asset))
            {
                UnbindAsset();
                BindAssetIfNeeded();
            }
        }

        private void BindAssetIfNeeded()
        {
            if (changed == null || asset == null) return;
            boundAsset = asset;
            boundAsset.Changed += OnAssetChanged;
        }

        private void UnbindAsset()
        {
            if (boundAsset == null) return;
            boundAsset.Changed -= OnAssetChanged;
            boundAsset = null;
        }

        private void OnAssetChanged()
        {
            onBeforeProcess -= OnAssetChanged;
            changed?.Invoke();
        }
    }
}
