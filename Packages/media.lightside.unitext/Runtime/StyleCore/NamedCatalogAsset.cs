using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace LightSide
{
    /// <summary>
    /// Abstract ScriptableObject base for named-entry catalogs. Subclasses give the catalog a
    /// concrete entry type, a <c>CreateAssetMenu</c> path, and (optionally) a payload validity
    /// filter for editor enumeration.
    /// </summary>
    /// <typeparam name="TEntry">The entry type stored in the catalog.</typeparam>
    public abstract class NamedCatalogAsset<TEntry> : ScriptableObject, INamedCatalog<TEntry>
    {
        [SerializeField, FormerlySerializedAs("gradients")]
        [Tooltip("List of named entries available for tag resolution.")]
        protected StyledList<TEntry> entries = new();

        private Dictionary<string, TEntry> lookup;

        /// <inheritdoc/>
        public event Action Changed;

        /// <summary>Direct read-only view over the entry list. Use <see cref="Add"/> to mutate at runtime.</summary>
        public IReadOnlyList<TEntry> Entries => entries;

        /// <summary>Number of entries in the catalog.</summary>
        public int Count => entries.Count;

        /// <summary>All entry names exposed by this catalog.</summary>
        public IEnumerable<string> Names
        {
            get
            {
                EnsureLookup();
                return lookup.Keys;
            }
        }

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

        /// <summary>Adds an entry and raises <see cref="Changed"/>. Marks the asset dirty in the editor.</summary>
        public void Add(TEntry entry)
        {
            var name = GetEntryName(entry);
            if (string.IsNullOrEmpty(name)) return;

            EnsureLookup();
            entries.Add(entry);
            lookup[name] = entry;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            RaiseChanged();
        }

        /// <summary>Extracts the lookup name from an entry. Return <see langword="null"/> or empty to skip it.</summary>
        protected abstract string GetEntryName(TEntry entry);

        /// <summary>Raises <see cref="Changed"/>. Safe to call without subscribers.</summary>
        protected void RaiseChanged() => Changed?.Invoke();

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            lookup = null;
            RaiseChanged();
        }
#endif

        private void OnEnable()
        {
            lookup = null;
        }
    }
}
