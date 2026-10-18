using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace LeetifyDiscord;

public static class MatchReport {
	private const int EmbedColor = 0xF84982;

	private const int TerroristTeam = 2;

	private const int CounterTerroristTeam = 3;

	public static DiscordWebhookPayload Create(Uri matchUrl, LeetifyMatch? match) {
		ArgumentNullException.ThrowIfNull(matchUrl);

		if (match == null) {
			return LinkOnly(matchUrl);
		}

		return new DiscordWebhookPayload {
			Embeds = [
				new DiscordEmbed {
					Title = DisplayMap(match.MapName),
					Url = matchUrl.ToString(),
					Description = BuildDescription(match),
					Color = EmbedColor,
					Thumbnail = MapThumbnail(match.MapName),
					Footer = DisplaySource(match.DataSource),
					Timestamp = match.FinishedAt
				}
			]
		};
	}

	public static DiscordWebhookPayload TestPayload() => new() {
		Embeds = [
			new DiscordEmbed {
				Title = "Leetify webhook test",
				Description = "ArchiSteamFarm can post to this channel.",
				Color = EmbedColor
			}
		]
	};

	private static string? BuildDescription(LeetifyMatch match) {
		StringBuilder text = new();
		string? score = FormatScore(match.TeamScores);

		if (score != null) {
			text.Append(score);
		}

		string? top = FormatTopPlayers(match.Stats);

		if (top != null) {
			if (text.Length > 0) {
				text.Append("\n\n");
			}

			text.Append(top);
		}

		return text.Length == 0 ? null : text.ToString();
	}

