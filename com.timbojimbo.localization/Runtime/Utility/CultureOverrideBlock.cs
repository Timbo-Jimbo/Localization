using System;
using System.Globalization;

namespace TimboJimbo.Localization.Utility
{
    public struct CultureOverrideBlock : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUICulture;

        public CultureOverrideBlock(CultureInfo overrideCulture)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            if (_originalCulture.Name != overrideCulture.Name)
                CultureInfo.CurrentCulture = overrideCulture;

            _originalUICulture = CultureInfo.CurrentUICulture;
            if (_originalUICulture.Name != overrideCulture.Name)
                CultureInfo.CurrentUICulture = overrideCulture;
        }

        public void Dispose()
        {
            if (CultureInfo.CurrentCulture.Name != _originalCulture.Name)
                CultureInfo.CurrentCulture = _originalCulture;

            if (CultureInfo.CurrentUICulture.Name != _originalUICulture.Name)
                CultureInfo.CurrentUICulture = _originalUICulture;
        }

        public static CultureOverrideBlock Auto(CultureInfo overrideCulture) => new CultureOverrideBlock(overrideCulture);
    }
}