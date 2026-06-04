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