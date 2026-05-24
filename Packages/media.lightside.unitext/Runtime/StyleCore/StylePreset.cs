using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Shareable, project-asset container of <see cref="Style"/> entries that components apply
    /// in bulk via <see cref="UniTextBase.AddStylePreset"/> or via
    /// <see cref="UniTextSettings.GlobalStylePreset"/>.
    /// </summary>
    /// <remarks>
    /// Mutating the preset at runtime through <see cref="AddStyle"/> / <see cref="RemoveStyle"/>
    /// / <see cref="ClearStyles"/> raises <see cref="Changed"/>; every <see cref="UniTextBase"/>
    /// that has this preset attached (locally or as the global preset) discards its runtime
    /// preset copies and re-instantiates from the new contents on the next frame.
    /// </remarks>
    [CreateAssetMenu(fileName = "StylePreset", menuName = "UniText/Style Preset")]
    public class StylePreset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Modifier/rule pairs that define how markup is parsed and applied (e.g., color, bold, links).")]
        private StyledList<Style> styles = new();

        /// <summary>
        /// Raised whenever the preset's styles list is mutated — either through the editor
        /// inspector (<c>OnValidate</c>) or programmatically via <see cref="AddStyle"/> /
        /// <see cref="RemoveStyle"/> / <see cref="ClearStyles"/>. Subscribers are expected to
        /// be idempotent (the event may fire several times in a row on rapid edits).
        /// </summary>
        public event Action Changed;

        /// <summary>Snapshot view of the contained styles. Mutate via <see cref="AddStyle"/>
        /// / <see cref="RemoveStyle"/> / <see cref="ClearStyles"/> so subscribers are notified.</summary>
        public IReadOnlyList<Style> Styles => styles;

        /// <summary>Appends a style to the preset and raises <see cref="Changed"/>.</summary>
        public void AddStyle(Style style)
        {
            if (style == null) return;
            styles.Add(style);
            Changed?.Invoke();
        }

        /// <summary>Removes the first occurrence of <paramref name="style"/>. Returns <see langword="true"/> on success.</summary>
        public bool RemoveStyle(Style style)
        {
            if (style == null) return false;
            if (!styles.Remove(style)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the style at the given index and raises <see cref="Changed"/>.</summary>
        public void RemoveStyleAt(int index)
        {
            if ((uint)index >= (uint)styles.Count) return;
            styles.RemoveAt(index);
            Changed?.Invoke();
        }

        /// <summary>Removes every style from the preset and raises <see cref="Changed"/>.</summary>
        public void ClearStyles()
        {
            if (styles.Count == 0) return;
            styles.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Internal access for migration paths and editor tooling that need the live list
        /// reference (drag-and-drop reordering, undoable edits). Production code should go
        /// through <see cref="AddStyle"/> / <see cref="RemoveStyle"/> / <see cref="ClearStyles"/>.
        /// </summary>
        internal StyledList<Style> StylesList => styles;

#if UNITY_EDITOR
        private void OnValidate()
        {
            Changed?.Invoke();
        }
#endif
    }
}
