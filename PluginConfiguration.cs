using System.Globalization;

namespace LeetifyDiscord;

public sealed class PluginConfiguration {
	public const ulong DefaultLeetifySteamId = 76561199102317235;

	public const string WebhookVariable = "DISCORD_WEBHOOK_URL";

	public const string ApiKeyVariable = "LEETIFY_API_KEY";

	public const string SteamIdVariable = "LEETIFY_STEAM_ID64";

	public Uri? WebhookUrl { get; private init; }

	public string? LeetifyApiKey { get; private init; }

	public ulong? ExtraSteamId { get; private init; }

	public IReadOnlyList<string> Warnings { get; private init; } = [];

	public static PluginConfiguration Read(Func<string, string?> environment) {
		ArgumentNullException.ThrowIfNull(environment);

		List<string> warnings = [];
		string? webhookText = environment(WebhookVariable)?.Trim();
		Uri? webhook = null;

		if (string.IsNullOrEmpty(webhookText)) {
			warnings.Add($"{WebhookVariable} is not set.");
		} else if (!TryParseDiscordWebhook(webhookText, out webhook)) {
			warnings.Add($"{WebhookVariable} is not an https Discord webhook URL.");
		}

		string? apiKey = environment(ApiKeyVariable)?.Trim();

		if (string.IsNullOrEmpty(apiKey)) {
			apiKey = null;
		}

		ulong? extraSteamId = null;
		string? steamText = environment(SteamIdVariable)?.Trim();

		if (!string.IsNullOrEmpty(steamText)) {
			if (!ulong.TryParse(steamText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) || !IsIndividualAccount(parsed)) {
				warnings.Add($"{SteamIdVariable} is not a SteamID64. Listening for the Leetify bot only.");
			} else if (parsed != DefaultLeetifySteamId) {
				extraSteamId = parsed;
			}
		}

		return new PluginConfiguration {
			WebhookUrl = webhook,
			LeetifyApiKey = apiKey,
			ExtraSteamId = extraSteamId,
			Warnings = warnings
		};
	}

	public bool Accepts(ulong steamId) => (steamId == DefaultLeetifySteamId) || (steamId == ExtraSteamId);

	public static bool IsIndividualAccount(ulong steamId) {
		// SteamID64 stores the account type in bits 52-55. Individual accounts use type 1.
		const int accountTypeShift = 52;
		const ulong accountTypeMask = 0xFUL << accountTypeShift;
		const ulong individualAccount = 1UL << accountTypeShift;

		return (steamId & accountTypeMask) == individualAccount;
	}

	public static bool TryParseDiscordWebhook(string text, out Uri? webhook) {
		webhook = null;

		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)) {
			return false;
		}

		if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		if (!uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		if (!uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase)) {
			return false;
		}

		webhook = uri;

		return true;
	}
}
