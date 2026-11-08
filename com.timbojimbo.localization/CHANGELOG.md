## [2.0.0] - 2026-11-09

### Changed

- Locales are identified by code. `LocaleCode` (a language and an optional country: `en`, `en-AU`) is a locale's identity; a `LocaleValuePair` stores a code, not a reference to a `LocalizationLocale` asset. Localized assets no longer reference the settings or its locales, so a content build (Addressables, AssetBundles) never carries its own copy of a locale, and a locale copied anyway is still the same locale. Compare locales with `a.Code == b.Code`, never by object
- `LocalizationSettings` is the one registry. It registers itself when loaded (the first one to load wins); a game that loads it another way calls `LocalizationSettings.Register(settings)`. `Find(code)` / `TryFind` give the registered locale for a code, `GetCulture(code)` its culture, `SetActiveLocale(code)` activates by code, and `ActiveCode` / `DefaultCode` are the active and default locales' codes
- Lookups take codes: `Resolve(LocaleCode)`, `TryFindValue(LocaleCode)`, `TryFindBestMatchValue(LocaleCode)`, `HasMeaningfulValue(LocaleCode)` (was `HasMeaningfulValueForLocale`). `ILocalizedValueResolver<T>` and `ILocalizedStringResolver` take codes; the `LocalizationLocale` overloads of `Resolve` and `HasMeaningfulValue` are extension methods that forward the locale's code
- Best-match resolution skips entries without a usable value instead of matching them. The order is unchanged: the exact code, the language alone, the language with any country, the default locale. An empty entry for the active locale (what a content build's copy produced) no longer wins and resolves to nothing
- A player only reads localized assets. Keeping an asset's entries in step with the settings' locales (one entry per locale, in the settings' order) is editor work: on import, when the inspector opens it, when a locale is added to or deleted from the settings. `LocalizedValue.SyncWithProjectLocales` is gone; the editor's `LocalizedValueEditorUtility.SyncWithProjectLocales` replaces it
- A missing value warns once per locale per asset, naming the asset, instead of on every call
- Plural rules follow the locale being resolved (the culture `Resolve` formats under), not the active locale
- The inspector's rows show the locale the settings register for each entry's code; an entry whose code the settings don't list shows as a warning row with a menu to remove such entries
- The AI translator and translation jobs work in codes: `AiTranslator.Translate(source, sourceCode, targetCodes, ...)`, `TranslationJobScope.SingleLocale(code)`, `TranslationGlossary.GetFiltered(codes, results)`, and `TranslationProgress.Code`

### Added

- **Tools > Localization > Migrate Assets To Locale Codes** re-saves every localized asset on codes. A 1.x asset still loads before that: its entries' codes are filled from the locale references they carry, which the migration then drops
- Edit-mode tests (`Tests/Editor`) covering code matching, empty-entry skipping, no runtime writes, and the 1.x migration

### Upgrading from 1.x

1. Update the package, let the project compile, then run **Tools > Localization > Migrate Assets To Locale Codes** and commit the re-saved assets
2. Replace `pair.Locale` with `pair.Code` (`new LocaleValuePair<string>(locale.Code, value)`), `HasMeaningfulValueForLocale(locale)` with `HasMeaningfulValue(locale)` or `HasMeaningfulValue(code)`, and any `localeA == localeB` with `localeA.Code == localeB.Code`
3. Code that implemented `ILocalizedValueResolver<T>` or `ILocalizedStringResolver` now implements the `LocaleCode` overloads

## [1.10.0] - 2026-10-19

### Changed

- The active locale's culture is now the current UI culture (`CultureInfo.CurrentUICulture`) while it is active, so whatever follows the UI language follows the active locale: which way the UI reads (UI Next's `UI.ReadingDirection`), resource lookups. With no locale at all, the device's UI culture comes back. Matching the device's locale still reads the device's own culture

## [1.9.0] - 2026-09-20

### Added

- `TextBlockLocalizer`, applying localized strings to `com.timbojimbo.ui.text`'s `TextBlock`, and seeding from a `TextBlock` in the localized string editor. Compiled only when that package is present

## [1.8.0] - 2026-09-18

### Changed

- Foldout headers, header labels and ghost buttons now come from `com.timbojimbo.core` (`FoldoutGUI`); `LocalizationEditorGUI.DrawFoldout` is removed and the remaining button/style members forward to Core. Adds a dependency on `com.timbojimbo.core` `0.6.0`
- `CHANGELOG.md` now ships inside the package folder

## [1.7.1] - 2026-09-15

### Fixed

- Replaced deprecated `GetInstanceID` editor usage with Unity's `GetEntityId` API, restoring compatibility with current Unity versions

## [1.7.0] - 2026-06-01

- Fixed some Editor script usage in runtime code preventing builds

## [1.6.0] - 2026-05-31

- Updated repo layout so that the repository is just a package and not a Unity Project + Package combo.

## [1.5.0] - 2026-05-31

- Added `ImageLocalizer`
- Fixed locales for non-default locale not being initialized correctly on `LocalizedValue` creation

## [1.4.0] - 2026-05-31

- Switched runtime text localizers to use `LocalizedString` and added legacy migration support from `LocalizableString`
- Fixed `TextLocalizer` and `TMPTextLocalizer` so localized text clears correctly, applies only when enabled, and unbinds reliably
- Added a reusable `LocalizedValue` property drawer with a localized asset picker, suggested match items, and `Create New...` support
- Improved locale management in `LocalizationSettingsEditor`: locales now sync across all `LocalizedValue` assets when added or deleted
- Added best-match locale resolution and meaningful-value checks to `LocalizedValue`
- Refined AI Translator inspector layout with foldable general/API sections, clearer API-state feedback, and default translator controls
- Added in-editor translating pulse/spinner indicators for localization fields
- Cleaned up editor helper APIs and localized value drawer reuse


## [1.3.0] - 2026-05-29

- Simplified usage context to fix recursive dirtying/saving

## [1.2.0] - 2026-05-26

- Fixed Null Refs in TMPTextLocalizer

## [1.1.0] - 2026-05-26

- Submitted package to OpenUPM.
- Updated installation instructions. 
 
## [1.0.0] - 2026-05-24

- This is the first release of *\<Localization\>*.