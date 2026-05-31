using TimboJimbo.Localization;

namespace TimboJimboEditor.Localization.Translations
{
    public readonly struct TranslationProgress
    {
        public readonly TranslationProgressPhase Phase;
        public readonly LocalizationLocale Locale;
        public readonly string LocaleCode;
        public readonly string Text;
        public readonly int CompletedCount;
        public readonly int TotalCount;
        public readonly string Message;

        public TranslationProgress(
            TranslationProgressPhase phase,
            LocalizationLocale locale,
            string localeCode,
            string text,
            int completedCount,
            int totalCount,
            string message
        )
        {
            Phase = phase;
            Locale = locale;
            LocaleCode = localeCode;
            Text = text;
            CompletedCount = completedCount;
            TotalCount = totalCount;
            Message = message;
        }
    }
}