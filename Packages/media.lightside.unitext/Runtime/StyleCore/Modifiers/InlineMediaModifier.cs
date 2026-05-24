using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Shared base for modifiers that embed inline-flowed media (prefab instances, sprite
    /// quads, future GPU mesh subitems) into text. Subclasses pick the entry type and wrapper
    /// type, supply a typed provider field, and translate entries into wrapper state — the
    /// base owns the cluster mapping, shape/render passes, lifecycle of wrapper instances,
    /// and provider event subscription.
    /// </summary>
    /// <typeparam name="TEntry">The catalog entry type (must extend <see cref="InlineMedia"/>).</typeparam>
    /// <typeparam name="TWrapper">The <see cref="MediaWrapper"/> subclass that materialises each on-screen occurrence.</typeparam>
    [Serializable]
    public abstract class InlineMediaModifier<TEntry, TWrapper> : BaseModifier
        where TEntry : InlineMedia
        where TWrapper : MediaWrapper, new()
    {
        private FastIntDictionary<TEntry> clusterToEntry;

        /// <summary>
        /// Every entry that has ever held wrapper instances during this modifier's lifetime.
        /// Iterated for activeCount-reset and UpdateInstances cleanup so removed-from-catalog
        /// entries still get torn down rather than leaking their GameObjects. Entries are
        /// pruned once their instance pool empties.
        /// </summary>
        private readonly HashSet<TEntry> tracked = new();

        /// <summary>The catalog this modifier consults. Concrete subclass exposes a typed provider field with the appropriate marker interface.</summary>
        protected abstract INamedCatalog<TEntry> Catalog { get; }

        /// <summary>
        /// Copies the per-frame visual state from <paramref name="entry"/> into <paramref name="wrapper"/>
        /// (sprite/color, prefab reference, etc.). <paramref name="cluster"/> is the source-text cluster
        /// index of this occurrence — subclasses use it to apply per-occurrence overrides parsed by
        /// <see cref="OnExtraTokens"/> from extra tag arguments.
        /// </summary>
        protected abstract void ConfigureWrapper(TWrapper wrapper, TEntry entry, int cluster);

        /// <summary>
        /// Returns <see langword="true"/> when the entry's renderable payload is present (sprite
        /// assigned, prefab assigned). Entries failing this check are skipped at render time but
        /// still influence layout.
        /// </summary>
        /// <remarks>
        /// Invoked from the parallel mesh-generation phase, so implementations MUST be worker-thread
        /// safe — use <c>is not null</c> reference checks rather than Unity's overloaded
        /// <c>!= null</c>, which calls <c>EnsureRunningOnMainThread</c> and throws off the main
        /// thread. Destroyed Unity assets are filtered correctly later by the main-thread
        /// <c>MediaWrapper.TryCreate</c> path.
        /// </remarks>
        protected abstract bool HasRenderable(TEntry entry);

        /// <summary>
        /// Hook for parsing additional comma-separated arguments after the entry name in a tag
        /// (e.g. the <c>i</c> in <c>&lt;sprite=name,i&gt;</c>). Called once per applied range with
        /// the cluster index and a reader positioned right after the name token. Empty
        /// <paramref name="extras"/> means no extra arguments were supplied — subclasses should
        /// clear any stale per-cluster state in that case so previous-rebuild overrides do not
        /// persist when the tag is later edited to drop the extras.
        /// </summary>
        protected virtual void OnExtraTokens(int cluster, ReadOnlySpan<char> extras) { }

        protected override void OnEnable()
        {
            clusterToEntry ??= new FastIntDictionary<TEntry>(16);
            clusterToEntry.Clear();

            UniTextBase.MeshApplied += OnPreRender;
            uniText.TextProcessor.Shaped += OnShaped;
            uniText.MeshGenerator.onRebuildStart += OnRebuildStart;
            uniText.MeshGenerator.onRebuildEnd += OnRebuildEnd;

            var catalog = Catalog;
            if (catalog != null) catalog.Changed += OnCatalogChanged;
        }

        protected override void OnDisable()
        {
            UniTextBase.MeshApplied -= OnPreRender;
            uniText.TextProcessor.Shaped -= OnShaped;
            uniText.MeshGenerator.onRebuildStart -= OnRebuildStart;
            uniText.MeshGenerator.onRebuildEnd -= OnRebuildEnd;

            var catalog = Catalog;
            if (catalog != null) catalog.Changed -= OnCatalogChanged;

            foreach (var entry in tracked) entry.activeCount = 0;

            UniTextBase.MeshApplied -= CleanupOnDisable;
            UniTextBase.MeshApplied += CleanupOnDisable;
        }

        protected override void OnDestroy()
        {
            clusterToEntry?.Clear();
            clusterToEntry = null;
        }

        /// <summary>
        /// Re-subscribes a swapped provider when the modifier is currently active. Concrete
        /// subclasses call this from the <c>Provider</c> property setter.
        /// </summary>
        protected void RebindCatalog(INamedCatalog<TEntry> oldCatalog, INamedCatalog<TEntry> newCatalog)
        {
            if (!IsInitialized) return;
            if (oldCatalog != null) oldCatalog.Changed -= OnCatalogChanged;
            if (newCatalog != null) newCatalog.Changed += OnCatalogChanged;
        }

        private void OnCatalogChanged() => uniText?.SetDirty(UniTextDirtyFlags.Text);

        protected override void OnApply(int start, int end, string parameter)
        {
            if (string.IsNullOrEmpty(parameter)) return;
            var catalog = Catalog;
            if (catalog == null) return;

            var commaIdx = parameter.IndexOf(',');
            var name = commaIdx < 0 ? parameter : parameter.Substring(0, commaIdx).TrimEnd();
            if (!catalog.TryGet(name, out var entry) || entry == null) return;

            clusterToEntry[start] = entry;
            tracked.Add(entry);

            var extras = commaIdx < 0
                ? ReadOnlySpan<char>.Empty
                : parameter.AsSpan(commaIdx + 1);
            OnExtraTokens(start, extras);
        }

        private void OnRebuildStart()
        {
            foreach (var entry in tracked) entry.activeCount = 0;
        }

        private void OnShaped()
        {
            if (clusterToEntry == null || clusterToEntry.Count == 0) return;

            var buf = buffers;
            var fontSize = buf.shapingFontSize > 0 ? buf.shapingFontSize : uniText.FontSize;
            var glyphs = buf.shapedGlyphs.data;
            var runs = buf.shapedRuns.data;
            var runCount = buf.shapedRuns.count;

            for (var r = 0; r < runCount; r++)
            {
                ref var run = ref runs[r];
                var glyphEnd = run.glyphStart + run.glyphCount;
                float width = 0;

                for (var g = run.glyphStart; g < glyphEnd; g++)
                {
                    var globalCluster = glyphs[g].cluster;
                    if (clusterToEntry.TryGetValue(globalCluster, out var entry))
                    {
                        glyphs[g].glyphId = -1;
                        glyphs[g].advanceX = entry.advance * fontSize;
                        glyphs[g].offsetX = entry.bearingX * fontSize;
                        glyphs[g].offsetY = entry.bearingY * fontSize;
                    }
                    width += glyphs[g].advanceX;
                }

                run.width = width;
            }
        }

        private void OnRebuildEnd()
        {
            if (clusterToEntry == null || clusterToEntry.Count == 0) return;

            var glyphs = uniText.ResultGlyphs;
            var fontSize = uniText.MeshGenerator.FontSize;

            for (var i = 0; i < glyphs.Length; i++)
            {
                if (!clusterToEntry.TryGetValue(glyphs[i].cluster, out var entry)) continue;
                if (!HasRenderable(entry)) continue;

                var glyph = glyphs[i];
                CreateInstance(entry, glyphs[i].cluster,
                    glyph.x + entry.bearingX * fontSize,
                    -glyph.y + entry.bearingY * fontSize,
                    entry.width * fontSize,
                    entry.height * fontSize);
            }
        }

        private void CreateInstance(TEntry entry, int cluster, float x, float y, float w, float h)
        {
            if (uniText == null) return;

            entry.activeCount++;

            TWrapper wrapper;
            if (entry.activeCount <= entry.instances.Count)
            {
                wrapper = (TWrapper)entry.instances[entry.activeCount - 1];
            }
            else
            {
                wrapper = new TWrapper { parent = uniText.cachedTransformData.rectTransform };
                entry.instances.Add(wrapper);
            }

            ConfigureWrapper(wrapper, entry, cluster);
            wrapper.isDirty = true;
            var p = wrapper.pivot;
            wrapper.anchoredPosition = new Vector2(x + w * p.x, y + h * p.y);
            wrapper.sizeDelta = new Vector2(w, h);
        }

        private void OnPreRender() => UpdateAndPruneTracked();

        private void CleanupOnDisable()
        {
            UniTextBase.MeshApplied -= CleanupOnDisable;
            if (uniText == null) return;
            UpdateAndPruneTracked();
        }

        private void UpdateAndPruneTracked()
        {
            if (tracked.Count == 0) return;

            List<TEntry> toRemove = null;
            foreach (var entry in tracked)
            {
                entry.UpdateInstances();
                if (entry.activeCount == 0 && entry.instances.Count == 0)
                {
                    toRemove ??= new List<TEntry>();
                    toRemove.Add(entry);
                }
            }
            if (toRemove != null)
                foreach (var entry in toRemove) tracked.Remove(entry);
        }

    }
}
