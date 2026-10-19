# LeetifyDiscord

ArchiSteamFarm plugin that posts a Leetify match to a Discord webhook when you played it with at least one Steam friend.

This directory is its own git repository. Keep it next to a checkout of ArchiSteamFarm. Do not commit it inside that checkout, and do not add it as a submodule.

```
repos/
  ArchiSteamFarm/
  LeetifyDiscord/
```

## How a match is posted

ArchiSteamFarm receives a Steam message. The plugin looks for an `https://leetify.com` or `https://www.leetify.com` match link and fetches that match from the Leetify API.

The plugin posts the match when a teammate is on your Steam friends list. You do not count as your own friend, and a friend on the other team does not count. The title is the map and the score, with your rounds first. Team rankings list you and those friends, in Leetify rating order, with rating and ADR in columns beside the names. Gold, silver, and bronze mark 1st through 3rd on the full team. 4️⃣ and 5️⃣ mark 4th and 5th. Your name is italic. One line above the rankings names you or a friend who scored an ace or a 4k, or who earned at least 5 MVPs. The embed is green for a win, red for a loss, and amber for a tie.

These lines in the ASF log describe the result:

- `LeetifyDiscord posted {url}.` means Discord accepted the message.
- `LeetifyDiscord skipped {url} because no Steam friend was on the team.` means the roster loaded and you queued without a friend.
- `LeetifyDiscord skipped {url} because the match details were unavailable.` means the roster could not be loaded, so a solo queue could not be told apart from a party.
- `Discord post failed for {url}` means Discord rejected the webhook request.

If the ASF log has no `LeetifyDiscord` line for a match, the message never reached the plugin. The plugin does not reply on Steam.

## Keep the bot able to receive chat

The account has to be logged on, and ASF has to pass friend messages through.

- Set `OnlineStatus` to anything except Offline. Offline drops friend messages before a plugin sees them. Invisible still receives them.
- Leave `ShutdownOnFarmingFinished` unset. With that farming preference, ASF stops the bot when card farming finishes, and later messages never arrive.
- Leave the bot `Enabled`, and do not leave it stopped. A stopped bot is disconnected from Steam.
- Playing a game on the account pauses farming. ASF stays logged on and still receives friend messages. When you leave the game, ASF resumes farming and plays another title until that finishes.
- A second ASF process on the same account signs this one out. The Steam client you play on does not.

## Build

You need the .NET 10 SDK and the `ArchiSteamFarm.dll` from the ASF release that will load the plugin. A plugin built against a different ASF assembly fails to load. Copy that DLL from `ASF-generic.zip` for the same release. Do not add an `[Export]` attribute. ASF already exports types that implement `IPlugin`, and the official single-file build does not ship `System.Composition.AttributedModel`.

To build and test:

```
mkdir lib
# copy ArchiSteamFarm.dll into lib/
dotnet test
dotnet publish -c Release -o out
```

`out/` contains `LeetifyDiscord.dll`. To build against an assembly outside `lib/`, pass its path:

```
dotnet publish -c Release -o out -p:AsfAssembly=PATH_TO_ARCHISTEAMFARM_DLL
```

`PATH_TO_ARCHISTEAMFARM_DLL` is the full path to `ArchiSteamFarm.dll`.

## Deploy

Copy `out/LeetifyDiscord.dll` to `plugins/LeetifyDiscord/` under the directory where ASF runs. Restart that ASF process. Keep the DLL in one plugins directory. A second copy loads the plugin twice.

## Configuration

Set these variables on the ASF process. This repository does not store them.

| Variable | Required | Purpose |
| --- | --- | --- |
| `DISCORD_WEBHOOK_URL` | yes | `https://discord.com/api/webhooks/...` on `discord.com` or `discordapp.com`. The plugin does not set the webhook name or avatar. |
| `LEETIFY_API_KEY` | no | Free key from [leetify.com/app/developer](https://leetify.com/app/developer). Without it, matches are not posted. |

`leetifytest`, from an account with Master access or the ASF console, posts a fixed Discord message.

## GitHub

https://github.com/SamLeatherdale/LeetifyDiscord
