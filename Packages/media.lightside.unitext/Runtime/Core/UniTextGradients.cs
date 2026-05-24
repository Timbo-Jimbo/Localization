using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// ScriptableObject containing named gradients for use with &lt;gradient=name&gt; tags.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create via Assets → Create → UniText → Gradients.
    /// Reference in UniTextSettings to make gradients available to all UniText components.
    /// </para>
    /// </remarks>
    /// <seealso cref="GradientModifier"/>
    /// <seealso cref="UniTextSettings"/>
    [CreateAssetMenu(fileName = "UniTextGradients", menuName = "UniText/Gradients")]
    public sealed class UniTextGradients : NamedCatalogAsset<UniTextGradients.NamedGradient>
    {
        /// <summary>A named gradient entry.</summary>
        [Serializable]
        public struct NamedGradient
        {
            /// <summary>Name used in markup (e.g., "rainbow" for &lt;gradient=rainbow&gt;).</summary>
            public string name;

            /// <summary>The Unity Gradient with color stops.</summary>
            public Gradient gradient;
        }

        protected override string GetEntryName(NamedGradient entry) => entry.name;

        /// <summary>Resolves a gradient by name (case-insensitive). Convenience over the generic <see cref="NamedCatalogAsset{TEntry}.TryGet"/>.</summary>
        public bool TryGetGradient(string name, out Gradient gradient)
        {
            if (TryGet(name, out var entry))
            {
                gradient = entry.gradient;
                return gradient != null;
            }
            gradient = null;
            return false;
        }

        /// <summary>All gradient names exposed by this asset. Convenience over the generic <see cref="NamedCatalogAsset{TEntry}.Names"/>.</summary>
        public IEnumerable<string> GradientNames => Names;

        /// <summary>Appends a named gradient by name + payload pair.</summary>
        public void Add(string gradientName, Gradient gradient)
        {
            Add(new NamedGradient { name = gradientName, gradient = gradient });
        }
    }
}
