using System.Globalization;
using NUnit.Framework;
using TimboJimbo.Localization;

namespace TimboJimboTests.Localization
{
    public sealed class LocaleCodeTests
    {
        [Test]
        public void ParsesLanguageAndCountryInEitherSeparatorAndCase()
        {
            Assert.AreEqual(new LocaleCode("en", "AU"), LocaleCode.Parse("en-AU"));
            Assert.AreEqual(new LocaleCode("en", "AU"), LocaleCode.Parse("EN_au"));
            Assert.AreEqual(new LocaleCode("en"), LocaleCode.Parse(" en "));
            Assert.IsTrue(LocaleCode.Parse("").IsNone);
            Assert.IsTrue(LocaleCode.Parse(null).IsNone);
            Assert.IsTrue(LocaleCode.Parse("-AU").IsNone);
        }

        [Test]
        public void NormalizesCaseAndTreatsEmptyCountryAsNone()
        {
            var code = new LocaleCode("EN", "au");
            Assert.AreEqual("en", code.Language);
            Assert.AreEqual("AU", code.Country);
            Assert.AreEqual("en-AU", code.ToString());

            Assert.AreEqual(new LocaleCode("en"), new LocaleCode("en", ""));
            Assert.AreEqual(new LocaleCode("en"), new LocaleCode("en", "   "));
            Assert.IsFalse(new LocaleCode("en").HasCountry);
            Assert.AreEqual("en", new LocaleCode("en").ToString());
        }

        [Test]
        public void EqualityIgnoresCaseAndHashesAlike()
        {
            var a = new LocaleCode("en", "AU");
            var b = LocaleCode.Parse("EN-au");

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(new LocaleCode("en"), new LocaleCode("en", "AU"));
            Assert.AreNotEqual(new LocaleCode("en"), new LocaleCode("fr"));
            Assert.AreEqual(LocaleCode.None, default(LocaleCode));
        }

        [Test]
        public void LanguageOnlyDropsTheCountry()
        {
            Assert.AreEqual(new LocaleCode("en"), new LocaleCode("en", "AU").LanguageOnly);
            Assert.AreEqual(new LocaleCode("en"), new LocaleCode("en").LanguageOnly);
        }

        [Test]
        public void ComesFromCulturesAndLocales()
        {
            Assert.AreEqual(new LocaleCode("en", "AU"), LocaleCode.Of(CultureInfo.GetCultureInfo("en-AU")));
            Assert.AreEqual(new LocaleCode("fr"), LocaleCode.Of(CultureInfo.GetCultureInfo("fr")));
            Assert.IsTrue(LocaleCode.Of(CultureInfo.InvariantCulture).IsNone);
            Assert.IsTrue(LocaleCode.Of((LocalizationLocale)null).IsNone);

            using var settings = new TestSettings("en");
            var copy = settings.CreateLocale(new LocaleCode("en"));
            Assert.AreEqual(settings.Default.Code, copy.Code, "two locale assets with the same code are the same locale");
            Assert.AreEqual("en", settings.Default.DisplayCode);
        }
    }
}
