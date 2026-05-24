using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>Combines multiple modifiers into a single modifier.</summary>
    /// <remarks>
    /// <para>
    /// Splits the tag parameter by <c>;</c> and passes each segment to the corresponding child modifier.
    /// For example, <c>&lt;wobble-color=3,10,1;#FF0000&gt;</c> passes <c>"3,10,1"</c> to the first modifier
    /// and <c>"#FF0000"</c> to the second.
    /// </para>
    /// <para>
    /// Child modifiers with no corresponding segment (fewer <c>;</c> groups than modifiers) receive
    /// a null parameter and fall back to their defaults.
    /// </para>
    /// </remarks>
    [Serializable]
    [TypeGroup("Utility", 10)]
    [TypeDescription("Combines multiple modifiers into one, splitting parameters by ';'.")]
    public sealed class CompositeModifier : BaseModifier
    {
        [Tooltip("Child modifiers to apply in order. Parameters are split by ';'.")]
        [SerializeField] private TypedList<BaseModifier> modifiers = new();

        /// <summary>
        /// Child modifiers applied in order. The tag parameter is split by <c>;</c>, with the
        /// <c>n</c>-th segment passed to the <c>n</c>-th child; absent segments deliver
        /// <see langword="null"/> so the child falls back to its own defaults.
        /// </summary>
        /// <remarks>
        /// Direct list mutation does not notify the owning component — use <see cref="SetModifiers"/>,
        /// <see cref="AddModifier"/>, <see cref="RemoveModifierAt"/>, or call
        /// <c>uniText.SetDirty(UniTextDirtyFlags.Text)</c> manually so the new child set is
        /// re-initialised and re-applied in the next parse pass.
        /// </remarks>
        public IReadOnlyList<BaseModifier> Modifiers => modifiers;

        /// <summary>Replaces the child modifier list, re-initialises children, and queues a re-parse.</summary>
        public void SetModifiers(IEnumerable<BaseModifier> newChildren)
        {
            DisposeChildren();
            modifiers.Clear();
            if (newChildren != null)
            {
                foreach (var m in newChildren)
                    modifiers.Add(m);
            }
            ResetChildren();
            uniText?.SetDirty(UniTextDirtyFlags.Text);
        }

        /// <summary>Appends one child modifier and queues a re-parse.</summary>
        public void AddModifier(BaseModifier child)
        {
            if (child == null) return;
            modifiers.Add(child);
            if (IsInitialized && uniText != null)
            {
                child.SetOwner(uniText);
                child.Prepare();
            }
            uniText?.SetDirty(UniTextDirtyFlags.Text);
        }

        /// <summary>Removes the child modifier at <paramref name="index"/> and queues a re-parse.</summary>
        public void RemoveModifierAt(int index)
        {
            if ((uint)index >= (uint)modifiers.Count) return;
            modifiers[index]?.Destroy();
            modifiers.RemoveAt(index);
            uniText?.SetDirty(UniTextDirtyFlags.Text);
        }

        private void DisposeChildren()
        {
            for (var i = 0; i < modifiers.Count; i++)
                modifiers[i]?.Destroy();
        }

        private void ResetChildren()
        {
            if (!IsInitialized || uniText == null) return;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                if (mod == null) continue;
                mod.SetOwner(uniText);
                mod.Prepare();
            }
        }

        public override void PrepareForParallel()
        {
            for (var i = 0; i < modifiers.Count; i++)
                modifiers[i]?.PrepareForParallel();
        }

        protected override void OnEnable()
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                if (mod == null) continue;
                mod.SetOwner(uniText);
                if (mod.IsInitialized) mod.Disable();
                mod.Prepare();
            }
        }

        protected override void OnDisable()
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                if (mod != null && mod.IsInitialized)
                    mod.Disable();
            }
        }

        protected override void OnDestroy()
        {
            for (var i = 0; i < modifiers.Count; i++)
                modifiers[i]?.Destroy();
        }

        protected override void OnApply(int start, int end, string parameter)
        {
            var count = modifiers.Count;
            if (count == 0) return;

            if (string.IsNullOrEmpty(parameter))
            {
                for (var i = 0; i < count; i++)
                    modifiers[i]?.Apply(start, end, null);
                return;
            }

            var span = parameter.AsSpan();
            var modIndex = 0;

            while (modIndex < count)
            {
                var sepIdx = span.IndexOf(';');
                ReadOnlySpan<char> segment;

                if (sepIdx < 0)
                {
                    segment = span;
                    span = ReadOnlySpan<char>.Empty;
                }
                else
                {
                    segment = span.Slice(0, sepIdx);
                    span = span.Slice(sepIdx + 1);
                }

                var mod = modifiers[modIndex];
                if (mod != null)
                {
                    var segStr = segment.IsEmpty ? null : segment.ToString();
                    mod.Apply(start, end, segStr);
                }

                modIndex++;
                if (sepIdx < 0) break;
            }

            for (var i = modIndex; i < count; i++)
                modifiers[i]?.Apply(start, end, null);
        }
    }
}
