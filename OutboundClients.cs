using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LeetifyDiscord;

public sealed class DiscordWebhookClient {
	private const int Attempts = 3;

	private readonly HttpClient _httpClient;
	private readonly Uri _webhookUrl;

	public DiscordWebhookClient(HttpClient httpClient, Uri webhookUrl) {
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(webhookUrl);

		_httpClient = httpClient;
		_webhookUrl = webhookUrl;
	}

	public async Task<bool> PostAsync(DiscordWebhookPayload payload, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(payload);

		string json = JsonSerializer.Serialize(payload, PluginJson.Options);

		for (int attempt = 1; attempt <= Attempts; attempt++) {
			using HttpRequestMessage request = new(HttpMethod.Post, _webhookUrl);
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");

			try {
				using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

				if (response.IsSuccessStatusCode) {
					return true;
				}
			} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
				// HttpClient.Timeout surfaces as a cancellation that does not belong to the caller.
			} catch (HttpRequestException) {
				// The exception text can include the webhook URL, so it stays out of the log.
			}

			if (attempt < Attempts) {
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
			}
		}

		return false;
	}
}

public sealed class LeetifyApiClient {
	private readonly HttpClient _httpClient;
	private readonly string _apiKey;

	public LeetifyApiClient(HttpClient httpClient, string apiKey) {
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentException.ThrowIfNullOrEmpty(apiKey);

		_httpClient = httpClient;
		_apiKey = apiKey;
	}

	public async Task<LeetifyMatch?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default) {
		using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"https://api-public.cs-prod.leetify.com/v2/matches/{matchId:D}"));
		request.Headers.TryAddWithoutValidation("_leetify_key", _apiKey);
		// The official single-file ASF build trims MediaTypeWithQualityHeaderValue.
		request.Headers.TryAddWithoutValidation("Accept", "application/json");

		try {
			using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode) {
				return null;
			}

			await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

			return await JsonSerializer.DeserializeAsync<LeetifyMatch>(stream, PluginJson.Options, cancellationToken).ConfigureAwait(false);
		} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			return null;
		} catch (Exception exception) when (exception is HttpRequestException or JsonException) {
			return null;
		}
	}
}

file static class PluginJson {
	// The official single-file ASF build trims the source-generator JsonTypeInfo overloads.
	internal static readonly JsonSerializerOptions Options = new() {
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		TypeInfoResolver = new DefaultJsonTypeInfoResolver()
	};
}
