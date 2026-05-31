using System.Collections.Generic;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    [CreateAssetMenu(fileName = "Project Wide Translation Context Blocks", menuName = "Localization/Project Wide Translation Context Blocks")]
    public class ProjectWideTranslationContextBlocks : ScriptableObject
    {
        public List<TranslationContextBlock> ContextBlocks = new List<TranslationContextBlock>();
    }
}