using System;
using UnityEngine;
using Cysharp.Text;

namespace TimboJimbo.Localization.Debugging
{
    [Serializable]
    public struct UsageContext
    {
        public UnityEngine.Object Context;
        public string SourcePath;
        public string PropertyPath;

        [HideInCallstack]
        public void Log(string message)
        {
            Debug.Log(WithMetadata(message), Context);
        }

        [HideInCallstack]
        public void LogWarning(string message)
        {
            Debug.LogWarning(WithMetadata(message), Context);
        }

        [HideInCallstack]
        public void LogError(string message)
        {
            Debug.LogError(WithMetadata(message), Context);
        }

        [HideInCallstack]
        public void LogException(Exception exception, string message = null)
        {
            Debug.LogException(new Exception(WithMetadata(message ?? exception.Message), exception), Context);
        }

        private string WithMetadata(string message)
        {
            return ZString.Format("{0}\nDebug Context: {1}", message, ToString());
        }
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(SourcePath))
                return ZString.Join(".", SourcePath, PropertyPath);

            return PropertyPath;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class InjectUsageContextAttribute : Attribute { }

}
