using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LeetifyDiscord.Tests;

[TestClass]
public sealed class LeetifyMessageParserTests {
	[TestMethod]
	public void FindsMatchDetailsLink() {
		const string message = "Your match is ready: https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b";

		IReadOnlyList<LeetifyMatchLink> links = LeetifyMessageParser.FindMatchLinks(message);

		Assert.HasCount(1, links);
		Assert.AreEqual(Guid.Parse("e30ce267-5b26-48ac-8ad5-68d97a53884b"), links[0].MatchId);
		Assert.AreEqual("https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b", links[0].Url.AbsoluteUri);
	}

	[TestMethod]
	public void AcceptsWwwHostAndTrimsTrailingPunctuation() {
		const string message = "See https://www.leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b.";

		IReadOnlyList<LeetifyMatchLink> links = LeetifyMessageParser.FindMatchLinks(message);

		Assert.HasCount(1, links);
		Assert.AreEqual("https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b", links[0].Url.AbsoluteUri);
	}

	[TestMethod]
	public void AcceptsAMatchLinkThatCarriesATrackingQuery() {
		const string message = "Your match is ready: https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b?utm_source=steam-bot&utm_campaign=match_report_available";

		IReadOnlyList<LeetifyMatchLink> links = LeetifyMessageParser.FindMatchLinks(message);

		Assert.HasCount(1, links);
		Assert.AreEqual(Guid.Parse("e30ce267-5b26-48ac-8ad5-68d97a53884b"), links[0].MatchId);
		Assert.AreEqual("https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b", links[0].Url.AbsoluteUri);
	}

	[TestMethod]
	public void IgnoresOtherHostsPlainHttpAndLinksWithoutAMatchId() {
		const string message = "https://example.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b http://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b https://leetify.com/app";

		IReadOnlyList<LeetifyMatchLink> links = LeetifyMessageParser.FindMatchLinks(message);

		Assert.IsEmpty(links);
	}
}

[TestClass]
public sealed class MatchReportTests {
	private static readonly Uri MatchUrl = new("https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b");

	private const ulong ViewerSteamId = 76561197960265729;

	[TestMethod]
	public void PostsTheLinkWhenMatchDetailsAreMissing() {
		DiscordWebhookPayload payload = MatchReport.Create(MatchUrl, null, ViewerSteamId);

		DiscordEmbed embed = SingleEmbed(payload);

		Assert.AreEqual("Leetify match", embed.Title);
		Assert.AreEqual(MatchUrl.AbsoluteUri, embed.Url);
		Assert.IsNull(embed.Description);
		Assert.IsNull(embed.Fields);
	}

