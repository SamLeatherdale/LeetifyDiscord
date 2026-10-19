# Agent notes

This repository is a sibling of ArchiSteamFarm. Do not modify ArchiSteamFarm, and do not nest this repository inside it.

## Build constraints

Reference `ArchiSteamFarm.dll` from the release that loads the plugin. Set `Private` to false so publish output is `LeetifyDiscord.dll` only. Pass `-p:AsfAssembly=` when the DLL is not in `lib/`. A mismatched assembly fails plugin load.

Do not add `[Export(typeof(IPlugin))]` or a `System.Composition` package. ASF exports `IPlugin` implementations itself. The official single-file build does not ship `System.Composition.AttributedModel`.

`SteamKit2` is a compile-only reference. The host already loads it. Call `SteamFriends.GetFriendRelationship`, which ASF itself calls.

The official single-file host is trimmed. These calls are missing there:

- `MediaTypeWithQualityHeaderValue` constructor. Set the Accept header with `TryAddWithoutValidation`.
- `JsonSerializer` overloads that take `JsonTypeInfo`. Serialize and deserialize with `JsonSerializerOptions`.
- `CultureInfo.TextInfo`. Title-case ASCII in this project without `TextInfo`.

`PluginJsonContext` is for tests. The plugin runtime uses the reflection options in `OutboundClients.cs`.

## Secrets and sample data

Do not commit `local.env`, webhook URLs, API keys, or personal Steam IDs. Tests use Valve's sample account IDs `76561197960265729` through `76561197960265732`, and the public Leetify example match `e30ce267-5b26-48ac-8ad5-68d97a53884b`.

Do not log `HttpRequestException` text. It can contain the webhook URL. Logging `MissingMethodException.Message` is safe.

## Match behavior

`MatchReport.PlayedWithFriend` decides whether a match is posted. A teammate must be a Steam friend. The viewer and opponents do not qualify. When the roster is missing, the plugin skips the match and logs the reason at info level.

Team place markers are 🥇, 🥈, 🥉, 4️⃣, and 5️⃣. Do not write `1. ` list lines. Discord keeps the first number and then counts upward.
