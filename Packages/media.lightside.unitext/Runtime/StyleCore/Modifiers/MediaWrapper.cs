using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightSide
{
    /// <summary>
    /// Owns a single inline-flowed RectTransform created by <see cref="InlineMediaModifier{TEntry, TWrapper}"/>.
    /// Subclasses decide how the instance is created (prefab clone vs fresh GameObject) and how
    /// per-frame visual state is applied (sprite/color, prefab-internal rebuild).
    /// </summary>
    public abstract class MediaWrapper
    {
        public RectTransform instance;
        public Transform parent;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;
        public bool isDirty;
        public bool needDestroy;
        protected bool created;

        private static List<ICanvasElement> canvasElementsBuffer;

        public void Setup()
        {
            if (needDestroy)
            {
                needDestroy = false;
                created = false;
                Destroy();
            }

            if (!isDirty) return;
            isDirty = false;

            if (!created)
            {
                if (!TryCreate()) return;
                created = true;
#if UNITY_EDITOR
                instance.gameObject.hideFlags = HideFlags.HideAndDontSave;
                ObjTracker.Track(instance.gameObject, this);
#endif
            }
            
            instance.localScale = Vector3.one;
            instance.anchorMin = new Vector2(0, 1);
            instance.anchorMax = new Vector2(0, 1);
            instance.anchoredPosition = anchoredPosition;
            instance.pivot = pivot;
            instance.sizeDelta = sizeDelta;

            ApplyContent();
            ForceCanvasRebuild();
        }

        public void Destroy()
        {
            if (instance == null) return;
            ObjectUtils.SafeDestroy(instance.gameObject);
            instance = null;
            created = false;
            OnDestroyed();
        }

        /// <summary>
        /// Creates the underlying <see cref="instance"/> RectTransform (and any sibling components).
        /// Return <see langword="false"/> to abort creation — e.g. the renderable payload is missing.
        /// </summary>
        protected abstract bool TryCreate();

        /// <summary>
        /// Applies per-frame visual state (sprite, tint, etc.) to <see cref="instance"/> and its
        /// children. Called after the transform has been positioned and sized. No-op for wrappers
        /// whose payload is fully owned by an instantiated prefab.
        /// </summary>
        protected abstract void ApplyContent();

        /// <summary>Optional hook called after <see cref="Destroy"/> tears down the instance, for subclass state reset.</summary>
        protected virtual void OnDestroyed() { }

        private void ForceCanvasRebuild()
        {
            canvasElementsBuffer ??= new List<ICanvasElement>();
            instance.GetComponentsInChildren(canvasElementsBuffer);
            for (var i = 0; i <= (int)CanvasUpdate.PostLayout; i++)
            for (var j = 0; j < canvasElementsBuffer.Count; j++)
                canvasElementsBuffer[j].Rebuild((CanvasUpdate)i);

            for (var i = (int)CanvasUpdate.PreRender; i < (int)CanvasUpdate.LatePreRender; i++)
            for (var j = 0; j < canvasElementsBuffer.Count; j++)
                canvasElementsBuffer[j].Rebuild((CanvasUpdate)i);
        }
    }

    /// <summary>Wrapper that clones a UI prefab into the host UniText.</summary>
    public sealed class PrefabMediaWrapper : MediaWrapper
    {
        public RectTransform prefab;

        protected override bool TryCreate()
        {
            if (prefab == null) return false;
            instance = Object.Instantiate(prefab, parent);
            return true;
        }

        protected override void ApplyContent() { }
    }

    /// <summary>Wrapper that creates a single-Image GameObject on demand.</summary>
    public sealed class SpriteImageWrapper : MediaWrapper
    {
        public Sprite sprite;
        public Color color;
        public bool preserveAspect;
        private Image image;

        protected override bool TryCreate()
        {
            if (sprite == null) return false;
            var go = new GameObject("UniText Sprite", typeof(RectTransform));
            instance = go.GetComponent<RectTransform>();
            instance.SetParent(parent, false);
            image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return true;
        }

        protected override void ApplyContent()
        {
            if (image.sprite != sprite) image.sprite = sprite;
            if (image.color != color) image.color = color;
            if (image.preserveAspect != preserveAspect) image.preserveAspect = preserveAspect;
        }

        protected override void OnDestroyed() => image = null;
    }
}
