using System;
using System.Collections.Generic;
using TimboJimbo.Localization;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;

namespace TimboJimboEditor.Localization
{
    [Overlay(typeof(SceneView), "Locale Changer", true)]
    public sealed class LocaleChangerOverlay : ToolbarOverlay
    {
        private const string LocaleDropdownElementId = "Localization/LocaleChangerDropdown";
        private const string IconAssetName = "LocalizedString";

        private static readonly List<LocalizationLocale> LocaleBuffer = new();

        private static LocalizationLocale[] _locales = Array.Empty<LocalizationLocale>();
        private static Texture2D _icon;

        public LocaleChangerOverlay() : base(LocaleDropdownElementId) { }

        [EditorToolbarElement(LocaleDropdownElementId, typeof(SceneView))]
        private sealed class LocaleDropdownElement : EditorToolbarDropdown
        {
            public LocaleDropdownElement()
            {
                tooltip = "Preview localized UI in the Scene View with a different active locale.";
                icon = GetIcon();
                clicked += ShowLocaleMenu;

                RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
                RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

                RefreshLabel();
            }

            private void OnAttachedToPanel(AttachToPanelEvent evt)
            {
                LocalizationSettings.OnActiveLocaleChanged += OnActiveLocaleChanged;
                EditorApplication.projectChanged += RefreshLabel;
                LocalePreviewPopupContent.OnActiveLocaleDisplayChanged += RefreshLabel;
                RefreshLabel();
            }

            private void OnDetachedFromPanel(DetachFromPanelEvent evt)
            {
                LocalizationSettings.OnActiveLocaleChanged -= OnActiveLocaleChanged;
                EditorApplication.projectChanged -= RefreshLabel;
                LocalePreviewPopupContent.OnActiveLocaleDisplayChanged -= RefreshLabel;
            }

            private void OnActiveLocaleChanged(LocalizationLocale locale)
            {
                RefreshLabel();
            }

            private void ShowLocaleMenu()
            {
                LocalizationBootstrapper.EnsureSettingsLoaded(promptToCreate: true);
                RefreshLabel();

                if (LocalizationSettings.IsInitialized == false)
                {
                    return;
                }

                PopupWindow.Show(worldBound, new LocalePreviewPopupContent());
            }

            private void RefreshLabel()
            {
                text = GetLocaleToolbarLabel();
            }
        }

        private static LocalizationSettings FindSettingsAsset()
        {
            return LocalizationBootstrapper.FindSettingsAsset();
        }

        private static LocalizationSettings EnsureSettingsLoaded()
        {
            return LocalizationBootstrapper.EnsureSettingsLoaded();
        }

        private static Texture2D GetIcon()
        {
            if (_icon != null)
            {
                return _icon;
            }

            string[] guids = AssetDatabase.FindAssets($"{IconAssetName} t:Texture2D");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != IconAssetName)
                {
                    continue;
                }

                _icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (_icon != null)
                {
                    return _icon;
                }
            }

