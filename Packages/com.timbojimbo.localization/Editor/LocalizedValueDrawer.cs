
using System;
using System.Collections.Generic;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TimboJimboEditor.Localization
{
    [CustomPropertyDrawer(typeof(LocalizedValue), true)]
    public class LocalizedValueDrawer : PropertyDrawer
    {
        private const float ObjectPickerButtonWidth = 20f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            DrawPicker(position, fieldInfo.FieldType, property, label);
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUI.GetPropertyHeight(property);
        }

        public static void DrawPicker(Rect position, Type localizedAssetType, SerializedProperty property, GUIContent label)
        {
            HandlePickerOpenClick(position, localizedAssetType, property);

            bool previousShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            GUI.SetNextControlName(property.propertyPath);
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.showMixedValue = previousShowMixedValue;
        }

        private static void HandlePickerOpenClick(Rect position, Type localizedAssetType, SerializedProperty property)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Space)
            {
                if (GUI.GetNameOfFocusedControl().Equals(property.propertyPath, StringComparison.Ordinal))
                {
                    ShowLocalizedAssetPicker(position, localizedAssetType, property);
                    currentEvent.Use();
                    return;
                }
            }

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0) return;

            Rect objectPickerPosition = position;
            objectPickerPosition.xMin = objectPickerPosition.xMax - ObjectPickerButtonWidth;

            if (!objectPickerPosition.Contains(currentEvent.mousePosition)) return;

            ShowLocalizedAssetPicker(objectPickerPosition, localizedAssetType, property);
            currentEvent.Use();
        }
        
        public static void ShowLocalizedAssetPicker(Rect position, Type localizedAssetType, SerializedProperty property)
        {
            var activatorScreenRect = GUIUtility.GUIToScreenRect(position);
            var dropdown = new LocalizedAssetDropdown(
                localizedAssetType,
                property,
                activatorScreenRect);

            dropdown.Show(position);
        }

        private sealed class LocalizedAssetDropdown : AdvancedDropdown
        {
            private static readonly Vector2 MinimumSize = new(320f, 360f);

            private readonly Type _assetType;
            private SerializedProperty _writeTargetProperty;
            private readonly Rect _activatorScreenRect;

            public LocalizedAssetDropdown(
                Type assetType,
                SerializedProperty property,
                Rect activatorScreenRect) : base(new AdvancedDropdownState())
            {
                _assetType = assetType;
                _writeTargetProperty = property;
                _activatorScreenRect = activatorScreenRect;
                minimumSize = MinimumSize;
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                LocalizedAssetFolder rootFolder = new(_assetType.Name);

                string[] guids = AssetDatabase.FindAssets($"t:{_assetType.Name}");
                Array.Sort(guids, CompareAssetNames);

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(path, _assetType);
                    if (asset == null) continue;

                    AddItemToFolder(rootFolder, asset, path);
                }

                SortFolder(rootFolder);
                LocalizedAssetFolder displayRootFolder = GetDisplayRootFolder(rootFolder);
                AdvancedDropdownItem root = new(GetFolderDisplayName(displayRootFolder));
                AddFolderChildren(root, displayRootFolder);

                if (displayRootFolder.Items.Count == 0 && displayRootFolder.Children.Count == 0)
                    root.AddChild(new AdvancedDropdownItem($"No {_assetType.Name} assets found"));

                root.AddChild(new CreateNewDropdownItem($"New..."));

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is LocalizedAssetDropdownItem assetItem)
                {
                    SelectItem(assetItem.Asset);
                    return;
                }

                if (item is CreateNewDropdownItem)
                {
                    var assetType = _assetType;
                    var screenRect = _activatorScreenRect;
                    var property = _writeTargetProperty;
                    EditorApplication.delayCall += () =>
                        LocalizedAssetCreationWindow.Show(screenRect, assetType, property);
                }
            }

            private static void AddItemToFolder(LocalizedAssetFolder root, UnityEngine.Object asset, string assetPath)
            {
                string folderPath = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(folderPath))
                {
                    root.Items.Add(asset);
                    return;
                }

                string[] segments = folderPath.Split('/');
                int startIndex = segments.Length > 0 && string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                LocalizedAssetFolder folder = root;
                for (int i = startIndex; i < segments.Length; i++)
                {
                    string segment = segments[i];
                    if (!string.IsNullOrEmpty(segment))
                        folder = folder.GetOrCreateChild(segment);
                }

                folder.Items.Add(asset);
            }

            private static void AddFolderChildren(AdvancedDropdownItem dropdownFolder, LocalizedAssetFolder folder)
            {
                for (int i = 0; i < folder.Children.Count; i++)
                {
                    LocalizedAssetFolder child = folder.Children[i];
                    AdvancedDropdownItem childDropdownFolder = new(child.Name);
                    AddFolderChildren(childDropdownFolder, child);
                    dropdownFolder.AddChild(childDropdownFolder);
                }

                for (int i = 0; i < folder.Items.Count; i++)
                    dropdownFolder.AddChild(new LocalizedAssetDropdownItem(folder.Items[i]));
            }

            private static void SortFolder(LocalizedAssetFolder folder)
            {
                folder.Items.Sort(CompareItemsByAssetName);
                folder.Children.Sort(CompareFoldersByName);

                for (int i = 0; i < folder.Children.Count; i++)
                    SortFolder(folder.Children[i]);
            }

            private static LocalizedAssetFolder GetDisplayRootFolder(LocalizedAssetFolder root)
            {
                LocalizedAssetFolder folder = root;
                while (folder.Items.Count == 0 && folder.Children.Count == 1)
                    folder = folder.Children[0];
                return folder;
            }

            private static string GetFolderDisplayName(LocalizedAssetFolder folder)
            {
                if (folder == null || string.IsNullOrEmpty(folder.Name)) return "Localized Assets";
                return folder.Name;
            }

            private static int CompareItemsByAssetName(UnityEngine.Object left, UnityEngine.Object right)
            {
                return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
            }

            private static int CompareFoldersByName(LocalizedAssetFolder left, LocalizedAssetFolder right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            }

            private void SelectItem(UnityEngine.Object asset)
            {
                if (asset == null) return;

                _writeTargetProperty.objectReferenceValue = asset;
                _writeTargetProperty.serializedObject.ApplyModifiedProperties();
            }

            private static int CompareAssetNames(string leftGuid, string rightGuid)
            {
                string leftPath = AssetDatabase.GUIDToAssetPath(leftGuid);
                string rightPath = AssetDatabase.GUIDToAssetPath(rightGuid);
                return string.Compare(GetAssetName(leftPath), GetAssetName(rightPath), StringComparison.OrdinalIgnoreCase);
            }

            private static string GetAssetName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
        }

        private sealed class LocalizedAssetDropdownItem : AdvancedDropdownItem
        {
            public readonly UnityEngine.Object Asset;
            public LocalizedAssetDropdownItem(UnityEngine.Object asset) : base(asset.name) { Asset = asset; }
        }

        private sealed class CreateNewDropdownItem : AdvancedDropdownItem
        {
            public CreateNewDropdownItem(string label) : base(label) { }
        }

        private sealed class LocalizedAssetFolder
        {
            public readonly string Name;
            public readonly List<LocalizedAssetFolder> Children = new();
            public readonly List<UnityEngine.Object> Items = new();

            public LocalizedAssetFolder(string name) { Name = name; }

            public LocalizedAssetFolder GetOrCreateChild(string name)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    LocalizedAssetFolder child = Children[i];
                    if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                        return child;
                }

                LocalizedAssetFolder newChild = new(name);
                Children.Add(newChild);
                return newChild;
            }
        }
    }

    internal sealed class LocalizedAssetCreationWindow : EditorWindow
    {
        private const string LocalizedPropertyName = "Localized";
        private const string DescriptionFieldName = "Description";
        private const string ValuesFieldName = "Values";
        private const string PairLocaleFieldName = "Locale";
        private const string PairValueFieldName = "Value";
        private const string DefaultFolder = "Assets/Localization";
        private const string FolderPrefKeyPrefix = "LocalizableValueDrawer.LastFolder.";
        private const string NameControlName = "CreateAssetName";

        private Type _assetType;

        // The asset is created in-memory up front so the editor's OnLocaleValueGUI hook can
        // render against real serialized state. Persisted via AssetDatabase.CreateAsset on
        // Create; destroyed on cancel/close.
        private ScriptableObject _asset;
        private SerializedObject _assetSerialized;
        private SerializedProperty _writeTargetProperty;
        private LocalizedValueEditor _assetEditor;
        private SerializedProperty _defaultLocaleValueProperty;
        private SerializedProperty _descriptionProperty;
        private LocalizationLocale _defaultLocale;
        private GUIContent _defaultLocaleValueLabel;
        private string _name;
        private bool _saved;

        private string _folder;
        private bool _focusQueued;
        private bool _suppressLostFocusClose;

        public static void Show(Rect activatorScreenRect, Type assetType, SerializedProperty property)
        {
            LocalizedAssetCreationWindow window = CreateInstance<LocalizedAssetCreationWindow>();
            window._assetType = assetType;
            window._writeTargetProperty = property;
            window._folder = EditorPrefs.GetString(FolderPrefKeyPrefix + assetType.Name, DefaultFolder);
            window.titleContent = new GUIContent($"Create {assetType.Name}");

            window.InitializeAsset();

            Vector2 size = new(360f, 360f);

            // Use ShowPopup so the window doesn't auto-close when focus is taken by the OS
            // folder picker. Dismissal is handled manually via Cancel button, Escape and
            // OnLostFocus.
            window.position = new Rect(activatorScreenRect.x, activatorScreenRect.yMax, size.x, size.y);
            window.minSize = new Vector2(size.x, 200f);
            window.maxSize = new Vector2(size.x, 800f);
            window.ShowPopup();
            window.Focus();
        }

        private void InitializeAsset()
        {
            _asset = CreateInstance(_assetType);
            _asset.hideFlags = HideFlags.DontSave;
            _asset.name = $"New {_assetType.Name}";
            _name = _asset.name;

            _assetSerialized = new SerializedObject(_asset);
            _descriptionProperty = _assetSerialized.FindProperty(DescriptionFieldName);
            _defaultLocale = LocalizationSettings.DefaultLocale;

            SerializedProperty valuesProperty = _assetSerialized.FindProperty(ValuesFieldName);
            if (valuesProperty != null && valuesProperty.isArray)
            {
                valuesProperty.arraySize = 1;
                SerializedProperty pair = valuesProperty.GetArrayElementAtIndex(0);
                SerializedProperty localeProperty = pair.FindPropertyRelative(PairLocaleFieldName);
                if (localeProperty != null) localeProperty.objectReferenceValue = _defaultLocale;

                _defaultLocaleValueProperty = pair.FindPropertyRelative(PairValueFieldName);
                _assetSerialized.ApplyModifiedPropertiesWithoutUndo();

                string localeName = _defaultLocale != null && !string.IsNullOrEmpty(_defaultLocale.NativeName)
                    ? _defaultLocale.NativeName
                    : "Default";
                _defaultLocaleValueLabel = new GUIContent($"{localeName} Value");
            }

            // Resolve the registered custom editor for this asset type (e.g. LocalizedStringEditor).
            // The popup delegates value rendering to its OnLocaleValueGUI hook so custom
            // editors automatically pick up the same control they use in the inspector.
            Editor created = Editor.CreateEditor(_asset);
            if (created is LocalizedValueEditor typed)
            {
                _assetEditor = typed;
                
                // seed some values with something relevant
                if(TryGetSingleTargetGo(out GameObject targetGo))
                {
                    if(_assetEditor.TrySeedDefaultValueFromTarget(targetGo, _defaultLocale, _defaultLocaleValueProperty))
                    {
                        _assetSerialized.ApplyModifiedPropertiesWithoutUndo();

                    }
                    _asset.name = _name = targetGo.name;
                }

                bool TryGetSingleTargetGo(out GameObject targetGo)
                {
                    targetGo = null;
                    if (_writeTargetProperty == null) return false;

                    var targets = _writeTargetProperty?.serializedObject?.targetObjects ?? Array.Empty<UnityEngine.Object>();

                    if (targets.Length != 1) return false;

                    if (targets[0] is GameObject go)
                    {
                        targetGo = go;
                        return true;
                    }

                    if (targets[0] is Component comp)
                    {
                        targetGo = comp.gameObject;
                        return true;
                    }

                    return false;
                }
            }
            else if (created != null)
            {
                DestroyImmediate(created);
            }
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
                GUIUtility.ExitGUI();
                return;
            }

            DrawBorder();

            const float padding = 4f;
            Rect contentRect = new(padding, padding, position.width - padding * 2f, position.height - padding * 2f);
            GUILayout.BeginArea(contentRect);

            EditorGUILayout.LabelField($"Create {_assetType.Name}", EditorStyles.boldLabel);

            DrawFolderField();

            GUI.SetNextControlName(NameControlName);
            _name = EditorGUILayout.TextField("Name", _name);

            if (_descriptionProperty != null)
            {
                _assetSerialized.UpdateIfRequiredOrScript();
                EditorGUILayout.PropertyField(_descriptionProperty);
                _assetSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (_defaultLocaleValueProperty != null)
            {
                _assetSerialized.UpdateIfRequiredOrScript();
                if (_defaultLocaleValueLabel != null)
                    EditorGUILayout.LabelField(_defaultLocaleValueLabel);

                if (_assetEditor != null)
                {
                    _assetEditor.OnLocaleValueGUI(_defaultLocale, _defaultLocaleValueProperty);
                }
                else
                {
                    EditorGUILayout.PropertyField(_defaultLocaleValueProperty, GUIContent.none, true);
                }
                _assetSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            bool canCreate = !string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(_folder);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Create"))
                {
                    CreateAsset();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.EndArea();

            if (!_focusQueued && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(NameControlName);
                _focusQueued = true;
            }
        }

        private void OnDestroy()
        {
            if (_assetEditor != null)
            {
                DestroyImmediate(_assetEditor);
                _assetEditor = null;
            }

            if (!_saved && _asset != null)
                DestroyImmediate(_asset);

            _asset = null;
            _assetSerialized = null;
            _defaultLocaleValueProperty = null;
            _descriptionProperty = null;
        }

        private void OnLostFocus()
        {
            if (_suppressLostFocusClose) return;
            Close();
        }

        private void DrawBorder()
        {
            if (Event.current.type != EventType.Repaint) return;

            Color borderColor = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 1f)
                : new Color(0.39f, 0.39f, 0.39f, 1f);

            Rect r = new(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), borderColor);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), borderColor);
        }

        private void DrawFolderField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Folder");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(_folder);
            }

            if (GUILayout.Button("...", GUILayout.Width(28f)))
            {
                string startFolder = AssetDatabase.IsValidFolder(_folder) ? _folder : "Assets";
                _suppressLostFocusClose = true;
                try
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Folder", startFolder, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string projectPath = Application.dataPath.Replace('\\', '/');
                        string normalized = picked.Replace('\\', '/');
                        if (normalized.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                        {
                            _folder = "Assets" + normalized.Substring(projectPath.Length);
                            if (string.IsNullOrEmpty(_folder)) _folder = "Assets";
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                "Invalid Folder",
                                "The selected folder must be inside the project's Assets folder.",
                                "OK");
                        }
                    }
                }
                finally
                {
                    Focus();
                    _suppressLostFocusClose = false;
                }
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CreateAsset()
        {
            string folder = string.IsNullOrWhiteSpace(_folder) ? DefaultFolder : _folder.Trim();
            EnsureFolderExists(folder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{_name.Trim()}.asset");
            _asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            _asset.hideFlags = HideFlags.None;

            AssetDatabase.CreateAsset(_asset, path);
            AssetDatabase.SaveAssets();

            EditorPrefs.SetString(FolderPrefKeyPrefix + _assetType.Name, folder);

            _saved = true;
            AssignToTargets(_asset);
            Close();
        }

        private void AssignToTargets(UnityEngine.Object asset)
        {
            _writeTargetProperty.objectReferenceValue = asset;
            _writeTargetProperty.serializedObject.ApplyModifiedProperties();
        }

        private static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] segments = folder.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal)) return;

            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}