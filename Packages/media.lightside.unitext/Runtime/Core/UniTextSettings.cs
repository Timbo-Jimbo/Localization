using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Global settings ScriptableObject for UniText configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Access via Edit → Project Settings → UniText.
    /// Contains editor-only default configurations for new UniText components.
    /// </para>
    /// </remarks>
    public sealed class UniTextSettings : ScriptableObject
    {
        private const string ResourcePath = "UniTextSettings";
        private const string UnicodeDataPath = "UnicodeData";

        private static TextAsset cachedUnicodeData;

        [Header("Runtime Assets")]
        [SerializeField]
        [Tooltip("Named gradients for <gradient=name> tags.")]
        private UniTextGradients gradients;

        /// <summary>Gets or sets the named gradients asset.</summary>
        public static UniTextGradients Gradients
        {
            get => Instance.gradients;
            set
            {
                if (value != Instance.gradients)
                {
                    Instance.gradients = value;
                    Changed?.Invoke(nameof(Gradients));
                }
            }
        }

        [SerializeField]
        [Tooltip("Project-wide StylePreset applied to every UniText component. " +
                 "Behaves identically to a per-component preset added last: local Styles " +
                 "and per-component StylePresets register first and override the global one " +
                 "when parse rules collide.")]
        private StylePreset globalStylePreset;

        /// <summary>
        /// Project-wide <see cref="StylePreset"/> applied to every <see cref="UniTextBase"/>
        /// component without per-component opt-in. Behaves as one extra entry appended after
        /// each component's local <c>StylePresets</c> list, so local <c>Styles</c> and
        /// per-component <c>StylePresets</c> register first and win parse-rule conflicts.
        /// </summary>
        /// <remarks>
        /// Assigning fires <see cref="Changed"/> with <c>nameof(GlobalStylePreset)</c>;
        /// subscribed components rebuild their runtime preset copies on the next frame via the
        /// standard <c>InvalidateStyles</c> path. Editing the preset's styles list at runtime
        /// (via the preset's own <see cref="StylePreset.AddStyle"/> /
        /// <see cref="StylePreset.RemoveStyle"/> / <see cref="StylePreset.ClearStyles"/>) is
        /// delivered through the asset's own <see cref="StylePreset.Changed"/> event, picked up
        /// by every component that has it subscribed (local <c>StylePresets</c> entry or this
        /// global slot).
        /// </remarks>
        public static StylePreset GlobalStylePreset
        {
            get => Instance != null ? Instance.globalStylePreset : null;
            set
            {
                if (Instance == null) return;
                if (Instance.globalStylePreset == value) return;
                Instance.globalStylePreset = value;
                Changed?.Invoke(nameof(GlobalStylePreset));
            }
        }

        /// <summary>
        /// Raised when a settings field is mutated. The argument is the property name
        /// (<c>nameof(...)</c>) of the field that changed, allowing subscribers to filter
        /// reactions surgically without per-component snapshot bookkeeping. The sentinel
        /// <see cref="All"/> means "the entire asset was replaced, re-read everything" and is
        /// fired by <see cref="SetInstance"/>. Inspector <c>OnValidate</c> fires one event per
        /// reactive field whose value differs from the last observed snapshot.
        /// </summary>
        /// <remarks>
        /// Industry analogue: .NET <c>INotifyPropertyChanged.PropertyChanged</c>. Subscribers
        /// typically use <see cref="Affects"/> to test whether the changed property matches
        /// their field of interest (with sentinel handling baked in).
        /// </remarks>
        public static event Action<string> Changed;

        /// <summary>
        /// Sentinel passed to <see cref="Changed"/> when the whole settings asset has been
        /// swapped (see <see cref="SetInstance"/>) and every field is potentially different
        /// from its previously observed value. <see cref="Affects"/> treats this as "matches
        /// every interested field" so consumers don't need to special-case it.
        /// </summary>
        public const string All = "All";

        /// <summary>
        /// Returns <see langword="true"/> when a <see cref="Changed"/> event with payload
        /// <paramref name="changedProperty"/> should trigger a reaction subscribed to
        /// <paramref name="interestedField"/> — either exact name match, or the <see cref="All"/>
        /// sentinel. Canonical filter for every <see cref="Changed"/> consumer.
        /// </summary>
        public static bool Affects(string changedProperty, string interestedField)
            => changedProperty == interestedField || changedProperty == All;

        internal const int ShaderSdf = 0;
        internal const int ShaderEmoji = 1;
        internal const int ShaderHighlight = 2;
        internal const int ShaderCount = 3;

        [SerializeField, HideInInspector]
        private Shader[] requiredShaders = new Shader[ShaderCount];

        internal static Shader GetShader(int index)
        {
            var inst = Instance;
            if (inst == null || inst.requiredShaders == null ||
                (uint)index >= (uint)inst.requiredShaders.Length)
                return null;
            return inst.requiredShaders[index];
        }

    #if UNITY_EDITOR
        [Header("Editor Defaults")]
        [SerializeField]
        [Tooltip("Default fonts assigned to new UniText components.")]
        private UniTextFontStack defaultFontStack;

        /// <summary>Gets the default fonts for new UniText components (Editor only).</summary>
        public static UniTextFontStack DefaultFontStack => Instance?.defaultFontStack;

        [SerializeField]
        [Tooltip("Prefab instantiated by GameObject > UI > UniText - Text. Falls back to code creation if null.")]
        private GameObject textPrefab;

        [SerializeField]
        [Tooltip("Prefab instantiated by GameObject > UI > UniText - Button. Falls back to code creation if null.")]
        private GameObject buttonPrefab;

        [SerializeField]
        [Tooltip("Prefab instantiated by GameObject > UI (World) > UniText - World Text. Falls back to code creation if null.")]
        private GameObject worldTextPrefab;

        /// <summary>Gets the prefab for creating Text UI objects (Editor only).</summary>
        public static GameObject TextPrefab => Instance?.textPrefab;

        /// <summary>Gets the prefab for creating Button UI objects (Editor only).</summary>
        public static GameObject ButtonPrefab => Instance?.buttonPrefab;

        /// <summary>Gets the prefab for creating world-space text objects (Editor only).</summary>
        public static GameObject WorldTextPrefab => Instance?.worldTextPrefab;
    #endif

        /// <summary>Gets the compiled Unicode data asset, loaded from Resources.</summary>
        internal static TextAsset UnicodeDataAsset
        {
            get
            {
                if (cachedUnicodeData == null)
                {
                    cachedUnicodeData = Resources.Load<TextAsset>(UnicodeDataPath);
                    if (cachedUnicodeData == null)
                        Debug.LogError($"UnicodeData not found at Resources/{UnicodeDataPath}.bytes");
                }
                return cachedUnicodeData;
            }
        }

        private static UniTextSettings instance;

        /// <summary>Returns true if the instance is already loaded (without triggering load).</summary>
        internal static bool IsNull => instance == null;

        /// <summary>Gets the singleton settings instance, loading from Resources if needed.</summary>
        public static UniTextSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<UniTextSettings>(ResourcePath);

                    if (instance == null)
                        Debug.LogError(
                            $"UniTextSettings not found at Resources/{ResourcePath}.asset. " +
                            "Create it via Assets > Create > UniText > Settings and place in Resources folder.");
                }

                return instance;
            }
        }

        /// <summary>
        /// Manually replaces the singleton settings asset (used by tests and custom
        /// initialization paths). Raises <see cref="Changed"/> with the <see cref="All"/>
        /// sentinel — every reactive field is potentially different now, so subscribers should
        /// re-bind everything.
        /// </summary>
        /// <param name="settings">The settings instance to use.</param>
        public static void SetInstance(UniTextSettings settings)
        {
            instance = settings;
            Changed?.Invoke(All);
        }

        [SerializeField]
        [Tooltip("Dictionary assets for SA-class scripts (Thai, Lao, Khmer, Myanmar). Drag & drop to enable.")]
        private StyledList<WordSegmentationDictionary> dictionaries;

        /// <summary>Gets the configured word segmentation dictionaries.</summary>
        public static StyledList<WordSegmentationDictionary> Dictionaries
            => Instance != null ? Instance.dictionaries : null;

        [SerializeField]
        [Tooltip("Project-wide BCP 47 language tag (e.g. zh-Hans, zh-Hant, ja, ko, en-US). " +
                 "Applied to any codepoint that has no component-level UniText.Language and no " +
                 "per-range <lang=...> override. Drives the OpenType 'locl' feature and " +
                 "FontFamily.preferredLanguage selection. Leave empty to disable.")]
        private string language = "";

        /// <summary>
        /// Gets or sets the project-wide BCP 47 language tag. Applied to any codepoint that has no
        /// component-level <c>UniText.Language</c> and no per-range <c>&lt;lang&gt;</c> override.
        /// </summary>
        public static string Language
        {
            get => Instance != null ? Instance.language : null;
            set
            {
                if (Instance == null) return;
                value ??= string.Empty;
                if (Instance.language == value) return;
                Instance.language = value;
                Changed?.Invoke(nameof(Language));
            }
        }

        /// <summary>
        /// Target vertex capacity per shard in <c>UniTextWorldBatcher</c>. Groups of
        /// <c>UniTextWorld</c> components that exceed this threshold are split into multiple
        /// shards so that structural rebuilds of one shard do not touch the others.
        /// </summary>
        /// <remarks>
        /// Default 8192 fits UInt16 indices (max 65 535) with large headroom and yields
        /// ~2048 glyphs per shard — one shard is enough for a typical scene of world-space
        /// text. Increase for very dense scenes to reduce draw calls; decrease for more
        /// fine-grained partial updates when the same group is massive and animated.
        /// </remarks>
        public static int WorldBatcherShardTargetVertexCount { get; set; } = 8192;

