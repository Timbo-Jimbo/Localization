# Localization Package

A simple localization package for Unity with a built in AI Transation tool.

# Installation

This package depends on [ZString.](https://github.com/Cysharp/ZString)
Install both packages through **Window > Package Manager** using **Add package from git URL...**.

1. Add ZString first: 
```
https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString
```
2. Then add Localization: 
```
https://github.com/Timbo-Jimbo/Localization.git?path=Packages/com.timbojimbo.localization
```

# Usage

## Project Setup

After installation, a popup will appear and help you get started generating some assets. Choose any folder inside `Assets` and it'll create a minimal working setup for you:

- a `LocalizationSettings.asset`
- an English default locale (`en`)
- a sample `HelloWorld.asset` localized string

By default, most projects will probably put these under something like `Assets/Localization`, but the exact location is not important.

If you rather set things up manually, then you can find all create options you need under `Create > Localization > Settings and Config`

> [!IMPORTANT]
> `LocalizationSettings` asset must be present in Unity's **Preloaded Assets** so it is available at runtime. The scaffolding helper adds it for you automatically. If you create or move the asset manually, double-check it under `Project Settings > Player > Preloaded Assets`.

## Localized Value Assets
Localized values are `ScriptableObject` assets that store per-locale data.

You can create them from the Project window with:

- `Create > Localization > Localized String`
- `Create > Localization > Localized Sprite`

After creating one, open it in the inspector and fill in the value for your default locale first. The inspector will also show the other locales from `LocalizationSettings`.

Asset location is not important here either. Put them wherever they make sense for your project structure.

## Applicator Components

Applicators are components you add to a `GameObject` to automatically push a localized value onto another component.

They are useful when you want UI text to update itself whenever:

- the active locale changes
- the referenced localized value changes
- formatting parameters change

Right now the built-in applicators are text-focused:

- `TMPTextLocalizer` for `TextMeshProUGUI`
- `LegacyTextLocalizer` for `UnityEngine.UI.Text`
- `UniTextLocalizer` for `LightSide.UniText` when the [UniText package](https://github.com/LightSideKittens/UniText) is detected.

In practice, you usually add one of these components, assign its target field, and then set the `LocalizableString` it should resolve.

## Scripting API

Most runtime access goes through `LocalizationSettings`, `Localized*`, and the `Localizable*` wrappers.

### Iterate Locales

Use `LocalizationSettings.Locales` to access the configured locale list.

```csharp
foreach (var locale in LocalizationSettings.Locales)
{
	Debug.Log(locale.DisplayCode);
}
```

### Check the Active Locale

Use `LocalizationSettings.ActiveLocale` to read the current locale.

```csharp
var activeLocale = LocalizationSettings.ActiveLocale;

if (activeLocale != null)
	Debug.Log($"Active locale: {activeLocale.DisplayCode}");
```

If you want to switch locales in code:

```csharp
LocalizationSettings.SetActiveLocale(myLocale);
```

### Manually Resolve Values

If you want to resolve values yourself instead of using an applicator, pass in the locale you want.

```csharp
var locale = LocalizationSettings.ActiveLocale;

string title = myLocalizedString.Resolve(locale);
string buttonLabel = myLocalizableString.Resolve(locale);
Sprite icon = myLocalizableSprite.Resolve(locale);
```

Formatted strings also support manual parameters:

```csharp
string coinsText = myLocalizedString.Resolve(locale, coinCount);
string scoreText = myLocalizableString.Resolve(locale, score);
```

# AI Translations

## The 'AI Translator' Asset

Create an AI translator asset through **Localization/Settings and Config/AI Translator (Editor)** and configure it via the inspector.

> [!IMPORTANT]
> The API key is entered in the inspector and stored in **EditorPrefs**, not inside the asset itself. You can safely commit your changes. Make sure to back up your API Keys because they won't be stored here! You'll need to enter it again on each machine you use.
 
### Context Blocks

Context blocks are free-form notes that get sent with every translation request.

They are useful for things like:

- lore or world rules
- tone of voice
- character personality
- UI style guidelines
- formatting rules
- project-specific translation dos and don'ts

If you have guidance that should apply everywhere, you can also create a **Project Wide Translation Context Blocks** asset. The blocks in this asset are appended to each request.

### OpenAI-Compatible Base URLs

This translator uses the OpenAI-style chat completions endpoint, so the base URL should point at the provider's OpenAI-compatible API root. The package will send requests to:

- `{baseUrl}/chat/completions`

Examples:

- **OpenAI:** `https://api.openai.com/v1`
- **Gemini (OpenAI compatibility):** `https://generativelanguage.googleapis.com/v1beta/openai`

Example model names:

- OpenAI: `gpt-4.1-mini`
- Gemini: `gemini-3.5-flash`

If you are using a proxy or another OpenAI-compatible service, set its base URL here as long as it supports the same chat completions format.

## Glossary
Use the glossary for terms you want translated consistently across your project.

Good glossary entries include:

- character names
- item names
- skill names
- faction names
- brand or product terms
- words that should stay untranslated

Example:

- `Slime Core` should always stay `Slime Core`
- `Ranger` should always translate to your preferred class name in each language

This helps the AI stay consistent instead of inventing a slightly different term every few strings.

## Advanced Formatting

Alongside normal C# formatting, the package supports a few custom formatters that are especially useful for AI-assisted translation.

### Plurals
Use `plural` when the text should change based on grammatical number.

Basic shape:

- `{0:plural|one=apple|other=apples}`

Example:

- `You found {0:plural|one=1 coin|other=[#:N0] coins}.`

This can produce:

- `You found 1 coin.`
- `You found 25 coins.`

In some languages the plural block may expand, shrink, or even disappear entirely if that reads more naturally.

Another example:

- `There {0:plural|one=is one player ready|other=are [#] players ready}.`

### Query
Use `query` when you want a small conditional chain.

Basic shape:

- `{0:query|<=0=Empty|<10=Almost empty|Full}`

Rules of thumb:

- conditions are checked from top to bottom
- the first match wins
- the fallback value goes last and has no `=`

Example:

- `Stock: {0:query|<=0=out of stock|<5=almost gone|available}`

This can produce:

- `Stock: out of stock`
- `Stock: almost gone`
- `Stock: available`

You can also use the value inside the result:

- `Lives: {0:query|<=0=Game Over|1=Last life|[#] lives left}`

### Map

Use `map` when you want exact value matching.

Basic shape:

- `{0:map|red=Red|blue=Blue|other=Unknown}`

This is useful for enums, booleans, short state labels, and other fixed values.

Examples:

- `Status: {0:map|true=Online|false=Offline}`
- `Rank: {0:map|bronze=Bronze|silver=Silver|gold=Gold|other=Unranked}`

### The [#] Placeholder

Use `[#]` inside a custom formatter block to refer to the same value that the formatter is currently evaluating.

Think of it like the “current item” for that formatter:

- `{0}` is the normal parameter outside the block
- `[#]` is the internal placeholder inside the block

Examples:

- `{0:plural|one=one apple|other=[#] apples}`
- `{0:query|<=0=none|1=one life|[#] lives}`

You can also apply normal numeric formatting to it:

- `[#:N0]`
- `[#:0.##]`

Example:

- `You found {0:plural|one=1 coin|other=[#:N0] coins}.`

Important:

- use square brackets, not curly braces
- `[#]` is only for use inside `plural`, `query`, and `map` formatter blocks
- do not nest normal placeholders like `{0}` or `{1}` inside a formatter block

> [!TIP]
> The package uses the **Project Wide Translation Context Blocks** feature to 'teach' the AI Translators how to use these Advanced Formatters. The asset is located in `Packages/com.timbojimbo.localization/Editor/BuiltInFormatterRules.asset` if you're curious! 

# AI Usage Disclosure

The overall architecture and serialized data structures designed by a human. An LLM was used in some Runtime logic, and used extensively in Editor Tooling and Documentation.