	private static string DisplayMap(string? mapName) {
		if (string.IsNullOrWhiteSpace(mapName)) {
			return "Leetify match";
		}

		if (MapNames.TryGetValue(mapName, out string? display)) {
			return display;
		}

		string trimmed = mapName;

		if (trimmed.StartsWith("de_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("cs_", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("ar_", StringComparison.OrdinalIgnoreCase)) {
			trimmed = trimmed[3..];
		}

		return TitleCase(trimmed);
	}

	private static DiscordEmbedFooter? DisplaySource(string? dataSource) {
		if (string.IsNullOrWhiteSpace(dataSource)) {
			return null;
		}

		string text = dataSource.ToLowerInvariant() switch {
			"matchmaking" => "Matchmaking",
			"faceit" => "FACEIT",
			"premier" => "Premier",
			"wingman" => "Wingman",
			_ => TitleCase(dataSource)
		};

		return new DiscordEmbedFooter { Text = text };
	}

	private static string EscapeDiscord(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("*", "\\*", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal).Replace("~", "\\~", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal);

	private static string TitleCase(string value) {
		StringBuilder text = new(value.Length);
		bool capitalize = true;

		foreach (char character in value) {
			if ((character == '_') || (character == ' ')) {
				text.Append(' ');
				capitalize = true;

				continue;
			}

			text.Append(capitalize && (character is >= 'a' and <= 'z') ? (char) (character - 32) : character);
			capitalize = false;
		}

		return text.ToString();
	}

	private static string? FormatScore(List<TeamScore>? scores) {
		if ((scores == null) || (scores.Count == 0)) {
			return null;
		}

		TeamScore? terrorists = scores.FirstOrDefault(score => score.TeamNumber == TerroristTeam);
		TeamScore? counterTerrorists = scores.FirstOrDefault(score => score.TeamNumber == CounterTerroristTeam);

		if ((terrorists != null) && (counterTerrorists != null)) {
			return $"**{terrorists.Score.ToString(CultureInfo.InvariantCulture)} : {counterTerrorists.Score.ToString(CultureInfo.InvariantCulture)}**\nTerrorists · Counter-Terrorists";
		}

		return string.Join("    ·    ", scores.Select(static score => $"**Team {score.TeamNumber.ToString(CultureInfo.InvariantCulture)}**  {score.Score.ToString(CultureInfo.InvariantCulture)}"));
	}

	private static string FormatRating(double rating) => (rating * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

	private static string? FormatTopPlayers(List<PlayerStats>? stats) {
		if ((stats == null) || (stats.Count == 0)) {
			return null;
		}

		List<PlayerStats> top = stats.Where(static stat => !string.IsNullOrWhiteSpace(stat.Name)).OrderByDescending(static stat => stat.LeetifyRating).Take(3).ToList();

		if (top.Count == 0) {
			return null;
		}

		StringBuilder text = new("**Top ratings**");

		for (int index = 0; index < top.Count; index++) {
			PlayerStats player = top[index];
			text.Append(CultureInfo.InvariantCulture, $"\n{index + 1}.  {EscapeDiscord(player.Name!)}  `{FormatRating(player.LeetifyRating)}`");
		}

		return text.ToString();
	}

	private static DiscordWebhookPayload LinkOnly(Uri matchUrl) => new() {
		Embeds = [
			new DiscordEmbed {
				Title = "Leetify match",
				Url = matchUrl.ToString(),
				Color = EmbedColor
			}
		]
	};

	private static DiscordEmbedThumbnail? MapThumbnail(string? mapName) {
		if (string.IsNullOrWhiteSpace(mapName) || !mapName.All(static character => char.IsAsciiLetterOrDigit(character) || (character == '_'))) {
			return null;
		}

		return new DiscordEmbedThumbnail {
			Url = $"https://leetify.com/assets/images/maps-webp/small/{mapName}_1_small.webp"
		};
	}

	private static readonly Dictionary<string, string> MapNames = new(StringComparer.OrdinalIgnoreCase) {
		["de_ancient"] = "Ancient",
		["de_anubis"] = "Anubis",
		["de_cache"] = "Cache",
		["de_dust2"] = "Dust II",
		["de_inferno"] = "Inferno",
		["de_mirage"] = "Mirage",
		["de_nuke"] = "Nuke",
		["de_overpass"] = "Overpass",
		["de_train"] = "Train",
		["de_vertigo"] = "Vertigo",
		["cs_alpine"] = "Alpine",
		["cs_italy"] = "Italy",
		["cs_office"] = "Office"
	};
}

public sealed class DiscordWebhookPayload {
	[JsonPropertyName("embeds")]
	public DiscordEmbed[]? Embeds { get; set; }
}

public sealed class DiscordEmbed {
	[JsonPropertyName("title")]
	public string? Title { get; set; }

	[JsonPropertyName("url")]
	public string? Url { get; set; }

	[JsonPropertyName("description")]
	public string? Description { get; set; }

	[JsonPropertyName("color")]
	public int? Color { get; set; }

	[JsonPropertyName("thumbnail")]
	public DiscordEmbedThumbnail? Thumbnail { get; set; }

	[JsonPropertyName("footer")]
	public DiscordEmbedFooter? Footer { get; set; }

	[JsonPropertyName("timestamp")]
	public DateTimeOffset? Timestamp { get; set; }

	[JsonPropertyName("fields")]
	public DiscordEmbedField[]? Fields { get; set; }
}

public sealed class DiscordEmbedThumbnail {
	[JsonPropertyName("url")]
	public string? Url { get; set; }
}

public sealed class DiscordEmbedFooter {
	[JsonPropertyName("text")]
	public string? Text { get; set; }
}

public sealed class DiscordEmbedField {
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("value")]
	public string? Value { get; set; }

	[JsonPropertyName("inline")]
	public bool Inline { get; set; }
}

public sealed class LeetifyMatch {
	[JsonPropertyName("id")]
	public string? Id { get; set; }

	[JsonPropertyName("finished_at")]
	public DateTimeOffset? FinishedAt { get; set; }

	[JsonPropertyName("data_source")]
	public string? DataSource { get; set; }

	[JsonPropertyName("map_name")]
	public string? MapName { get; set; }

	[JsonPropertyName("team_scores")]
	public List<TeamScore>? TeamScores { get; set; }

	[JsonPropertyName("stats")]
	public List<PlayerStats>? Stats { get; set; }
}

public sealed class TeamScore {
	[JsonPropertyName("team_number")]
	public int TeamNumber { get; set; }

	[JsonPropertyName("score")]
	public int Score { get; set; }
}

public sealed class PlayerStats {
	[JsonPropertyName("steam64_id")]
	public string? Steam64Id { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("initial_team_number")]
	public int InitialTeamNumber { get; set; }

	[JsonPropertyName("leetify_rating")]
	public double LeetifyRating { get; set; }
}
