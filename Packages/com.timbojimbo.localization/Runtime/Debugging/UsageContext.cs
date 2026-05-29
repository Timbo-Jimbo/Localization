using System;
using UnityEngine;
using Cysharp.Text;

namespace TimboJimbo.Localization.Debugging
{
    [Serializable]
    public struct UsageContext
    {
        public UnityEngine.Object Context;

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
            var name = Context != null ? Context.name : "<Missing>";
            var type = Context != null ? Context.GetType().Name : "<Missing>";
            var path = string.Empty;

            {
                var transform = default(Transform);
                if (Context is GameObject go)
                    transform = go.transform;
                else if (Context is Component comp)
                    transform = comp.transform;
                
                if (transform != null)
                {
                    //build path from hierarchy
                    using var pathBuilder = ZString.CreateStringBuilder();
                    while (transform != null)
                    {
                        pathBuilder.Insert(0, transform.name);
                        transform = transform.parent;
                        if (transform != null)
                            pathBuilder.Insert(0, "/");
                    }
                    pathBuilder.Insert(0, " at ");
                    path = pathBuilder.ToString();
                }
            }

            return ZString.Format("{0}: {3}. (Context: '{1}'{2})", name, type, path, message);
        }

    }
}
