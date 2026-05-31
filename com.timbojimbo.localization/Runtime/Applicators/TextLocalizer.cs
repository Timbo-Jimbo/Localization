using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace TimboJimbo.Localization.Applicators
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public abstract class TextLocalizer : MonoBehaviour, ILocalizationChangeListener, ISerializationCallbackReceiver
    {
        public LocalizedString LocalizedString;

        [FormerlySerializedAs("LocalizableString"), HideInInspector] 
        [SerializeField] private LocalizableString _legacyLocalizableString = new();
        
        [NonSerialized] private bool _hasApplied;
        [NonSerialized] private string _lastAppliedText;
        [CanBeNull] private FormattingResolver _formattingResolver;

        protected virtual void OnEnable()
        {
            LocalizationSettings.AddListener(this);

            // Guards against cases where the user may have manually 
            // Apply'ed prior to enabling this components 
            if (!_hasApplied) Apply();
        }

        protected virtual void OnDisable()
        {
            LocalizationSettings.RemoveListener(this);
            _hasApplied = false;
            _lastAppliedText = null;
        }

        protected virtual void OnDestroy()
        {
            FormattingResolver.Release(ref _formattingResolver);
        }

        protected virtual void OnValidate()
        {
            if(gameObject.activeInHierarchy && enabled)
                Apply();
        }

        protected abstract void ApplyTextToTarget(string text);
        protected abstract void ClearFromTarget();

        public virtual void OnLocalizedValueChanged(LocalizedValue value)
        {
            if (LocalizedString != null && LocalizedString == value)
                Apply();
        }

        public virtual void OnLocaleChanged(LocalizationLocale locale)
        {
            Apply();
        }

        public void Apply()
        {
            var activeLocale = LocalizationSettings.ActiveLocale;

            if (LocalizedString == null || activeLocale == null)
            {
                ClearFromTarget();
                _hasApplied = true;
                _lastAppliedText = null;
                return;
            }

            var resolvedText = _formattingResolver?.Resolve(LocalizedString, activeLocale) ?? LocalizedString.Resolve(activeLocale);
            _hasApplied = true;

            if (string.Equals(resolvedText, _lastAppliedText))
                return;

            _lastAppliedText = resolvedText;
            ApplyTextToTarget(resolvedText);
        }

        public void SetFormattingParams<T1>(T1 param1)
        {
            var changed = FormattingResolver<T1>.Get(ref _formattingResolver, param1);
            if (changed) Apply();
        }

        public void SetFormattingParams<T1, T2>(T1 param1, T2 param2)
        {
            var changed = FormattingResolver<T1, T2>.Get(ref _formattingResolver, param1, param2);
            if (changed) Apply();
        }

        public void SetFormattingParams<T1, T2, T3>(T1 param1, T2 param2, T3 param3)
        {
            var changed = FormattingResolver<T1, T2, T3>.Get(ref _formattingResolver, param1, param2, param3);
            if (changed) Apply();
        }

        public void SetFormattingParams<T1, T2, T3, T4>(T1 param1, T2 param2, T3 param3, T4 param4)
        {
            var changed = FormattingResolver<T1, T2, T3, T4>.Get(ref _formattingResolver, param1, param2, param3, param4);
            if (changed) Apply();
        }

        public void SetFormattingParams<T1, T2, T3, T4, T5>(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            var changed = FormattingResolver<T1, T2, T3, T4, T5>.Get(ref _formattingResolver, param1, param2, param3, param4, param5);
            if (changed) Apply();
        }

        public void ClearFormattingParams()
        {
            if (FormattingResolver.Release(ref _formattingResolver))
                Apply();
        }

        // todo, remove this legacy support in future major version
        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (_legacyLocalizableString != null && _legacyLocalizableString.Localized != null)
            {
                LocalizedString = _legacyLocalizableString.Localized;
                _legacyLocalizableString.Localized = null;
            }
        }

        private abstract class FormattingResolver
        {
            public abstract string Resolve(LocalizedString localizedString, LocalizationLocale locale);
            protected abstract void Release();

            public static bool Release(ref FormattingResolver resolver)
            {
                if (resolver == null)
                    return false;

                resolver.Release();
                resolver = null;

                return true;
            }
        }

        private class FormattingResolver<T1> : FormattingResolver
        {
            private static readonly ObjectPool<FormattingResolver<T1>> Pool = new(
                createFunc: () => new(),
                actionOnRelease: resolver =>
                {
                    resolver.Param1 = default;
                }
            );

            private T1 Param1;
            public override string Resolve(LocalizedString localizedString, LocalizationLocale locale)
            {
                return localizedString.Resolve(locale, Param1);
            }

            protected override void Release()
            {
                Pool.Release(this);
            }

            private static FormattingResolver<T1> Get(T1 param1)
            {
                var resolver = Pool.Get();
                resolver.Param1 = param1;
                return resolver;
            }

            public static bool Get(ref FormattingResolver resolver, T1 param1)
            {
                if (resolver is FormattingResolver<T1> typedResolver)
                {
                    var changed = !EqualityComparer<T1>.Default.Equals(typedResolver.Param1, param1);

                    typedResolver.Param1 = param1;

                    return changed;
                }

                Release(ref resolver);
                resolver = Get(param1);
                return true;
            }
        }

        private class FormattingResolver<T1, T2> : FormattingResolver
        {
            private static readonly ObjectPool<FormattingResolver<T1, T2>> Pool = new(
                createFunc: () => new(),
                actionOnRelease: resolver =>
                {
                    resolver.Param1 = default;
                    resolver.Param2 = default;
                }
            );

            private T1 Param1;
            private T2 Param2;

            public override string Resolve(LocalizedString localizedString, LocalizationLocale locale)
            {
                return localizedString.Resolve(locale, Param1, Param2);
            }

            protected override void Release()
            {
                Pool.Release(this);
            }

            public static FormattingResolver<T1, T2> Get(T1 param1, T2 param2)
            {
                var resolver = Pool.Get();
                resolver.Param1 = param1;
                resolver.Param2 = param2;
                return resolver;
            }

            public static bool Get(ref FormattingResolver resolver, T1 param1, T2 param2)
            {
                if (resolver is FormattingResolver<T1, T2> typedResolver)
                {
                    var changed = !EqualityComparer<T1>.Default.Equals(typedResolver.Param1, param1) ||
                                  !EqualityComparer<T2>.Default.Equals(typedResolver.Param2, param2);

                    typedResolver.Param1 = param1;
                    typedResolver.Param2 = param2;

                    return changed;
                }

                Release(ref resolver);
                resolver = Get(param1, param2);
                return true;
            }
        }

        private class FormattingResolver<T1, T2, T3> : FormattingResolver
        {
            private static readonly ObjectPool<FormattingResolver<T1, T2, T3>> Pool = new(
                createFunc: () => new(),
                actionOnRelease: resolver =>
                {
                    resolver.Param1 = default;
                    resolver.Param2 = default;
                    resolver.Param3 = default;
                }
            );

            private T1 Param1;
            private T2 Param2;
            private T3 Param3;

            public override string Resolve(LocalizedString localizedString, LocalizationLocale locale)
            {
                return localizedString.Resolve(locale, Param1, Param2, Param3);
            }

            protected override void Release()
            {
                Pool.Release(this);
            }

            public static FormattingResolver<T1, T2, T3> Get(T1 param1, T2 param2, T3 param3)
            {
                var resolver = Pool.Get();
                resolver.Param1 = param1;
                resolver.Param2 = param2;
                resolver.Param3 = param3;
                return resolver;
            }

            public static bool Get(ref FormattingResolver resolver, T1 param1, T2 param2, T3 param3)
            {
                if (resolver is FormattingResolver<T1, T2, T3> typedResolver)
                {
                    var changed = !EqualityComparer<T1>.Default.Equals(typedResolver.Param1, param1) ||
                                  !EqualityComparer<T2>.Default.Equals(typedResolver.Param2, param2) ||
                                  !EqualityComparer<T3>.Default.Equals(typedResolver.Param3, param3);

                    typedResolver.Param1 = param1;
                    typedResolver.Param2 = param2;
                    typedResolver.Param3 = param3;

                    return changed;
                }

                Release(ref resolver);
                resolver = Get(param1, param2, param3);
                return true;
            }
        }

        private class FormattingResolver<T1, T2, T3, T4> : FormattingResolver
        {
            private static readonly ObjectPool<FormattingResolver<T1, T2, T3, T4>> Pool = new(
                createFunc: () => new(),
                actionOnRelease: resolver =>
                {
                    resolver.Param1 = default;
                    resolver.Param2 = default;
                    resolver.Param3 = default;
                    resolver.Param4 = default;
                }
            );

            private T1 Param1;
            private T2 Param2;
            private T3 Param3;
            private T4 Param4;

            public override string Resolve(LocalizedString localizedString, LocalizationLocale locale)
            {
                return localizedString.Resolve(locale, Param1, Param2, Param3, Param4);
            }

            protected override void Release()
            {
                Pool.Release(this);
            }

            public static FormattingResolver<T1, T2, T3, T4> Get(T1 param1, T2 param2, T3 param3, T4 param4)
            {
                var resolver = Pool.Get();
                resolver.Param1 = param1;
                resolver.Param2 = param2;
                resolver.Param3 = param3;
                resolver.Param4 = param4;
                return resolver;
            }

            public static bool Get(ref FormattingResolver resolver, T1 param1, T2 param2, T3 param3, T4 param4)
            {
                if (resolver is FormattingResolver<T1, T2, T3, T4> typedResolver)
                {
                    var changed = !EqualityComparer<T1>.Default.Equals(typedResolver.Param1, param1) ||
                                  !EqualityComparer<T2>.Default.Equals(typedResolver.Param2, param2) ||
                                  !EqualityComparer<T3>.Default.Equals(typedResolver.Param3, param3) ||
                                  !EqualityComparer<T4>.Default.Equals(typedResolver.Param4, param4);

                    typedResolver.Param1 = param1;
                    typedResolver.Param2 = param2;
                    typedResolver.Param3 = param3;
                    typedResolver.Param4 = param4;

                    return changed;
                }

                Release(ref resolver);
                resolver = Get(param1, param2, param3, param4);
                return true;
            }
        }

        private class FormattingResolver<T1, T2, T3, T4, T5> : FormattingResolver
        {
            private static readonly ObjectPool<FormattingResolver<T1, T2, T3, T4, T5>> Pool = new(
                createFunc: () => new(),
                actionOnRelease: resolver =>
                {
                    resolver.Param1 = default;
                    resolver.Param2 = default;
                    resolver.Param3 = default;
                    resolver.Param4 = default;
                    resolver.Param5 = default;
                }
            );

            private T1 Param1;
            private T2 Param2;
            private T3 Param3;
            private T4 Param4;
            private T5 Param5;

            public override string Resolve(LocalizedString localizedString, LocalizationLocale locale)
            {
                return localizedString.Resolve(locale, Param1, Param2, Param3, Param4, Param5);
            }

            protected override void Release()
            {
                Pool.Release(this);
            }

            public static FormattingResolver<T1, T2, T3, T4, T5> Get(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
            {
                var resolver = Pool.Get();
                resolver.Param1 = param1;
                resolver.Param2 = param2;
                resolver.Param3 = param3;
                resolver.Param4 = param4;
                resolver.Param5 = param5;
                return resolver;
            }

            public static bool Get(ref FormattingResolver resolver, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
            {
                if (resolver is FormattingResolver<T1, T2, T3, T4, T5> typedResolver)
                {
                    var changed = !EqualityComparer<T1>.Default.Equals(typedResolver.Param1, param1) ||
                                  !EqualityComparer<T2>.Default.Equals(typedResolver.Param2, param2) ||
                                  !EqualityComparer<T3>.Default.Equals(typedResolver.Param3, param3) ||
                                  !EqualityComparer<T4>.Default.Equals(typedResolver.Param4, param4) ||
                                  !EqualityComparer<T5>.Default.Equals(typedResolver.Param5, param5);

                    typedResolver.Param1 = param1;
                    typedResolver.Param2 = param2;
                    typedResolver.Param3 = param3;
                    typedResolver.Param4 = param4;
                    typedResolver.Param5 = param5;

                    return changed;
                }

                Release(ref resolver);
                resolver = Get(param1, param2, param3, param4, param5);
                return true;
            }
        }
    }
}