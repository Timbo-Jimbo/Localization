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
        public override bool TrySeedDefaultValueFromTarget(GameObject target, LocalizationLocale locale, SerializedProperty valueProperty)
        {
            if (target.TryGetComponent(out Image image))
            {
                valueProperty.objectReferenceValue = image.sprite;
                return true;
            }

            if (target.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                valueProperty.objectReferenceValue = spriteRenderer.sprite;
                return true;
            }
            
            return false;
        }

    }
}