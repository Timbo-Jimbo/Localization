using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization
{
    [InitializeOnLoad]
    internal static class LocalizationBootstrapper
    {
        private const string LocalesPropertyName = "_locales";
        private const string DefaultLocalePropertyName = "_defaultLocale";
        private const string PromptDismissedSessionKey = "TimboJimbo.Localization.Bootstrapper.PromptDismissed";
        private const string DefaultRootFolder = "Assets/Localization";
        private const string SettingsFolderName = "Settings";
        private const string SettingsAssetName = "LocalizationSettings";
        private const string HelloWorldAssetName = "HelloWorld";
        private const string HelloWorldAssetValue = "Hello, World!";

        private static bool _promptQueued;
        private static bool _promptOpen;

        static LocalizationBootstrapper()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.delayCall += EnsureSettingsPreloadedOnLaunch;
            QueuePromptIfNeeded();
        }

        internal static LocalizationSettings EnsureSettingsLoaded(bool promptToCreate = false)
        {
            if (LocalizationSettings.IsInitialized)
            {
                ClearDismissedPrompt();
                EnsurePreloadedAsset(LocalizationSettings.ActiveAsset);
                return LocalizationSettings.ActiveAsset;
            }

            LocalizationSettings settings = FindSettingsAsset();
            if (settings != null)
            {
                ClearDismissedPrompt();
                EnsurePreloadedAsset(settings);
                return settings;
            }

            if (promptToCreate)
            {
                return PromptForMissingSettings(forcePrompt: true);
            }

            return null;
        }

        internal static LocalizationSettings FindSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(LocalizationSettings)}");
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<LocalizationSettings>(path);
        }

        private static void OnProjectChanged()
        {
            LocalizationSettings settings = FindSettingsAsset();
            if (settings != null)
            {
                ClearDismissedPrompt();
                EnsurePreloadedAsset(settings);
                return;
            }

            QueuePromptIfNeeded();
        }

        private static void EnsureSettingsPreloadedOnLaunch()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            LocalizationSettings settings = EnsureSettingsLoaded();
            if (settings != null)
            {
                EnsurePreloadedAsset(settings);
            }
        }

        private static void QueuePromptIfNeeded()
        {
            if (Application.isBatchMode || _promptQueued)
            {
                return;
            }

            _promptQueued = true;
            EditorApplication.delayCall += TryPromptForMissingSettings;
        }

        private static void TryPromptForMissingSettings()
        {
            _promptQueued = false;

            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueuePromptIfNeeded();
                return;
            }

            if (FindSettingsAsset() != null)
            {
                ClearDismissedPrompt();
                return;
            }

            if (SessionState.GetBool(PromptDismissedSessionKey, false))
            {
                return;
            }

            PromptForMissingSettings(forcePrompt: false);
        }

        private static LocalizationSettings PromptForMissingSettings(bool forcePrompt)
        {
            if (Application.isBatchMode || _promptOpen)
            {
                return null;
            }

            if (!forcePrompt && SessionState.GetBool(PromptDismissedSessionKey, false))
            {
                return null;
            }

            LocalizationSettings existingSettings = FindSettingsAsset();
            if (existingSettings != null)
            {
                ClearDismissedPrompt();
                return existingSettings;
            }

            _promptOpen = true;
            try
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "Localization Setup",
                    "No LocalizationSettings asset was found. Choose a folder to scaffold the default localization assets.",
                    "Choose Folder",
                    "Not Now",
                    "Cancel");

                if (option != 0)
                {
                    if (!forcePrompt)
                    {
                        SessionState.SetBool(PromptDismissedSessionKey, true);
                    }

                    return null;
                }

                string selectedFolder = EditorUtility.OpenFolderPanel(
                    "Create Localization Assets In",
                    GetDefaultAbsoluteFolder(),
                    string.Empty);

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    if (!forcePrompt)
                    {
                        SessionState.SetBool(PromptDismissedSessionKey, true);
                    }

                    return null;
                }

                //Make sure the folder is visible to the AssetDatabase before trying to create assets in it
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!TryGetAssetFolder(selectedFolder, out string assetFolder))
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Folder",
                        "The selected folder must be inside the project's Assets folder.",
                        "OK");
                    return null;
                }

                LocalizationSettings createdSettings = CreateBootstrapAssets(assetFolder);
                if (createdSettings != null)
                {
                    ClearDismissedPrompt();
                    EnsurePreloadedAsset(createdSettings);
                    Selection.activeObject = createdSettings;
                }

                return createdSettings;
            }
            finally
            {
                _promptOpen = false;
            }
        }

        private static LocalizationSettings CreateBootstrapAssets(string rootFolder)
        {
            EnsureFolderExists(rootFolder);

            string settingsFolder = $"{rootFolder}/{SettingsFolderName}";
            EnsureFolderExists(settingsFolder);

            string settingsPath = $"{settingsFolder}/{SettingsAssetName}.asset";
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = SettingsAssetName;
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            LocalizationLocale englishLocale = EnsureEnglishLocale(settings);
            EnsureHelloWorldAsset(rootFolder, englishLocale);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
        }

        private static void EnsurePreloadedAsset(LocalizationSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            string settingsPath = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrEmpty(settingsPath))
            {
                return;
            }

            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            List<UnityEngine.Object> updatedAssets = preloadedAssets
                .Where(asset => asset != null)
                .ToList();

            bool alreadyPresent = updatedAssets.Exists(asset =>
                asset == settings ||
                string.Equals(AssetDatabase.GetAssetPath(asset), settingsPath, StringComparison.OrdinalIgnoreCase));

            if (alreadyPresent && updatedAssets.Count == preloadedAssets.Length)
            {
                return;
            }

            if (!alreadyPresent)
            {
                updatedAssets.Add(settings);
            }

            PlayerSettings.SetPreloadedAssets(updatedAssets.ToArray());
        }

        private static LocalizationLocale EnsureEnglishLocale(LocalizationSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            SerializedObject settingsObject = new(settings);
            settingsObject.Update();

            SerializedProperty localesProperty = settingsObject.FindProperty(LocalesPropertyName);
            SerializedProperty defaultLocaleProperty = settingsObject.FindProperty(DefaultLocalePropertyName);

            LocalizationLocale englishLocale = FindLocale(localesProperty, "en", string.Empty);
            if (englishLocale == null)
            {
                englishLocale = CreateLocaleAsset(settings, CultureInfo.GetCultureInfo("en"));
                int newIndex = localesProperty.arraySize;
                localesProperty.InsertArrayElementAtIndex(newIndex);
                localesProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = englishLocale;
            }

            defaultLocaleProperty.objectReferenceValue = englishLocale;
            settingsObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(englishLocale);
            EditorUtility.SetDirty(settings);
            return englishLocale;
        }

        private static LocalizationLocale FindLocale(SerializedProperty localesProperty, string languageCode, string countryCode)
        {
            if (localesProperty == null)
            {
                return null;
            }

            for (int i = 0; i < localesProperty.arraySize; i++)
            {
                var locale = localesProperty.GetArrayElementAtIndex(i).objectReferenceValue as LocalizationLocale;
                if (locale == null)
                {
                    continue;
                }

                bool isLanguageMatch = string.Equals(locale.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase);
                bool isCountryMatch = string.Equals(locale.CountryCode ?? string.Empty, countryCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                if (isLanguageMatch && isCountryMatch)
                {
                    return locale;
                }
            }

            return null;
        }

        private static LocalizationLocale CreateLocaleAsset(LocalizationSettings settings, CultureInfo culture)
        {
            var locale = ScriptableObject.CreateInstance<LocalizationLocale>();
            locale.name = !string.IsNullOrEmpty(culture.EnglishName) ? culture.EnglishName : culture.Name;

            SerializedObject localeObject = new(locale);
            localeObject.FindProperty("_languageCode").stringValue = culture.TwoLetterISOLanguageName.ToLowerInvariant();
            localeObject.FindProperty("_countryCode").stringValue = string.Empty;
            localeObject.FindProperty("_nativeName").stringValue = culture.NativeName;
            localeObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(locale, settings);
            return locale;
        }

        private static void EnsureHelloWorldAsset(string rootFolder, LocalizationLocale englishLocale)
        {
            if (englishLocale == null)
            {
                return;
            }

            string helloWorldPath = $"{rootFolder}/{HelloWorldAssetName}.asset";
            var helloWorld = AssetDatabase.LoadAssetAtPath<LocalizedString>(helloWorldPath);

            if (helloWorld == null)
            {
                helloWorld = ScriptableObject.CreateInstance<LocalizedString>();
                helloWorld.name = HelloWorldAssetName;
                AssetDatabase.CreateAsset(helloWorld, helloWorldPath);
            }

            helloWorld.Values ??= new List<LocaleValuePair<string>>();

            bool updated = false;
            for (int i = 0; i < helloWorld.Values.Count; i++)
            {
                LocaleValuePair<string> pair = helloWorld.Values[i];
                if (pair == null || pair.Locale != englishLocale)
                {
                    continue;
                }

                pair.Value = HelloWorldAssetValue;
                updated = true;
                break;
            }

            if (!updated)
            {
                helloWorld.Values.Add(new LocaleValuePair<string>
                {
                    Locale = englishLocale,
                    Value = HelloWorldAssetValue,
                });
            }

            EditorUtility.SetDirty(helloWorld);
        }

        private static string GetDefaultAbsoluteFolder()
        {
            string defaultFolder = DefaultRootFolder.Replace('/', Path.DirectorySeparatorChar);
            string absoluteFolder = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, defaultFolder);
            return Directory.Exists(absoluteFolder) ? absoluteFolder : Application.dataPath;
        }

        private static bool TryGetAssetFolder(string absoluteFolder, out string assetFolder)
        {
            assetFolder = null;

            string normalizedProjectPath = Application.dataPath.Replace('\\', '/');
            string normalizedSelectedFolder = absoluteFolder.Replace('\\', '/');
            if (!normalizedSelectedFolder.StartsWith(normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetFolder = "Assets" + normalizedSelectedFolder.Substring(normalizedProjectPath.Length);
            if (string.IsNullOrEmpty(assetFolder))
            {
                assetFolder = "Assets";
            }

            return true;
        }

        private static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            {
                return;
            }

            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static void ClearDismissedPrompt()
        {
            SessionState.SetBool(PromptDismissedSessionKey, false);
        }
    }
}