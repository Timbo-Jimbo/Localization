using System.Collections.Generic;
using TimboJimbo.Localization;
using TimboJimboEditor.Localization.Utility;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization
{
    /// <summary>
    /// Re-saves every localized asset on locale codes. Needed once when moving a project from 1.x, whose entries were
    /// keyed by locale references; until it runs, 1.x assets still work (their codes are filled from the references on
    /// load) but every build copies the referenced locales.
    /// </summary>
    public static class LocaleCodeMigration
    {
        public const string MenuPath = "Tools/Localization/Migrate Assets To Locale Codes";

        [MenuItem(MenuPath)]
        public static void MigrateFromMenu()
        {
            var result = MigrateAll();
            var message = result.Migrated.Count == 0
                ? $"All {result.Checked} localized assets already use locale codes."
                : $"Migrated {result.Migrated.Count} of {result.Checked} localized assets to locale codes.";

            Debug.Log($"[Localization] {message}");
            EditorUtility.DisplayDialog("Migrate Assets To Locale Codes", message, "OK");
        }

        public static Result MigrateAll()
        {
            var result = new Result();
            var assets = LocalizedValueEditorUtility.FindAllLocalizedValuesInProject();

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < assets.Count; i++)
                {
                    var asset = assets[i];
                    result.Checked++;
                    EditorUtility.DisplayProgressBar("Migrate Assets To Locale Codes", asset.name, (i + 1f) / assets.Count);

                    var changed = LocalizedValueEditorUtility.MigrateToLocaleCodes(asset);
                    changed |= LocalizedValueEditorUtility.SyncWithProjectLocales(asset);
                    if (changed)
                        result.Migrated.Add(asset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        public sealed class Result
        {
            public int Checked;
            public readonly List<LocalizedValue> Migrated = new();
        }
    }
}