#if UNITY_EDITOR
        /// <summary>
        /// Per-field snapshots used by <see cref="OnValidate"/> to detect which field the
        /// inspector edit actually touched, so <see cref="Changed"/> can be fired with a
        /// concrete property name instead of a generic "something changed" sentinel.
        /// Adding a reactive field: add a snapshot here, compare in <c>OnValidate</c>, fire
        /// <c>Changed?.Invoke(nameof(...))</c>.
        /// </summary>
        [NonSerialized] private UniTextGradients lastObservedGradients;
        [NonSerialized] private StylePreset lastObservedGlobalStylePreset;
        [NonSerialized] private string lastObservedLanguage;

        private void OnValidate()
        {
            if (lastObservedGradients != gradients)
            {
                lastObservedGradients = gradients;
                Changed?.Invoke(nameof(Gradients));
            }
            if (lastObservedGlobalStylePreset != globalStylePreset)
            {
                lastObservedGlobalStylePreset = globalStylePreset;
                Changed?.Invoke(nameof(GlobalStylePreset));
            }
            if (lastObservedLanguage != language)
            {
                lastObservedLanguage = language;
                Changed?.Invoke(nameof(Language));
            }
            // dictionaries: StyledList content edits don't change the list reference, so a
            // surgical snapshot would need a per-item compare. Fire unconditionally on every
            // OnValidate — Dictionaries-side consumers cost a constant-time switch dispatch.
            Changed?.Invoke(nameof(Dictionaries));
        }
#endif
    }
}
