using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace LeetifyDiscord;

public static class MatchReport {
	private const int EmbedColor = 0xF84982;

	private const int WinColor = 0x3BA55C;

	private const int LossColor = 0xED4245;

	private const int TieColor = 0xF0B429;

	public static DiscordWebhookPayload Create(Uri matchUrl, LeetifyMatch? match, ulong steamId, Func<ulong, bool>? isFriend = null) {
		ArgumentNullException.ThrowIfNull(matchUrl);

		if (match == null) {
			return LinkOnly(matchUrl);
		}

		MatchCard card = BuildCard(match, steamId, isFriend);

		return new DiscordWebhookPayload {
			Embeds = [
				new DiscordEmbed {
					Title = DisplayMap(match.MapName),
					Url = matchUrl.ToString(),
					Description = card.Description,
					Color = card.Color,
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

	private static MatchCard BuildCard(LeetifyMatch match, ulong steamId, Func<ulong, bool>? isFriend) {
		StringBuilder text = new();
		ScorePresentation? score = PresentScore(match, steamId);

		if (score != null) {
			text.Append(score.Value.Text);
		}

		string? top = FormatTopPlayers(match.Stats, steamId, isFriend);

		if (top != null) {
			if (text.Length > 0) {
				text.Append("\n\n");
			}

			text.Append(top);
		}

		return new MatchCard(text.Length == 0 ? null : text.ToString(), score?.Color ?? EmbedColor);
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

	private static ScorePresentation? PresentScore(LeetifyMatch match, ulong steamId) {
		List<TeamScore>? scores = match.TeamScores;

		if ((scores == null) || (scores.Count == 0)) {
			return null;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		PlayerStats? viewer = match.Stats?.FirstOrDefault(stat => stat.Steam64Id == steamText);
		TeamScore? ours = viewer == null ? null : scores.FirstOrDefault(score => score.TeamNumber == viewer.InitialTeamNumber);
		TeamScore? theirs = ours == null ? null : scores.FirstOrDefault(score => score.TeamNumber != ours.TeamNumber);

		if ((ours != null) && (theirs != null)) {
			int color = ours.Score > theirs.Score ? WinColor : ours.Score < theirs.Score ? LossColor : TieColor;
			string text = $"**{ours.Score.ToString(CultureInfo.InvariantCulture)} : {theirs.Score.ToString(CultureInfo.InvariantCulture)}**\nYou · Opponents";

			return new ScorePresentation(text, color);
		}

		List<TeamScore> ordered = scores.OrderByDescending(static score => score.Score).ThenBy(static score => score.TeamNumber).ToList();

		if (ordered.Count >= 2) {
			return new ScorePresentation($"**{ordered[0].Score.ToString(CultureInfo.InvariantCulture)} : {ordered[1].Score.ToString(CultureInfo.InvariantCulture)}**", EmbedColor);
		}

		return new ScorePresentation($"**{ordered[0].Score.ToString(CultureInfo.InvariantCulture)}**", EmbedColor);
	}

	private static string FormatRating(double rating) => (rating * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

	private static string? FormatTopPlayers(List<PlayerStats>? stats, ulong steamId, Func<ulong, bool>? isFriend) {
		if ((stats == null) || (stats.Count == 0)) {
			return null;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		PlayerStats? viewer = stats.FirstOrDefault(stat => stat.Steam64Id == steamText);

		if (viewer == null) {
			return null;
		}

		List<PlayerStats> ranked = stats.Where(stat => (stat.InitialTeamNumber == viewer.InitialTeamNumber) && !string.IsNullOrWhiteSpace(stat.Name)).OrderByDescending(static stat => stat.LeetifyRating).ToList();

		if (ranked.Count == 0) {
			return null;
		}

		int viewerIndex = ranked.FindIndex(stat => stat.Steam64Id == steamText);
		StringBuilder text = new("**Your team**");
		bool listed = false;

		for (int index = 0; index < ranked.Count; index++) {
			if ((isFriend != null) && (index != viewerIndex) && !IsFriend(ranked[index], isFriend)) {
				continue;
			}

			AppendPlayer(text, ranked[index], index, viewerIndex);
			listed = true;
		}

		return listed ? text.ToString() : null;
	}

	private static bool IsFriend(PlayerStats player, Func<ulong, bool> isFriend) => ulong.TryParse(player.Steam64Id, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId) && isFriend(steamId);

	private static void AppendPlayer(StringBuilder text, PlayerStats player, int index, int viewerIndex) {
		string name = EscapeDiscord(player.Name!);

		if (index == viewerIndex) {
			name = $"*{name}*";
		}

		text.Append(CultureInfo.InvariantCulture, $"\n{index + 1}.  {name}  `{FormatRating(player.LeetifyRating)}`");
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

	private readonly record struct MatchCard(string? Description, int Color);

	private readonly record struct ScorePresentation(string Text, int Color);

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
