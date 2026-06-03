using System;
using System.Collections.Generic;
using TimboJimbo.Localization.StringFormatters;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.Localization
{
    public static class LocalizationEditorGUI
    {
        public static GUIStyle HeaderStyle => Styles.HeaderStyle;
        public static GUIStyle StatLabelStyle => Styles.StatLabelStyle;
        public static GUIStyle RowTitleStyle => Styles.RowTitleStyle;
        public static GUIStyle RowSubtitleStyle => Styles.RowSubtitleStyle;
        public static GUIStyle DefaultTagStyle => Styles.DefaultTagStyle;
        public static GUIStyle MissingTagStyle => Styles.MissingTagStyle;
        private const float TranslatingPulseFrequency = 1.5f;
        private const float TranslatingPulseMinAlpha = 0.35f;

        public static PulseScopeHandle PulseScope(bool pulse)
        {
            return new PulseScopeHandle(pulse);
        }

        public static void DrawFoldout(
            ref bool expanded, 
            Action drawContent,
            Action<bool> onToggle,
            Action<bool> onGroupToggle = null
        )
        {
            if (onGroupToggle == null)
                onGroupToggle = onToggle;

            // BeginHorizontal with a styled background paints the row behind the
            // child controls automatically, so the content stays visible.
            // The style's left padding already reserves space for the foldout arrow,
            // so content begins to the right of the arrow.
            using (var scope = new EditorGUILayout.HorizontalScope(Styles.FoldoutRowStyle, GUILayout.ExpandWidth(true)))
            {
                Event evt = Event.current;

                // A fixed oversized bleed reliably covers the full inspector width
                // regardless of sidebars, scroll bars, or currentViewWidth quirks.
                const float BleedAmount = 4000f;
                var rowRect = scope.rect;
                Rect bleedRect = new(
                    rowRect.x - BleedAmount,
                    rowRect.y,
                    rowRect.width + (BleedAmount * 2f),
                    rowRect.height);

                if (evt.type == EventType.Repaint)
                {
                    // Bg
                    EditorGUI.DrawRect(bleedRect, Styles.FoldoutBackgroundColor);

                    // Top border.
                    EditorGUI.DrawRect(
                        new Rect(bleedRect.x, rowRect.y, bleedRect.width, Styles.FoldoutTopBorderThickness),
                        Styles.FoldoutBorderColor
                    );

                    // Foldout arrow vertically centered within the row.
                    float arrowHeight = EditorGUIUtility.singleLineHeight;
                    Rect arrowRect = new(
                        rowRect.x,
                        rowRect.y + ((rowRect.height - arrowHeight) * 0.5f),
                        13f,
                        arrowHeight);
                    EditorStyles.foldout.Draw(arrowRect, GUIContent.none, false, false, expanded, false);
                }

                using (new GUILayout.HorizontalScope(GUILayout.MinHeight(EditorGUIUtility.singleLineHeight + 2)))
                {
                    drawContent?.Invoke();
                }
            
                // Detect a click on the row. Inner controls (kebab buttons etc.) get to
                // consume the event first; if they did, evt.type will be Used here.
                bool toggled = false;
                bool wasGroupToggle = false;
                
                if (evt.type == EventType.MouseDown && evt.button == 0 && bleedRect.Contains(evt.mousePosition))
                {
                    toggled = true;
                    wasGroupToggle = evt.alt;
                    GUI.changed = true;
                    evt.Use();
                }

                if (toggled)
                {
                    expanded = !expanded;

                    if (wasGroupToggle)
                        onGroupToggle?.Invoke(expanded);
                    else
                        onToggle?.Invoke(expanded);
                }
            }
        }

        public static void DrawSpinner(Rect rect)
        {
            int frame = (int)(EditorApplication.timeSinceStartup * 10) % 12;
            GUIContent icon = EditorGUIUtility.IconContent($"WaitSpin{frame:00}");
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(rect, icon);
            GUI.color = prevColor;
            RepaintEditors();
        }


        public static bool KebabMenuButton(string label = null) => GhostButton("_Menu", label);
        public static bool RemoveButton(string label = null) => GhostButton("Toolbar Minus", label);
        public static bool AddButton(string label = null) => GhostButton("Toolbar Plus", label);

        /// <summary>
        /// Draws a small icon button with a hover highlight. The icon is looked
        /// up via <see cref="EditorGUIUtility.IconContent(string)"/>. Auto-sizes
        /// to fit the icon (and optional label). Returns true on click.
        /// </summary>
        public static bool GhostButton(string iconName, string label = null)
        {
            GUIContent icon = EditorGUIUtility.IconContent(iconName);
            GUIContent content = string.IsNullOrEmpty(label)
                ? icon
                : new GUIContent(label, icon.image, icon.tooltip);

            return GUILayout.Button(content, Styles.GhostIconStyle, GUILayout.ExpandWidth(false));
        }

        public static void ButtonGroup(
            List<ButtonGroupEntry> buttons,
            Action<GenericMenu> populateContextMenu = null)
        {
            int buttonCount = buttons.Count;
            bool hasKebab = populateContextMenu != null;
            int total = buttonCount + (hasKebab ? 1 : 0);
            if (total == 0)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < buttonCount; i++)
                {
                    GUIStyle style = GetButtonGroupStyle(i, total);
                    if (GUILayout.Button(buttons[i].Label, style))
                        buttons[i].OnClick?.Invoke();
                }

                if (hasKebab)
                {
                    GUIStyle style = GetButtonGroupStyle(buttonCount, total);
                    GUIContent icon = EditorGUIUtility.IconContent("_Menu");
                    if (GUILayout.Button(icon, style, GUILayout.Width(28f)))
                    {
                        GenericMenu menu = new();
                        populateContextMenu(menu);
                        menu.ShowAsContext();
                    }
                }
            }

            static GUIStyle GetButtonGroupStyle(int index, int totalSize)
            {
                if (totalSize == 1) return EditorStyles.miniButton;
                if (index == 0) return EditorStyles.miniButtonLeft;
                if (index == totalSize - 1) return EditorStyles.miniButtonRight;
                return EditorStyles.miniButtonMid;
            }
        }

        public static string FormatTextArea(string value, float minHeight)
        {
            return FormatTextAreaGUI.Draw(value, minHeight);
        }

        private static class FormatTextAreaGUI
        {
            private static readonly Color[] ParamColors =
            {
                new Color(0.12f, 1.00f, 0.12f, 0.85f),
                new Color(0.12f, 0.65f, 1.00f, 0.85f),
                new Color(1.00f, 0.30f, 0.30f, 0.85f),
                new Color(1.00f, 0.55f, 0.15f, 0.85f),
                new Color(0.65f, 0.35f, 1.00f, 0.85f),
            };

            /// <summary>
            /// Layout variant. Draws an expanding text area inside a scroll view of the given
            /// minimum height. Overlay highlights are aligned automatically because the text
            /// area itself never scrolls — the surrounding scroll view does.
            /// </summary>
            public static string Draw(string value, float minHeight)
            {
                string text = value ?? string.Empty;


                GUILayout.BeginVertical(GUILayout.MinHeight(minHeight));
                string newValue = EditorGUILayout.TextArea(text, Styles.TextAreaStyle, GUILayout.ExpandHeight(true));
                GUILayout.EndVertical();
                
                if (Event.current.type == EventType.Repaint)
                {
                    Rect textAreaRect = GUILayoutUtility.GetLastRect();
                    DrawOverlay(textAreaRect, newValue);
                }

                return newValue;

                void DrawOverlay(Rect textAreaRect, string overlayText)
                {
                    if (string.IsNullOrEmpty(overlayText)) return;

                    string parseText = overlayText;
                    if (HasUnclosedBrace(overlayText))
                        parseText += "}";

                    IReadOnlyList<FormatToken> tokens = FormatHelper.ExtractTokens(parseText);
                    if (tokens.Count == 0) return;

                    GUIContent content = new(overlayText);
                    float lineHeight = Styles.TextAreaStyle.lineHeight;

                    for (int i = 0; i < tokens.Count; i++)
                    {
                        FormatToken token = tokens[i];
                        Color color = ParamColors[Mathf.Max(0, token.ParameterIndex) % ParamColors.Length];

                        //lets hard code some colors for our built in formatters:
                        if (token.FormatterName.AsSpan().EndsWith(PluralFormatter.Prefix) || token.FormatterName.AsSpan().EndsWith(PluralFormatter.ShortPrefix))
                            color = new Color(1.000f, 0.651f, 0.000f, 1.000f);
                        if (token.FormatterName.AsSpan().EndsWith(MapFormatter.Prefix) || token.FormatterName.AsSpan().EndsWith(MapFormatter.ShortPrefix))
                            color = new Color(0.925f, 0.027f, 0.027f, 1.000f);
                        if (token.FormatterName.AsSpan().EndsWith(QueryFormatter.Prefix) || token.FormatterName.AsSpan().EndsWith(QueryFormatter.ShortPrefix))
                            color = new Color(0.000f, 0.765f, 1.000f, 1.000f);

                        IReadOnlyList<FormatSpan> spans = token.SyntaxRanges;
                        for (int s = 0; s < spans.Count; s++)
                        {
                            FormatSpan span = spans[s];
                            DrawSpan(overlayText, span.StartIndex, span.EndIndexExclusive, content, textAreaRect, lineHeight, color);
                        }
                    }

                    bool HasUnclosedBrace(string t)
                    {
                        for (int i = t.Length - 1; i >= 0; i--)
                        {
                            char c = t[i];
                            if (c == '}')
                            {
                                if (i > 0 && t[i - 1] == '}') { i--; continue; }
                                return false;
                            }
                            if (c == '{')
                            {
                                if (i > 0 && t[i - 1] == '{') { i--; continue; }
                                return true;
                            }
                        }
                        return false;
                    }

                    void DrawSpan(string spanText, int start, int end, GUIContent contentObj, Rect rect, float lineH, Color col)
                    {
                        int rangeEnd = Mathf.Min(end, spanText.Length);
                        int segmentStart = -1;

                        for (int i = start; i < rangeEnd; i++)
                        {
                            char c = spanText[i];
                            if (c == '\n' || c == '\r')
                            {
                                if (segmentStart >= 0)
                                {
                                    DrawSegment(spanText, segmentStart, i, contentObj, rect, lineH, col);
                                    segmentStart = -1;
                                }
                                continue;
                            }

                            if (segmentStart < 0)
                                segmentStart = i;
                        }

                        if (segmentStart >= 0)
                            DrawSegment(spanText, segmentStart, rangeEnd, contentObj, rect, lineH, col);
                    }

                    void DrawSegment(string segText, int start, int end, GUIContent contentObj, Rect rect, float lineH, Color col)
                    {
                        if (start >= end) return;

                        Vector2 startPos = Styles.TextAreaStyle.GetCursorPixelPosition(rect, contentObj, ToVisualIndex(segText, start));
                        Vector2 endPos = Styles.TextAreaStyle.GetCursorPixelPosition(rect, contentObj, ToVisualIndex(segText, end));

                        // Single visual line: draw the whole segment in one label.
                        if (Mathf.Abs(endPos.y - startPos.y) < 0.1f)
                        {
                            DrawLabel(new Rect(startPos.x, startPos.y, Mathf.Max(1f, endPos.x - startPos.x), lineH),
                                segText.Substring(start, end - start), col);
                            return;
                        }

                        // Segment wrapped across visual lines: fall back to per-character draws so
                        // every glyph follows the text area's wrap points exactly.
                        for (int i = start; i < end; i++)
                        {
                            Vector2 pos = Styles.TextAreaStyle.GetCursorPixelPosition(rect, contentObj, ToVisualIndex(segText, i));
                            Vector2 next = Styles.TextAreaStyle.GetCursorPixelPosition(rect, contentObj, ToVisualIndex(segText, i + 1));
                            float width = Mathf.Max(1f, next.x - pos.x);
                            DrawLabel(new Rect(pos.x, pos.y, width, lineH), segText[i].ToString(), col);
                        }

                        void DrawLabel(Rect labelRect, string labelText, Color labelColor)
                        {
                            Color prevColor = Styles.OverlayStyle.normal.textColor;
                            Styles.OverlayStyle.normal.textColor = labelColor;
                            GUI.Label(labelRect, labelText, Styles.OverlayStyle);
                            Styles.OverlayStyle.normal.textColor = prevColor;
                        }

                        int ToVisualIndex(string textVal, int codeUnitIndex)
                        {
                            int visualIndex = 0;
                            int idx = 0;

                            while (idx < codeUnitIndex && idx < textVal.Length)
                            {
                                visualIndex++;

                                if (char.IsHighSurrogate(textVal[idx]) && idx + 1 < textVal.Length && char.IsLowSurrogate(textVal[idx + 1]))
                                    idx += 2;
                                else
                                    idx++;

                                while (idx < codeUnitIndex && idx < textVal.Length && (textVal[idx] == '\uFE0E' || textVal[idx] == '\uFE0F'))
                                    idx++;
                            }

                            return visualIndex;
                        }
                    }
                }
            }
        }
        
        private static void RepaintEditors()
        {
            foreach (var item in ActiveEditorTracker.sharedTracker.activeEditors)
            {
                item.Repaint();
            }
        }
        
        internal static class Styles
        {
            public static readonly Color MissingTextColor = new(0.95f, 0.55f, 0.20f, 1f);
            private static readonly Color SeparatorColorDarkSkin = new(1f, 1f, 1f, 0.06f);
            private static readonly Color SeparatorColorLightSkin = new(0f, 0f, 0f, 0.10f);
            private static readonly Color SubtleTextColorDarkSkin = new(0.70f, 0.70f, 0.70f, 1f);
            private static readonly Color SubtleTextColorLightSkin = new(0.35f, 0.35f, 0.35f, 1f);
            private static readonly Color FoldoutBackgroundColorDarkSkin = new(0.19f, 0.19f, 0.19f, 1f);
            private static readonly Color FoldoutBackgroundColorLightSkin = new(0.74f, 0.74f, 0.74f, 1f);
            private static readonly Color FoldoutBorderColorDarkSkin = new(0f, 0f, 0f, 0.38f);
            private static readonly Color FoldoutBorderColorLightSkin = new(0f, 0f, 0f, 0.18f);
            private static readonly Color HoverColorDarkSkin = new(1f, 1f, 1f, 0.04f);
            private static readonly Color HoverColorLightSkin = new(0f, 0f, 0f, 0.04f);

            private const float FoldoutContentLeftPadding = 22f;
            private const float FoldoutContentRightPadding = 8f;
            private const float FoldoutVerticalPadding = 5f;
            public const float FoldoutTopBorderThickness = 1f;

            private static GUIStyle _headerStyle;
            private static GUIStyle _statLabelStyle;
            private static GUIStyle _rowTitleStyle;
            private static GUIStyle _rowSubtitleStyle;
            private static GUIStyle _defaultTagStyle;
            private static GUIStyle _missingTagStyle;
            private static GUIStyle _foldoutRowStyle;
            private static GUIStyle _ghostIconStyle;
            private static GUIStyle _textAreaStyle;
            private static GUIStyle _overlayStyle;
            private static Texture2D _foldoutBackgroundTexture;
            private static bool _stylesUseProSkin;

            public static Color SeparatorColor { get { Ensure(); return ForCurrentSkin(SeparatorColorDarkSkin, SeparatorColorLightSkin); } }
            public static Color SubtleTextColor { get { Ensure(); return ForCurrentSkin(SubtleTextColorDarkSkin, SubtleTextColorLightSkin); } }
            public static Color FoldoutBackgroundColor { get { Ensure(); return ForCurrentSkin(FoldoutBackgroundColorDarkSkin, FoldoutBackgroundColorLightSkin); } }
            public static Color FoldoutBorderColor { get { Ensure(); return ForCurrentSkin(FoldoutBorderColorDarkSkin, FoldoutBorderColorLightSkin); } }
            public static Color HoverColor { get { Ensure(); return ForCurrentSkin(HoverColorDarkSkin, HoverColorLightSkin); } }

            public static GUIStyle HeaderStyle { get { Ensure(); return _headerStyle; } }
            public static GUIStyle StatLabelStyle { get { Ensure(); return _statLabelStyle; } }
            public static GUIStyle RowTitleStyle { get { Ensure(); return _rowTitleStyle; } }
            public static GUIStyle RowSubtitleStyle { get { Ensure(); return _rowSubtitleStyle; } }
            public static GUIStyle DefaultTagStyle { get { Ensure(); return _defaultTagStyle; } }
            public static GUIStyle MissingTagStyle { get { Ensure(); return _missingTagStyle; } }
            public static GUIStyle FoldoutRowStyle { get { Ensure(); return _foldoutRowStyle; } }
            public static GUIStyle GhostIconStyle { get { Ensure(); return _ghostIconStyle; } }
            public static GUIStyle TextAreaStyle { get { Ensure(); return _textAreaStyle; } }
            public static GUIStyle OverlayStyle { get { Ensure(); return _overlayStyle; } }

            private static Color ForCurrentSkin(Color darkSkinColor, Color lightSkinColor)
            {
                return EditorGUIUtility.isProSkin ? darkSkinColor : lightSkinColor;
            }

            private static void Ensure()
            {
                bool isProSkin = EditorGUIUtility.isProSkin;
                if (_headerStyle != null && _stylesUseProSkin == isProSkin)
                {
                    return;
                }

                _stylesUseProSkin = isProSkin;

                Color subtle = ForCurrentSkin(SubtleTextColorDarkSkin, SubtleTextColorLightSkin);

                if (_foldoutBackgroundTexture != null)
                    UnityEngine.Object.DestroyImmediate(_foldoutBackgroundTexture);

                _foldoutBackgroundTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _foldoutBackgroundTexture.SetPixel(0, 0, ForCurrentSkin(FoldoutBackgroundColorDarkSkin, FoldoutBackgroundColorLightSkin));
                _foldoutBackgroundTexture.Apply();

                _foldoutRowStyle = new GUIStyle
                {
                    normal = { background = _foldoutBackgroundTexture },
                    padding = new RectOffset(
                        (int)FoldoutContentLeftPadding,
                        (int)FoldoutContentRightPadding,
                        (int)(FoldoutTopBorderThickness + FoldoutVerticalPadding),
                        (int)FoldoutVerticalPadding),
                    margin = new RectOffset(0, 0, 0, 0),
                    stretchWidth = true,
                };

                _headerStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };

                _statLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = subtle },
                };

                _rowTitleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft,
                };

                _rowSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = subtle },
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft
                };

                _defaultTagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = subtle },
                    alignment = TextAnchor.MiddleRight,
                };

                _missingTagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = MissingTextColor },
                    alignment = TextAnchor.MiddleRight,
                };

                _ghostIconStyle = new GUIStyle(EditorStyles.iconButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    imagePosition = ImagePosition.ImageLeft,
                    fontSize = EditorStyles.label.fontSize,
                    fixedWidth = 0f,
                    fixedHeight = EditorGUIUtility.singleLineHeight,
                };

                _textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };

                _overlayStyle = new GUIStyle
                {
                    font = _textAreaStyle.font,
                    fontSize = _textAreaStyle.fontSize,
                    fontStyle = _textAreaStyle.fontStyle,
                    alignment = TextAnchor.UpperLeft,
                    richText = false,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0),
                };
            }
        }
    
        public readonly struct ButtonGroupEntry
        {
            public readonly string Label;
            public readonly Action OnClick;

            public ButtonGroupEntry(string label, Action onClick)
            {
                Label = label;
                OnClick = onClick;
            }
        }

        public struct PulseScopeHandle : IDisposable
        {
            private readonly bool _pulse;
            private readonly Color _previousColor;

            public PulseScopeHandle(bool pulse)
            {
                _pulse = pulse;
                _previousColor = GUI.color;

                if(!pulse) return;

                float pulseValue = TranslatingPulseMinAlpha
                    + (1f - TranslatingPulseMinAlpha)
                    * (0.5f + 0.5f * Mathf.Sin((float)(EditorApplication.timeSinceStartup * Mathf.PI * 2f * TranslatingPulseFrequency)));

                GUI.color = new Color(_previousColor.r, _previousColor.g, _previousColor.b, _previousColor.a * pulseValue);
            }

            public void Dispose()
            {
                if (!_pulse) return;

                GUI.color = _previousColor;
                RepaintEditors();
            }
        }
        

    }
}