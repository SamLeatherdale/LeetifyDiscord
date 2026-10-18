namespace LeetifyDiscord;

public sealed class PluginConfiguration {
	public const string WebhookVariable = "DISCORD_WEBHOOK_URL";

	public const string ApiKeyVariable = "LEETIFY_API_KEY";

	public Uri? WebhookUrl { get; private init; }

	public string? LeetifyApiKey { get; private init; }

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

		return new PluginConfiguration {
			WebhookUrl = webhook,
			LeetifyApiKey = apiKey,
			Warnings = warnings
		};
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
