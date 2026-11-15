using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GameHub.Protocol;

namespace GameHub.Client;

/// <summary>
/// The interactive console client: one task reads server messages and
/// renders them, another reads your keystrokes. Commands are plain words —
/// the JSON protocol stays hidden unless you want to see it.
/// </summary>
public sealed class GameClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _name;
    private StreamWriter? _writer;

    public GameClient(string host, int port, string name)
    {
        _host = host;
        _port = port;
        _name = name;
    }

    public async Task RunAsync(CancellationToken token = default)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(_host, _port, token);
        var stream = tcp.GetStream();
        var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var reader = new StreamReader(stream, noBom);
        _writer = new StreamWriter(stream, noBom) { AutoFlush = true };

        Console.WriteLine($"Connected to {_host}:{_port} as {_name}. Type 'help' for commands.");
        Send(Wire.Hello, new() { ["name"] = _name });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var readTask = ReadLoopAsync(reader, cts.Token);
        var inputTask = InputLoopAsync(cts.Token);

        await Task.WhenAny(readTask, inputTask);
        cts.Cancel();
        Console.WriteLine("Disconnected. Bye!");
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is null) break;
                if (!Wire.TryParse(line, out var type, out var data)) continue;
                Render(type, data);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { Console.WriteLine("Connection to the server was lost."); }
    }

    private static void Render(string type, JsonElement data)
    {
        switch (type)
        {
            case Wire.Welcome:
                Console.WriteLine(Wire.GetString(data, "motd"));
                break;
            case Wire.Info:
                Console.WriteLine($"* {Wire.GetString(data, "message")}");
                break;
            case Wire.Error:
                Console.WriteLine($"! {Wire.GetString(data, "message")}");
                break;
            case Wire.Queued:
                Console.WriteLine($"Waiting for an opponent… (position {Wire.GetInt(data, "position", 1)})");
                break;
            case Wire.GameStart:
                Console.WriteLine();
                Console.WriteLine($"Game on! {Wire.GetString(data, "red")} (R) vs {Wire.GetString(data, "yellow")} (Y).");
                Console.WriteLine($"You are {(Wire.GetString(data, "you_are") == "R" ? "Red (R)" : "Yellow (Y)")}.");
                break;
            case Wire.Board:
            {
                Console.WriteLine();
                if (data.TryGetProperty("board", out var board))
                    foreach (var row in board.EnumerateArray())
                        Console.WriteLine(row.GetString());
                var result = Wire.GetString(data, "result");
                if (result == "Ongoing")
                    Console.WriteLine($"{Wire.GetString(data, "current")}'s turn — type a column number 1-7.");
                else if (result == "Draw")
                    Console.WriteLine("Draw! Type 'rematch' to play again.");
                else
                    Console.WriteLine($"{Wire.GetString(data, "winner")} wins! Type 'rematch' to play again.");
                break;
            }
            case Wire.ChatMessage:
                Console.WriteLine($"{Wire.GetString(data, "from")}: {Wire.GetString(data, "text")}");
                break;
            case Wire.RoomList:
            {
                if (!data.TryGetProperty("rooms", out var rooms) || rooms.GetArrayLength() == 0)
                {
                    Console.WriteLine("No open rooms right now. Type 'queue' to start one.");
                    break;
                }
                Console.WriteLine("Open rooms:");
                foreach (var room in rooms.EnumerateArray())
                {
                    var over = room.GetProperty("over").GetBoolean() ? " (finished)" : "";
                    Console.WriteLine($"  {room.GetProperty("id").GetString()}  " +
                        $"{room.GetProperty("red").GetString()} vs {room.GetProperty("yellow").GetString()}" +
                        $"  — {room.GetProperty("spectators").GetInt32()} watching{over}");
                }
                Console.WriteLine("Watch one with: watch <room id>");
                break;
            }
        }
    }

    private async Task InputLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var line = await Task.Run(Console.ReadLine, token);
            if (line is null) break;
            if (!HandleCommand(line.Trim())) break;
        }
    }

    /// <summary>Returns false when the client should quit.</summary>
    private bool HandleCommand(string line)
    {
        if (line.Length == 0) return true;
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "help":
                PrintHelp();
                break;
            case "quit":
            case "exit":
                Send(Wire.Leave);
                return false;
            case "queue":
                Send(Wire.Queue);
                break;
            case "leave":
                Send(Wire.Leave);
                break;
            case "rooms":
                Send(Wire.Rooms);
                break;
            case "watch":
                if (parts.Length < 2) Console.WriteLine("Usage: watch <room id>  (see 'rooms' first)");
                else Send(Wire.Spectate, new() { ["room"] = parts[1] });
                break;
            case "say":
                if (parts.Length < 2) Console.WriteLine("Usage: say <message>");
                else Send(Wire.Chat, new() { ["text"] = parts[1] });
                break;
            case "rematch":
                Send(Wire.Rematch);
                break;
            default:
                // A bare number is a move: columns are 1-7 for humans.
                if (int.TryParse(command, out var column) && column is >= 1 and <= 7)
                    Send(Wire.Move, new() { ["column"] = column - 1 });
                else
                    Console.WriteLine("Unknown command. Type 'help'.");
                break;
        }
        return true;
    }

    private void Send(string type, Dictionary<string, object?>? fields = null)
    {
        try { _writer?.WriteLine(Wire.Encode(type, fields)); }
        catch (IOException) { Console.WriteLine("Could not send — the connection is gone."); }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  queue            Find an opponent");
        Console.WriteLine("  <1-7>            Drop a disc in that column (your turn only)");
        Console.WriteLine("  say <text>       Chat with the room");
        Console.WriteLine("  rooms            List games you can watch");
        Console.WriteLine("  watch <room id>  Spectate a game");
        Console.WriteLine("  rematch          Ask for another game (after one ends)");
        Console.WriteLine("  leave            Leave the room");
        Console.WriteLine("  quit             Disconnect");
    }
}
