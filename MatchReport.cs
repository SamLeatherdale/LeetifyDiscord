using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace LeetifyDiscord;

public static class MatchReport {
	private const int EmbedColor = 0xF84982;

	private const int WinColor = 0x3BA55C;

	private const int LossColor = 0xED4245;

	private const int TieColor = 0xF0B429;

	public static bool PlayedWithFriend(LeetifyMatch match, ulong steamId, Func<ulong, bool> isFriend) {
		ArgumentNullException.ThrowIfNull(match);
		ArgumentNullException.ThrowIfNull(isFriend);

		if (match.Stats == null) {
			return false;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		PlayerStats? viewer = match.Stats.FirstOrDefault(stat => stat.Steam64Id == steamText);

		if (viewer == null) {
			return false;
		}

		foreach (PlayerStats stat in match.Stats) {
			if ((stat.InitialTeamNumber != viewer.InitialTeamNumber) || (stat.Steam64Id == steamText)) {
				continue;
			}

			if (IsFriend(stat, isFriend)) {
				return true;
			}
		}

		return false;
	}

	public static DiscordWebhookPayload Create(Uri matchUrl, LeetifyMatch? match, ulong steamId, Func<ulong, bool>? isFriend = null) {
		ArgumentNullException.ThrowIfNull(matchUrl);

		if (match == null) {
			return LinkOnly(matchUrl);
		}

		MatchCard card = BuildCard(match, steamId, isFriend);

		return new DiscordWebhookPayload {
			Embeds = [
				new DiscordEmbed {
					Title = card.Title,
					Url = matchUrl.ToString(),
					Color = card.Color,
					Image = PreviewImage(matchUrl),
					Description = card.Description,
					Fields = card.Fields,
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
		ScorePresentation? score = PresentScore(match, steamId);
		string map = DisplayMap(match.MapName);
		string title = score == null ? map : $"{map} - {score.Value.Text}";

		return new MatchCard(title, FormatHighlight(match.Stats, steamId, isFriend), FormatTopPlayers(match.Stats, steamId, isFriend), score?.Color ?? EmbedColor);
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
			string text = $"{ours.Score.ToString(CultureInfo.InvariantCulture)} : {theirs.Score.ToString(CultureInfo.InvariantCulture)}";

			return new ScorePresentation(text, color);
		}

		List<TeamScore> ordered = scores.OrderByDescending(static score => score.Score).ThenBy(static score => score.TeamNumber).ToList();

		if (ordered.Count >= 2) {
			return new ScorePresentation($"{ordered[0].Score.ToString(CultureInfo.InvariantCulture)} : {ordered[1].Score.ToString(CultureInfo.InvariantCulture)}", EmbedColor);
		}

		return new ScorePresentation(ordered[0].Score.ToString(CultureInfo.InvariantCulture), EmbedColor);
	}

	private const int MvpHighlightMinimum = 5;

	private static string FormatRating(double rating) => (rating * 100).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

	private static string FormatAdr(double adr) => adr.ToString("0.#", CultureInfo.InvariantCulture);

	private static string? FormatHighlight(List<PlayerStats>? stats, ulong steamId, Func<ulong, bool>? isFriend) {
		List<PlayerStats>? ranked = RankedTeammates(stats, steamId);

		if (ranked == null) {
			return null;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		int viewerIndex = ranked.FindIndex(stat => stat.Steam64Id == steamText);
		List<PlayerStats> listed = [];

		for (int index = 0; index < ranked.Count; index++) {
			if (IsListed(ranked[index], index, viewerIndex, isFriend)) {
				listed.Add(ranked[index]);
			}
		}

		PlayerStats? multiKill = BestMultiKill(listed);

		if (multiKill != null) {
			int tier = multiKill.Multi5k > 0 ? 5 : 4;
			int count = tier == 5 ? multiKill.Multi5k : multiKill.Multi4k;

			return MultiKillLine(DisplayName(multiKill, multiKill.Steam64Id == steamText), tier, count);
		}

		PlayerStats? mvp = null;

		foreach (PlayerStats player in listed) {
			if ((player.Mvps < MvpHighlightMinimum) || ((mvp != null) && (player.Mvps <= mvp.Mvps))) {
				continue;
			}

			mvp = player;
		}

		return mvp == null ? null : $"{DisplayName(mvp, mvp.Steam64Id == steamText)} earned {mvp.Mvps.ToString(CultureInfo.InvariantCulture)} MVPs";
	}

	private static PlayerStats? BestMultiKill(List<PlayerStats> ranked) {
		PlayerStats? best = null;
		int bestTier = 0;
		int bestCount = 0;

		foreach (PlayerStats player in ranked) {
			int tier = player.Multi5k > 0 ? 5 : player.Multi4k > 0 ? 4 : 0;

			if (tier == 0) {
				continue;
			}

			int count = tier == 5 ? player.Multi5k : player.Multi4k;

			if ((best != null) && ((tier < bestTier) || ((tier == bestTier) && (count <= bestCount)))) {
				continue;
			}

			best = player;
			bestTier = tier;
			bestCount = count;
		}

		return best;
	}

	private static string MultiKillLine(string name, int tier, int count) {
		if (tier == 5) {
			return count == 1 ? $"{name} scored an ace" : $"{name} scored {count.ToString(CultureInfo.InvariantCulture)} aces";
		}

		return count == 1 ? $"{name} scored a 4k" : $"{name} scored {count.ToString(CultureInfo.InvariantCulture)} 4ks";
	}

	private static DiscordEmbedField[]? FormatTopPlayers(List<PlayerStats>? stats, ulong steamId, Func<ulong, bool>? isFriend) {
		List<PlayerStats>? ranked = RankedTeammates(stats, steamId);

		if (ranked == null) {
			return null;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		int viewerIndex = ranked.FindIndex(stat => stat.Steam64Id == steamText);
		StringBuilder names = new();
		StringBuilder ratings = new();
		StringBuilder damage = new();
		bool listed = false;

		for (int index = 0; index < ranked.Count; index++) {
			if (!IsListed(ranked[index], index, viewerIndex, isFriend)) {
				continue;
			}

			if (listed) {
				names.Append('\n');
				ratings.Append('\n');
				damage.Append('\n');
			}

			names.Append(CultureInfo.InvariantCulture, $"{PlaceMarker(index + 1)}  {DisplayName(ranked[index], index == viewerIndex)}");
			ratings.Append(CultureInfo.InvariantCulture, $"`{FormatRating(ranked[index].LeetifyRating)}`");
			damage.Append(CultureInfo.InvariantCulture, $"`{FormatAdr(ranked[index].Dpr)}`");
			listed = true;
		}

		if (!listed) {
			return null;
		}

		return [
			new DiscordEmbedField {
				Name = "Team rankings",
				Value = names.ToString(),
				Inline = true
			},
			new DiscordEmbedField {
				Name = "Rating",
				Value = ratings.ToString(),
				Inline = true
			},
			new DiscordEmbedField {
				Name = "ADR",
				Value = damage.ToString(),
				Inline = true
			}
		];
	}

	private static List<PlayerStats>? RankedTeammates(List<PlayerStats>? stats, ulong steamId) {
		if ((stats == null) || (stats.Count == 0)) {
			return null;
		}

		string steamText = steamId.ToString(CultureInfo.InvariantCulture);
		PlayerStats? viewer = stats.FirstOrDefault(stat => stat.Steam64Id == steamText);

		if (viewer == null) {
			return null;
		}

		List<PlayerStats> ranked = stats.Where(stat => (stat.InitialTeamNumber == viewer.InitialTeamNumber) && !string.IsNullOrWhiteSpace(stat.Name)).OrderByDescending(static stat => stat.LeetifyRating).ToList();

		return ranked.Count == 0 ? null : ranked;
	}

	private static string DisplayName(PlayerStats player, bool viewer) {
		string name = EscapeDiscord(player.Name!);

		return viewer ? $"*{name}*" : name;
	}

	private static bool IsListed(PlayerStats player, int index, int viewerIndex, Func<ulong, bool>? isFriend) => (isFriend == null) || (index == viewerIndex) || IsFriend(player, isFriend);

	private static bool IsFriend(PlayerStats player, Func<ulong, bool> isFriend) => ulong.TryParse(player.Steam64Id, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId) && isFriend(steamId);

	private static string PlaceMarker(int place) => place switch {
		1 => "🥇",
		2 => "🥈",
		3 => "🥉",
		4 => "4️⃣",
		5 => "5️⃣",
		_ => place.ToString(CultureInfo.InvariantCulture)
	};

	private static DiscordWebhookPayload LinkOnly(Uri matchUrl) => new() {
		Embeds = [
			new DiscordEmbed {
				Title = "Leetify match",
				Url = matchUrl.ToString(),
				Color = EmbedColor,
				Image = PreviewImage(matchUrl)
			}
		]
	};

	private static DiscordEmbedImage? PreviewImage(Uri matchUrl) {
		string? matchId = matchUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

		if (!Guid.TryParse(matchId, out Guid parsed)) {
			return null;
		}

		return new DiscordEmbedImage {
			Url = $"https://api.cs-prod.leetify.com/preview-image/match-details/{parsed:D}"
		};
	}

	private readonly record struct MatchCard(string Title, string? Description, DiscordEmbedField[]? Fields, int Color);

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

	[JsonPropertyName("image")]
	public DiscordEmbedImage? Image { get; set; }

	[JsonPropertyName("footer")]
	public DiscordEmbedFooter? Footer { get; set; }

	[JsonPropertyName("timestamp")]
	public DateTimeOffset? Timestamp { get; set; }

	[JsonPropertyName("fields")]
	public DiscordEmbedField[]? Fields { get; set; }
}

public sealed class DiscordEmbedImage {
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

	[JsonPropertyName("dpr")]
	public double Dpr { get; set; }

	[JsonPropertyName("mvps")]
	public int Mvps { get; set; }

	[JsonPropertyName("multi4k")]
	public int Multi4k { get; set; }

	[JsonPropertyName("multi5k")]
	public int Multi5k { get; set; }
}
