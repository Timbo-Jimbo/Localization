using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization
{
    [CustomEditor(typeof(LocalizationSettings))]
    public sealed class LocalizationSettingsEditor : Editor
    {
        private const string LocalesPropertyName = "_locales";
        private const string DefaultLocalePropertyName = "_defaultLocale";
        private const string FoldoutStateKeyPrefix = "LocalizationSettingsEditor.Foldout.";
        private const float DefaultTagWidth = 60f;
        private SerializedProperty _localesProperty;
        private SerializedProperty _defaultLocaleProperty;

        private readonly Dictionary<UnityEngine.Object, Editor> _localeEditors = new();


        [InitializeOnLoadMethod]
        private static void InitializeInstanceInEditMode()
        {
            LocalizationBootstrapper.EnsureSettingsLoaded();
        }

        private void OnEnable()
        {
            _localesProperty = serializedObject.FindProperty(LocalesPropertyName);
            _defaultLocaleProperty = serializedObject.FindProperty(DefaultLocalePropertyName);
        }

        private void OnDisable()
        {
            foreach (var editor in _localeEditors.Values)
            {
                if (editor != null) DestroyImmediate(editor);
            }
            _localeEditors.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawLocalizationSettingsEditor();
            DrawEntries();
            DrawAddButton();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLocalizationSettingsEditor()
        {
            int total = _localesProperty.arraySize;
            int missing = CountMissing();
            int valid = Mathf.Max(0, total - missing);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Locales", LocalizationEditorGUI.HeaderStyle);
                GUILayout.FlexibleSpace();

                string statText = missing == 0
                    ? $"{total} {(total == 1 ? "locale" : "locales")}"
                    : $"{valid} / {total} assigned  •  {missing} missing";
                GUILayout.Label(statText, LocalizationEditorGUI.StatLabelStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
            }

            if (total == 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "No locales configured. Click \"Add Locale\" below to create one.",
                    MessageType.Info);
            }
            else if (_defaultLocaleProperty.objectReferenceValue == null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("No default locale set. Mark one of the locales as default.", MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawEntries()
        {
            for (int i = 0; i < _localesProperty.arraySize; i++)
            {
                DrawEntry(i);
            }
        }

        private void DrawEntry(int index)
        {
            SerializedProperty localeProperty = _localesProperty.GetArrayElementAtIndex(index);
            var locale = localeProperty != null ? localeProperty.objectReferenceValue as LocalizationLocale : null;
            bool isDefault = locale != null && locale == _defaultLocaleProperty.objectReferenceValue;

            bool expanded = GetEntryExpanded(index);
            bool menuClicked = false;
            LocalizationEditorGUI.DrawFoldout(
                ref expanded,
                () => DrawEntryHeaderContent(locale, isDefault, ref menuClicked),
                onToggle: toggle => SetEntryExpanded(index, toggle),
                onGroupToggle: toggle =>
                {
                    for (int i = 0; i < _localesProperty.arraySize; i++)
                    {
                        SetEntryExpanded(i, toggle);
                    }
                }
            );

            if (menuClicked)
                ShowEntryMenu(locale, isDefault);

            if (!expanded || locale == null) return;

            EditorGUILayout.Space(4f);

            Editor subEditor = GetOrCreateSubEditor(locale);
            if (subEditor != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    subEditor.OnInspectorGUI();
                }
            }

            EditorGUILayout.Space(2f);
        }

        private static void DrawEntryHeaderContent(LocalizationLocale locale, bool isDefault, ref bool menuClicked)
        {
            GUILayout.Label(GetLocaleTitle(locale), LocalizationEditorGUI.RowTitleStyle, GUILayout.ExpandWidth(false));

            string subtitle = GetLocaleSubtitle(locale);
            if (!string.IsNullOrEmpty(subtitle))
            {
                GUILayout.Space(6f);
                GUILayout.Label(subtitle, LocalizationEditorGUI.RowSubtitleStyle, GUILayout.ExpandWidth(false));
            }

            GUILayout.FlexibleSpace();

            if (locale == null)
            {
                GUILayout.Label("Missing", LocalizationEditorGUI.MissingTagStyle, GUILayout.Width(DefaultTagWidth));
            }
            else if (isDefault)
            {
                GUILayout.Label("Default", LocalizationEditorGUI.DefaultTagStyle, GUILayout.Width(DefaultTagWidth));
            }
            else
            {
                GUILayout.Space(DefaultTagWidth);
            }

            if (LocalizationEditorGUI.DrawKebabMenu())
            {
                menuClicked = true;
            }
        }

        private void ShowEntryMenu(LocalizationLocale locale, bool isDefault)
        {
            var menu = new GenericMenu();
            var settings = (LocalizationSettings)target;

            if (locale != null)
            {
                if (isDefault)
                {
                    menu.AddDisabledItem(new GUIContent("Set as Default"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Set as Default"), false, () => EditorApplication.delayCall = () =>
                    {
                        serializedObject.Update();
                        var activeLocalePrev = LocalizationSettings.ActiveLocale;

                        _defaultLocaleProperty.objectReferenceValue = locale;
                        serializedObject.ApplyModifiedProperties();

                        if (activeLocalePrev != LocalizationSettings.ActiveLocale)
                            LocalizationSettings.SetActiveLocale(LocalizationSettings.ActiveLocale);
                        
                    });
                }
                menu.AddSeparator(string.Empty);
            }

            string deleteLabel = locale != null ? $"Delete \"{GetLocaleTitle(locale)}\"" : "Remove Missing Entry";
            menu.AddItem(new GUIContent(deleteLabel), false, () => EditorApplication.delayCall = () => ConfirmAndRemove(settings, locale));

            menu.ShowAsContext();
        }

        private void ConfirmAndRemove(LocalizationSettings settings, LocalizationLocale locale)
        {
            if (locale != null)
            {
                string title = GetLocaleTitle(locale);
                bool confirmed = EditorUtility.DisplayDialog(
                    "Delete Locale",
                    $"Delete locale \"{title}\"?\n\nAny LocalizedString or LocalizedSprite values pointing at it will lose their translation. This cannot be undone via Ctrl+Z reliably.",
                    "Delete",
                    "Cancel");
                if (!confirmed) return;
            }

            RemoveLocale(settings, locale);
        }

        private void DrawAddButton()
        {
            if (GUILayout.Button("+ Add Locale", GUILayout.Height(24f)))
            {
                Rect anchor = GUILayoutUtility.GetLastRect();
                ShowAddLocaleDropdown(anchor);
            }
        }

        private void ShowAddLocaleDropdown(Rect anchor)
        {
            var settings = (LocalizationSettings)target;
            var targetLocales = settings.GetLocales();
            var existingCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < targetLocales.Count; i++)
            {
                var existing = targetLocales[i];
                if (existing == null) continue;
                string code = existing.DisplayCode;
                if (!string.IsNullOrEmpty(code)) existingCodes.Add(code);
            }

            var dropdown = new AddLocaleDropdown(existingCodes, culture =>
            {
                // Defer the actual creation so the dropdown finishes closing first.
                EditorApplication.delayCall += () => CreateLocale(settings, culture);
            });
            dropdown.Show(anchor);
        }

        private void RemoveLocale(LocalizationSettings settings, LocalizationLocale locale)
        {
            if (settings == null) return;

            var isDeletingActiveLocale = locale != null && locale == LocalizationSettings.ActiveLocale;

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty(LocalesPropertyName);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == locale || (locale == null && element.objectReferenceValue == null))
                {
                    element.objectReferenceValue = null; // Required before DeleteArrayElementAtIndex on object refs.
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            SerializedProperty defaultProp = so.FindProperty(DefaultLocalePropertyName);
            if (defaultProp.objectReferenceValue == locale)
            {
                defaultProp.objectReferenceValue = list.arraySize > 0
                    ? list.GetArrayElementAtIndex(0).objectReferenceValue
                    : null;
            }
            so.ApplyModifiedProperties();

            if (locale != null && AssetDatabase.IsSubAsset(locale))
            {
                Undo.DestroyObjectImmediate(locale);
            }

            if (locale != null && _localeEditors.TryGetValue(locale, out var editor))
            {
                if (editor != null) DestroyImmediate(editor);
                _localeEditors.Remove(locale);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            if(isDeletingActiveLocale)
                LocalizationSettings.ClearActiveLocale();
        }

        private static LocalizationLocale CreateLocale(LocalizationSettings settings, CultureInfo culture)
        {
            var locale = CreateInstance<LocalizationLocale>();

            if (culture != null)
            {
                locale.name = !string.IsNullOrEmpty(culture.NativeName) ? culture.NativeName : culture.EnglishName;
            }
            else
            {
                locale.name = "New Locale";
            }

            SerializedObject localeSo = new SerializedObject(locale);
            if (culture != null)
            {
                string[] parts = culture.Name.Split('-');
                localeSo.FindProperty("_languageCode").stringValue = parts.Length > 0 ? parts[0] : string.Empty;
                localeSo.FindProperty("_countryCode").stringValue = parts.Length > 1 ? parts[1] : string.Empty;
                localeSo.FindProperty("_nativeName").stringValue = culture.NativeName;
            }
            localeSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(locale, "Create Locale");
            AssetDatabase.AddObjectToAsset(locale, settings);

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty(LocalesPropertyName);
            int newIndex = list.arraySize;
            list.InsertArrayElementAtIndex(newIndex);
            list.GetArrayElementAtIndex(newIndex).objectReferenceValue = locale;

            SerializedProperty defaultProp = so.FindProperty(DefaultLocalePropertyName);
            if (defaultProp.objectReferenceValue == null)
            {
                defaultProp.objectReferenceValue = locale;
            }

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            return locale;
        }

        private Editor GetOrCreateSubEditor(LocalizationLocale locale)
        {
            if (locale == null) return null;
            if (_localeEditors.TryGetValue(locale, out var editor) && editor != null)
            {
                return editor;
            }

            editor = CreateEditor(locale);
            _localeEditors[locale] = editor;
            return editor;
        }

        private int CountMissing()
        {
            int count = 0;
            for (int i = 0; i < _localesProperty.arraySize; i++)
            {
                SerializedProperty p = _localesProperty.GetArrayElementAtIndex(i);
                if (p == null || p.objectReferenceValue == null) count++;
            }
            return count;
        }

        private static string GetLocaleTitle(LocalizationLocale locale)
        {
            if (locale == null) return "Missing Locale";
            if (locale.CultureInfo != null) return locale.CultureInfo.EnglishName;
            if (!string.IsNullOrWhiteSpace(locale.NativeName)) return locale.NativeName;
            if (!string.IsNullOrWhiteSpace(locale.name)) return locale.name;
            return "Unnamed Locale";
        }

        private static string GetLocaleSubtitle(LocalizationLocale locale)
        {
            if (locale == null) return null;
            string code = locale.DisplayCode;
            return string.IsNullOrEmpty(code) ? null : code;
        }

        private bool GetEntryExpanded(int index) => SessionState.GetBool(GetFoldoutKey(index), false);
        private void SetEntryExpanded(int index, bool expanded) => SessionState.SetBool(GetFoldoutKey(index), expanded);

        private string GetFoldoutKey(int index)
        {
            UnityEngine.Object firstTarget = serializedObject.targetObject;
            string assetPath = firstTarget != null ? AssetDatabase.GetAssetPath(firstTarget) : null;
            string guid = string.IsNullOrEmpty(assetPath)
                ? (firstTarget != null ? firstTarget.GetInstanceID().ToString() : "none")
                : AssetDatabase.AssetPathToGUID(assetPath);
            return FoldoutStateKeyPrefix + guid + "." + index;
        }

        private sealed class AddLocaleDropdown : AdvancedDropdown
        {
            private static readonly Vector2 MinimumSize = new(320f, 360f);

            private readonly HashSet<string> _existingCodes;
            private readonly Action<CultureInfo> _onSelect;

            public AddLocaleDropdown(HashSet<string> existingCodes, Action<CultureInfo> onSelect) : base(new AdvancedDropdownState())
            {
                _existingCodes = existingCodes;
                _onSelect = onSelect;
                minimumSize = MinimumSize;
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Add Locale");

                var emptyItem = new CultureDropdownItem("Empty Locale", null);
                root.AddChild(emptyItem);
                root.AddSeparator();

                // Group cultures by parent language so the dropdown is browsable as a tree.
                var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.OrdinalIgnoreCase);

                var cultures = CultureCache.AllCultures;
                for (int i = 0; i < cultures.Count; i++)
                {
                    CultureInfo culture = cultures[i];
                    string parentName = culture.Parent != null && !string.IsNullOrEmpty(culture.Parent.EnglishName)
                        ? culture.Parent.EnglishName
                        : "Other";

                    if (!groups.TryGetValue(parentName, out AdvancedDropdownItem groupItem))
                    {
                        groupItem = new AdvancedDropdownItem(parentName);
                        groups.Add(parentName, groupItem);
                        root.AddChild(groupItem);
                    }

                    string label = $"{culture.EnglishName}  ({culture.Name})";
                    var item = new CultureDropdownItem(label, culture)
                    {
                        enabled = !_existingCodes.Contains(culture.Name),
                    };
                    groupItem.AddChild(item);
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is CultureDropdownItem cultureItem)
                {
                    _onSelect?.Invoke(cultureItem.Culture);
                }
            }

            private sealed class CultureDropdownItem : AdvancedDropdownItem
            {
                public readonly CultureInfo Culture;

                public CultureDropdownItem(string name, CultureInfo culture) : base(name)
                {
                    Culture = culture;
                }
            }

            private sealed class CultureCache
            {
                private static List<CultureInfo> _allCultures;

                public static IReadOnlyList<CultureInfo> AllCultures
                {
                    get { Ensure(); return _allCultures; }
                }

                private static void Ensure()
                {
                    if (_allCultures != null) return;

                    CultureInfo[] all = CultureInfo.GetCultures(CultureTypes.SpecificCultures | CultureTypes.NeutralCultures);
                    _allCultures = new List<CultureInfo>(all.Length);
                    for (int i = 0; i < all.Length; i++)
                    {
                        CultureInfo culture = all[i];
                        if (string.IsNullOrEmpty(culture.Name)) continue; // Skip invariant.
                        _allCultures.Add(culture);
                    }
                    _allCultures.Sort((a, b) => string.Compare(a.EnglishName, b.EnglishName, StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}