using System.Collections.Generic;
using System.Linq;
using TimboJimbo.Localization;
using UnityEditor;

namespace TimboJimboEditor.Localization.Utility
{
    public static class LocalizedValueEditorUtility
    {
        public static List<LocalizedValue> FindAllLocalizedValuesInProject()
        {
            return AssetDatabase.FindAssets($"t:{nameof(LocalizedValue)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<LocalizedValue>()
                .Where(lv => lv != null)
                .ToList();
        }
    }
}