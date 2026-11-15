using System.Text.Json;

namespace GameHub.Protocol;

/// <summary>
/// The wire protocol: newline-delimited JSON objects, one message per line.
/// Every message is an object with a string "type" field; all other fields
/// are type-specific. Keeping the envelope flat means any client — including
/// a raw `telnet`-style session — can speak it by hand.
/// </summary>
public static class Wire
{
    // Client -> server
    public const string Hello = "hello";       // { "name": "alice" }
    public const string Queue = "queue";       // join matchmaking
    public const string LeaveQueue = "leave_queue";
    public const string Move = "move";         // { "column": 0-6 }
    public const string Chat = "chat";         // { "text": "..." }
    public const string Spectate = "spectate"; // { "room": "abc123" }
    public const string Rooms = "rooms";       // list open rooms
    public const string Rematch = "rematch";
    public const string Leave = "leave";       // leave room / spectating

    // Server -> client
    public const string Welcome = "welcome";     // { "name", "motd" }
    public const string Queued = "queued";       // { "position" }
    public const string GameStart = "game_start"; // { "room", "red", "yellow", "you_are" }
    public const string Board = "board";         // { "room", "board", "current", "result", "winner", "you_are" }
    public const string ChatMessage = "chat_msg"; // { "from", "text" }
    public const string RoomList = "room_list";  // { "rooms": [...] }
    public const string Info = "info";           // { "message" }
    public const string Error = "error";         // { "message" }

    /// <summary>Serializes one message: {"type": ..., ...fields}.</summary>
    public static string Encode(string type, Dictionary<string, object?>? fields = null)
    {
        var dict = new Dictionary<string, object?> { ["type"] = type };
        if (fields is not null)
            foreach (var (key, value) in fields)
                dict[key] = value;
        return JsonSerializer.Serialize(dict);
    }

    /// <summary>Parses one line into its type plus the raw JSON object.</summary>
    public static bool TryParse(string line, out string type, out JsonElement data)
    {
        type = "";
        data = default;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
                return false;
            type = t.GetString()!;
            data = root.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string GetString(JsonElement data, string field, string fallback = "")
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.String)
            return value.GetString()!;
        return fallback;
    }

    public static int GetInt(JsonElement data, string field, int fallback = -1)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var n))
            return n;
        return fallback;
    }
}
