using System;
using System.Collections.Generic;

namespace LightSide
{
    /// <summary>
    /// Common entry-data base for inline-flowed media (<see cref="InlineSprite"/>,
    /// <see cref="InlineObject"/>). Carries the name and layout metrics that both shape
    /// (advance, bearing) and rendering (width, height) need; the renderable payload is
    /// added by the concrete subclass.
    /// </summary>
    /// <remarks>
    /// All metrics are in em units (relative to <c>fontSize</c>):
    /// <list type="bullet">
    /// <item><c>width</c>=1, <c>height</c>=1 means the media is <c>fontSize</c> x <c>fontSize</c> pixels</item>
    /// <item><c>advance</c>=1 means the cursor moves by <c>fontSize</c> pixels after this media</item>
    /// <item><c>bearingX</c>/<c>bearingY</c> are offsets relative to <c>fontSize</c></item>
    /// </list>
    /// </remarks>
    [Serializable]
    public abstract class InlineMedia
    {
        /// <summary>Lookup name used in the tag parameter (e.g. <c>jump</c> in <c>&lt;sprite=jump&gt;</c>). Case-insensitive.</summary>
        public string name;

        /// <summary>Width in em units (1 = fontSize).</summary>
        public float width = 1;

        /// <summary>Height in em units (1 = fontSize).</summary>
        public float height = 1;

        /// <summary>Horizontal offset in em units.</summary>
        public float bearingX;

        /// <summary>Vertical offset in em units.</summary>
        public float bearingY;

        /// <summary>Advance width in em units (1 = fontSize).</summary>
        public float advance = 1;

        /// <summary>Number of on-screen occurrences claimed in the current rebuild pass; reset to 0 on every rebuild start.</summary>
        [NonSerialized] public int activeCount;

        /// <summary>Pool of live wrappers backing the on-screen occurrences of this entry. Indexed up to <see cref="activeCount"/>.</summary>
        [NonSerialized] internal readonly List<MediaWrapper> instances = new();

        /// <summary>
        /// Trims excess wrappers when fewer occurrences are active than instances exist, then
        /// runs <see cref="MediaWrapper.Setup"/> on every remaining instance to apply pending
        /// transform/content updates. Called per frame after the mesh rebuild.
        /// </summary>
        public void UpdateInstances()
        {
            var diff = activeCount - instances.Count;

            if (diff < 0)
            {
                diff *= -1;
                for (var i = 0; i < diff; i++)
                {
                    var last = instances.Count - 1;
                    instances[last].Destroy();
                    instances.RemoveAt(last);
                }
            }

            for (var i = 0; i < instances.Count; i++)
                instances[i].Setup();
        }
    }
}
