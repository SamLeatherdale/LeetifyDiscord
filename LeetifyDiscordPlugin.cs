using System.Text.Json.Serialization;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Storage;
using SteamKit2;

namespace LeetifyDiscord;

// ASF exports IPlugin implementations by convention. An [Export] attribute would require
// System.Composition.AttributedModel, which the official single-file build does not ship.
public sealed class LeetifyDiscordPlugin : IBotCommand2, IBotMessage {
	private const string TestCommand = "LEETIFYTEST";

	private readonly HttpClient _httpClient = new() {
		Timeout = TimeSpan.FromSeconds(15)
	};

	private PluginConfiguration _configuration = null!;
	private DiscordWebhookClient? _discord;
	private LeetifyApiClient? _leetify;

	[JsonInclude]
	public string Name => nameof(LeetifyDiscordPlugin);

	[JsonInclude]
	public Version Version => typeof(LeetifyDiscordPlugin).Assembly.GetName().Version ?? new Version(1, 0, 0);

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		ArgumentNullException.ThrowIfNull(bot);
		ArgumentNullException.ThrowIfNull(args);

		if ((args.Length == 0) || !args[0].Equals(TestCommand, StringComparison.OrdinalIgnoreCase)) {
			return null;
		}

		if (access < EAccess.Master) {
			return null;
		}

		if (_discord == null) {
			return "Discord webhook is not configured.";
		}

		bool posted = await _discord.PostAsync(MatchReport.TestPayload()).ConfigureAwait(false);

		return posted ? "Posted a test message to Discord." : "Discord did not accept the test message.";
	}

	public async Task<string?> OnBotMessage(Bot bot, ulong steamID, string message) {
		ArgumentNullException.ThrowIfNull(bot);
		ArgumentException.ThrowIfNullOrEmpty(message);

		if (_discord == null) {
			return null;
		}

		foreach (LeetifyMatchLink link in LeetifyMessageParser.FindMatchLinks(message)) {
			try {
				LeetifyMatch? match = _leetify == null ? null : await _leetify.GetMatchAsync(link.MatchId).ConfigureAwait(false);
				Func<ulong, bool> isFriend = candidate => IsSteamFriend(bot, candidate);

				if (match == null) {
					bot.ArchiLogger.LogGenericInfo($"LeetifyDiscord skipped {link.Url} because the match details were unavailable.");

					continue;
				}

				if (!MatchReport.PlayedWithFriend(match, bot.SteamID, isFriend)) {
					bot.ArchiLogger.LogGenericInfo($"LeetifyDiscord skipped {link.Url} because no Steam friend was on the team.");

					continue;
				}

				bool posted = await _discord.PostAsync(MatchReport.Create(link.Url, match, bot.SteamID, isFriend)).ConfigureAwait(false);

				if (posted) {
					bot.ArchiLogger.LogGenericInfo($"LeetifyDiscord posted {link.Url}.");
				} else {
					bot.ArchiLogger.LogGenericWarning($"Discord post failed for {link.Url}");
				}
			} catch (Exception exception) {
				string detail = exception is MissingMethodException ? $"{exception.GetType().Name}: {exception.Message}" : exception.GetType().Name;

				bot.ArchiLogger.LogGenericWarning($"LeetifyDiscord failed for {link.Url}: {detail}");
			}
		}

		return null;
	}

	private static bool IsSteamFriend(Bot bot, ulong steamId) => (steamId != 0) && (steamId != bot.SteamID) && (bot.SteamFriends.GetFriendRelationship(steamId) == EFriendRelationship.Friend);

	public Task OnLoaded() {
		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LeetifyDiscord/1.0");
		_configuration = PluginConfiguration.Read(static name => Environment.GetEnvironmentVariable(name));

		foreach (string warning in _configuration.Warnings) {
			ASF.ArchiLogger.LogGenericWarning(warning);
		}

		if (_configuration.WebhookUrl == null) {
			ASF.ArchiLogger.LogGenericWarning("LeetifyDiscord is idle because the Discord webhook is not configured.");

			return Task.CompletedTask;
		}

		_discord = new DiscordWebhookClient(_httpClient, _configuration.WebhookUrl);

		if (_configuration.LeetifyApiKey != null) {
			_leetify = new LeetifyApiClient(_httpClient, _configuration.LeetifyApiKey);
			ASF.ArchiLogger.LogGenericInfo("LeetifyDiscord will include match details from the Leetify API.");
		} else {
			ASF.ArchiLogger.LogGenericWarning("LeetifyDiscord will not post matches because the Leetify API key is not set.");
		}

		ASF.ArchiLogger.LogGenericInfo("LeetifyDiscord is listening for Leetify match links.");

		return Task.CompletedTask;
	}
}
