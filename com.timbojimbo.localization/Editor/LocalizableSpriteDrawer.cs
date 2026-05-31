using System;
using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization
{
    [UnityEditor.CustomPropertyDrawer(typeof(LocalizableSprite))]
    public sealed class LocalizableSpriteDrawer : LocalizableValueDrawer
    {
        protected override Type LocalizedAssetType => typeof(LocalizedSprite);
    }
}