	[TestMethod]
	public void ShowsTheScoreAndTheThreeHighestRatings() {
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, SampleMatch(), ViewerSteamId));

		Assert.AreEqual("Nuke - 13 : 3", embed.Title);
		Assert.AreEqual(MatchUrl.AbsoluteUri, embed.Url);
		Assert.AreEqual(0x3BA55C, embed.Color);
		Assert.AreEqual("https://api.cs-prod.leetify.com/preview-image/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b", embed.Image?.Url);
		Assert.AreEqual("Matchmaking", embed.Footer?.Text);
		Assert.IsNull(embed.Description);
		Assert.IsNotNull(embed.Fields);
		Assert.HasCount(3, embed.Fields);
		Assert.AreEqual("Team rankings", embed.Fields[0].Name);
		Assert.IsTrue(embed.Fields[0].Inline);
		Assert.AreEqual("🥇  *Nova*\n🥈  Ken\n🥉  Mira\n4️⃣  Lux\n5️⃣  Ren", embed.Fields[0].Value);
		Assert.AreEqual("Rating", embed.Fields[1].Name);
		Assert.IsTrue(embed.Fields[1].Inline);
		Assert.AreEqual("`+12.32`\n`+5.00`\n`+1.00`\n`-1.00`\n`-4.00`", embed.Fields[1].Value);
		Assert.AreEqual("ADR", embed.Fields[2].Name);
		Assert.IsTrue(embed.Fields[2].Inline);
		Assert.AreEqual("`80`\n`70.5`\n`60`\n`50`\n`40`", embed.Fields[2].Value);
		string names = embed.Fields[0].Value ?? "";
		Assert.IsFalse(names.Contains("S1mple", StringComparison.Ordinal));
		Assert.IsFalse(names.Contains("Ash", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ShowsANegativeRatingOnLeetifysScale() {
		const string json = """
			{"map_name":"de_nuke","data_source":"matchmaking","team_scores":[{"team_number":2,"score":3},{"team_number":3,"score":13}],"stats":[{"name":"Ash","steam64_id":"76561197960265730","initial_team_number":2,"leetify_rating":-0.0218}]}
			""";

		LeetifyMatch? match = JsonSerializer.Deserialize(json, PluginJsonContext.Default.LeetifyMatch);
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, 76561197960265730));

		Assert.AreEqual("Nuke - 3 : 13", embed.Title);
		Assert.AreEqual(0xED4245, embed.Color);
		Assert.IsNotNull(embed.Fields);
		Assert.AreEqual("🥇  *Ash*", embed.Fields[0].Value);
		Assert.AreEqual("`-2.18`", embed.Fields[1].Value);
		Assert.AreEqual("`0`", embed.Fields[2].Value);
	}

	[TestMethod]
	public void ShowsTheScoreWhenPlayersHaveNoNames() {
		LeetifyMatch match = SampleMatch();

		foreach (PlayerStats player in match.Stats!) {
			player.Name = null;
		}

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Nuke - 13 : 3", embed.Title);
		Assert.IsNull(embed.Description);
		Assert.IsNull(embed.Fields);
	}

	[TestMethod]
	public void ShowsBothSidesWhenTheScoresAreLevel() {
		LeetifyMatch match = SampleMatch();
		match.TeamScores![0].Score = 13;
		match.TeamScores[1].Score = 13;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Nuke - 13 : 13", embed.Title);
		Assert.AreEqual(0xF0B429, embed.Color);
	}

	[TestMethod]
	public void KeepsTeamPlaceForFriends() {
		LeetifyMatch match = SampleMatch();
		match.Stats![1].Steam64Id = "76561197960265731";
		match.Stats[3].Steam64Id = "76561197960265732";

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId, static steamId => steamId is 76561197960265730 or 76561197960265731 or 76561197960265732));

		Assert.IsNotNull(embed.Fields);
		Assert.AreEqual("🥇  *Nova*\n🥈  Ken\n4️⃣  Lux", embed.Fields[0].Value);
		Assert.AreEqual("`+12.32`\n`+5.00`\n`-1.00`", embed.Fields[1].Value);
		Assert.AreEqual("`80`\n`70.5`\n`50`", embed.Fields[2].Value);
		string names = embed.Fields[0].Value ?? "";
		Assert.IsFalse(names.Contains("Mira", StringComparison.Ordinal));
		Assert.IsFalse(names.Contains("Ren", StringComparison.Ordinal));
		Assert.IsFalse(names.Contains("Ash", StringComparison.Ordinal));
		Assert.IsFalse(names.Contains("🥉", StringComparison.Ordinal));
	}

	[TestMethod]
	public void RecognizesAFriendOnTheSameTeam() {
		LeetifyMatch match = SampleMatch();
		match.Stats![1].Name = null;
		match.Stats[1].Steam64Id = "76561197960265731";

		Assert.IsTrue(MatchReport.PlayedWithFriend(match, ViewerSteamId, static steamId => steamId == 76561197960265731));
	}

	[TestMethod]
	public void IgnoresAFriendOnTheOtherTeam() {
		Assert.IsFalse(MatchReport.PlayedWithFriend(SampleMatch(), ViewerSteamId, static steamId => steamId == 76561197960265730));
	}

	[TestMethod]
	public void IgnoresATeamWithNoFriends() {
		Assert.IsFalse(MatchReport.PlayedWithFriend(SampleMatch(), ViewerSteamId, static _ => false));
		Assert.IsFalse(MatchReport.PlayedWithFriend(SampleMatch(), 76561197960265728, static _ => true));
	}

	[TestMethod]
	public void ListsOnlyTheRecipientsTeam() {
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, SampleMatch(), 76561197960265730));

		Assert.AreEqual("Nuke - 3 : 13", embed.Title);
		Assert.AreEqual(0xED4245, embed.Color);
		Assert.IsNotNull(embed.Fields);
		Assert.AreEqual("🥇  S1mple\n🥈  *Ash*", embed.Fields[0].Value);
		Assert.AreEqual("`+20.00`\n`-2.18`", embed.Fields[1].Value);
		Assert.AreEqual("`100`\n`30.2`", embed.Fields[2].Value);
		string names = embed.Fields[0].Value ?? "";
		Assert.IsFalse(names.Contains("Nova", StringComparison.Ordinal));
		Assert.IsFalse(names.Contains("Ken", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ShowsAFourKillAboveTheRankings() {
		LeetifyMatch match = SampleMatch();
		match.Stats![2].Multi4k = 1;
		match.Stats[6].Multi5k = 1;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Mira scored a 4k", embed.Description);
	}

	[TestMethod]
	public void PrefersAnAceOverAFourKillAndAHighMvpCount() {
		LeetifyMatch match = SampleMatch();
		match.Stats![0].Multi5k = 1;
		match.Stats[2].Multi4k = 2;
		match.Stats[1].Mvps = 6;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("*Nova* scored an ace", embed.Description);
	}

	[TestMethod]
	public void UsesTheHigherMultiKillCount() {
		LeetifyMatch match = SampleMatch();
		match.Stats![0].Multi4k = 1;
		match.Stats[1].Multi4k = 2;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Ken scored 2 4ks", embed.Description);
	}

	[TestMethod]
	public void FallsBackToFiveMvps() {
		LeetifyMatch match = SampleMatch();
		match.Stats![1].Mvps = 5;
		match.Stats[2].Mvps = 4;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Ken earned 5 MVPs", embed.Description);
	}

	[TestMethod]
	public void IgnoresFourMvps() {
		LeetifyMatch match = SampleMatch();
		match.Stats![1].Mvps = 4;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.IsNull(embed.Description);
	}

	[TestMethod]
	public void KeepsHighlightsToFriendsAndTheViewer() {
		LeetifyMatch match = SampleMatch();
		match.Stats![2].Multi5k = 1;
		match.Stats[1].Steam64Id = "76561197960265731";
		match.Stats[1].Multi4k = 1;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId, static steamId => steamId == 76561197960265731));

		Assert.AreEqual("Ken scored a 4k", embed.Description);
	}

	[TestMethod]
	public void KeepsTheViewersOwnHighlight() {
		LeetifyMatch match = SampleMatch();
		match.Stats![0].Multi4k = 1;
		match.Stats[2].Multi5k = 1;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId, static _ => false));

		Assert.AreEqual("*Nova* scored a 4k", embed.Description);
	}

	[TestMethod]
	public void OmitsAHighlightThatBelongsToNobodyListed() {
		LeetifyMatch match = SampleMatch();
		match.Stats![2].Multi5k = 1;
		match.Stats[1].Mvps = 6;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId, static _ => false));

		Assert.IsNull(embed.Description);
	}

	private static LeetifyMatch SampleMatch() => new() {
		MapName = "de_nuke",
		DataSource = "matchmaking",
		FinishedAt = DateTimeOffset.Parse("2026-10-18T05:46:37Z", System.Globalization.CultureInfo.InvariantCulture),
		TeamScores = [
			new TeamScore { TeamNumber = 2, Score = 3 },
			new TeamScore { TeamNumber = 3, Score = 13 }
		],
		Stats = [
			new PlayerStats { Name = "Nova", Steam64Id = "76561197960265729", InitialTeamNumber = 3, LeetifyRating = 0.1232, Dpr = 80 },
			new PlayerStats { Name = "Ken", InitialTeamNumber = 3, LeetifyRating = 0.05, Dpr = 70.5 },
			new PlayerStats { Name = "Mira", InitialTeamNumber = 3, LeetifyRating = 0.01, Dpr = 60 },
			new PlayerStats { Name = "Lux", InitialTeamNumber = 3, LeetifyRating = -0.01, Dpr = 50 },
			new PlayerStats { Name = "Ren", InitialTeamNumber = 3, LeetifyRating = -0.04, Dpr = 40 },
			new PlayerStats { Name = "Ash", Steam64Id = "76561197960265730", InitialTeamNumber = 2, LeetifyRating = -0.0218, Dpr = 30.2 },
			new PlayerStats { Name = "S1mple", InitialTeamNumber = 2, LeetifyRating = 0.2, Dpr = 100 }
		]
	};

	private static DiscordEmbed SingleEmbed(DiscordWebhookPayload payload) {
		Assert.IsNotNull(payload.Embeds);
		Assert.HasCount(1, payload.Embeds);

		return payload.Embeds[0];
	}
}

[TestClass]
public sealed class PluginConfigurationTests {
	[TestMethod]
	public void ReadsADiscordWebhookAndOptionalApiKey() {
		PluginConfiguration configuration = PluginConfiguration.Read(name => name switch {
			PluginConfiguration.WebhookVariable => "https://discord.com/api/webhooks/1/secret",
			PluginConfiguration.ApiKeyVariable => "key",
			_ => null
		});

		Assert.AreEqual("https://discord.com/api/webhooks/1/secret", configuration.WebhookUrl?.AbsoluteUri);
		Assert.AreEqual("key", configuration.LeetifyApiKey);
		Assert.IsEmpty(configuration.Warnings);
	}

	[TestMethod]
	public void RejectsAWebhookThatIsNotDiscord() {
		PluginConfiguration configuration = PluginConfiguration.Read(name => name switch {
			PluginConfiguration.WebhookVariable => "https://example.com/api/webhooks/1/secret",
			_ => null
		});

		Assert.IsNull(configuration.WebhookUrl);
		Assert.IsNull(configuration.LeetifyApiKey);
		Assert.HasCount(1, configuration.Warnings);
	}
}
