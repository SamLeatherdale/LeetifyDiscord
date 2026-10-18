using System.Text.RegularExpressions;

namespace LeetifyDiscord;

public readonly record struct LeetifyMatchLink(Uri Url, Guid MatchId);

public static partial class LeetifyMessageParser {
	public static IReadOnlyList<LeetifyMatchLink> FindMatchLinks(string message) {
		ArgumentNullException.ThrowIfNull(message);

		List<LeetifyMatchLink> links = [];

		foreach (Match match in UrlPattern().Matches(message)) {
			string candidate = match.Value.TrimEnd('.', ',', ';', ')', ']', '>');

			if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
				continue;
			}

			if (!IsLeetifyHost(uri.Host)) {
				continue;
			}

			Match identifier = MatchIdPattern().Match(uri.AbsolutePath);

			if (!identifier.Success || !Guid.TryParse(identifier.Value, out Guid matchId)) {
				continue;
			}

			links.Add(new LeetifyMatchLink(MatchPage(matchId), matchId));
		}

		return links;
	}

	public static Uri MatchPage(Guid matchId) => new($"https://leetify.com/app/match-details/{matchId:D}");

	private static bool IsLeetifyHost(string host) => host.Equals("leetify.com", StringComparison.OrdinalIgnoreCase) || host.Equals("www.leetify.com", StringComparison.OrdinalIgnoreCase);

	[GeneratedRegex(@"https://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex UrlPattern();

	[GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
	private static partial Regex MatchIdPattern();
}
