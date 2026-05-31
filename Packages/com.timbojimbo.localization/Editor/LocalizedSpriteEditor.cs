using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimboJimboEditor.Localization
{
    [CustomEditor(typeof(LocalizedSprite))]
    [CanEditMultipleObjects]
    public sealed class LocalizedSpriteEditor : LocalizedValueEditor
    {
        public override bool TryFindAndSeedDefaultValue(GameObject context, LocalizationLocale locale, SerializedProperty targetToSeed)
        {
            if (context.TryGetComponent(out Image image))
            {
                targetToSeed.objectReferenceValue = image.sprite;
                return true;
            }

            if (context.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                targetToSeed.objectReferenceValue = spriteRenderer.sprite;
                return true;
            }
            
            return false;
        }

    }
}