            return null;
        }

        private static void RebuildLocaleOptions()
        {
            LocaleBuffer.Clear();

            IReadOnlyList<LocalizationLocale> configuredLocales = LocalizationSettings.Locales;
            for (int i = 0; i < configuredLocales.Count; i++)
            {
                LocalizationLocale locale = configuredLocales[i];
                if (locale != null)
                {
                    LocaleBuffer.Add(locale);
                }
            }

            if (LocaleBuffer.Count == 0 && LocalizationSettings.DefaultLocale != null)
            {
                LocaleBuffer.Add(LocalizationSettings.DefaultLocale);
            }

            _locales = LocaleBuffer.ToArray();
        }

        private sealed class LocalePreviewPopupContent : PopupWindowContent
        {
            private const float PopupWidth = 280f;
            private const float RowHeight = 22f;
            private const float HeaderHeight = 18f;
            private const float FooterSpacing = 8f;
            private const float Padding = 6f;
            private const int MaxVisibleLocaleRows = 30;

            private static GUIStyle _rowStyle;
            private static GUIStyle _headerStyle;
            private static GUIStyle _selectedRowStyle;
            private static GUIStyle _treeViewLineStyle;

            private readonly LocalizationLocale _originalLocale;
            private readonly bool _openedOnDefaultLocale;

            private Vector2 _scrollPosition;
            private LocalizationLocale _hoveredLocale;
            private bool _committed;

            public static event Action OnActiveLocaleDisplayChanged;
            public static (bool overrideShownActiveLocale, LocalizationLocale activeLocale) ActiveLocaleDisplay
            {
                get; private set;
            }

            private static GUIStyle RowStyle
            {
                get
                {
                    if (_rowStyle != null)
                    {
                        return _rowStyle;
                    }

                    _rowStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                    };

                    return _rowStyle;
                }
            }

            private static GUIStyle TreeViewLineStyle
            {
                get
                {
                    if (_treeViewLineStyle != null)
                    {
                        return _treeViewLineStyle;
                    }

                    _treeViewLineStyle = FindBuiltinStyle("TV Line") ?? RowStyle;
                    return _treeViewLineStyle;
                }
            }

            private static GUIStyle HeaderStyle
            {
                get
                {
                    if (_headerStyle != null)
                    {
                        return _headerStyle;
                    }

                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        margin = new RectOffset(0, 0, 0, 0),
                    };

                    return _headerStyle;
                }
            }

            private static GUIStyle SelectedRowStyle
            {
                get
                {
                    if (_selectedRowStyle != null)
                    {
                        return _selectedRowStyle;
                    }

                    _selectedRowStyle = new GUIStyle(EditorStyles.whiteLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(0, 0, 0, 0),
                    };

                    return _selectedRowStyle;
                }
            }

            private static GUIStyle FindBuiltinStyle(string styleName)
            {
                GUIStyle style = GUI.skin?.FindStyle(styleName);
                if (style != null)
                {
                    return style;
                }

                GUISkin inspectorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
                return inspectorSkin?.FindStyle(styleName);
            }

            public LocalePreviewPopupContent()
            {
                EnsureSettingsLoaded();
                _originalLocale = LocalizationSettings.ActiveLocale;
                _openedOnDefaultLocale = LocalizationSettings.ActiveLocale == LocalizationSettings.DefaultLocale;
            }

            public override void OnOpen()
            {
                if (editorWindow != null)
                    editorWindow.wantsMouseMove = true;

                ActiveLocaleDisplay = (true, LocalizationSettings.ActiveLocale);
                OnActiveLocaleDisplayChanged?.Invoke();
            }


            public override Vector2 GetWindowSize()
            {
                EnsureSettingsLoaded();
                RebuildLocaleOptions();

                int localeCount = LocalizationSettings.IsInitialized
                    ? Mathf.Max(1, _locales.Length)
                    : 1;
                int visibleLocaleCount = Mathf.Min(localeCount, MaxVisibleLocaleRows);
                float localeAreaHeight = visibleLocaleCount * RowHeight;

                float height = Padding * 2f
                    + HeaderHeight
                    + 4f
                    + localeAreaHeight
                    + FooterSpacing
                    + 1f
                    + FooterSpacing
                    + RowHeight;

                if (LocalizationSettings.IsInitialized == false)
                {
                    height += 40f;
                }

                return new Vector2(PopupWidth, height);
            }

            public override void OnGUI(Rect rect)
            {
                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
                {
                    editorWindow.Close();
                    currentEvent.Use();
                    return;
                }

                Rect contentRect = new(Padding, Padding, rect.width - Padding * 2f, rect.height - Padding * 2f);
                float listTop = contentRect.y + HeaderHeight + 4f;
                float settingsTop = contentRect.yMax - RowHeight;
                float separatorTop = settingsTop - FooterSpacing - 1f;
                float listBottom = separatorTop - FooterSpacing;

                Rect headerRect = new(contentRect.x, contentRect.y, contentRect.width, HeaderHeight);
                Rect listRect = new(contentRect.x, listTop, contentRect.width, Mathf.Max(0f, listBottom - listTop));
                Rect separatorRect = new(contentRect.x, separatorTop, contentRect.width, 1f);
                Rect settingsRect = new(contentRect.x, settingsTop, contentRect.width, RowHeight);

                GUI.Label(headerRect, "Active Locale", HeaderStyle);
                DrawLocaleList(listRect, currentEvent);

                DrawSeparator(separatorRect);

                if (LocalizationSettings.IsInitialized && GUI.Button(settingsRect, "Settings..."))
                {
                    Selection.activeObject = EnsureSettingsLoaded();
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }

                if (currentEvent.type == EventType.MouseMove)
                {
                    editorWindow.Repaint();
                }
            }

            public override void OnClose()
            {
                ActiveLocaleDisplay = (false, null);
                OnActiveLocaleDisplayChanged?.Invoke();

                if (_committed)
                {
                    return;
                }

                if (_openedOnDefaultLocale)
                {
                    LocalizationSettings.ClearActiveLocale();
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                    return;
                }

                ApplyLocale(_originalLocale);
            }

            private void DrawLocaleList(Rect listRect, Event currentEvent)
            {
                if (listRect.height <= 0f)
                {
                    return;
                }

                if (LocalizationSettings.IsInitialized == false)
                {
                    EditorGUI.HelpBox(listRect, "No LocalizationSettings asset found.", MessageType.Info);
                    return;
                }

                RebuildLocaleOptions();

                if (_locales.Length == 0)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUI.Button(new Rect(listRect.x, listRect.y, listRect.width, RowHeight), "No locales configured");
                    }

                    return;
                }

                int visibleLocaleCount = Mathf.Min(_locales.Length, MaxVisibleLocaleRows);
                float requestedListHeight = visibleLocaleCount * RowHeight;
                float viewportHeight = Mathf.Min(listRect.height, requestedListHeight);
                float contentHeight = _locales.Length * RowHeight;
                bool needsScroll = contentHeight > viewportHeight + 0.5f;
                float verticalScrollbarWidth = needsScroll ? 13f : 0f;

                Rect viewRect = new(0f, 0f, Mathf.Max(0f, listRect.width - verticalScrollbarWidth), contentHeight);

                if (needsScroll)
                {
                    _scrollPosition = GUI.BeginScrollView(listRect, _scrollPosition, viewRect, false, true);
                }
                else
                {
                    GUI.BeginGroup(listRect);
                }

                for (int i = 0; i < _locales.Length; i++)
                {
                    DrawLocaleRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), _locales[i], currentEvent);
                }

                if (needsScroll)
                {
                    GUI.EndScrollView();
                }
                else
                {
                    GUI.EndGroup();
                }
            }

            private void DrawLocaleRow(Rect rowRect, LocalizationLocale locale, Event currentEvent)
            {
                bool isHovered = rowRect.Contains(currentEvent.mousePosition);
                bool isCurrentLocale = locale == _originalLocale;

                DrawRowBackground(rowRect, isHovered, isCurrentLocale);

                var (leftHandSide, rightHandSide) = GetLocaleMenuLabel(locale, LocalizationSettings.DefaultLocale);
                var activeIndicator = isCurrentLocale ? "✓ " : "";

                rowRect.xMin += 8f;
                rowRect.xMax -= 8f;
                
                GUILayout.BeginArea(rowRect);
                GUILayout.BeginHorizontal();
                GUILayout.Label(activeIndicator, isCurrentLocale ? SelectedRowStyle : RowStyle, GUILayout.Width(20), GUILayout.ExpandHeight(true));
                GUILayout.Label(leftHandSide, isCurrentLocale ? SelectedRowStyle : RowStyle, GUILayout.ExpandHeight(true));
                if (rightHandSide != null)            
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(rightHandSide, isCurrentLocale ? SelectedRowStyle : RowStyle, GUILayout.ExpandHeight(true));
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();

                if (isHovered && currentEvent.type == EventType.MouseMove && _hoveredLocale != locale)
                {
                    _hoveredLocale = locale;
                    ApplyLocale(locale);
                    currentEvent.Use();
                }

                if (isHovered && currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    _committed = true;
                    ApplyLocale(locale);
                    editorWindow.Close();
                    currentEvent.Use();
                }
            }

            private static void DrawRowBackground(Rect rowRect, bool isHovered, bool isCurrentLocale)
            {
                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                TreeViewLineStyle.Draw(rowRect, GUIContent.none, isHovered, isCurrentLocale, isCurrentLocale, isCurrentLocale);
            }

            private static void DrawSeparator(Rect separatorRect)
            {
                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                Color separatorColor = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.08f)
                    : new Color(0f, 0f, 0f, 0.14f);
                EditorGUI.DrawRect(separatorRect, separatorColor);
            }
        }

        private static void ApplyLocale(LocalizationLocale locale)
        {
            LocalizationSettings.SetActiveLocale(locale);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static (string leftHandSide, string rightHandSide) GetLocaleMenuLabel(LocalizationLocale locale, LocalizationLocale defaultLocale)
        {
            string displayCode = locale.DisplayCode;
            string name = locale.NativeName;

            if (locale.CultureInfo != null)
                name = locale.CultureInfo.EnglishName;

            string label = string.IsNullOrEmpty(displayCode) ? name : $"{name} ({displayCode})";
            return (leftHandSide: label, rightHandSide: locale == defaultLocale ? "Default" : null);
        }

        private static string GetLocaleToolbarLabel()
        {
            if(!LocalizationSettings.IsInitialized) 
                return "No LocalizationSettings";

            var locale = LocalizationSettings.ActiveLocale;
            
            var (overrideShownActiveLocale, activeLocale) = LocalePreviewPopupContent.ActiveLocaleDisplay;
            if (overrideShownActiveLocale)
                locale = activeLocale;

            if (locale == null)
                return "Missing Locale";

            string displayCode = locale.DisplayCode;
            string name = locale.NativeName;

            if (locale.CultureInfo != null)
                name = locale.CultureInfo.EnglishName;

            return " " + (string.IsNullOrEmpty(displayCode) ? name : $"{name}  •  {displayCode}");
        }
    }
}