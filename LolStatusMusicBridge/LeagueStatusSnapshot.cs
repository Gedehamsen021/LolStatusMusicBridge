using System.Text.Json.Nodes;

namespace LolStatusMusicBridge;

internal sealed record LeagueStatusSnapshot(JsonObject Payload, string StatusMessage);
