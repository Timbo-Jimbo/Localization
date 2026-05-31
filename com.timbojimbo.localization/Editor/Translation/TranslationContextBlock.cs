using System;
using UnityEngine;

namespace TimboJimboEditor.Localization.Translations
{
    [Serializable]
    public class TranslationContextBlock
    {
        [SerializeField]
        private string _title;

        [SerializeField, TextArea(3, 15)]
        private string _body;

        public string Title
        {
            get => _title;
        }
        public string Body
        {
            get => _body;
        }

        public TranslationContextBlock(string title, string body)
        {
            _title = title;
            _body = body;
        }
    }
}