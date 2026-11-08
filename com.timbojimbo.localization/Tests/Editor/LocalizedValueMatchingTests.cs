using System.Linq;
using NUnit.Framework;
using TimboJimbo.Localization;
using UnityEngine;

namespace TimboJimboTests.Localization
{
    public sealed class LocalizedValueMatchingTests
    {
        [Test]
        public void ExactCodeWinsOverLanguageOnlyAndDefault()
        {
            using var settings = new TestSettings("en", "en-AU", "fr");
            var text = Strings.Create(settings, ("en", "colour"), ("en-AU", "colour, mate"), ("fr", "couleur"));

            Assert.AreEqual("colour, mate", text.Resolve(LocaleCode.Parse("en-AU")));
            Assert.AreEqual("colour", text.Resolve(LocaleCode.Parse("en")));
            Assert.AreEqual("couleur", text.Resolve(LocaleCode.Parse("fr")));
        }

        [Test]
        public void LanguageOnlyEntryServesACountryVariant()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "colour"), ("fr", "couleur"));

            Assert.AreEqual("couleur", text.Resolve(LocaleCode.Parse("fr-CA")));
        }

        [Test]
        public void AnotherCountryOfTheLanguageBeatsTheDefault()
        {
            using var settings = new TestSettings("en", "fr-CA");
            var text = Strings.Create(settings, ("en", "colour"), ("fr-CA", "couleur"));

            Assert.AreEqual("couleur", text.Resolve(LocaleCode.Parse("fr-FR")));
        }

        [Test]
        public void UnknownLanguageFallsBackToTheDefaultLocale()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "colour"), ("fr", "couleur"));

            Assert.AreEqual("colour", text.Resolve(LocaleCode.Parse("de")));
            Assert.AreEqual("colour", text.Resolve(LocaleCode.None));
        }

        [Test]
        public void EmptyEntriesAreSkippedNotMatched()
        {
            // The content-build bug: an empty entry for the active locale sat first and was taken as the perfect match.
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("fr", null), ("en", "colour"), ("fr", ""));

            Assert.AreEqual("colour", text.Resolve(LocaleCode.Parse("fr")));
            Assert.IsTrue(text.TryFindBestMatchValue(LocaleCode.Parse("fr"), out var match));
            Assert.AreEqual("en", match.Code.ToString());
        }

        [Test]
        public void NothingUsableResolvesToTheFallbackAndReportsNoMatch()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "   "), ("fr", null));

            Assert.IsFalse(text.TryFindBestMatchValue(LocaleCode.Parse("fr"), out _));
            Assert.AreEqual("<Missing String>", text.Resolve(LocaleCode.Parse("fr")));
        }

        [Test]
        public void HasMeaningfulValueIsExactNotBestMatch()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "colour"), ("fr", ""));

            Assert.IsTrue(text.HasMeaningfulValue(LocaleCode.Parse("en")));
            Assert.IsFalse(text.HasMeaningfulValue(LocaleCode.Parse("fr")));
            Assert.IsFalse(text.HasMeaningfulValue(LocaleCode.Parse("en-AU")));
            Assert.IsTrue(text.HasMeaningfulValue(settings.Default));
        }

        [Test]
        public void ResolvingByLocaleForwardsItsCodeSoACopyOfTheLocaleResolves()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "colour"), ("fr", "couleur"));

            // Not the settings' object: what a content build ships when it copied the locale.
            var copyOfFrench = settings.CreateLocale(LocaleCode.Parse("fr"));
            Assert.AreNotSame(settings.Locales[1], copyOfFrench);

            Assert.AreEqual("couleur", text.Resolve(copyOfFrench));
            Assert.AreEqual("couleur", text.Resolve(copyOfFrench, 1));
            Assert.AreEqual("colour", text.Resolve((LocalizationLocale)null));
        }

        [Test]
        public void FormattedResolveUsesTheTargetLocalesCultureAndPluralRules()
        {
            using var settings = new TestSettings("en", "fr");
            var text = Strings.Create(settings, ("en", "{0:N1} {0:plural|one=apple|other=apples}"), ("fr", "{0:N1} {0:plural|one=pomme|other=pommes}"));

            Assert.AreEqual("1.5 apples", text.Resolve(LocaleCode.Parse("en"), 1.5));
            // French: a decimal comma, and 1.5 is still singular.
            Assert.AreEqual("1,5 pomme", text.Resolve(LocaleCode.Parse("fr"), 1.5));
        }

        [Test]
        public void ResolvingNeverWritesToTheAsset()
        {
            using var settings = new TestSettings("en", "fr", "de");
            var text = Strings.Create(settings, ("fr", "couleur"), ("en", "colour"));
            var before = text.Values.Select(v => (v.Code, v.Value)).ToArray();

            text.Resolve(LocaleCode.Parse("de"));
            text.Resolve(LocaleCode.Parse("ja-JP"));
            text.Resolve(settings.CreateLocale(LocaleCode.Parse("ja")));
            text.TryFindValue(LocaleCode.Parse("de"), out _);
            text.HasMeaningfulValue(LocaleCode.Parse("de"));

            // Re-enabling is what a load does; it must not add, remove or reorder entries either.
            text.OnEnableForTests();

            CollectionAssert.AreEqual(before, text.Values.Select(v => (v.Code, v.Value)).ToArray());
            Assert.AreEqual(2, text.Values.Count, "no entry was added for the settings' 'de' locale at runtime");
        }

        [Test]
        public void WarnsOncePerLocalePerAsset()
        {
            using var settings = new TestSettings("en");
            var text = Strings.Create(settings, ("en", ""));
            var other = Strings.Create(settings, ("en", ""));

            Assert.IsTrue(text.MarkWarned(LocaleCode.Parse("fr")));
            Assert.IsFalse(text.MarkWarned(LocaleCode.Parse("fr")));
            Assert.IsTrue(text.MarkWarned(LocaleCode.Parse("de")));
            Assert.IsTrue(other.MarkWarned(LocaleCode.Parse("fr")), "another asset warns on its own account");
        }

        [Test]
        public void SettingsFindAndActivateByCode()
        {
            using var settings = new TestSettings("en", "fr-CA");

            Assert.AreSame(settings.Locales[1], LocalizationSettings.Find(LocaleCode.Parse("FR_ca")));
            Assert.IsNull(LocalizationSettings.Find(LocaleCode.Parse("fr")));
            Assert.IsTrue(LocalizationSettings.SetActiveLocale(LocaleCode.Parse("fr-CA")));
            Assert.AreEqual(LocaleCode.Parse("fr-CA"), LocalizationSettings.ActiveCode);
            Assert.IsFalse(LocalizationSettings.SetActiveLocale(LocaleCode.Parse("de")));
            Assert.AreEqual(LocaleCode.Parse("fr-CA"), LocalizationSettings.ActiveCode, "an unknown code changes nothing");
            Assert.AreEqual("fr-CA", LocalizationSettings.GetCulture(LocaleCode.Parse("fr-CA")).Name);
            Assert.AreEqual("de", LocalizationSettings.GetCulture(LocaleCode.Parse("de")).Name, "an unregistered code still has its culture");
        }
    }

    internal static class LocalizedValueTestExtensions
    {
        public static void OnEnableForTests(this LocalizedValue value)
        {
            // What a load does to the entries: nothing but filling codes from 1.x locale references.
            value.FillCodesFromLegacyLocales();
        }
    }
}
