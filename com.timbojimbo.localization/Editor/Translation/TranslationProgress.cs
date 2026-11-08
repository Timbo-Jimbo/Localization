using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization.Translations
{
    public readonly struct TranslationProgress
    {
        public readonly TranslationProgressPhase Phase;

        /// <summary>The locale this update is about; <see cref="LocaleCode.None"/> for updates about the request as a whole.</summary>
        public readonly LocaleCode Code;

        /// <summary>The locale code as the model wrote it, for an unexpected one <see cref="Code"/> can't carry.</summary>
        public readonly string RawLocaleCode;
        public readonly string Text;
        public readonly int CompletedCount;
        public readonly int TotalCount;
        public readonly string Message;

        public TranslationProgress(
            TranslationProgressPhase phase,
            LocaleCode code,
            string rawLocaleCode,
            string text,
            int completedCount,
            int totalCount,
            string message
        )
        {
            Phase = phase;
            Code = code;
            RawLocaleCode = rawLocaleCode;
            Text = text;
            CompletedCount = completedCount;
            TotalCount = totalCount;
            Message = message;
        }
    }
}
