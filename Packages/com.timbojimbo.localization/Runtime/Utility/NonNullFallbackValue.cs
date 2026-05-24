using UnityEngine;

namespace TimboJimbo.Localization.Utility
{
    internal static class NonNullFallbackValue
    {
        private static Sprite _defaultSprite;

        public static T ForType<T>()
        {
            if (typeof(T) == typeof(Sprite))
            {
                if (_defaultSprite == null)
                    _defaultSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);

                return (T)(object)_defaultSprite;
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)"<Missing String>";
            }

            return default;
        }
    }
}