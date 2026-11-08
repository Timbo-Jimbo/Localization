using System;
using System.Collections.Generic;
using TimboJimbo.Localization.Debugging;
using TimboJimbo.Localization.Utility;
using UnityEngine;

namespace TimboJimbo.Localization
{
    public abstract class LocalizableValue<TContainer, TValue> : ILocalizedValueResolver<TValue>
        where TContainer : ILocalizedValueResolver<TValue>
    {
        public TContainer Localized;
        public TValue Unlocalized;
        public bool IsLocalized => Localized != null;

        [SerializeField]
        protected UsageContext UsageContext;

        [NonSerialized] private HashSet<LocaleCode> _warnedCodes;

        public TValue Resolve(LocaleCode code)
        {
            if (IsLocalized)
                return Localized.Resolve(code);

            if (Unlocalized == null)
            {
                WarnMissing(code);
                return NonNullFallbackValue.ForType<TValue>();
            }

            return Unlocalized;
        }

        /// <summary>Warns that nothing resolved for <paramref name="code"/>: once per code for this value, and only in a playing game.</summary>
        protected void WarnMissing(LocaleCode code)
        {
            //todo: replace with EditorAwareUtil.IsLiveInstance -> using DebugContextInfo.Context as target obj
            if (!Application.isPlaying) return;

            _warnedCodes ??= new HashSet<LocaleCode>();
            if (!_warnedCodes.Add(code)) return;

            UsageContext.LogWarning($"No localized asset and no unlocalized value for locale \"{code}\". Returning the default value. (Warned once per locale.)");
        }
    }
}
