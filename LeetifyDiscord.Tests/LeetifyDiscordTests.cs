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
	public void IgnoresOtherHostsPlainHttpAndLinksWithoutAMatchId() {
		const string message = "https://example.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b http://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b https://leetify.com/app";

		IReadOnlyList<LeetifyMatchLink> links = LeetifyMessageParser.FindMatchLinks(message);

		Assert.IsEmpty(links);
	}
}

[TestClass]
public sealed class MatchReportTests {
	private static readonly Uri MatchUrl = new("https://leetify.com/app/match-details/e30ce267-5b26-48ac-8ad5-68d97a53884b");

	[TestMethod]
	public void PostsTheLinkWhenMatchDetailsAreMissing() {
		DiscordWebhookPayload payload = MatchReport.Create(MatchUrl, null);

		DiscordEmbed embed = SingleEmbed(payload);

		Assert.AreEqual("Leetify match", embed.Title);
		Assert.AreEqual(MatchUrl.AbsoluteUri, embed.Url);
		Assert.IsNull(embed.Description);
		Assert.IsNull(embed.Fields);
	}

	[TestMethod]
	public void ShowsTheScoreAndTheThreeHighestRatings() {
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, SampleMatch()));

		Assert.AreEqual("Nuke", embed.Title);
		Assert.AreEqual(MatchUrl.AbsoluteUri, embed.Url);
		Assert.AreEqual(0xF84982, embed.Color);
		Assert.AreEqual("https://leetify.com/assets/images/maps-webp/small/de_nuke_1_small.webp", embed.Thumbnail?.Url);
		Assert.AreEqual("Matchmaking", embed.Footer?.Text);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**3 : 13**");
		StringAssert.Contains(embed.Description, "Terrorists · Counter-Terrorists");
		StringAssert.Contains(embed.Description, "1.  S1mple  `+20.00`");
		StringAssert.Contains(embed.Description, "2.  Nova  `+12.32`");
		StringAssert.Contains(embed.Description, "3.  Ken  `+5.00`");
		Assert.IsFalse(embed.Description.Contains("Ash", StringComparison.Ordinal));
		Assert.IsFalse(embed.Description.Contains("Kills", StringComparison.Ordinal));
		Assert.IsNull(embed.Fields);
	}

	[TestMethod]
	public void ShowsANegativeRatingOnLeetifysScale() {
		const string json = """
			{"map_name":"de_nuke","data_source":"matchmaking","team_scores":[{"team_number":2,"score":3},{"team_number":3,"score":13}],"stats":[{"name":"Ash","steam64_id":"76561197960265730","initial_team_number":2,"leetify_rating":-0.0218}]}
			""";

		LeetifyMatch? match = JsonSerializer.Deserialize(json, PluginJsonContext.Default.LeetifyMatch);
		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match));

		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**3 : 13**");
		StringAssert.Contains(embed.Description, "1.  Ash  `-2.18`");
	}

	[TestMethod]
	public void ShowsTheScoreWhenPlayersHaveNoNames() {
		LeetifyMatch match = SampleMatch();

		foreach (PlayerStats player in match.Stats!) {
			player.Name = null;
		}

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match));

		Assert.AreEqual("Nuke", embed.Title);
		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**3 : 13**");
		Assert.IsFalse(embed.Description.Contains("Top ratings", StringComparison.Ordinal));
	}

	[TestMethod]
	public void ShowsBothSidesWhenTheScoresAreLevel() {
		LeetifyMatch match = SampleMatch();
		match.TeamScores![0].Score = 13;
		match.TeamScores[1].Score = 13;

		DiscordEmbed embed = SingleEmbed(MatchReport.Create(MatchUrl, match));

		Assert.IsNotNull(embed.Description);
		StringAssert.Contains(embed.Description, "**13 : 13**");
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
			new PlayerStats { Name = "Nova", LeetifyRating = 0.1232 },
			new PlayerStats { Name = "Ken", LeetifyRating = 0.05 },
			new PlayerStats { Name = "Ash", LeetifyRating = -0.0218 },
			new PlayerStats { Name = "S1mple", LeetifyRating = 0.2 }
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
	public void ReadsADiscordWebhookAndOptionalValues() {
		PluginConfiguration configuration = PluginConfiguration.Read(name => name switch {
			PluginConfiguration.WebhookVariable => "https://discord.com/api/webhooks/1/secret",
			PluginConfiguration.ApiKeyVariable => "key",
			PluginConfiguration.SteamIdVariable => "76561197960265729",
			_ => null
		});

		Assert.AreEqual("https://discord.com/api/webhooks/1/secret", configuration.WebhookUrl?.AbsoluteUri);
		Assert.AreEqual("key", configuration.LeetifyApiKey);
		Assert.AreEqual(76561197960265729UL, configuration.LeetifySteamId);
		Assert.IsEmpty(configuration.Warnings);
	}

	[TestMethod]
	public void RejectsAWebhookThatIsNotDiscordAndFallsBackToTheLeetifyBot() {
		PluginConfiguration configuration = PluginConfiguration.Read(name => name switch {
			PluginConfiguration.WebhookVariable => "https://example.com/api/webhooks/1/secret",
			PluginConfiguration.SteamIdVariable => "nope",
			_ => null
		});

		Assert.IsNull(configuration.WebhookUrl);
		Assert.IsNull(configuration.LeetifyApiKey);
		Assert.AreEqual(PluginConfiguration.DefaultLeetifySteamId, configuration.LeetifySteamId);
		Assert.HasCount(2, configuration.Warnings);
	}

	[TestMethod]
	public void RecognizesTheLeetifyBotAsAnIndividualAccount() {
		Assert.IsTrue(PluginConfiguration.IsIndividualAccount(PluginConfiguration.DefaultLeetifySteamId));
		Assert.IsTrue(PluginConfiguration.IsIndividualAccount(76561197960265729));
		Assert.IsFalse(PluginConfiguration.IsIndividualAccount(0));
	}
}
