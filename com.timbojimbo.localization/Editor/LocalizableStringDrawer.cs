using System;
using TimboJimbo.Localization;
using UnityEditor;

namespace TimboJimboEditor.Localization
{
    [CustomPropertyDrawer(typeof(LocalizableString))]
    public sealed class LocalizableStringDrawer : LocalizableValueDrawer
    {
        protected override Type LocalizedAssetType => typeof(LocalizedString);
    }
}