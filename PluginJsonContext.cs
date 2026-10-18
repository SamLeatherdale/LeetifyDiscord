using System.Text.Json.Serialization;

namespace LeetifyDiscord;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiscordWebhookPayload))]
[JsonSerializable(typeof(LeetifyMatch))]
public partial class PluginJsonContext : JsonSerializerContext;
