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

		Assert.AreEqual("Nuke", embed.Title);
		Assert.AreEqual(MatchUrl.AbsoluteUri, embed.Url);
		Assert.AreEqual(0x3BA55C, embed.Color);
		Assert.AreEqual("https://leetify.com/assets/images/maps-webp/small/de_nuke_1_small.webp", embed.Thumbnail?.Url);
		Assert.AreEqual("Matchmaking", embed.Footer?.Text);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**13 : 3**");
		StringAssert.Contains(embed.Description, "You · Opponents");
		StringAssert.Contains(embed.Description, "🥇  *Nova*  `+12.32`");
		StringAssert.Contains(embed.Description, "🥈  Ken  `+5.00`");
		StringAssert.Contains(embed.Description, "🥉  Mira  `+1.00`");
		StringAssert.Contains(embed.Description, "4️⃣  Lux  `-1.00`");
		StringAssert.Contains(embed.Description, "5️⃣  Ren  `-4.00`");
		Assert.IsFalse(embed.Description.Contains("S1mple", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("Ash", StringComparison.Ordinal));
		Assert.IsNull(embed.Fields);
	}

	[TestMethod]
	public void ShowsANegativeRatingOnLeetifysScale() {
		const string json = """
			{"map_name":"de_nuke","data_source":"matchmaking","team_scores":[{"team_number":2,"score":3},{"team_number":3,"score":13}],"stats":[{"name":"Ash","steam64_id":"76561197960265730","initial_team_number":2,"leetify_rating":-0.0218}]}
			""";

		LeetifyMatch? match = JsonSerializer.Deserialize(json, PluginJsonContext.Default.LeetifyMatch);
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, 76561197960265730));

		Assert.AreEqual(0xED4245, embed.Color);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**3 : 13**");
		StringAssert.Contains(embed.Description, "🥇  *Ash*  `-2.18`");
	}

	[TestMethod]
	public void ShowsTheScoreWhenPlayersHaveNoNames() {
		LeetifyMatch match = SampleMatch();

		foreach (PlayerStats player in match.Stats!) {
			player.Name = null;
		}

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual("Nuke", embed.Title);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**13 : 3**");
		Assert.IsFalse(embed.Description.Contains("Your team", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ShowsBothSidesWhenTheScoresAreLevel() {
		LeetifyMatch match = SampleMatch();
		match.TeamScores![0].Score = 13;
		match.TeamScores[1].Score = 13;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId));

		Assert.AreEqual(0xF0B429, embed.Color);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**13 : 13**");
	}

	[TestMethod]
	public void KeepsTeamPlaceForFriends() {
		LeetifyMatch match = SampleMatch();
		match.Stats![1].Steam64Id = "76561197960265731";
		match.Stats[3].Steam64Id = "76561197960265732";

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match, ViewerSteamId, static steamId => steamId is 76561197960265730 or 76561197960265731 or 76561197960265732));

		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "🥇  *Nova*  `+12.32`");
		StringAssert.Contains(embed.Description, "🥈  Ken  `+5.00`");
		StringAssert.Contains(embed.Description, "4️⃣  Lux  `-1.00`");
		Assert.IsFalse(embed.Description.Contains("Mira", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("Ren", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("Ash", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("🥉", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ListsOnlyTheRecipientsTeam() {
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, SampleMatch(), 76561197960265730));

		Assert.AreEqual(0xED4245, embed.Color);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**3 : 13**");
		StringAssert.Contains(embed.Description, "🥇  S1mple  `+20.00`");
		StringAssert.Contains(embed.Description, "🥈  *Ash*  `-2.18`");
		Assert.IsFalse(embed.Description.Contains("Nova", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("Ken", StringComparison.Ordinal));
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
			new PlayerStats { Name = "Nova", Steam64Id = "76561197960265729", InitialTeamNumber = 3, LeetifyRating = 0.1232 },
			new PlayerStats { Name = "Ken", InitialTeamNumber = 3, LeetifyRating = 0.05 },
			new PlayerStats { Name = "Mira", InitialTeamNumber = 3, LeetifyRating = 0.01 },
			new PlayerStats { Name = "Lux", InitialTeamNumber = 3, LeetifyRating = -0.01 },
			new PlayerStats { Name = "Ren", InitialTeamNumber = 3, LeetifyRating = -0.04 },
			new PlayerStats { Name = "Ash", Steam64Id = "76561197960265730", InitialTeamNumber = 2, LeetifyRating = -0.0218 },
			new PlayerStats { Name = "S1mple", InitialTeamNumber = 2, LeetifyRating = 0.2 }
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
