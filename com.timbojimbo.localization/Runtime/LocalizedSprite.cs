using UnityEngine;

namespace TimboJimbo.Localization
{
    [CreateAssetMenu(fileName = "New Localized Sprite", menuName = "Localization/Localized Sprite")]
    public class LocalizedSprite : LocalizedValue<Sprite>
    {
        protected override bool IsMeaningfulValue(Sprite value)
        {
            return value != null;
        }
    }
}