# LeetifyDiscord

ArchiSteamFarm plugin that posts Leetify match messages to a Discord webhook.

This directory is its own git repository. It sits next to a checkout of ArchiSteamFarm and is not a commit or a nested repository inside that checkout.

```
repos/
  ArchiSteamFarm/
  LeetifyDiscord/
```

## Build

Requires the .NET 10 SDK and the `ArchiSteamFarm.dll` from the ASF release that will load the plugin. The Raspberry Pi is running the official `linux-arm64` build, currently `6.3.10.6`. A plugin built against a different ASF version will not load. Do not add an `[Export]` attribute: ASF already exports types that implement `IPlugin`, and the official single-file build does not ship `System.Composition.AttributedModel`.

```
# From an ASF-generic.zip of that release
mkdir lib
# copy ArchiSteamFarm.dll into lib/
dotnet test
dotnet publish -c Release -o out
```

Copy `out/LeetifyDiscord.dll` to `plugins/LeetifyDiscord/` and restart ASF. The publish output is only that DLL.

To build against another copy of the assembly:

```
dotnet publish -c Release -o out -p:AsfAssembly=C:\path\to\ArchiSteamFarm.dll
```

## Configuration

Set these on the ASF process. They are not stored in this repository.

| Variable | Required | Purpose |
| --- | --- | --- |
| `DISCORD_WEBHOOK_URL` | yes | `https://discord.com/api/webhooks/...` or `discordapp.com`. The plugin does not set the webhook name or avatar. |
| `LEETIFY_API_KEY` | no | Free key from [leetify.com/app/developer](https://leetify.com/app/developer). Without it, only the match link is posted. |

Any Steam message that contains a Leetify match link is posted. The ASF bot that receives it must not use OnlineStatus Offline. The plugin does not reply on Steam.

`leetifytest`, from an account with Master access or the ASF console, posts a fixed Discord message.

## GitHub

https://github.com/SamLeatherdale/LeetifyDiscord

Do not add this repository as a submodule of ArchiSteamFarm.
