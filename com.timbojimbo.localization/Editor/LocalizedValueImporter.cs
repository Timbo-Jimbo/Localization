using TimboJimbo.Localization;
using TimboJimboEditor.Localization.Utility;
using UnityEditor;

namespace TimboJimboEditor.Localization
{
    /// <summary>
    /// Keeps imported localized assets in step with the settings' locales: a newly created or re-imported asset gets an
    /// entry per locale, in the settings' order. Entries for unknown codes are kept; the inspector shows them.
    /// </summary>
    internal sealed class LocalizedValueImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (!LocalizationSettings.IsInitialized) return;

            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not LocalizedValue localizedValue) continue;
                    if (LocalizedValueEditorUtility.SyncWithProjectLocales(localizedValue))
                        AssetDatabase.SaveAssetIfDirty(localizedValue);
                }
            }
        }
    }
}
