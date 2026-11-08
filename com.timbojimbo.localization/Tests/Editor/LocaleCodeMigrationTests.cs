using System.IO;
using NUnit.Framework;
using TimboJimbo.Localization;
using TimboJimboEditor.Localization.Utility;
using UnityEditor;
using UnityEngine;

namespace TimboJimboTests.Localization
{
    /// <summary>
    /// A 1.x asset (entries keyed by locale references) written to disk, imported, read, and migrated.
    /// </summary>
    public sealed class LocaleCodeMigrationTests
    {
        private const string Folder = "Assets/__LocalizationMigrationTests";
        private const string SettingsPath = Folder + "/Settings.asset";
        private const string StringPath = Folder + "/Greeting.asset";

        private LocalizationSettings _previousSettings;

        [SetUp]
        public void SetUp()
        {
            _previousSettings = LocalizationSettings.ActiveAsset;
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            if (_previousSettings != null)
                LocalizationSettings.Register(_previousSettings);
        }

        [Test]
        public void A1xAssetLoadsWithCodesFilledFromItsLocaleReferencesAndMigratesToCodes()
        {
            // The 1.x shape: a settings asset whose locales are sub-assets, referenced by the string's entries.
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            var english = AddLocale(settings, new LocaleCode("en"), "English");
            var french = AddLocale(settings, new LocaleCode("fr", "CA"), "français");
            using (var serialized = new SerializedObject(settings))
            {
                var locales = serialized.FindProperty("_locales");
                locales.arraySize = 2;
                locales.GetArrayElementAtIndex(0).objectReferenceValue = english;
                locales.GetArrayElementAtIndex(1).objectReferenceValue = french;
                serialized.FindProperty("_defaultLocale").objectReferenceValue = english;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
            LocalizationSettings.Register(settings);

            File.WriteAllText(StringPath, LegacyStringYaml(english, french));
            AssetDatabase.ImportAsset(StringPath, ImportAssetOptions.ForceSynchronousImport);

            var text = AssetDatabase.LoadAssetAtPath<LocalizedString>(StringPath);
            Assert.IsNotNull(text);

            // Loaded: the codes come from the references, so the asset already resolves...
            Assert.AreEqual(new LocaleCode("en"), text.Values[0].Code);
            Assert.AreEqual(new LocaleCode("fr", "CA"), text.Values[1].Code);
            Assert.AreEqual("Bonjour", text.Resolve(LocaleCode.Parse("fr-CA")));
            Assert.AreEqual("Hello", text.Resolve(LocaleCode.Parse("en")));
            // ...but still carries the references, so a build would still copy the locales.
            Assert.IsTrue(LocalizedValueEditorUtility.NeedsMigration(text));
            StringAssert.Contains("guid:", File.ReadAllText(StringPath));

            Assert.IsTrue(LocalizedValueEditorUtility.MigrateToLocaleCodes(text));
            AssetDatabase.SaveAssets();

            var saved = File.ReadAllText(StringPath);
            StringAssert.Contains("_language: en", saved);
            StringAssert.Contains("_language: fr", saved);
            StringAssert.Contains("_country: CA", saved);
            StringAssert.DoesNotContain("guid:", saved.Substring(saved.IndexOf("Values:", System.StringComparison.Ordinal)), "no entry references a locale any more");
            Assert.IsFalse(LocalizedValueEditorUtility.NeedsMigration(text));

            // Round trip: re-import and the migrated asset reads the same.
            AssetDatabase.ImportAsset(StringPath, ImportAssetOptions.ForceSynchronousImport);
            text = AssetDatabase.LoadAssetAtPath<LocalizedString>(StringPath);
            Assert.AreEqual("Bonjour", text.Resolve(LocaleCode.Parse("fr-CA")));
            Assert.AreEqual("Hello", text.Resolve(LocaleCode.Parse("en")));
        }

        [Test]
        public void SyncGivesAnAssetOneEntryPerProjectLocaleInOrderAndKeepsValues()
        {
            using var settings = new TestSettings("en", "fr", "de");
            var text = Strings.Create(settings, ("de", "Farbe"), ("en", "colour"), ("xx", "unknown"));

            Assert.IsTrue(LocalizedValueEditorUtility.SyncWithProjectLocales(text));

            CollectionAssert.AreEqual(new[] { "en", "fr", "de", "xx" }, text.Values.ConvertAll(v => v.Code.ToString()));
            Assert.AreEqual("colour", text.Values[0].Value);
            Assert.AreEqual("", text.Values[1].Value, "the new entry is empty");
            Assert.AreEqual("Farbe", text.Values[2].Value);
            Assert.AreEqual("unknown", text.Values[3].Value, "an entry the settings don't list is kept");

            Assert.IsFalse(LocalizedValueEditorUtility.SyncWithProjectLocales(text), "a second sync changes nothing");

            Assert.IsTrue(LocalizedValueEditorUtility.SyncWithProjectLocales(text, removeUnknownCodes: true));
            CollectionAssert.AreEqual(new[] { "en", "fr", "de" }, text.Values.ConvertAll(v => v.Code.ToString()));

            Assert.IsTrue(LocalizedValueEditorUtility.RemoveEntries(text, LocaleCode.Parse("fr")));
            CollectionAssert.AreEqual(new[] { "en", "de" }, text.Values.ConvertAll(v => v.Code.ToString()));
        }

        private static LocalizationLocale AddLocale(LocalizationSettings settings, LocaleCode code, string nativeName)
        {
            var locale = ScriptableObject.CreateInstance<LocalizationLocale>();
            locale.name = nativeName;
            using (var serialized = new SerializedObject(locale))
            {
                serialized.FindProperty("_languageCode").stringValue = code.Language;
                serialized.FindProperty("_countryCode").stringValue = code.Country;
                serialized.FindProperty("_nativeName").stringValue = nativeName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.AddObjectToAsset(locale, settings);
            return locale;
        }

        private static string LegacyStringYaml(LocalizationLocale english, LocalizationLocale french)
        {
            var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<LocalizedString>());
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(english, out string settingsGuid, out long englishId));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(french, out string _, out long frenchId));

            return
$@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}
  m_Name: Greeting
  m_EditorClassIdentifier:
  Description:
  Values:
  - Locale: {{fileID: {englishId}, guid: {settingsGuid}, type: 2}}
    Value: Hello
  - Locale: {{fileID: {frenchId}, guid: {settingsGuid}, type: 2}}
    Value: Bonjour
";
        }
    }
}
