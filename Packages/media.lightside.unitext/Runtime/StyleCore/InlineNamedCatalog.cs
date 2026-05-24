using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Serializable base for <see cref="INamedCatalog{TEntry}"/> implementations that keep
    /// their entries inline on the owning modifier — edited in the inspector or mutated
    /// at runtime via <see cref="SetEntries"/>, <see cref="Add"/>, <see cref="Remove"/>.
    /// </summary>
    /// <remarks>
    /// Concrete subclasses supply <see cref="GetEntryName"/>. The
    /// <see cref="INamedCatalog{TEntry}.Changed"/> event fires automatically on every mutation;
    /// subscribers stay alive only through their own delegate, no static channels.
    /// </remarks>
    /// <typeparam name="TEntry">The entry type stored in the catalog.</typeparam>
    [Serializable]
    public abstract class InlineNamedCatalog<TEntry> : INamedCatalog<TEntry>, ISerializationCallbackReceiver
    {
        private static Action onBeforeProcess;
        
        static InlineNamedCatalog() => UniTextBase.BeforeProcess += () => onBeforeProcess?.Invoke();

        [SerializeField]
        [Tooltip("Inline named entries available to this modifier. Names are case-insensitive.")]
        protected StyledList<TEntry> entries = new();

        private Dictionary<string, TEntry> lookup;

        /// <inheritdoc/>
        public event Action Changed;

        /// <summary>Read-only view over the current entry list.</summary>
        public IReadOnlyList<TEntry> Entries => entries;

        /// <inheritdoc/>
        public bool TryGet(string name, out TEntry entry)
        {
            if (string.IsNullOrEmpty(name))
            {
                entry = default;
                return false;
            }
            EnsureLookup();
            return lookup.TryGetValue(name, out entry);
        }

        /// <inheritdoc/>
        public IEnumerable<TEntry> Enumerate()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!string.IsNullOrEmpty(GetEntryName(e)))
                    yield return e;
            }
        }

        /// <summary>Replaces the entry list and raises <see cref="Changed"/>.</summary>
        public void SetEntries(IEnumerable<TEntry> newEntries)
        {
            entries.Clear();
            if (newEntries != null)
            {
                foreach (var e in newEntries)
                    entries.Add(e);
            }
            lookup = null;
            RaiseChanged();
        }

        /// <summary>Appends one entry and raises <see cref="Changed"/>.</summary>
        public void Add(TEntry entry)
        {
            entries.Add(entry);
            lookup = null;
            RaiseChanged();
        }

        /// <summary>Removes the first entry with the given name. Returns <see langword="true"/> on success.</summary>
        public bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (var i = 0; i < entries.Count; i++)
            {
                if (string.Equals(GetEntryName(entries[i]), name, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                    lookup = null;
                    RaiseChanged();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Extracts the lookup name from an entry. Return <see langword="null"/> or empty to skip it.</summary>
        protected abstract string GetEntryName(TEntry entry);

        /// <summary>Raises <see cref="Changed"/>. Safe to call without subscribers.</summary>
        protected void RaiseChanged()
        {
            onBeforeProcess -= RaiseChanged;
            Changed?.Invoke();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            lookup = null;
            onBeforeProcess -= RaiseChanged;
            onBeforeProcess += RaiseChanged;
        }

        private void EnsureLookup()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, TEntry>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var name = GetEntryName(e);
                if (!string.IsNullOrEmpty(name))
                    lookup[name] = e;
            }
        }
    }
}
